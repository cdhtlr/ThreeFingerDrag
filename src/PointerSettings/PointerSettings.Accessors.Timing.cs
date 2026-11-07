namespace ThreeFingerDrag;

internal static partial class PointerSettings
{
	public static int GetDragFallbackPollIntervalMs() => GetConfiguredSettings().DragFallbackPollIntervalMs;
	public static int GetWindowDragSmoothingFrameMs() => GetConfiguredSettings().WindowDragSmoothingFrameMs;
	public static int GetWindowDragSmoothingMinTimeConstantMs() => GetConfiguredSettings().WindowDragSmoothingMinTimeConstantMs;
	public static int GetWindowDragSmoothingMaxTimeConstantMs() => GetConfiguredSettings().WindowDragSmoothingMaxTimeConstantMs;
	public static int GetWindowDragSmoothingMaxElapsedMs() => GetConfiguredSettings().WindowDragSmoothingMaxElapsedMs;
	public static int GetSettingsInputDebounceMs() => GetConfiguredSettings().SettingsInputDebounceMs;
	public static int GetSettingsStatIntervalMs() => GetConfiguredSettings().SettingsStatIntervalMs;
	public static int GetMouseCancelDragPollIntervalMs() => GetConfiguredSettings().MouseCancelDragPollIntervalMs;
	public static int GetTouchCancelDragPollIntervalMs() => GetConfiguredSettings().TouchCancelDragPollIntervalMs;
	public static int GetTouchStationaryDragDropHoldMs() => GetConfiguredSettings().TouchStationaryDragDropHoldMs;
	public static int GetTouchDragDropAnchorGraceMs() => GetConfiguredSettings().TouchDragDropAnchorGraceMs;
	public static int GetTouchFingerLiftGraceMs() => GetConfiguredSettings().TouchFingerLiftGraceMs;
	public static int GetTouchscreenCancelDragPollIntervalMs() => GetConfiguredSettings().TouchscreenCancelDragPollIntervalMs;
	public static int GetTouchscreenContactLostGraceMs() => GetConfiguredSettings().TouchscreenContactLostGraceMs;
	public static int GetTouchscreenRecentExternalCursorMovementMs() => GetConfiguredSettings().TouchscreenRecentExternalCursorMovementMs;
	public static int GetHitTestTimeoutMs() => GetConfiguredSettings().HitTestTimeoutMs;
	public static int GetUiAutomationClassifyTimeoutMs() => GetConfiguredSettings().UiAutomationClassifyTimeoutMs;
	public static int GetTitleBarHitTestTimeoutMs() => GetConfiguredSettings().TitleBarHitTestTimeoutMs;
	public static int GetForcedDragModePollIntervalMs() => GetConfiguredSettings().ForcedDragModePollIntervalMs;
	public static int GetWindowMoveArrowSequenceTimeoutMs() => GetConfiguredSettings().WindowMoveArrowSequenceTimeoutMs;
	public static int GetWindowMoveNudgeIntervalMs() => GetConfiguredSettings().WindowMoveNudgeIntervalMs;
	public static int GetNotificationSafetyTimeoutMs() => GetConfiguredSettings().NotificationSafetyTimeoutMs;
	public static int GetNotificationBalloonTimeoutMs() => GetConfiguredSettings().NotificationBalloonTimeoutMs;
	public static int GetSettingsEventPollIntervalMs() => GetConfiguredSettings().SettingsEventPollIntervalMs;
	public static int GetSettingsCaptureRefreshIntervalMs() => GetConfiguredSettings().SettingsCaptureRefreshIntervalMs;
}
