namespace ThreeFingerDrag;

internal static partial class PointerSettings
{
	public static bool ShouldShowSolidColorDetectionRectangle() => GetConfiguredSettings().ShowSolidColorDetectionRectangle;
	public static int GetSolidColorDetectionRectangleDisplayMs() => GetDragModeIndicatorDisplayMs();
	public static bool ShouldShowDragModeIndicator() => GetConfiguredSettings().ShowDragModeIndicator;
	public static bool ShouldShowDragModeIndicatorDetails() => GetConfiguredSettings().ShowDragModeIndicatorDetails;
	public static bool ShouldShowTouchHoldForceWindowDragIndicator() => GetConfiguredSettings().ShowTouchHoldForceWindowDragIndicator;
	public static Color GetWindowDragIndicatorColor() => GetConfiguredSettings().WindowDragIndicatorColor;
	public static Color GetOtherDragIndicatorColor() => GetConfiguredSettings().OtherDragIndicatorColor;
	public static Color GetWindowPositionFallbackIndicatorColor() => GetConfiguredSettings().WindowPositionFallbackIndicatorColor;
	public static Color GetTouchHoldForceWindowDragIndicatorColor() => GetConfiguredSettings().TouchHoldForceWindowDragIndicatorColor;
	public static Color GetIndicatorPanelBackgroundColor() => GetConfiguredSettings().IndicatorPanelBackgroundColor;
	public static Color GetIndicatorPanelTextColor() => GetConfiguredSettings().IndicatorPanelTextColor;
	public static Color GetSolidColorUniformBorderColor() => GetConfiguredSettings().SolidColorUniformBorderColor;
	public static Color GetSolidColorMixedBorderColor() => GetConfiguredSettings().SolidColorMixedBorderColor;
}
