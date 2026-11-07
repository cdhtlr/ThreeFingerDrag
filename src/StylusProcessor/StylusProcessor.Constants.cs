namespace ThreeFingerDrag;

internal sealed partial class StylusProcessor
{
	/// <summary>Maximum pen travel, in pixels, that still counts as a stationary hold.</summary>
	private const int HoldStillTolerancePixels = 6;
}