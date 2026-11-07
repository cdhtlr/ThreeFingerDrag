using System.Diagnostics;

namespace ThreeFingerDrag;

internal static partial class WindowDragController
{
	private const uint GaRoot = 2;
	private const int SwRestore = 9;
	private const int SmCyCaption = 4;
	private const int SmCxFrame = 32;
	private const int SmCyFrame = 33;
	private const int SmCxPaddedBorder = 92;
	private const int SmXVirtualScreen = 76;
	private const int SmYVirtualScreen = 77;
	private const int SmCxVirtualScreen = 78;
	private const int SmCyVirtualScreen = 79;
	private const uint SwpNoSize = 0x0001;
	private const uint SwpNoZOrder = 0x0004;
	private const uint SwpNoActivate = 0x0010;
	private const uint SwpAsyncWindowPos = 0x4000;
	private const int GwsMaximize = 0x01000000;
	private const int GwlStyle = -16;
	private const uint ClrInvalid = 0xFFFFFFFF;
	private const uint WmNcHitTest = 0x0084;
	private const uint SmtoAbortIfHung = 0x0002;
	// Each caption probe waits at most 20 ms, so a hung app cannot stall the input thread.
	private static uint TitleBarHitTestTimeoutMilliseconds => (uint)PointerSettings.GetTitleBarHitTestTimeoutMs();
	// The caption search looks up to 256 px left and right of the cursor, in 8 px steps.
	private const int TitleBarSearchRadiusPx = 256;
	private const int TitleBarSearchStepPx = 8;
	private const int GwlExStyle = -20;
	private const int WsExTopMost = 0x00000008;
	private const int HtCaption = 2;
	private const int HtSysMenu = 3;

	private const uint DwmwaExtendedFrameBounds = 9;

	// Timer period for smoothing, about 125 updates per second.
	private static double SmoothingFrameMs => PointerSettings.GetWindowDragSmoothingFrameMs();
	// The window trails the input by about this many pixels at any speed. The easing time constant
	// is derived from the window's current speed, so slow motion stays soft and fast motion stays responsive.
	private const double SmoothingLagPx = 12;
	// Clamp the time constant so very slow motion does not lag for seconds and very fast motion does not snap.
	// The smoothing math runs in Stopwatch ticks, so these bounds are converted once here, not per tick.
	// The smoothing math runs in Stopwatch ticks. Bounds are converted on each read so config changes apply.
	private static double SmoothingFrameTicks => SmoothingFrameMs * Stopwatch.Frequency / 1000.0;
	private static double MinSmoothingTimeConstantTicks => PointerSettings.GetWindowDragSmoothingMinTimeConstantMs() * Stopwatch.Frequency / 1000.0;
	private static double MaxSmoothingTimeConstantTicks => PointerSettings.GetWindowDragSmoothingMaxTimeConstantMs() * Stopwatch.Frequency / 1000.0;
	// Cap the elapsed time per tick so a stalled timer does not cause one huge jump.
	private static double MaxSmoothingElapsedTicks => PointerSettings.GetWindowDragSmoothingMaxElapsedMs() * Stopwatch.Frequency / 1000.0;
	// Largest move per axis in one tick, so a big queued distance is eased in over several ticks.
	private const int MaxSmoothingStepPx = 40;
}
