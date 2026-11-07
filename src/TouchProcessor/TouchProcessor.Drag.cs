namespace ThreeFingerDrag;

public sealed partial class TouchProcessor
{
	/// <summary>
	/// Stops the drag at its last stable cursor position when its contacts change, preventing
	/// post-lift touchpad movement from shifting the drop point.
	/// </summary>
	private void FreezeAndStopDrag(string reason)
	{
		// Cancel the grace period. The drag is ending for good.
		fingerLiftGraceActive = false;
		fingerLiftGraceTimer.Stop();

		var dropPoint = dragDropAnchorCursorPoint ?? recentCleanCursorPoint ?? MouseInject.LastAppliedCursorPoint ?? (MouseInject.TryGetCursorPoint(out var cursorPoint) ? cursorPoint : null);
		CursorDiagnosticLogger.Record("touch_drag", "freeze_and_stop", before: currentCursorPointOrNull(), after: dropPoint, details: new { reason, state = state.ToString(), dropX = dropPoint?.X, dropY = dropPoint?.Y });

		dragDropAnchorCursorPoint = dropPoint;
		if (dropPoint.HasValue) _ = MouseInject.TrySetCursorPoint(dropPoint.Value);

		SetState(State.CLEAR);
		StopActiveDrag(reason);
		accumulatedDistance = 0;
	}

	/// <summary>Drops the active drag on a flick, without requiring finger lift.</summary>
	private void ExecuteFlickToDrop()
	{
		var dropPoint = dragDropAnchorCursorPoint ?? recentCleanCursorPoint ?? MouseInject.LastAppliedCursorPoint ?? (MouseInject.TryGetCursorPoint(out var cp) ? cp : (Point?)null);

		if (dropPoint.HasValue) _ = MouseInject.TrySetCursorPoint(dropPoint.Value);

		CursorDiagnosticLogger.Record("touch_flick", "drop", after: dropPoint, details: new { dropX = dropPoint?.X, dropY = dropPoint?.Y, state = state.ToString() });
		// Go to idle, not moving, so the flick does not restart the drag.
		SetState(State.THREE_FINGER_IDLE);
		StopActiveDrag("flick_drop");
		ClearDragDropAnchorState();
		accumulatedDistance = 0;
		// Block new drag starts until the fingers lift or settle still.
		suppressDragUntilFingersLiftOrStop = true;
		stationarySinceUtc = null;
	}

	/// <summary>Current cursor position, or null when it cannot be read.</summary>
	private static Point? currentCursorPointOrNull() => MouseInject.TryGetCursorPoint(out var point) ? point : null;

	/// <summary>
	/// Ends the active drag, closes the window session, and starts the post-lift anchor hold.
	/// </summary>
	/// <param name="reason">Short label written to diagnostics.</param>
	/// <param name="pasteOcrText">When true, pending OCR text is pasted into the drop target.</param>
	private void StopActiveDrag(string reason, bool pasteOcrText = true)
	{
		CursorDiagnosticLogger.Record("touch_drag", "stop", details: new { reason, pasteOcrText, hasPendingOcrText = OcrCaptureService.HasPendingDropText, mode = dragExecutionMode.ToString(), hadAnchor = dragDropAnchorCursorPoint.HasValue });
		lastObservedCursorPointForAnchor = null;
		dragDropAnchorStableSinceTickMs = null;

		// Capture the stable drop point before releasing buttons.
		if (!dragDropAnchorCursorPoint.HasValue)
		{
			var dropPoint = recentCleanCursorPoint ?? MouseInject.LastAppliedCursorPoint ?? (MouseInject.TryGetCursorPoint(out var cp) ? cp : null);
			dragDropAnchorCursorPoint = dropPoint;
		}

		ReapplyDragDropAnchor();

		dragEngine.EndDrag(this, reason);
		// Inject text after releasing the drag button so the target receives it as a paste/type
		// action, rather than as keyboard input during the drag.
		if (OcrCaptureService.HasPendingDropText) OcrCaptureService.CompleteDrop(pasteOcrText);

		dragExecutionMode = DragExecutionMode.Mouse;
		ForcedDragModeService.SetTouchHoldForced(false);

		// Keep the anchor briefly to absorb delayed touchpad resync movement.
		if (dragDropAnchorCursorPoint.HasValue)
		{
			var graceMs = PointerSettings.GetTouchpadPostLiftAnchorGraceMs();
			CursorDiagnosticLogger.Record("touch_anchor", "hold_started", details: new { graceMs });
			dragDropAnchorHoldTimer.Interval = graceMs;
			dragDropAnchorHoldTimer.Stop();
			dragDropAnchorHoldTimer.Start();
		}
	}
}
