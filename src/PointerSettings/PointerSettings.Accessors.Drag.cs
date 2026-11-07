namespace ThreeFingerDrag;

internal static partial class PointerSettings
{
	public static int GetDragModeIndicatorDiameter() => GetConfiguredSettings().DragModeIndicatorDiameter;
	public static int GetDragModeIndicatorDisplayMs() => GetConfiguredSettings().DragModeIndicatorDisplayMs;
	public static int GetTouchHoldForceWindowDragIndicatorDisplayMs() => GetDragModeIndicatorDisplayMs();
	public static string GetForceWindowDragKey() => GetConfiguredSettings().ForceWindowDragKey;
	public static string GetForceNonWindowDragKey() => GetConfiguredSettings().ForceNonWindowDragKey;
	public static string GetDragModeSwitcherShortcut() => GetConfiguredSettings().DragModeSwitcherShortcut;
	public static bool IsForcedModeToggleSoundEnabled() => GetConfiguredSettings().EnableForcedModeToggleSound;
	public static int GetTouchpadDragFingerCount() => GetConfiguredSettings().TouchpadDragFingerCount;
	public static int GetTouchscreenDragFingerCount() => GetConfiguredSettings().TouchscreenDragFingerCount;
	public static string GetTouchDragButtons() => GetConfiguredSettings().TouchDragButtons;
	public static string GetMouseDragTriggerButton() => GetConfiguredSettings().MouseDragTriggerButton;
	public static string GetWindowMoveModifierKey() => GetConfiguredSettings().WindowMoveModifierKey;
	public static int GetWindowMoveStepPixels() => GetConfiguredSettings().WindowMoveStepPixels;
}
