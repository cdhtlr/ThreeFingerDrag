namespace ThreeFingerDrag;

/// <summary>Drag session lifecycle and hold-tolerance math.</summary>
internal sealed partial class MouseDragProcessor
{
	/// <summary>Starts a drag session. Returns false when the engine refuses it.</summary>
	private bool StartDrag(Point point)
	{
		// Ask the engine to classify the window under the pointer and start a session.
		if (!dragEngine.TryStartDrag(this, out var session)) return false;

		dragActive = true;
		// Prefer the live cursor position. It can differ from the raw input point.
		lastPoint = MouseInject.TryGetCursorPoint(out var current) ? current : point;
		// Only poll the cancel key when one is configured.
		if (!string.IsNullOrEmpty(PointerSettings.GetDragCancelKey())) cancelPollTimer.Start();

		CursorDiagnosticLogger.Record("mouse_gesture", forcedWindowMode ? "forced_window_drag_started" : "smart_drag_started", point, lastPoint, new { windowHandle = session?.WindowHandle.ToInt64(), classifiedWindow = session is not null });
		return true;
	}

	/// <summary>Ends the drag, pastes pending OCR text, and stops the cancel-key poll.</summary>
	private void EndDrag(string reason)
	{
		// Read the window handle before the engine drops the session, so it can be logged.
		var windowHandle = dragEngine.GetSession(this)?.WindowHandle.ToInt64();
		dragEngine.EndDrag(this, reason);
		// Paste any OCR text that was waiting for this drop.
		if (OcrCaptureService.HasPendingDropText) OcrCaptureService.CompleteDrop(paste: true);

		cancelPollTimer.Stop();
		CursorDiagnosticLogger.Record("mouse_gesture", "drag_ended", lastPoint, null, new { reason, windowHandle, forcedWindowMode });
		dragActive = false;
	}

	/// <summary>Returns true when the pointer has moved farther than the allowed tap tolerance from its start point. Compares squared distances, so no square root is needed.</summary>
	private static bool DistanceExceedsTolerance(Point start, Point current)
	{
		var dx = current.X - start.X;
		var dy = current.Y - start.Y;
		return (long)dx * dx + (long)dy * dy > (long)HoldTolerancePixels * HoldTolerancePixels;
	}
}
