using Linearstar.Windows.RawInput;

namespace ThreeFingerDrag;

public sealed partial class TouchProcessor
{
	private static TimeSpan StationaryDragDropHold => TimeSpan.FromMilliseconds(PointerSettings.GetTouchStationaryDragDropHoldMs());

	/// <summary>
	/// Handles one touchpad digitizer packet. Tracks finger count changes, the contact centroid,
	/// and drives drag start, move, flick, pause, and lift handling.
	/// </summary>
	public void Update(RawInputDigitizerData data)
	{
		UpdateDragDropAnchorHoldState();
		// Snapshot the cursor once per packet, for the diagnostic records written below.
		_ = MouseInject.TryGetCursorPoint(out var cursorAtTouchReport);

		// Digitizer packets can arrive during the anchor hold too, so reapply it here as well.
		ReapplyDragDropAnchor();

		// Keep only finger contacts that are touching the pad. Pens and other contact kinds are ignored.
		var newContacts = data.Contacts.Where(contact => contact.IsButtonDown.GetValueOrDefault(false) && contact.Kind == RawInputDigitizerContactKind.Finger).ToArray();

		// An empty packet means every finger has lifted.
		if (data.ContactsCount == 0)
		{
			if (previousContactCount > 0) CursorDiagnosticLogger.Record("touch_contact_change", "all_fingers_lifted", currentCursorPointOrNull(), cursorAtTouchReport, new { beforeContactCount = previousContactCount, afterContactCount = 0, state = state.ToString(), dragMode = dragExecutionMode.ToString() });
			// Reset the gesture when the pad is empty and close any active or paused drag.
			// Remember whether a drag was open before the state is reset.
			var wasDragging = state == State.THREE_FINGER_DRAG || HasOpenDragSession;
			var wasActiveDrag = wasDragging;
			if (state != State.CLEAR) SetState(State.CLEAR);
			if (wasActiveDrag) StopActiveDrag("all_fingers_lifted");

			accumulatedDistance = 0;
			previousContacts = [];
			previousCenterPoint = null;
			previousContactCount = 0;
			currentContactCount = 0;
			suppressDragUntilFingersLiftOrStop = false;
			stationarySinceUtc = null;

			// Keep the anchor through the driver's delayed resync packets.
			// Without an open drag or a pinned anchor there is nothing to hold. Release the anchor.
			if (!wasActiveDrag && !dragDropAnchorCursorPoint.HasValue) ReleaseDragDropAnchor();
			return;
		}

		// ContactsCount is only trusted when at least one finger contact is in the list.
		// Otherwise the count is treated as zero.
		var newContactsCount = data.ContactsCount;
		if (newContacts.Length == 0) newContactsCount = 0;
		currentContactCount = newContactsCount;

		if (newContactsCount != previousContactCount)
		{
			CursorDiagnosticLogger.Record("touch_contact_change", newContactsCount > previousContactCount ? "finger_down" : "finger_lifted", currentCursorPointOrNull(), cursorAtTouchReport, new { beforeContactCount = previousContactCount, afterContactCount = newContactsCount, state = state.ToString() });

			// Solid-colour sampling is tied to the gesture arm point. Do not touch it during a drag.
			var configuredDragFingerCount = PointerSettings.GetTouchpadDragFingerCount();
			// Sample the touch-start colour only when the gesture arms, not mid-drag.
			// Classifiers are only needed at arm time. Keep the stored result while a drag runs.
			if (state != State.THREE_FINGER_DRAG)
			{
				if (previousContactCount < configuredDragFingerCount && newContactsCount == configuredDragFingerCount)
				{
					// Sample the cursor here when available. Otherwise fall back to the packet-time cursor.
					var samplePoint = MouseInject.TryGetCursorPoint(out var pointAtSample) ? pointAtSample : cursorAtTouchReport;
					if (!PointerSettings.IsSolidColorDetectionEnabled()) WindowDragController.ClearTouchStartSolidColorResult(); else _ = WindowDragController.TryGetTouchStartSolidColor(samplePoint, out _);
				}
				// Any other finger count makes the stored colour stale.
				else if (newContactsCount != configuredDragFingerCount)
				{
					WindowDragController.ClearTouchStartSolidColorResult();
				}
			}
		}

		// Nothing is tracked yet. Adopt the new contacts and wait for the next packet to measure motion.
		if (state == State.CLEAR)
		{
			SetState(MapState(newContactsCount));
			accumulatedDistance = 0;
			previousContacts = newContacts;
			previousCenterPoint = null;
			previousContactCount = newContactsCount;
			return;
		}

		// Same finger count as the last packet, so this packet may carry movement.
		// Count changes are handled further below.
		if (newContactsCount > 0 && previousContactCount == newContactsCount)
		{
			// Fingers were swapped or replaced. The centroid jump is not motion, so it is reset.
			if (!ContactSetsMatch(previousContacts, newContacts))
			{
				// A changed contact set invalidates the old centroid and ends any drag it drove.
				if (state == State.THREE_FINGER_DRAG || HasOpenDragSession)
				{
					CursorDiagnosticLogger.Record("touch_contact_change", "contact_set_changed", currentCursorPointOrNull(), cursorAtTouchReport, new { contactCount = newContactsCount, state = state.ToString() });
					FreezeAndStopDrag("contact_set_changed");
				}
				previousContacts = newContacts;
				previousCenterPoint = null;
				previousContactCount = newContactsCount;
				return;
			}

			// Movement is measured on the centroid of all contacts, not on each finger.
			var center = CalculateCenterPoint(newContacts);
			Point diff = new();
			double stepDistance = 0;
			// The first packet after a reset has no previous centroid, so it adds no travel.
			if (previousCenterPoint.HasValue)
			{
				diff = center - (Size)previousCenterPoint.Value;
				stepDistance = Math.Sqrt((diff.X * diff.X) + (diff.Y * diff.Y));
				accumulatedDistance += stepDistance;
			}

			// After a flick drop, the cursor stays free until the fingers have moved less than
			// 1.5 units per packet for 150 ms. Any larger movement restarts that timer.
			if (suppressDragUntilFingersLiftOrStop)
			{
				if (stepDistance < 1.5)
				{
					stationarySinceUtc ??= DateTime.UtcNow;
					if (DateTime.UtcNow - stationarySinceUtc.Value >= StationaryDragDropHold)
					{
						CursorDiagnosticLogger.Record("touch_flick_suppress", "released_after_stationary", details: new { holdMs = StationaryDragDropHold.TotalMilliseconds });
						suppressDragUntilFingersLiftOrStop = false;
						stationarySinceUtc = null;
						WindowDragController.ClearTouchStartSolidColorResult();
						accumulatedDistance = 0;
					}
				} else
				{
					stationarySinceUtc = null;
					accumulatedDistance = 0;
				}
			}

			previousCenterPoint = center;
			previousContacts = newContacts;
			previousContactCount = newContactsCount;

			// Enough travel turns an idle state into its moving state. This separates taps from swipes.
			if (accumulatedDistance > 15 && !suppressDragUntilFingersLiftOrStop)
			{
				SetState(state switch
				{
					State.ONE_FINGER_IDLE => State.ONE_FINGER_MOVING,
					State.TWO_FINGER_IDLE => State.TWO_FINGER_MOVING,
					State.THREE_FINGER_IDLE => State.THREE_FINGER_MOVING,
					_ => state,
				});
			}

			// Start the drag once the moving state reaches the configured finger count.
			if (state == MapMovingState(PointerSettings.GetTouchpadDragFingerCount()) && !suppressDragUntilFingersLiftOrStop)
			{
				var dragStartCursor = MouseInject.TryGetCursorPoint(out var dragStartPoint) ? dragStartPoint : (Point?)null;
				// The drag engine refused the drag. Keep the current state and try again on the next packet.
				if (!dragEngine.TryStartDrag(this, out _))
				{
					CursorDiagnosticLogger.Record("touch_drag_start", "refused", details: new { state = state.ToString(), contacts = newContactsCount });
					return;
				}
				// Background window moves do not need the cursor, so use them when the session allows it.
				dragExecutionMode = activeDragSession?.MoveWithoutForeground == true ? DragExecutionMode.BackgroundWindow : DragExecutionMode.Mouse;

				// Log the drag start with the cursor position and the window under it.
				CursorDiagnosticLogger.Record("drag_start", "TryStartDrag", dragStartCursor,
					MouseInject.TryGetCursorPoint(out var afterDragStart) ? afterDragStart : dragStartCursor,
					new
					{
						result = activeDragSession is null ? "mouse_passthrough" : "window_drag",
						mode = dragExecutionMode.ToString(),
						windowHandle = activeDragSession?.WindowHandle.ToInt64(),
						window = activeDragSession is not null ? CursorDiagnosticLogger.DescribeWindow(activeDragSession.WindowHandle, dragStartCursor) : CursorDiagnosticLogger.DescribeWindow(0, dragStartCursor),
						fingerMonitor = dragStartCursor.HasValue ? CursorDiagnosticLogger.GetMonitorAtPoint(dragStartCursor.Value) : null,
						state = state.ToString()
					});

				SetState(State.THREE_FINGER_DRAG);
			}

			if (state == State.THREE_FINGER_DRAG)
			{
				var cursorBeforeDragMove = currentCursorPointOrNull();
				// Save the last clean position before touchpad lift-off jitter begins.
				if (MouseInject.TryGetCursorPoint(out var currentCursorPos))
				{
					recentCleanCursorPoint = previousAppliedCursorPoint ?? currentCursorPos;
					previousAppliedCursorPoint = currentCursorPos;
				}

				// If a drop anchor is pinned from an earlier drag, check it for a flick or a swipe release.
				if (dragDropAnchorCursorPoint.HasValue)
				{
					anchorTouchCenter ??= center;
					accumulatedAnchorTouchDistance += stepDistance;
					var displacementFromAnchor = Math.Sqrt(Math.Pow(center.X - anchorTouchCenter.Value.X, 2) + Math.Pow(center.Y - anchorTouchCenter.Value.Y, 2));

					// A fast flick drops the drag while the fingers are still down. The per-packet speed is
					// checked directly, or accumulated travel is checked against a smaller per-packet speed.
					if (PointerSettings.IsTouchFlickToDropEnabled() && (stepDistance >= PointerSettings.GetTouchFlickToDropVelocityThreshold() || (accumulatedAnchorTouchDistance >= PointerSettings.GetTouchFlickToDropVelocityThreshold() && stepDistance >= PointerSettings.GetTouchpadNaturalDragReleaseThreshold())))
					{
						ExecuteFlickToDrop();
						return;
					}

					// A slower swipe away from the anchor releases the cursor lock, so the mouse moves again.
					if (displacementFromAnchor >= PointerSettings.GetTouchpadNaturalDragReleaseThreshold() || accumulatedAnchorTouchDistance >= PointerSettings.GetTouchpadNaturalDragReleaseThreshold())
					{
						CursorDiagnosticLogger.Record("touch_anchor", "released_by_swipe", details: new { displacementFromAnchor, accumulatedAnchorTouchDistance, thresholdPx = PointerSettings.GetTouchpadNaturalDragReleaseThreshold() });
						ClearDragDropAnchorState();
					}
				}

				// Window drags move the window directly and leave the cursor alone.
				if (dragExecutionMode == DragExecutionMode.BackgroundWindow && activeDragSession is not null)
				{
					_ = dragEngine.Move(this, diff, cursorAlreadyMoved: false);
					if (CursorDiagnosticLogger.IsEnabled)
					{
						CursorDiagnosticLogger.Record("drag_move", "WindowDragController.MoveWindow", cursorBeforeDragMove, currentCursorPointOrNull(),
						new
						{
							mode = "BackgroundWindow",
							dx = diff.X,
							dy = diff.Y,
							windowHandle = activeDragSession.WindowHandle.ToInt64(),
							window = CursorDiagnosticLogger.DescribeWindow(activeDragSession.WindowHandle)
						});
					}
				} else
				{
					// Mouse mode: move the cursor by the centroid step. The target then moves with it.
					MouseInject.Move(diff);
					CursorDiagnosticLogger.Record("drag_move", "MouseInject.Move", cursorBeforeDragMove, currentCursorPointOrNull(),
					new
					{
						mode = "Mouse",
						dx = diff.X,
						dy = diff.Y,
						cursorMonitor = currentCursorPointOrNull().HasValue ? CursorDiagnosticLogger.GetMonitorAtPoint(currentCursorPointOrNull()!.Value) : null
					});
					if (CursorDiagnosticLogger.IsEnabled)
					{
						var cursorAfterMouseMove = currentCursorPointOrNull();
						Rectangle? windowAfterMouseMove = null;
						if (activeDragSession is not null && WindowDragController.TryGetWindowBounds(activeDragSession.WindowHandle, out var windowBoundsNow)) windowAfterMouseMove = windowBoundsNow;
						CursorDiagnosticLogger.RecordSpeedSample("touch_mouse_mode", diff, cursorBeforeDragMove, cursorAfterMouseMove, null, windowAfterMouseMove);
					}

					// If the target ignores the synthetic drag, move it directly and keep the session.
					if (activeDragSession is { UsingWindowPositionFallback: false } && (diff.X != 0 || diff.Y != 0 || WindowDragController.IsWindowPositionFallbackPending(activeDragSession))) if (dragEngine.Move(this, diff, cursorAlreadyMoved: true)) dragExecutionMode = DragExecutionMode.BackgroundWindow;
				}

				// If a window-position fallback finished while in mouse mode, switch to window moves.
				if (dragExecutionMode == DragExecutionMode.Mouse && activeDragSession is { UsingWindowPositionFallback: false } && dragEngine.CompletePendingFallback(this)) dragExecutionMode = DragExecutionMode.BackgroundWindow;
			}

			return;
		}

		// Finger count went up.
		if (previousContactCount < newContactsCount)
		{
			// A finger returned during the grace period and the count is back at the drag count.
			// Stop the grace timer and resume the drag without restarting it.
			if (fingerLiftGraceActive && newContactsCount == PointerSettings.GetTouchpadDragFingerCount() && HasOpenDragSession)
			{
				fingerLiftGraceActive = false;
				fingerLiftGraceTimer.Stop();
				CursorDiagnosticLogger.Record("touch_finger_lift_grace", "resumed", details: new { contacts = newContactsCount });
				SetState(State.THREE_FINGER_DRAG);
				previousContacts = newContacts;
				previousCenterPoint = null;
				previousContactCount = newContactsCount;
				return;
			}

			if (state == State.THREE_FINGER_DRAG)
			{
				// Extra fingers pause the drag. It resumes when the count returns to the drag count.
				SetState(State.FOUR_OR_MORE);
			} else if (state != State.THREE_FINGER_DRAG || newContactsCount != PointerSettings.GetTouchpadDragFingerCount())
			{
				// A two-finger move keeps its state. Other states follow the finger count.
				if (state != State.TWO_FINGER_MOVING)
				{
					SetState(MapState(newContactsCount));
					accumulatedDistance = 0;
				}
			}

			previousContacts = newContacts;
			previousCenterPoint = null;
			previousContactCount = newContactsCount;
			return;
		}

		// Finger count went down.
		if (previousContactCount > newContactsCount)
		{
			if (state == State.THREE_FINGER_DRAG)
			{
				if (newContactsCount > 0)
				{
					// Some fingers are still down. Start the grace timer instead of ending the drag now.
					if (!fingerLiftGraceActive)
					{
						fingerLiftGraceActive = true;
						fingerLiftGraceTimer.Stop();
						fingerLiftGraceTimer.Interval = FingerLiftGraceMs;
						fingerLiftGraceTimer.Start();
						CursorDiagnosticLogger.Record("touch_finger_lift_grace", "started", details: new { remainingContacts = newContactsCount, graceMs = FingerLiftGraceMs });
					}
					previousContacts = newContacts;
					previousCenterPoint = null;
					previousContactCount = newContactsCount;
					return;
				}
				// All fingers are up. End the drag now and clear the grace state.
				// The all-fingers path above covers the same case. This is a safety clear.
				fingerLiftGraceActive = false;
				fingerLiftGraceTimer.Stop();
				// Use the lift report position; the prior sample may lag by one packet.
				// Pin the drop point where the cursor was just before the lift.
				var preLiftPoint = MouseInject.LastAppliedCursorPoint ?? (MouseInject.TryGetCursorPoint(out var liveCursorPoint) ? liveCursorPoint : (Point?)null);
				if (preLiftPoint.HasValue) dragDropAnchorCursorPoint = preLiftPoint;

				FreezeAndStopDrag("fingers_lifted");
			} else if (state == State.FOUR_OR_MORE && newContactsCount == PointerSettings.GetTouchpadDragFingerCount() && HasOpenDragSession)
			{
				// Resume a paused drag when the contact count returns to three.
				SetState(State.THREE_FINGER_DRAG);
			} else if (state == State.FOUR_OR_MORE && newContactsCount < PointerSettings.GetTouchpadDragFingerCount() && HasOpenDragSession)
			{
				// Below three contacts, the paused drag cannot resume; close it now.
				FreezeAndStopDrag("paused_contacts_below_threshold");
			}
			// Two or more fingers keep a two-finger move going. Other states map to the new count.
			else if (!(state == State.TWO_FINGER_MOVING && newContactsCount >= 2))
			{
				SetState(MapState(newContactsCount));
				accumulatedDistance = 0;
			}

			previousContacts = newContacts;
			previousCenterPoint = null;
			previousContactCount = newContactsCount;
		}
	}

