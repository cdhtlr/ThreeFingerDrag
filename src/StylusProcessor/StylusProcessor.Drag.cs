namespace ThreeFingerDrag;

internal sealed partial class StylusProcessor
{
	/// <summary>Starts a drag session. Forced drags fail if no window is found under the pen.</summary>
	private bool StartWindowDrag(Point point, bool forced)
	{
		if (!dragEngine.TryStartDrag(this, out var session, physicalButtonAlreadyHeld: true)) return false;
		// A forced drag needs a real window. Without one, undo the arming and fall back.
		if (forced && session is null)
		{
			dragEngine.EndDrag(this, "forced_window_drag_failed");
			CursorDiagnosticLogger.Record("stylus_drag", "forced_window_drag_start_failed", holdStartPoint, point, details: new { reason = "no_window_under_pen" });
			holdArmed = false;
			ForcedDragModeService.SetStylusHoldForced(false);
			return false;
		}

		dragActive = true;
		// The mode name is used only for the diagnostic log.
		var mode = forced ? "forced_window_drag_started" : session is null ? "smart_drag_started" : "window_drag_started";
		CursorDiagnosticLogger.Record("stylus_drag", mode, holdStartPoint, point,
			new
			{
				windowHandle = session?.WindowHandle.ToInt64(),
				forced,
				inputSource = "stylus",
				inputPosition = point,
				target = CursorDiagnosticLogger.IsEnabled ? WindowDragController.DescribeWindowAtPoint(point) : null,
				element = CursorDiagnosticLogger.IsEnabled ? UiAutomationDragClassifier.DescribeElementAtPoint(point) : null
			});
		// Show the drag indicator only for a real window drag.
		if (session is not null) DragModeIndicatorOverlay.Show(point, isWindowDrag: true, forced ? "Stylus hold" : "Stylus drag", recognizedText: null);

		return true;
	}

	/// <summary>Ends the pen contact. Stops timers, ends any drag, pastes pending OCR text, and resets all per-contact state.</summary>
	private void EndContact(string reason)
	{
		holdTimer.Stop();
		ocrHoldTimer.Stop();
		if (dragActive)
		{
			dragEngine.EndDrag(this, reason);
			if (OcrCaptureService.HasPendingDropText) OcrCaptureService.CompleteDrop(paste: true);
			CursorDiagnosticLogger.Record("stylus_drag", "pen_contact_ended", lastMovePoint, null, new { reason, movedWindow = true });
		}
		else if (penDown)
		{
			CursorDiagnosticLogger.Record("stylus_drag", "pen_contact_ended_without_drag", lastMovePoint, null, new { reason, holdArmed, holdCanceled });
		}
		if (holdArmed)
		{
			CursorDiagnosticLogger.Record("stylus_drag", "hold_forced_disarmed", lastMovePoint, null, new { reason });
			ForcedDragModeService.SetStylusHoldForced(false);
		}

		penDown = false;
		IsPenContactActive = false;
		holdCanceled = false;
		holdArmed = false;
		dragActive = false;
	}
}
