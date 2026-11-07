using Linearstar.Windows.RawInput;

namespace ThreeFingerDrag;

/// <summary>Translates Windows digitizer and mouse reports into touchpad drag gestures.</summary>
public sealed partial class TouchProcessor : IDisposable
{
	/// <summary>High-level contact states used by gesture recognition.</summary>
	public enum State
	{
		CLEAR,
		ONE_FINGER_IDLE,
		ONE_FINGER_MOVING,
		TWO_FINGER_IDLE,
		TWO_FINGER_MOVING,
		THREE_FINGER_IDLE,
		THREE_FINGER_MOVING,
		THREE_FINGER_DRAG,
		FOUR_OR_MORE,
	}

	/// <summary>Selects whether touch movement is sent as mouse input or applied to a window.</summary>
	private enum DragExecutionMode
	{
		Mouse,
		BackgroundWindow,
	}

	/// <summary>Current gesture state, driven by the finger count and drag progress.</summary>
	private State state = State.CLEAR;
	/// <summary>Finger travel, in digitizer units, since the last contact change. Above 15 it moves an idle state to its moving state.</summary>
	private double accumulatedDistance;
	/// <summary>Fires once after a stationary three-finger hold, to start OCR capture.</summary>
	private readonly System.Timers.Timer ocrHoldTimer;
	/// <summary>Fires once after a stationary hold, to force window-drag mode when that option is enabled.</summary>
	private readonly System.Timers.Timer forceWindowDragHoldTimer;
	/// <summary>Polls the cancel key every 75 ms while a drag is active.</summary>
	private readonly System.Timers.Timer cancelDragKeyPollTimer;
	/// <summary>Releases the drop anchor when the post-lift grace time runs out.</summary>
	private readonly System.Timers.Timer dragDropAnchorHoldTimer;
	/// <summary>Owns the window drag sessions and performs window moves.</summary>
	private readonly DragEngine dragEngine;
	/// <summary>Contacts from the previous packet. Used to match finger identities and measure motion.</summary>
	private RawInputDigitizerContact[] previousContacts = [];
	/// <summary>Centroid of the previous packet. Null after a reset, so the next packet measures no step.</summary>
	private Point? previousCenterPoint;
	/// <summary>Finger count from the previous packet. Used to detect fingers going down or up.</summary>
	private int previousContactCount;
	/// <summary>Finger count from the latest packet. Read by timer callbacks on other threads.</summary>
	private volatile int currentContactCount;
	/// <summary>The open window drag session for this processor, or null when there is none.</summary>
	private WindowDragController.DragSession? activeDragSession => dragEngine.GetSession(this);
	/// <summary>Whether the current drag moves the cursor (Mouse) or moves a window in the background (BackgroundWindow).</summary>
	private DragExecutionMode dragExecutionMode = DragExecutionMode.Mouse;
	/// <summary>Screen point that pins the drop after a drag ends. Null when no anchor is active.</summary>
	private Point? dragDropAnchorCursorPoint;
	/// <summary>Cursor position seen on the previous check. Used to detect real mouse movement during the anchor hold.</summary>
	private Point? lastObservedCursorPointForAnchor;
	/// <summary>When the cursor last stayed still. The anchor is set once this has lasted for the hold time.</summary>
	private long? dragDropAnchorStableSinceTickMs;
	/// <summary>Touch centroid when the anchor was set. Finger movement away from it is measured against this point.</summary>
	private Point? anchorTouchCenter;
	/// <summary>Finger travel since the anchor was set. Used for flick and swipe-release decisions.</summary>
	private double accumulatedAnchorTouchDistance;
	/// <summary>Set after a flick drop. Blocks a new drag until the fingers lift or settle still.</summary>
	private bool suppressDragUntilFingersLiftOrStop;
	/// <summary>When the fingers became still after a flick drop. Cleared as soon as they move.</summary>
	private DateTime? stationarySinceUtc;
	/// <summary>Cursor point saved just before touchpad lift-off jitter. Used as the drop point.</summary>
	private Point? recentCleanCursorPoint;
	/// <summary>Cursor point from the previous drag frame. It is copied to recentCleanCursorPoint on the next frame.</summary>
	private Point? previousAppliedCursorPoint;
	private readonly System.Timers.Timer fingerLiftGraceTimer;
	/// <summary>True while the grace period runs after a finger lift during a drag.</summary>
	private volatile bool fingerLiftGraceActive;

