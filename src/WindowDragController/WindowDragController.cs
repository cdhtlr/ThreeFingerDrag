namespace ThreeFingerDrag;

/// <summary>Classifies drag targets and performs foreground or direct-position window movement.</summary>
internal static partial class WindowDragController
{
	// Result of the solid-color check made when a touch starts. Null means no check ran.
	private static bool? touchStartSolidColorIsSingleColor;
	internal static bool HasTouchStartSolidColorResult => touchStartSolidColorIsSingleColor.HasValue;
	internal static bool TouchStartSolidColorIsSingleColor => touchStartSolidColorIsSingleColor.GetValueOrDefault();
}
