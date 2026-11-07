namespace ThreeFingerDrag;

internal static partial class PointerSettings
{
	/// <summary>Cursor speed in tenths of input movement, where 10 is 1x.</summary>
	public static int GetCursorSpeed() => GetConfiguredCursorSpeed();
	/// <summary>
	/// Window glide speed in tenths of input movement, on the same scale as cursorSpeed: 10 means 1x,
	/// so the default forced window drag speed of 50 gives 5x.
	/// </summary>
	public static int GetWindowPositionSpeed() => GetConfiguredSettings().WindowPositionSpeed;
	/// <summary>Side length in pixels of the solid-color sample square. Always odd.</summary>
	public static int GetSolidColorDetectionRectangleSize() => GetConfiguredSettings().SolidColorDetectionRectangleSize;
	// Simple accessors below read the cached settings snapshot. Each one maps to the config key of the same meaning.
	public static bool ShouldBringDraggedWindowToForeground() => GetConfiguredSettings().BringDraggedWindowToForeground;
	public static bool ShouldKeepWindowWithinScreen() => GetConfiguredSettings().KeepWindowWithinScreen;
	public static bool IsHitTestDetectionEnabled() => GetConfiguredSettings().EnableHitTestDetection;
	public static bool IsCursorShapeDetectionEnabled() => GetConfiguredSettings().EnableCursorShapeDetection;
	public static bool IsEmptyIBeamWindowDragEnabled() => GetConfiguredSettings().EnableEmptyIBeamWindowDrag;
	public static int GetIBeamTextDetectionAreaSize() => GetConfiguredSettings().IBeamTextDetectionAreaSize;
	public static bool IsUiAutomationDetectionEnabled() => GetConfiguredSettings().EnableUiAutomationDetection;
	public static bool IsControlClassNameDetectionEnabled() => GetConfiguredSettings().EnableControlClassNameDetection;
	public static bool IsSolidColorDetectionEnabled() => GetConfiguredSettings().EnableSolidColorDetection;
	public static IReadOnlyList<TargetDetection> GetTargetDetectionOrder() => GetConfiguredSettings().TargetDetectionOrder;
}