	/// <summary>Creates the processor and its timers. Timer intervals come from PointerSettings.</summary>
	internal TouchProcessor(DragEngine dragEngine)
	{
		this.dragEngine = dragEngine;
		// Starts optional OCR capture after a stationary three-finger hold.
		ocrHoldTimer = new System.Timers.Timer(PointerSettings.GetOcrHoldDurationMs()) { AutoReset = false };
		ocrHoldTimer.Elapsed += OcrHoldTimerElapsed;

		// Arms the window-drag override after the configured stationary hold.
		forceWindowDragHoldTimer = new System.Timers.Timer(PointerSettings.GetDragHoldDurationMs()) { AutoReset = false };
		forceWindowDragHoldTimer.Elapsed += ForceWindowDragHoldTimerElapsed;

		// Checks the cancel key only while a drag is active.
		cancelDragKeyPollTimer = new System.Timers.Timer(PointerSettings.GetTouchCancelDragPollIntervalMs()) { AutoReset = true };
		cancelDragKeyPollTimer.Elapsed += CancelDragKeyPollTimerElapsed;

		// Holds the drop point briefly to absorb the touchpad's post-lift cursor resync.
		dragDropAnchorHoldTimer = new System.Timers.Timer(PointerSettings.GetTouchpadDragDropAnchorHoldMs()) { AutoReset = false };
		dragDropAnchorHoldTimer.Elapsed += DragDropAnchorHoldTimerElapsed;

		// Enables the hook to block touchpad movement that conflicts with an active drag.
		MouseInject.ShouldSuppressExternalCursorMovement = ShouldSuppressExternalCursorMovement;

		// Grace period: waits before ending a drag when a finger lifts accidentally.
		fingerLiftGraceTimer = new System.Timers.Timer(FingerLiftGraceMs) { AutoReset = false };
		fingerLiftGraceTimer.Elapsed += (_, _) =>
		{
			// If the grace period expires and the drag has not been resumed, end it.
			if (fingerLiftGraceActive)
			{
				fingerLiftGraceActive = false;
				CursorDiagnosticLogger.Record("touch_finger_lift_grace", "expired");
				FreezeAndStopDrag("finger_lift_grace_expired");
			}
		};
		CursorDiagnosticLogger.Record("touch_processor", "created", details: new { ocrHoldMs = PointerSettings.GetOcrHoldDurationMs(), dragHoldMs = PointerSettings.GetDragHoldDurationMs(), anchorHoldMs = PointerSettings.GetTouchpadDragDropAnchorHoldMs(), fingerLiftGraceMs = FingerLiftGraceMs });
	}

	/// <summary>True after Dispose has run.</summary>
	public bool IsDisposed { get; private set; }

	/// <summary>Handler signature for state change notifications.</summary>
	public delegate void StateChangeEvent(object sender, State newState);
	/// <summary>Raised after every state change, with the new state.</summary>
	public event StateChangeEvent? StateChange;

	/// <summary>
	/// Registers the touchpad and mouse raw input devices so their reports arrive as WM_INPUT
	/// on the given window.
	/// </summary>
	public static void Register(IntPtr hwnd)
	{
		RawInputDevice.RegisterDevice(HidUsageAndPage.TouchPad, RawInputDeviceFlags.InputSink, hwnd);
		RawInputDevice.RegisterDevice(HidUsageAndPage.Mouse, RawInputDeviceFlags.InputSink, hwnd);
		CursorDiagnosticLogger.Record("touch_processor", "raw_input_registered", details: new { hwnd = hwnd.ToInt64() });
	}

	/// <summary>
	/// Handles a mouse report. A left button release ends a three-finger drag that is still open.
	/// </summary>
	public void Update(RawInputMouseData data)
	{
		UpdateDragDropAnchorHoldState();

		// Mouse and digitizer reports are interleaved, so reapply the anchor on every mouse packet.
		ReapplyDragDropAnchor();

		if (!data.Mouse.Buttons.HasFlag(Linearstar.Windows.RawInput.Native.RawMouseButtonFlags.LeftButtonUp)) return;
		// Fallback sessions end by finger lift. Our injected release of the native drag must not end them.
		if (activeDragSession is { UsingWindowPositionFallback: true })
		{
			CursorDiagnosticLogger.Record("touch_mouse_report", "left_button_up_ignored_fallback");
			return;
		}

		if (state == State.THREE_FINGER_DRAG || HasOpenDragSession)
		{
			CursorDiagnosticLogger.Record("touch_mouse_report", "left_button_up_stop_drag", details: new { state = state.ToString() });
			SetState(State.CLEAR);
			StopActiveDrag("mouse_left_button_up_observed");
		}
	}

	/// <summary>
	/// Stops the timers, unregisters raw input, and clears the cursor suppression hook.
	/// Safe to call more than once.
	/// </summary>
	public void Dispose()
	{
		if (IsDisposed) return;

		CursorDiagnosticLogger.Record("touch_processor", "dispose");
		ocrHoldTimer.Dispose();
		forceWindowDragHoldTimer.Dispose();
		cancelDragKeyPollTimer.Dispose();
		dragDropAnchorHoldTimer.Dispose();
		fingerLiftGraceTimer.Dispose();
		RawInputDevice.UnregisterDevice(HidUsageAndPage.TouchPad);
		RawInputDevice.UnregisterDevice(HidUsageAndPage.Mouse);

		// Clear the disposed instance from the hook; null means movement is not suppressed.
		MouseInject.ShouldSuppressExternalCursorMovement = null;

		IsDisposed = true;
		GC.SuppressFinalize(this);
	}

	/// <summary>Finalizer. Calls Dispose if the owner did not.</summary>
	~TouchProcessor() => Dispose();

	/// <summary>Whether external cursor movement should be blocked during a drag or anchor hold.</summary>
	private bool ShouldSuppressExternalCursorMovement() => state == State.THREE_FINGER_DRAG || dragDropAnchorCursorPoint.HasValue;

	/// <summary>
	/// Whether a drag session is open, including one paused while extra fingers are down. FSM state
	/// alone cannot distinguish a paused drag from an unrelated four-finger gesture.
	/// </summary>
	private bool HasOpenDragSession => activeDragSession is not null || MouseInject.LeftButtonPressed;

	/// <summary>Unused fast inverse square root helper. Not called by the gesture code.</summary>
	private static float InvSqrt(float value)
	{
		var half = 0.5f * value;
		var bits = BitConverter.SingleToInt32Bits(value);
		// Initial guess from the magic constant, then one Newton-Raphson refinement step.
		bits = 0x5f3759df - (bits >> 1);
		value = BitConverter.Int32BitsToSingle(bits);
		value *= 1.5f - (half * value * value);
		return value;
	}
}
