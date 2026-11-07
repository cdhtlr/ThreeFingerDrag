using System.Timers;

namespace ThreeFingerDrag;

public sealed partial class TouchProcessor
{
	/// <summary>Timer callback. Starts OCR capture if the fingers are still in the drag posture.</summary>
	private void OcrHoldTimerElapsed(object? sender, ElapsedEventArgs e)
	{
		if (IsConfiguredStationaryHoldState())
		{
			CursorDiagnosticLogger.Record("touch_ocr_hold", "triggered", details: new { state = state.ToString(), contacts = currentContactCount });
			OcrCaptureService.TriggerCapture();
		}
		else CursorDiagnosticLogger.Record("touch_ocr_hold", "skipped_not_in_hold_state", details: new { state = state.ToString(), contacts = currentContactCount });
	}

	/// <summary>Timer callback. Forces window-drag mode and shows its indicator if the hold is still active.</summary>
	private void ForceWindowDragHoldTimerElapsed(object? sender, ElapsedEventArgs e)
	{
		if (!IsConfiguredStationaryHoldState())
		{
			CursorDiagnosticLogger.Record("touch_force_window_drag_hold", "skipped_not_in_hold_state", details: new { state = state.ToString(), contacts = currentContactCount });
			return;
		}

		CursorDiagnosticLogger.Record("touch_force_window_drag_hold", "forced", details: new { state = state.ToString(), contacts = currentContactCount });
		ForcedDragModeService.SetTouchHoldForced(true);
		if (PointerSettings.ShouldShowTouchHoldForceWindowDragIndicator())
		{
			var cursorPoint = MouseInject.LastAppliedCursorPoint ?? (MouseInject.TryGetCursorPoint(out var liveCursorPoint) ? liveCursorPoint : default);
			DragModeIndicatorOverlay.ShowHoldForceWindowDrag(cursorPoint, "Touchpad hold forced");
		}
	}

	/// <summary>Timer callback. Ends the drag when the configured cancel key is pressed.</summary>
	private void CancelDragKeyPollTimerElapsed(object? sender, ElapsedEventArgs e)
	{
		if (state == State.THREE_FINGER_DRAG && ForcedDragModeService.IsCancelDragKeyPressed())
		{
			CursorDiagnosticLogger.Record("touch_cancel_key", "pressed", details: new { cancelKey = PointerSettings.GetDragCancelKey() });
			SetState(State.CLEAR);
			StopActiveDrag("cancel_key_pressed", pasteOcrText: false);
		}
	}

	/// <summary>Maps a finger count to its idle state. Four or more fingers share one state.</summary>
	private static State MapState(int contactsCount) => contactsCount switch
	{
		0 => State.CLEAR,
		1 => State.ONE_FINGER_IDLE,
		2 => State.TWO_FINGER_IDLE,
		3 => State.THREE_FINGER_IDLE,
		_ => State.FOUR_OR_MORE,
	};

	/// <summary>Maps a finger count to its moving state. Four or more fingers share one state.</summary>
	private static State MapMovingState(int fingerCount) => fingerCount switch
	{
		1 => State.ONE_FINGER_MOVING,
		2 => State.TWO_FINGER_MOVING,
		3 => State.THREE_FINGER_MOVING,
		_ => State.FOUR_OR_MORE,
	};

	/// <summary>
	/// Changes the gesture state. Arms or cancels the hold timers and the cancel-key poll,
	/// then raises StateChange.
	/// </summary>
	private void SetState(State newState)
	{
		var previousState = state;
		var oldState = state;
		state = newState;

		// Arm hold actions on entry to the configured stationary contact state; cancel them on exit.
		if (newState == MapState(PointerSettings.GetTouchpadDragFingerCount()) && currentContactCount == PointerSettings.GetTouchpadDragFingerCount())
		{
			ocrHoldTimer.Stop();
			ocrHoldTimer.Start();

			// Arm the optional touch-hold window-drag override.
			if (PointerSettings.IsTouchHoldForceWindowDragEnabled())
			{
				forceWindowDragHoldTimer.Interval = PointerSettings.GetDragHoldDurationMs();
				forceWindowDragHoldTimer.Stop();
				forceWindowDragHoldTimer.Start();
			}
		} else if (previousState == MapState(PointerSettings.GetTouchpadDragFingerCount()))
		{
			ocrHoldTimer.Stop();
			forceWindowDragHoldTimer.Stop();

			// Keep a triggered override for the ensuing drag; clear it if the gesture was abandoned.
			if (newState != MapMovingState(PointerSettings.GetTouchpadDragFingerCount())) ForcedDragModeService.SetTouchHoldForced(false);
		}

		// Poll the cancel key only during an active drag and when one is configured.
		if (newState == State.THREE_FINGER_DRAG)
		{
			if (!string.IsNullOrEmpty(PointerSettings.GetDragCancelKey())) cancelDragKeyPollTimer.Start();
		} else
		{
			cancelDragKeyPollTimer.Stop();
		}

		StateChange?.Invoke(this, newState);
		CursorDiagnosticLogger.Record("touch_state", "state_change", null, null, new { before = oldState.ToString(), after = newState.ToString() });
	}

	/// <summary>True when the fingers sit at the configured drag count and the state matches it.</summary>
	private bool IsConfiguredStationaryHoldState() => state == MapState(PointerSettings.GetTouchpadDragFingerCount()) && currentContactCount == PointerSettings.GetTouchpadDragFingerCount();
}
