namespace ThreeFingerDrag;

internal sealed partial class MouseDragProcessor
{
	/// <summary>Maximum pointer travel, in pixels, that still counts as a stationary hold.</summary>
	private const int HoldTolerancePixels = 6;
}