	/// <summary>Average position of all contacts, in digitizer units. One contact returns its own position.</summary>
	private static Point CalculateCenterPoint(RawInputDigitizerContact[] contacts)
	{
		if (contacts.Length == 0) throw new ArgumentException("empty list of contacts");
		if (contacts.Length == 1) return new Point(contacts[0].X, contacts[0].Y);

		Point point = new(0, 0);
		foreach (var contact in contacts)
		{
			point.X += contact.X;
			point.Y += contact.Y;
		}

		point.X /= contacts.Length;
		point.Y /= contacts.Length;
		return point;
	}

	/// <summary>
	/// Compares contact IDs when available. Missing IDs or different array lengths are treated as
	/// unchanged because some touchpads do not report IDs.
	/// </summary>
	private static bool ContactSetsMatch(RawInputDigitizerContact[] previous, RawInputDigitizerContact[] current)
	{
		if (previous.Length != current.Length) return true;

		var previousIds = new int[previous.Length];
		for (var i = 0; i < previous.Length; i++)
		{
			if (previous[i].Identifier is not int previousId) return true;
			previousIds[i] = previousId;
		}

		foreach (var contact in current)
		{
			if (contact.Identifier is not int currentId) return true;
			if (Array.IndexOf(previousIds, currentId) < 0) return false;
		}

		return true;
	}
}
