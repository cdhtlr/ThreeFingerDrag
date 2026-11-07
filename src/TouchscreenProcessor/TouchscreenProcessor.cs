using Linearstar.Windows.RawInput;

namespace ThreeFingerDrag;

/// <summary>Routes touchscreen contact coordinates through the shared drag engine.</summary>
internal sealed partial class TouchscreenProcessor : IDisposable
{
	// Instance state. Accessed on the raw input thread, except contactLostGraceActive, which the timer reads.
	/// <summary>Performs cursor moves and window drags for this processor.</summary>
	private readonly DragEngine dragEngine;
	/// <summary>Fires once after a stationary hold. Arms forced window-drag mode.</summary>
	private readonly System.Timers.Timer forceWindowDragHoldTimer;
	// Keep OCR's hold duration independent from the optional forced-window-drag hold.
	private readonly System.Timers.Timer ocrHoldTimer;
	// Polls the cancel key every 75 ms while a drag is active.
	private readonly System.Timers.Timer cancelDragPollTimer = new(PointerSettings.GetTouchscreenCancelDragPollIntervalMs()) { AutoReset = true };
	private readonly System.Timers.Timer contactLostGraceTimer;
	/// <summary>True while the grace timer runs after a finger lift during a drag.</summary>
	private volatile bool contactLostGraceActive;
	/// <summary>True from the moment the required fingers are down until the contact ends.</summary>
	private bool contactDown;
	/// <summary>True while a drag started by this contact is running.</summary>
	private bool dragActive;
	/// <summary>Set when movement passed the tolerance before a drag. The hold can no longer force window mode.</summary>
	private bool holdCanceled;
	/// <summary>True when the stationary hold has armed forced window-drag mode for this contact.</summary>
	private bool holdArmed;
	/// <summary>Set after a drag ends or fails. Blocks a new drag until all fingers lift.</summary>
	private bool suppressDragUntilLift;
	/// <summary>True when another device owns the cursor. The touch then moves its window without moving the cursor.</summary>
	private bool independentWindowDrag;
	/// <summary>Window session for an independent drag. Null when no window was found.</summary>
	private WindowDragController.DragSession? independentWindowSession;
	/// <summary>Centroid when the contact began. Hold and drag tolerances are measured from here.</summary>
	private Point holdStartPoint;
	/// <summary>Centroid from the previous report. Movement deltas are measured from here.</summary>
	private Point lastPoint;
	/// <summary>Centroid from the latest report. Used for the hold and OCR checks.</summary>
	private Point currentPoint;
	// IDs of the fingers that started this contact. Later reports are filtered by these IDs, so a new finger cannot join the gesture.
	private int[] activeContactIds = [];
	internal static readonly string[] details = new[] { "Finger", "Pen" };

	/// <summary>Creates the processor. It uses the shared engine to move windows and drag elements.</summary>
	public TouchscreenProcessor(DragEngine dragEngine)
	{
		this.dragEngine = dragEngine;
		// Fires once after a stationary hold to arm forced window-drag mode, if that option is on.
		forceWindowDragHoldTimer = new System.Timers.Timer(PointerSettings.GetDragHoldDurationMs()) { AutoReset = false };
		forceWindowDragHoldTimer.Elapsed += ForceWindowDragHoldTimerElapsed;
		// Fires once after a stationary hold to start OCR. Its duration comes from settings.
		ocrHoldTimer = new System.Timers.Timer(PointerSettings.GetOcrHoldDurationMs()) { AutoReset = false };
		ocrHoldTimer.Elapsed += (_, _) =>
		{
			// OCR runs only for a stationary contact that is not dragging.
			if (contactDown && !dragActive && !DistanceExceedsTolerance(holdStartPoint, currentPoint))
			{
				CursorDiagnosticLogger.Record("touchscreen_processor", "ocr_hold_fired", null, currentPoint);
				OcrCaptureService.TriggerCapture(currentPoint, "touchscreen");
			}
		};
		cancelDragPollTimer.Elapsed += (_, _) => { if (dragActive && ForcedDragModeService.IsCancelDragKeyPressed()) CancelDrag(); };
		// Grace timer length. A finger lifted during a drag can come back within this time.
		contactLostGraceTimer = new System.Timers.Timer(ContactLostGraceMs) { AutoReset = false };
		contactLostGraceTimer.Elapsed += (_, _) =>
		{
			// No finger came back in time. End the drag.
			if (!contactLostGraceActive) return;
			CursorDiagnosticLogger.Record("touchscreen_processor", "contact_lost_grace_expired", null, currentPoint, new { dragActive });
			EndContact("contact_lost_grace_expired");
		};
		CursorDiagnosticLogger.Record("touchscreen_processor", "processor_created", null, null, new { holdMs = PointerSettings.GetDragHoldDurationMs(), ocrHoldMs = PointerSettings.GetOcrHoldDurationMs(), forceHoldEnabled = PointerSettings.IsTouchHoldForceWindowDragEnabled() });
	}

	/// <summary>Registers for raw touchscreen input on the given window.</summary>
	public static void Register(IntPtr hwnd)
	{
		RawInputDevice.RegisterDevice(HidUsageAndPage.TouchScreen, RawInputDeviceFlags.InputSink, hwnd);
		CursorDiagnosticLogger.Record("touchscreen_processor", "raw_input_registered", null, null, new { hwnd = hwnd.ToInt64() });
	}

	/// <summary>Returns true when the contact has moved farther than the allowed tap tolerance from its start point.</summary>
	private static bool DistanceExceedsTolerance(Point start, Point current)
	{
		var dx = current.X - start.X;
		var dy = current.Y - start.Y;
		return (long)dx * dx + (long)dy * dy > (long)HoldStillTolerancePixels * HoldStillTolerancePixels;
	}
}
