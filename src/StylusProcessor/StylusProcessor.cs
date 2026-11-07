using Linearstar.Windows.RawInput;

namespace ThreeFingerDrag;

/// <summary>Routes pen drags through the shared smart-drag engine, with an optional hold-to-force mode.</summary>
internal sealed partial class StylusProcessor : IDisposable
{
	/// <summary>Guards pen state shared between the raw input thread and timer callbacks.</summary>
	private readonly object sync = new();
	private readonly DragEngine dragEngine;
	/// <summary>Fires once after the hold duration. Arms forced window mode if the pen is still.</summary>
	private readonly System.Timers.Timer holdTimer = new() { AutoReset = false };
	// OCR uses the same stationary-hold gesture as touch input, independently of forced-drag mode.
	private readonly System.Timers.Timer ocrHoldTimer = new() { AutoReset = false };
	/// <summary>Set once Dispose runs. Every callback checks it and does nothing afterwards.</summary>
	private bool isDisposed;
	/// <summary>True from pen-tip contact until lift.</summary>
	private bool penDown;
	/// <summary>Set when the pen moved past the tolerance. The hold can no longer force window mode or start OCR.</summary>
	private bool holdCanceled;
	/// <summary>True when the stationary hold has armed forced window-drag mode for this contact.</summary>
	private bool holdArmed;
	/// <summary>True between a successful drag start and the end of the contact.</summary>
	private bool dragActive;
	/// <summary>True while the pen tip is touching. Read by other components; written on the input thread.</summary>
	public static volatile bool IsPenContactActive;
	/// <summary>Cursor point where the pen touched. Hold and drag tolerances are measured from here.</summary>
	private Point holdStartPoint;
	/// <summary>Last cursor point handled. Each movement step is measured from here.</summary>
	private Point lastMovePoint;

	/// <summary>Creates the processor. It uses the shared engine to move windows and drag elements.</summary>
	public StylusProcessor(DragEngine dragEngine)
	{
		this.dragEngine = dragEngine;
		CursorDiagnosticLogger.Record("stylus_processor", "processor_created");
		// Forced window mode is armed by the hold timer, not by the pen moving.
		holdTimer.Elapsed += HoldTimerElapsed;
		ocrHoldTimer.Elapsed += (_, _) =>
		{
			lock (sync)
			{
				// OCR runs only for a still pen that has not been canceled by movement.
				if (!isDisposed && penDown && !holdCanceled && !DistanceExceedsTolerance(holdStartPoint, lastMovePoint))
				{
					CursorDiagnosticLogger.Record("stylus_processor", "ocr_hold_elapsed", lastMovePoint, lastMovePoint, details: new { source = "stylus" });
					OcrCaptureService.TriggerCapture(lastMovePoint, "stylus");
				}
			}
		};
	}

	/// <summary>Registers for raw pen input on the given window, so pen reports reach Update.</summary>
	public static void Register(IntPtr hwnd)
	{
		RawInputDevice.RegisterDevice(HidUsageAndPage.Pen, RawInputDeviceFlags.InputSink, hwnd);
		CursorDiagnosticLogger.Record("stylus_processor", "pen_device_registered", details: new { hwnd = hwnd.ToInt64() });
	}

	/// <summary>Ends any active contact, disposes the OCR timer, and unregisters the pen device.</summary>
	public void Dispose()
	{
		lock (sync)
		{
			if (isDisposed) return;
			isDisposed = true;
			CursorDiagnosticLogger.Record("stylus_processor", "processor_disposed");
			EndContact("processor_disposed");
			ocrHoldTimer.Dispose();
		}
		RawInputDevice.UnregisterDevice(HidUsageAndPage.Pen);
		CursorDiagnosticLogger.Record("stylus_processor", "pen_device_unregistered");
		GC.SuppressFinalize(this);
	}
}
