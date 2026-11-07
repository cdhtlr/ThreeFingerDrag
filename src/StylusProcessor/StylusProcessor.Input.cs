using Linearstar.Windows.RawInput;

namespace ThreeFingerDrag;

internal sealed partial class StylusProcessor
{
	/// <summary>Handles one raw digitizer report from a pen device. Ends the contact when the pen lifts, the eraser is used, or stylus input is turned off.</summary>
	public void Update(RawInputDigitizerData data)
	{
		var usage = data.Device?.UsageAndPage;
		if (usage is null || !usage.Value.Equals(HidUsageAndPage.Pen)) return;

		// The eraser end of a pen reports as a separate contact kind. It cancels the pen gesture.
		var eraserDown = data.Contacts.Any(contact => contact.Kind == RawInputDigitizerContactKind.Eraser && contact.IsButtonDown.GetValueOrDefault(false));
		// Pen tip contact, the only contact that starts or continues a stylus drag.
		var tipDown = data.Contacts.Any(contact => contact.Kind == RawInputDigitizerContactKind.Pen && contact.IsButtonDown.GetValueOrDefault(false));
		// The first pen-tip contact carries the coordinates used for mapping and logging.
		var penContact = data.Contacts.FirstOrDefault(contact => contact.Kind == RawInputDigitizerContactKind.Pen && contact.IsButtonDown.GetValueOrDefault(false));

		lock (sync)
		{
			// Any of these conditions ends the contact. The reason string is only for the diagnostic log.
			if (isDisposed || !PointerSettings.IsStylusEnabled() || eraserDown || !tipDown)
			{
				EndContact(isDisposed ? "processor_disposed" : !PointerSettings.IsStylusEnabled() ? "stylus_disabled" : eraserDown ? "eraser_end_detected" : "pen_tip_lifted");
				return;
			}

			// Pen position is read from the live cursor. The raw report is only used for logging.
			if (!MouseInject.TryGetCursorPoint(out var cursorPoint))
			{
				EndContact("cursor_read_failed");
				return;
			}

			// Mark the contact before any early return, so other components see the pen as in use.
			IsPenContactActive = true;
			if (penContact is null)
			{
				EndContact("pen_contact_missing");
				return;
			}
			if (CursorDiagnosticLogger.IsEnabled)
			{
				LogPenReport(data, penContact, cursorPoint, tipDown, eraserDown);
			}

			// First report of a contact. Start the hold timers and measure the hold from this point.
			if (!penDown)
			{
				penDown = true;
				holdCanceled = false;
				holdArmed = false;
				// Both points start at the touch position, so the first step is measured from there.
				holdStartPoint = lastMovePoint = cursorPoint;
				// OCR is requested after the shorter OCR hold.
				ocrHoldTimer.Interval = PointerSettings.GetOcrHoldDurationMs();
				ocrHoldTimer.Start();
				// Forced window mode is off unless the stylus hold option is enabled.
				if (PointerSettings.IsStylusHoldForceWindowDragEnabled())
				{
					holdTimer.Interval = PointerSettings.GetDragHoldDurationMs();
					holdTimer.Start();
				}
				CursorDiagnosticLogger.Record("stylus_input", "pen_contact_started", cursorPoint, cursorPoint, new { holdForceEnabled = PointerSettings.IsStylusHoldForceWindowDragEnabled(), holdMs = holdTimer.Interval });
				return;
			}

			// The pen left the hold tolerance before any drag started. Decide how the drag begins.
			if (!dragActive && DistanceExceedsTolerance(holdStartPoint, cursorPoint))
			{
				if (ocrHoldTimer.Enabled)
				{
					ocrHoldTimer.Stop();
					CursorDiagnosticLogger.Record("stylus_input", "ocr_hold_canceled", holdStartPoint, cursorPoint, new { reason = "moved_past_tolerance" });
				}
				if (holdArmed)
				{
					// The hold armed forced window mode, so the drag is forced.
					if (!StartWindowDrag(cursorPoint, forced: true)) return;
				} else
				{
					if (!holdCanceled) CursorDiagnosticLogger.Record("stylus_input", "hold_canceled", holdStartPoint, cursorPoint, new { reason = "moved_past_tolerance" });
					holdCanceled = true;
					holdTimer.Stop();
					if (!StartWindowDrag(cursorPoint, forced: false)) return;
				}
			}

			if (!dragActive) return;
			// Movement since the last step, applied to the drag engine.
			Point movement = new(cursorPoint.X - lastMovePoint.X, cursorPoint.Y - lastMovePoint.Y);
			// Skip empty steps so the engine is not called with zero movement.
			if (movement.X != 0 || movement.Y != 0)
			{
				_ = dragEngine.Move(this, movement, cursorAlreadyMoved: true);
				lastMovePoint = cursorPoint;
			}
		}
	}

	/// <summary>Writes the raw pen report and its mapped screen position to the diagnostic log.</summary>
	private static void LogPenReport(RawInputDigitizerData data, RawInputDigitizerContact penContact, Point cursorPoint, bool tipDown, bool eraserDown)
	{
		// Map the raw digitizer units to screen pixels, for logging only.
		var penPoint = TouchscreenDeviceMapper.MapDigitizerPoint(data, penContact);
		CursorDiagnosticLogger.RecordInputSample("stylus", "pen_report", penPoint, cursorPoint,
			new
			{
				rawX = penContact.X,
				rawY = penContact.Y,
				minX = penContact.MinX,
				maxX = penContact.MaxX,
				minY = penContact.MinY,
				maxY = penContact.MaxY,
				tipDown,
				eraserDown,
				stylusMonitor = CursorDiagnosticLogger.GetMonitorAtPoint(penPoint),
				cursorMonitor = CursorDiagnosticLogger.GetMonitorAtPoint(cursorPoint),
				target = WindowDragController.DescribeWindowAtPoint(penPoint)
			});
	}
}
