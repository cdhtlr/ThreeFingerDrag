using System.Timers;
using Linearstar.Windows.RawInput;

namespace ThreeFingerDrag;

/// <summary>Drag, hold, and contact-end handling for <see cref="TouchscreenProcessor"/>.</summary>
internal sealed partial class TouchscreenProcessor
{
	private static TimeSpan RecentExternalCursorMovement => TimeSpan.FromMilliseconds(PointerSettings.GetTouchscreenRecentExternalCursorMovementMs());

	/// <summary>Arms forced window-drag mode if the touch stayed within tolerance for the full hold.</summary>
	private void ForceWindowDragHoldTimerElapsed(object? sender, ElapsedEventArgs e)
	{
		if (!contactDown || holdCanceled || holdArmed || !PointerSettings.IsTouchHoldForceWindowDragEnabled() || DistanceExceedsTolerance(holdStartPoint, currentPoint)) return;
		holdArmed = true;
		ForcedDragModeService.SetTouchHoldForced(true);
		DragModeIndicatorOverlay.ShowHoldForceWindowDrag(currentPoint, "Touchscreen hold forced");
		CursorDiagnosticLogger.Record("touchscreen_drag", "window_drag_armed", holdStartPoint, currentPoint, new { holdMs = PointerSettings.GetDragHoldDurationMs() });
	}

	/// <summary>Ends the drag at a flick, dropping the window where it is. Independent drags are only released.</summary>
	private void DropOnFlick(Point point)
	{
		if (independentWindowDrag)
		{
			independentWindowSession = null;
			dragActive = false;
			suppressDragUntilLift = true;
			CursorDiagnosticLogger.Record("touchscreen_drag", "independent_window_drag_ended", point, null, new { reason = "flick_drop" });
			return;
		}
		var session = dragEngine.GetSession(this);
		dragEngine.EndDrag(this, "flick_drop");
		dragActive = false;
		cancelDragPollTimer.Stop();
		CursorDiagnosticLogger.Record("touchscreen_drag", "flick_drop", point, point, new { windowHandle = session?.WindowHandle.ToInt64() });
	}

	/// <summary>Ends the drag because the cancel key was pressed. Disarms forced mode too.</summary>
	private void CancelDrag()
	{
		dragEngine.EndDrag(this, "cancel_key_pressed");
		if (holdArmed) ForcedDragModeService.SetTouchHoldForced(false);
		holdArmed = false;
		CursorDiagnosticLogger.Record("touchscreen_drag", "drag_cancelled", lastPoint, currentPoint);
	}

	/// <summary>True when another device controls the cursor: a drag from another processor, a held mouse trigger, an active pen, or external cursor movement in the last 180 ms.</summary>
	private bool HasConcurrentCursorInput() => dragEngine.HasActiveDragFromOther(this)
		|| MouseDragProcessor.IsTriggerHeld
		|| StylusProcessor.IsPenContactActive
		|| DateTime.UtcNow - MouseInject.LastExternalCursorMovementUtc < RecentExternalCursorMovement;

	/// <summary>Ends the contact. Stops timers, ends any drag, pastes pending OCR text, and clears per-contact state.</summary>
	private void EndContact(string reason)
	{
		if (!contactDown) return;

		contactLostGraceActive = false;
		contactLostGraceTimer.Stop();
		ocrHoldTimer.Stop();
		forceWindowDragHoldTimer.Stop();
		cancelDragPollTimer.Stop();
		if (dragActive)
		{
			if (independentWindowDrag)
			{
				independentWindowSession = null;
				CursorDiagnosticLogger.Record("touchscreen_drag", "independent_window_drag_ended", lastPoint, null, new { reason });
			} else
			{
				var movedWindow = dragEngine.GetSession(this) is not null;
				dragEngine.EndDrag(this, reason);
				if (OcrCaptureService.HasPendingDropText) OcrCaptureService.CompleteDrop(paste: true);
				CursorDiagnosticLogger.Record("touchscreen_drag", "contact_ended", lastPoint, null, new { reason, movedWindow });
			}
		} else
		{
			CursorDiagnosticLogger.Record("touchscreen_drag", "contact_ended_without_drag", lastPoint, null, new { reason, holdArmed, holdCanceled, suppressDragUntilLift });
		}
		contactDown = false;
		activeContactIds = [];
		dragActive = false;
		independentWindowDrag = false;
		holdCanceled = false;
		suppressDragUntilLift = false;
		if (holdArmed)
		{
			ForcedDragModeService.SetTouchHoldForced(false);
			CursorDiagnosticLogger.Record("touchscreen_drag", "hold_forced_disarmed", lastPoint, null, new { reason });
		}

		holdArmed = false;
	}

	/// <summary>Ends the contact, disposes timers, and unregisters the touchscreen device.</summary>
	public void Dispose()
	{
		EndContact("processor_disposed");
		forceWindowDragHoldTimer.Dispose();
		ocrHoldTimer.Dispose();
		cancelDragPollTimer.Dispose();
		contactLostGraceTimer.Dispose();
		RawInputDevice.UnregisterDevice(HidUsageAndPage.TouchScreen);
		CursorDiagnosticLogger.Record("touchscreen_drag", "processor_disposed");
		GC.SuppressFinalize(this);
	}
}
