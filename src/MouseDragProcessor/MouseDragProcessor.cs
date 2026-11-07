namespace ThreeFingerDrag;

/// <summary>Converts a recorded non-primary mouse button into smart drags and stationary-hold actions.</summary>
internal sealed partial class MouseDragProcessor : IDisposable
{
	/// <summary>Guards all state shared between the input thread and timer callbacks.</summary>
	private readonly object sync = new();
	/// <summary>Performs the actual window or cursor movement for the drag.</summary>
	private readonly DragEngine dragEngine;
	// Matches the touch gesture: a stationary trigger hold requests OCR before drag movement begins.
	private readonly System.Timers.Timer ocrHoldTimer = new() { AutoReset = false };
	// Fires once after the hold duration. Arms forced window mode if the pointer has not moved.
	private readonly System.Timers.Timer forceHoldTimer = new() { AutoReset = false };
	// Polls the cancel key every 75 ms while a drag is active, so the drag can be aborted from the keyboard.
	private readonly System.Timers.Timer cancelPollTimer = new(PointerSettings.GetMouseCancelDragPollIntervalMs()) { AutoReset = true };
	/// <summary>True while the trigger mouse button is down.</summary>
	private bool triggerHeld;
	/// <summary>True when the hold forced window-drag mode instead of a normal smart drag.</summary>
	private bool forcedWindowMode;
	/// <summary>True between a successful drag start and its end.</summary>
	private bool dragActive;
	/// <summary>True while the trigger is held and movement is being detected. The hook freezes the cursor during this time.</summary>
	private volatile bool detecting;
	/// <summary>True when the drag was refused for this press. Movement then goes back to normal tracking.</summary>
	private bool detectionRefused;
	/// <summary>Virtual pointer position during detection, built from raw mouse deltas while the cursor is frozen.</summary>
	private Point detectionPoint;
	/// <summary>Where the trigger was pressed. Hold and drag tolerances are measured from here.</summary>
	private Point pressPoint;
	/// <summary>Last pointer position handled. Each movement delta is measured from here.</summary>
	private Point lastPoint;
	/// <summary>Read by other components to know whether the trigger is down. Written on the input thread.</summary>
	public static volatile bool IsTriggerHeld;

	/// <summary>Creates the processor. It uses the shared engine to move windows and drag elements.</summary>
	public MouseDragProcessor(DragEngine dragEngine)
	{
		this.dragEngine = dragEngine;
		// Still held and still near the press point: the hold means the user wants OCR of the text under the pointer.
		ocrHoldTimer.Elapsed += (_, _) => {lock (sync) { var fire = triggerHeld && !DistanceExceedsTolerance(pressPoint, lastPoint); CursorDiagnosticLogger.Record("mouse_hold", fire ? "ocr_hold_triggered" : "ocr_hold_skipped", details: new { triggerHeld, dragActive }); if (fire) OcrCaptureService.TriggerCapture(); }};
		forceHoldTimer.Elapsed += (_, _) =>
		{
			lock (sync)
			{
				// Forced window mode needs a stationary hold. Skip if the trigger was released, a drag already started, or the pointer moved.
				if (!PointerSettings.IsClickHoldForceWindowDragEnabled() || !triggerHeld || dragActive || DistanceExceedsTolerance(pressPoint, lastPoint))
				{
					CursorDiagnosticLogger.Record("mouse_hold", "force_window_drag_skipped", details: new { triggerHeld, dragActive });
					return;
				}
				CursorDiagnosticLogger.Record("mouse_hold", "force_window_drag_armed");
				// Mark the window-drag mode for this press and tell the shared service, so the rest of the app knows.
				forcedWindowMode = true;
				ForcedDragModeService.SetMouseHoldForced(true);
				DragModeIndicatorOverlay.ShowHoldForceWindowDrag(pressPoint, "Mouse hold forced");
				CursorDiagnosticLogger.Record("mouse_gesture", "window_mode_armed", pressPoint, lastPoint, new { holdMs = PointerSettings.GetDragHoldDurationMs() });
			}
		};
		// Ends the drag when the configured cancel key is pressed.
		cancelPollTimer.Elapsed += (_, _) => {lock (sync) if (dragActive && ForcedDragModeService.IsCancelDragKeyPressed()) EndDrag("cancel_key_pressed");};
		// Hook the raw mouse input. Its handlers run on the input thread, so they take the lock.
		MouseInject.PhysicalMouseTriggerChanged = OnTriggerButton;
		MouseInject.PhysicalMouseMoved = OnMouseMoved;
		MouseInject.PhysicalMouseRawDelta = OnRawMouseDelta;
		MouseInject.ShouldHoldExternalCursorMovement = () => detecting;
	}


	/// <summary>Stops timers, ends any active drag, and removes the raw input hooks.</summary>
	public void Dispose()
	{
		lock (sync)
		{
			triggerHeld = false;
			IsTriggerHeld = false;
			detecting = false;
			forceHoldTimer.Stop();
			ocrHoldTimer.Stop();
			EndDrag("processor_disposed");
			ForcedDragModeService.SetMouseHoldForced(false);
			forceHoldTimer.Dispose();
			ocrHoldTimer.Dispose();
			cancelPollTimer.Dispose();
		}
		MouseInject.PhysicalMouseTriggerChanged = null;
		MouseInject.PhysicalMouseMoved = null;
		MouseInject.PhysicalMouseRawDelta = null;
		MouseInject.ShouldHoldExternalCursorMovement = null;
	}
}
