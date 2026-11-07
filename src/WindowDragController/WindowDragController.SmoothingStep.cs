namespace ThreeFingerDrag;

internal static partial class WindowDragController
{
	/// <summary>Returns the part of the remaining distance to move this tick, rounded away from zero.</summary>
	private static int SmoothingStep(int remaining, double follow)
	{
		if (remaining == 0) return 0;

		var step = (int)Math.Round(remaining * follow, MidpointRounding.AwayFromZero);
		// Always move at least 1 px toward the target, so the window does not stall just short of it.
		return step == 0 ? Math.Sign(remaining) : step;
	}
}
