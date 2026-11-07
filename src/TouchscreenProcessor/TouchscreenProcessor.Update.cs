using Linearstar.Windows.RawInput;

namespace ThreeFingerDrag;

/// <summary>Raw digitizer report handling for <see cref="TouchscreenProcessor"/>.</summary>
internal sealed partial class TouchscreenProcessor
{
	/// <summary>Handles one raw digitizer report. Starts, moves, drops, or ends the drag based on the finger count and motion.</summary>
	public void Update(RawInputDigitizerData data)
	{
		if (!data.Device?.UsageAndPage.Equals(HidUsageAndPage.TouchScreen) ?? true) return;

		// Some touchscreen digitizers (including the device observed in diagnostics) report
		// touch contacts as Kind=Pen even though the HID usage page is TouchScreen (0D:04).
		// Since Program routes stylus devices by their separate Pen usage/page, accepting this
		// kind here does not merge actual pen input into the touchscreen stream.
		// Finger and pen-kind contacts both count as touch. Only contacts still down are kept.
		var downContacts = data.Contacts.Where(c => (c.Kind is RawInputDigitizerContactKind.Finger or RawInputDigitizerContactKind.Pen) && c.IsButtonDown.GetValueOrDefault(false)).ToArray();
		// Number of fingers the gesture needs. Any other count pauses or ends the gesture.
		var requiredCount = PointerSettings.GetTouchscreenDragFingerCount();
		if (downContacts.Length != requiredCount)
		{
			if (CursorDiagnosticLogger.IsEnabled)
			{
				var hasCursor = MouseInject.TryGetCursorPoint(out var cursorAtContactChange);
				var action = downContacts.Length == 0 ? contactDown ? "contact_lifted" : "no_accepted_contacts" : "contact_count_mismatch";
				CursorDiagnosticLogger.RecordInputSample("touchscreen", action, downContacts.Length == 0 ? null : TouchscreenDeviceMapper.CalculateCentroid(data, downContacts), hasCursor ? cursorAtContactChange : null,
					new
					{
						deviceUsagePage = data.Device?.UsageAndPage.ToString(),
						activeFingerContactCount = downContacts.Length,
						requiredContactCount = requiredCount,
						acceptedContactKinds = details,
						rawDigitizerContactCount = data.Contacts.Length,
						rawContacts = data.Contacts.Select(c => new
						{
							kind = c.Kind.ToString(),
							c.Identifier,
							c.IsButtonDown,
							c.X,
							c.Y,
							c.MinX,
							c.MaxX,
							c.MinY,
							c.MaxY
						}).ToArray()
					});
			}
			// A change in finger count during a drag must not start or reset the drag.
			// If one finger lifts and another stays down, wait for the grace period before ending the drag.
			// The lifted finger may come back, so the drag keeps going until the timer runs out.
			if (dragActive && downContacts.Length > 0)
			{
				// Cancel any existing grace countdown and start a fresh one.
				if (!contactLostGraceActive)
				{
					contactLostGraceActive = true;
					contactLostGraceTimer.Stop();
					contactLostGraceTimer.Interval = ContactLostGraceMs;
					contactLostGraceTimer.Start();
					CursorDiagnosticLogger.Record("touchscreen_update", "contact_lost_grace_started", null, null, new { activeFingerContactCount = downContacts.Length, requiredContactCount = requiredCount, graceMs = ContactLostGraceMs });
				}
				return;
			}
			// All fingers lifted or no active drag. Cancel any pending grace and end normally.
			if (contactLostGraceActive) CursorDiagnosticLogger.Record("touchscreen_update", "contact_lost_grace_cancelled", null, null, new { reason = "all_fingers_lifted_or_no_drag" });
			contactLostGraceActive = false;
			contactLostGraceTimer.Stop();
			EndContact("all_fingers_lifted_or_no_drag");
			return;
		}

		// Required contact count is down: cancel any pending grace period (finger returned in time).
		if (contactLostGraceActive)
		{
			contactLostGraceActive = false;
			contactLostGraceTimer.Stop();
			CursorDiagnosticLogger.Record("touchscreen_update", "contact_lost_grace_cancelled", null, null, new { reason = "required_contact_count_restored", activeFingerContactCount = downContacts.Length });
		}

		var contacts = downContacts;
		// Once the contact has started, only track the fingers that were part of it.
		if (contactDown)
		{
			// Pick the tracked fingers by ID. A missing ID means the finger count no longer matches.
			contacts = activeContactIds.Select(id => downContacts.FirstOrDefault(c => c.Identifier == id)).Where(c => c is not null).ToArray()!;
			// A tracked finger is gone, or a new one replaced it. Pause or end the drag.
			if (contacts.Length != requiredCount)
			{
				if (dragActive && downContacts.Length > 0)
				{
					if (!contactLostGraceActive)
					{
						contactLostGraceActive = true;
						contactLostGraceTimer.Stop();
						contactLostGraceTimer.Interval = ContactLostGraceMs;
						contactLostGraceTimer.Start();
						CursorDiagnosticLogger.Record("touchscreen_update", "contact_lost_grace_started", null, null, new { reason = "tracked_contact_mismatch", trackedContactCount = contacts.Length, requiredContactCount = requiredCount, graceMs = ContactLostGraceMs });
					}
					return;
				}
				if (contactLostGraceActive) CursorDiagnosticLogger.Record("touchscreen_update", "contact_lost_grace_cancelled", null, null, new { reason = "tracked_contact_mismatch_without_drag" });
				contactLostGraceActive = false;
				EndContact("finger_count_mismatch");
				return;
			}
		}

		// Average position of the tracked fingers, in screen pixels.
		var point = TouchscreenDeviceMapper.CalculateCentroid(data, contacts);
		if (CursorDiagnosticLogger.IsEnabled)
		{
			var hasCursorPosition = MouseInject.TryGetCursorPoint(out var cursorPosition);
			CursorDiagnosticLogger.RecordInputSample("touchscreen", "contact_report", point,
				hasCursorPosition ? cursorPosition : null, new
				{
					contactCount = downContacts.Length,
					requiredContactCount = requiredCount,
					acceptedContactKinds = details,
					deviceUsagePage = data.Device?.UsageAndPage.ToString(),
					activeContactIds = downContacts.Select(c => c.Identifier).ToArray(),
					rawDigitizerContactCount = data.Contacts.Length,
					rawContacts = data.Contacts.Select(c => new
					{
						kind = c.Kind.ToString(),
						c.Identifier,
						c.IsButtonDown,
						c.X,
						c.Y,
						c.MinX,
						c.MaxX,
						c.MinY,
						c.MaxY
					}).ToArray(),
					independentWindowDrag,
					target = CursorDiagnosticLogger.IsEnabled ? WindowDragController.DescribeWindowAtPoint(point) : null
				});
		}
		// Another device took the cursor during this contact. Continue as an independent window move.
		if (contactDown && !independentWindowDrag && HasConcurrentCursorInput())
		{
			// If another device takes cursor ownership after this touch began, stop the
			// synthetic drag cleanly and continue only as an independent window move.
			if (dragActive) dragEngine.EndDrag(this, "concurrent_cursor_input_detected");

			dragActive = false;
			independentWindowDrag = true;
			if (holdArmed) ForcedDragModeService.SetTouchHoldForced(false);

			holdArmed = false;
			forceWindowDragHoldTimer.Stop();
			ocrHoldTimer.Stop();
			CursorDiagnosticLogger.Record("touchscreen_update", "switched_to_independent_pointer", lastPoint, point);
		}
		if (!contactDown)
		{
			// First report of a contact. Record the start point and decide between a cursor drag and an independent window drag.
			contactDown = true;
			// Another device already controls the cursor, so this touch must not move it.
			independentWindowDrag = HasConcurrentCursorInput();
			independentWindowSession = null;
			holdCanceled = false;
			holdArmed = false;
			activeContactIds = [.. contacts.Select(c => c.Identifier).Where(id => id.HasValue).Select(id => id!.Value)];
			// All three points start at the centroid, so the first movement is measured from it.
			holdStartPoint = lastPoint = currentPoint = point;
			if (!independentWindowDrag)
			{
				// Place the cursor at the contact centroid (already in screen coordinates of the touched monitor).
				_ = MouseInject.TrySetCursorPoint(point);
				ocrHoldTimer.Interval = PointerSettings.GetOcrHoldDurationMs();
				ocrHoldTimer.Start();
			}
			CursorDiagnosticLogger.Record("touchscreen_update", "contact_started", point, point);
			if (!independentWindowDrag && PointerSettings.IsTouchHoldForceWindowDragEnabled())
			{
				forceWindowDragHoldTimer.Interval = PointerSettings.GetDragHoldDurationMs();
				forceWindowDragHoldTimer.Start();
			}
			return;
		}

		currentPoint = point;
		// Movement past the tolerance cancels the OCR hold, which only applies to a still touch.
		if (!dragActive && DistanceExceedsTolerance(holdStartPoint, point) && ocrHoldTimer.Enabled)
		{
			ocrHoldTimer.Stop();
			CursorDiagnosticLogger.Record("touchscreen_update", "ocr_hold_canceled_by_movement", holdStartPoint, point);
		}
		// Movement since the last report. It moves the cursor or the window.
		Point delta = new(point.X - lastPoint.X, point.Y - lastPoint.Y);
		// Movement past the tolerance starts a drag. A forced hold keeps the drag in window mode.
		if (!dragActive && !suppressDragUntilLift && DistanceExceedsTolerance(holdStartPoint, point))
		{
			// Movement without an armed hold cancels it. Otherwise the hold can still force window mode.
			holdCanceled = !holdArmed;
			forceWindowDragHoldTimer.Stop();
			if (independentWindowDrag)
			{
				// Find the window under the touch start point. The window is moved without the cursor.
				independentWindowSession = WindowDragController.TryStartTouchscreenWindowDrag(holdStartPoint);
				dragActive = independentWindowSession is not null;
				if (!dragActive) suppressDragUntilLift = true;

				CursorDiagnosticLogger.Record("touchscreen_update", dragActive ? "independent_window_drag_started" : "independent_target_not_window",
					holdStartPoint, point, new
					{
						windowHandle = independentWindowSession?.WindowHandle.ToInt64(),
						inputSource = "touchscreen",
						inputPosition = holdStartPoint,
						target = CursorDiagnosticLogger.IsEnabled ? WindowDragController.DescribeWindowAtPoint(holdStartPoint) : null,
						element = CursorDiagnosticLogger.IsEnabled ? UiAutomationDragClassifier.DescribeElementAtPoint(holdStartPoint) : null
					});
				lastPoint = point;
				return;
			}
			// Normal path. The engine decides which window, if any, to move.
			if (!dragEngine.TryStartDrag(this, out var session)) return;

			dragActive = true;
			// Only poll the cancel key when one is configured.
			if (!string.IsNullOrEmpty(PointerSettings.GetDragCancelKey()))
			{
				cancelDragPollTimer.Start();
				CursorDiagnosticLogger.Record("touchscreen_update", "cancel_key_poll_started", null, null, new { cancelKey = PointerSettings.GetDragCancelKey() });
			}

			CursorDiagnosticLogger.Record("touchscreen_update", "drag_started", holdStartPoint, point,
				new
				{
					windowHandle = session?.WindowHandle.ToInt64(),
					classifiedWindow = session is not null,
					forcedWindow = holdArmed,
					inputSource = "touchscreen",
					inputPosition = holdStartPoint,
					target = CursorDiagnosticLogger.IsEnabled ? WindowDragController.DescribeWindowAtPoint(holdStartPoint) : null,
					element = CursorDiagnosticLogger.IsEnabled ? UiAutomationDragClassifier.DescribeElementAtPoint(holdStartPoint) : null
				});
		}

		if (dragActive)
		{
			if (independentWindowDrag)
			{
				// Same delta as the cursor path, but applied to the window only.
				Point independentDelta = new(point.X - lastPoint.X, point.Y - lastPoint.Y);
				if (independentWindowSession is not null)
				{
					// Window bounds are read only when diagnostics are on, to keep the normal path cheap.
					var windowBeforeMove = CursorDiagnosticLogger.IsEnabled && WindowDragController.TryGetWindowBounds(independentWindowSession.WindowHandle, out var wBefore) ? wBefore : (Rectangle?)null;
					WindowDragController.MoveWindow(independentWindowSession, independentDelta, "touchscreen", point);
					if (CursorDiagnosticLogger.IsEnabled)
					{
						var windowAfterMove = WindowDragController.TryGetWindowBounds(independentWindowSession.WindowHandle, out var wAfter) ? wAfter : (Rectangle?)null;
						CursorDiagnosticLogger.RecordSpeedSample("touchscreen_independent_window", independentDelta, null, null, windowBeforeMove, windowAfterMove);
					}
				}
				lastPoint = point;
				return;
			}
			// Length of this step, in screen pixels. Compared against the flick threshold.
			var distance = Math.Sqrt((double)delta.X * delta.X + (double)delta.Y * delta.Y);
			// A fast step during a drag is a flick. It drops the window where it stands.
			if (PointerSettings.IsTouchFlickToDropEnabled() && distance >= PointerSettings.GetTouchFlickToDropVelocityThreshold())
			{
				CursorDiagnosticLogger.Record("touchscreen_update", "flick_drop_triggered", lastPoint, point, new { distance, thresholdPx = PointerSettings.GetTouchFlickToDropVelocityThreshold() });
				DropOnFlick(point);
				lastPoint = point;
				return;
			}
			if (delta.X != 0 || delta.Y != 0)
			{
				// Move the cursor relatively from its current position instead of warping it to the touch point.
				// Move the cursor by the delta. Warping it to the touch point would make it jump.
				MouseInject.Move(delta);
				_ = dragEngine.Move(this, delta, cursorAlreadyMoved: true);
			}
		}
		lastPoint = point;
	}
}
