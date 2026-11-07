namespace ThreeFingerDrag;

/// <summary>Raw input handling: trigger press/release and pointer movement.</summary>
internal sealed partial class MouseDragProcessor
{
	/// <summary>Handles trigger press and release. A press starts hold timers; a release ends the drag or replays the click.</summary>
	private void OnTriggerButton(string button, bool pressed, Point point)
	{
		lock (sync)
		{
			if (pressed)
			{
				// A drag that outlived the trigger release is latched by ClickLock. Pressing the trigger again drops it.
				if (!triggerHeld && dragActive)
				{
					CursorDiagnosticLogger.Record("mouse_trigger", "clicklock_drop", details: new { button });
					EndDrag($"trigger_{button}_clicklock_drop");
					// ClickLock latches the synthetic left button released after a long drag. A left click clears that latch.
					if (IsClickLockEnabled()) MouseInject.ReplayMouseClick("Left");
					forcedWindowMode = false;
					ForcedDragModeService.SetMouseHoldForced(false);
					return;
				}
				if (triggerHeld) return;
				CursorDiagnosticLogger.Record("mouse_trigger", "pressed", point, details: new { button });
				// Start a new press. A repeated down event with the trigger already held is ignored.
				triggerHeld = true;
				IsTriggerHeld = true;
				// Record the press origin. Both points start here, so the first movement is measured from the press.
				pressPoint = lastPoint = point;
				detectionPoint = point;
				// Freezing starts on the first movement while held, not on press, so a plain click is not delayed.
				detecting = false;
				detectionRefused = false;
				forcedWindowMode = false;
				// OCR is requested after a shorter hold than forced window mode.
				ocrHoldTimer.Interval = PointerSettings.GetOcrHoldDurationMs();
				ocrHoldTimer.Start();
				// Only start the forced-window timer when that option is enabled.
				if (PointerSettings.IsClickHoldForceWindowDragEnabled())
				{
					forceHoldTimer.Interval = PointerSettings.GetDragHoldDurationMs();
					forceHoldTimer.Start();
				}
				return;
			}

			if (!triggerHeld) return;

			CursorDiagnosticLogger.Record("mouse_trigger", "released", point, details: new { button, dragActive, detecting });
			triggerHeld = false;
			IsTriggerHeld = false;
			forceHoldTimer.Stop();
			if (detecting)
			{
				// Released before detection finished. Unfreeze, and apply the movement the user made while holding.
				detecting = false;
				_ = MouseInject.TrySetCursorPoint(detectionPoint);
			}
			if (dragActive)
			{
				// With ClickLock on, the drag stays latched after release. The next trigger press drops it.
				if (IsClickLockEnabled()) return;
				// The button was released during a drag. End the drag instead of replaying the click.
				EndDrag($"trigger_{button}_released");
			} else
			{
				// No drag happened, so the press was a plain click. Send it to the system so it is not lost.
				CursorDiagnosticLogger.Record("mouse_trigger", "plain_click_replayed", details: new { button });
				MouseInject.ReplayMouseClick(button);
			}

			forcedWindowMode = false;
			ForcedDragModeService.SetMouseHoldForced(false);
		}
	}

	/// <summary>Handles raw pointer movement while the trigger is held. Starts the drag once the tolerance is exceeded.</summary>
	private void OnMouseMoved(Point point)
	{
		lock (sync)
		{
			// While detecting, the cursor is frozen, so hook positions are ignored. Raw deltas drive the pointer instead.
			// While detection is pending, the raw path drives the pointer and the cursor is frozen, so hook positions are ignored.
			if (detecting || !(dragActive || (triggerHeld && detectionRefused))) return;
			ProcessMove(point);
		}
	}

	/// <summary>Adds raw mouse deltas while the trigger is held. The first movement starts detection and freezes the cursor.</summary>
	private void OnRawMouseDelta(int dx, int dy)
	{
		lock (sync)
		{
			if (!triggerHeld || dragActive || detectionRefused) return;
			if (!detecting)
			{
				CursorDiagnosticLogger.Record("mouse_detection", "started", pressPoint);
				detectionPoint = pressPoint;
				detecting = true;
			}
			detectionPoint = new Point(detectionPoint.X + dx, detectionPoint.Y + dy);
			lastPoint = detectionPoint;
			ProcessMove(detectionPoint);
		}
	}

	/// <summary>Applies one pointer position while the trigger is held. Starts the drag once the tolerance is exceeded.</summary>
	private void ProcessMove(Point point)
	{
		lock (sync)
		{
			// Movement is measured from the last point handled, not from the press point.
			var previousPoint = lastPoint;
			if (!dragActive)
			{
				// Before the drag starts, wait until the pointer leaves the tolerance zone or window mode is forced.
				if (!forcedWindowMode && !DistanceExceedsTolerance(pressPoint, point)) return;

				ocrHoldTimer.Stop();
				// The engine classifies at the frozen cursor, which is still the press point.
				if (!StartDrag(point))
				{
					CursorDiagnosticLogger.Record("mouse_detection", "drag_refused", point, details: new { detecting, forcedWindowMode });
					// Refused: release the freeze and move the cursor to where the user pointed, then resume normal tracking.
					if (detecting)
					{
						detecting = false;
						detectionRefused = true;
						_ = MouseInject.TrySetCursorPoint(point);
						lastPoint = point;
					}
					return;
				}
				detecting = false;
				// Apply the whole distance from the press point as one step, then resync to the live cursor.
				var first = new Point(point.X - pressPoint.X, point.Y - pressPoint.Y);
				if (first.X != 0 || first.Y != 0) _ = dragEngine.Move(this, first, cursorAlreadyMoved: false);
				lastPoint = MouseInject.TryGetCursorPoint(out var live) ? live : point;
				return;
			}

			// Movement since the last point handled, applied to the drag engine.
			if (PointerSettings.ShouldKeepWindowWithinScreen())
			{
				// Treat the desktop edge as a wall: the cursor stops there instead of moving past it.
				var desktop = WindowDragController.GetDesktopBounds();
				var clamped = new Point(
					Math.Clamp(point.X, desktop.Left, desktop.Right - 1),
					Math.Clamp(point.Y, desktop.Top, desktop.Bottom - 1));
				if (clamped != point)
				{
					MouseInject.TrySetCursorPoint(clamped);
					point = clamped;
				}
			}
			Point delta = new(point.X - previousPoint.X, point.Y - previousPoint.Y);
			ApplyDragMovement(point, delta);
		}
	}

	/// <summary>Reads the Windows ClickLock setting. Returns false when it is off or cannot be read.</summary>
	private static bool IsClickLockEnabled() => ClickLock.TryGetEnabled(out var enabled) && enabled;

	/// <summary>Sends one movement step to the drag engine and records the new last point.</summary>
	private void ApplyDragMovement(Point point, Point delta)
	{
		// The cursor has already moved (raw input), so the engine must not move it a second time.
		_ = dragEngine.Move(this, delta, cursorAlreadyMoved: true);
		lastPoint = point;
	}
}
