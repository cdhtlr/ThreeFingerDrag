namespace ThreeFingerDrag;

internal sealed partial class TouchscreenProcessor
{
	/// <summary>Maximum finger travel, in pixels, that still counts as a stationary hold.</summary>
	private const int HoldStillTolerancePixels = 6;

	// Grace period timer: when a finger accidentally lifts during a drag, wait this long
	// before ending the drag, giving the user a chance to re-place the finger.
	private static int ContactLostGraceMs => PointerSettings.GetTouchscreenContactLostGraceMs();
}