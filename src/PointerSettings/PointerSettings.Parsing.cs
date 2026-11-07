using System.Text.Json;

namespace ThreeFingerDrag;

internal static partial class PointerSettings
{
	/// <summary>Reads a string setting straight from the file, bypassing the cache. Returns the fallback on any error.</summary>
	private static string GetConfiguredStringFromFile(string name, string fallback)
	{
		try
		{
			var path = AppDataPaths.ConfigurationFilePath;
			using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
			return TryGetSettingProperty(doc.RootElement, name, out var item) && item.ValueKind == JsonValueKind.String ? item.GetString() ?? fallback : fallback;
		}
		catch { return fallback; }
	}
	/// <summary>Parses the config file into a <see cref="Settings"/> record. Missing or invalid values fall back to defaults, and numbers are clamped to their allowed range. Problems found are added to <paramref name="warnings"/>.</summary>
	private static Settings ParseConfiguredSettings(string configPath, List<string> warnings)
	{
		try
		{
			using var configStream = File.OpenRead(configPath);
			using JsonDocument configDocument = JsonDocument.Parse(configStream);
			var root = configDocument.RootElement;

			var cursorSpeed = DefaultCursorSpeed;
			if (TryGetSettingProperty(root, "cursorSpeed", out var cursorSpeedProperty) && cursorSpeedProperty.TryGetInt32(out var configuredCursorSpeed)) cursorSpeed = configuredCursorSpeed;

			var windowPositionSpeed = GetConfiguredInt32(root, "windowPositionSpeed", DefaultWindowPositionSpeed);

			var rectangleSize = DefaultSolidColorDetectionRectangleSize;
			if (TryGetSettingProperty(root, "solidColorDetectionRectangleSize", out var rectangleSizeProperty) && rectangleSizeProperty.TryGetInt32(out var configuredRectangleSize)) rectangleSize = configuredRectangleSize;

			var bringToForeground = GetConfiguredBoolean(root, "bringDraggedWindowToForeground", DefaultBringDraggedWindowToForeground);
			var keepWindowWithinScreen = GetConfiguredBoolean(root, "keepWindowWithinScreen", DefaultKeepWindowWithinScreen);
			var enableHitTestDetection = GetConfiguredBoolean(root, "enableHitTestDetection", DefaultEnableHitTestDetection);
			var enableCursorShapeDetection = GetConfiguredBoolean(root, "enableCursorShapeDetection", DefaultEnableCursorShapeDetection);
			var enableEmptyIBeamWindowDrag = GetConfiguredBoolean(root, "enableEmptyIBeamWindowDrag", DefaultEnableEmptyIBeamWindowDrag);
			var iBeamTextDetectionAreaSize = GetConfiguredInt32(root, "iBeamTextDetectionAreaSize", DefaultIBeamTextDetectionAreaSize);
			var enableUiAutomationDetection = GetConfiguredBoolean(root, "enableUiAutomationDetection", DefaultEnableUiAutomationDetection);
			var enableControlClassNameDetection = GetConfiguredBoolean(root, "enableControlClassNameDetection", DefaultEnableControlClassNameDetection);
			var enableSolidColorDetection = GetConfiguredBoolean(root, "enableSolidColorDetection", DefaultEnableSolidColorDetection);
			var targetDetectionOrder = GetTargetDetectionOrder(root);
			var enableOcrCapture = GetConfiguredBoolean(root, "enableOcrCapture", DefaultEnableOcrCapture);

			var ocrHoldDurationMs = DefaultOcrHoldDurationMs;
			if (TryGetSettingProperty(root, "ocrHoldDurationMs", out var ocrHoldDurationProperty) && ocrHoldDurationProperty.TryGetInt32(out var configuredOcrHoldDurationMs)) ocrHoldDurationMs = configuredOcrHoldDurationMs;

			var ocrCaptureRadius = DefaultOcrCaptureRadius;
			if (TryGetSettingProperty(root, "ocrCaptureRadius", out var ocrCaptureRadiusProperty) && ocrCaptureRadiusProperty.TryGetInt32(out var configuredOcrCaptureRadius)) ocrCaptureRadius = configuredOcrCaptureRadius;

			var ocrMaximumWordGapPercent = GetConfiguredInt32(root, "ocrMaximumWordGapPercent", DefaultOcrMaximumWordGapPercent);
			var ocrCopyToClipboard = GetConfiguredBoolean(root, "ocrCopyToClipboard", DefaultOcrCopyToClipboard);
			var ocrActivationDelayMs = GetConfiguredInt32(root, "ocrActivationDelayMs", DefaultOcrActivationDelayMs);
			var ocrClickDelayMs = GetConfiguredInt32(root, "ocrClickDelayMs", DefaultOcrClickDelayMs);
			var ocrClipboardRestoreDelayMs = GetConfiguredInt32(root, "ocrClipboardRestoreDelayMs", DefaultOcrClipboardRestoreDelayMs);
			var dragFallbackPollIntervalMs = GetConfiguredInt32(root, "dragFallbackPollIntervalMs", DefaultDragFallbackPollIntervalMs);
			var windowDragSmoothingFrameMs = GetConfiguredInt32(root, "windowDragSmoothingFrameMs", DefaultWindowDragSmoothingFrameMs);
			var windowDragSmoothingMinTimeConstantMs = GetConfiguredInt32(root, "windowDragSmoothingMinTimeConstantMs", DefaultWindowDragSmoothingMinTimeConstantMs);
			var windowDragSmoothingMaxTimeConstantMs = GetConfiguredInt32(root, "windowDragSmoothingMaxTimeConstantMs", DefaultWindowDragSmoothingMaxTimeConstantMs);
			var windowDragSmoothingMaxElapsedMs = GetConfiguredInt32(root, "windowDragSmoothingMaxElapsedMs", DefaultWindowDragSmoothingMaxElapsedMs);
			var settingsInputDebounceMs = GetConfiguredInt32(root, "settingsInputDebounceMs", DefaultSettingsInputDebounceMs);
			var settingsStatIntervalMs = GetConfiguredInt32(root, "settingsStatIntervalMs", DefaultSettingsStatIntervalMs);
			var mouseCancelDragPollIntervalMs = GetConfiguredInt32(root, "mouseCancelDragPollIntervalMs", DefaultMouseCancelDragPollIntervalMs);
			var touchCancelDragPollIntervalMs = GetConfiguredInt32(root, "touchCancelDragPollIntervalMs", DefaultTouchCancelDragPollIntervalMs);
			var touchStationaryDragDropHoldMs = GetConfiguredInt32(root, "touchStationaryDragDropHoldMs", DefaultTouchStationaryDragDropHoldMs);
			var touchDragDropAnchorGraceMs = GetConfiguredInt32(root, "touchDragDropAnchorGraceMs", DefaultTouchDragDropAnchorGraceMs);
			var touchFingerLiftGraceMs = GetConfiguredInt32(root, "touchFingerLiftGraceMs", DefaultTouchFingerLiftGraceMs);
			var touchscreenCancelDragPollIntervalMs = GetConfiguredInt32(root, "touchscreenCancelDragPollIntervalMs", DefaultTouchscreenCancelDragPollIntervalMs);
			var touchscreenContactLostGraceMs = GetConfiguredInt32(root, "touchscreenContactLostGraceMs", DefaultTouchscreenContactLostGraceMs);
			var touchscreenRecentExternalCursorMovementMs = GetConfiguredInt32(root, "touchscreenRecentExternalCursorMovementMs", DefaultTouchscreenRecentExternalCursorMovementMs);
			var hitTestTimeoutMs = GetConfiguredInt32(root, "hitTestTimeoutMs", DefaultHitTestTimeoutMs);
			var uiAutomationClassifyTimeoutMs = GetConfiguredInt32(root, "uiAutomationClassifyTimeoutMs", DefaultUiAutomationClassifyTimeoutMs);
			var titleBarHitTestTimeoutMs = GetConfiguredInt32(root, "titleBarHitTestTimeoutMs", DefaultTitleBarHitTestTimeoutMs);
			var forcedDragModePollIntervalMs = GetConfiguredInt32(root, "forcedDragModePollIntervalMs", DefaultForcedDragModePollIntervalMs);
			var windowMoveArrowSequenceTimeoutMs = GetConfiguredInt32(root, "windowMoveArrowSequenceTimeoutMs", DefaultWindowMoveArrowSequenceTimeoutMs);
			var windowMoveNudgeIntervalMs = GetConfiguredInt32(root, "windowMoveNudgeIntervalMs", DefaultWindowMoveNudgeIntervalMs);
			var notificationSafetyTimeoutMs = GetConfiguredInt32(root, "notificationSafetyTimeoutMs", DefaultNotificationSafetyTimeoutMs);
			var notificationBalloonTimeoutMs = GetConfiguredInt32(root, "notificationBalloonTimeoutMs", DefaultNotificationBalloonTimeoutMs);
			var settingsEventPollIntervalMs = GetConfiguredInt32(root, "settingsEventPollIntervalMs", DefaultSettingsEventPollIntervalMs);
			var settingsCaptureRefreshIntervalMs = GetConfiguredInt32(root, "settingsCaptureRefreshIntervalMs", DefaultSettingsCaptureRefreshIntervalMs);

			var ocrLanguageTag = DefaultOcrLanguageTag;
			if (TryGetSettingProperty(root, "ocrLanguageTag", out var ocrLanguageTagProperty) && ocrLanguageTagProperty.ValueKind == JsonValueKind.String) ocrLanguageTag = ocrLanguageTagProperty.GetString() ?? DefaultOcrLanguageTag;

			var showSolidColorDetectionRectangle = GetConfiguredBoolean(root, "showSolidColorDetectionRectangle", DefaultShowSolidColorDetectionRectangle);
			var solidColorDetectionRectangleDisplayMs = GetConfiguredInt32(root, "solidColorDetectionRectangleDisplayMs", DefaultSolidColorDetectionRectangleDisplayMs);
			var showDragModeIndicator = GetConfiguredBoolean(root, "showDragModeIndicator", DefaultShowDragModeIndicator);
			var showDragModeIndicatorDetails = GetConfiguredBoolean(root, "showDragModeIndicatorDetails", DefaultShowDragModeIndicatorDetails);
			var showTouchHoldForceWindowDragIndicator = GetConfiguredBoolean(root, "showTouchHoldForceWindowDragIndicator", DefaultShowTouchHoldForceWindowDragIndicator);
			var windowDragIndicatorColor = GetConfiguredColor(root, "windowDragIndicatorColor", DefaultWindowDragIndicatorColor);
			var otherDragIndicatorColor = GetConfiguredColor(root, "otherDragIndicatorColor", DefaultOtherDragIndicatorColor);
			var windowPositionFallbackIndicatorColor = GetConfiguredColor(root, "windowPositionFallbackIndicatorColor", DefaultWindowPositionFallbackIndicatorColor);
			var touchHoldForceWindowDragIndicatorColor = GetConfiguredColor(root, "touchHoldForceWindowDragIndicatorColor", DefaultTouchHoldForceWindowDragIndicatorColor);
			var indicatorPanelBackgroundColor = GetConfiguredColor(root, "indicatorPanelBackgroundColor", DefaultIndicatorPanelBackgroundColor);
			var indicatorPanelTextColor = GetConfiguredColor(root, "indicatorPanelTextColor", DefaultIndicatorPanelTextColor);
			var solidColorUniformBorderColor = GetConfiguredColor(root, "solidColorUniformBorderColor", DefaultSolidColorUniformBorderColor);
			var solidColorMixedBorderColor = GetConfiguredColor(root, "solidColorMixedBorderColor", DefaultSolidColorMixedBorderColor);
			// Every indicator color must be unique. Duplicates from the config are replaced, following IndicatorColorSpecs order.
			var distinctIndicatorColors = EnsureDistinctIndicatorColors(
			[
				windowDragIndicatorColor, otherDragIndicatorColor, windowPositionFallbackIndicatorColor,
				touchHoldForceWindowDragIndicatorColor, indicatorPanelBackgroundColor, indicatorPanelTextColor,
				solidColorUniformBorderColor, solidColorMixedBorderColor,
			]);
			(windowDragIndicatorColor, otherDragIndicatorColor, windowPositionFallbackIndicatorColor,
				touchHoldForceWindowDragIndicatorColor, indicatorPanelBackgroundColor, indicatorPanelTextColor,
				solidColorUniformBorderColor, solidColorMixedBorderColor) = (
				distinctIndicatorColors[0], distinctIndicatorColors[1], distinctIndicatorColors[2],
				distinctIndicatorColors[3], distinctIndicatorColors[4], distinctIndicatorColors[5],
				distinctIndicatorColors[6], distinctIndicatorColors[7]);
			var touchHoldForceWindowDragIndicatorDisplayMs = GetConfiguredInt32(root, "touchHoldForceWindowDragIndicatorDisplayMs", DefaultTouchHoldForceWindowDragIndicatorDisplayMs);
			var dragModeIndicatorDiameter = GetConfiguredInt32(root, "dragModeIndicatorDiameter", DefaultDragModeIndicatorDiameter);
			var dragModeIndicatorDisplayMs = GetConfiguredInt32(root, "dragModeIndicatorDisplayMs", DefaultDragModeIndicatorDisplayMs);
			var forceWindowDragKey = GetConfiguredString(root, "forceWindowDragKey", DefaultForceWindowDragKey);
			var forceNonWindowDragKey = GetConfiguredString(root, "forceNonWindowDragKey", DefaultForceNonWindowDragKey);
			var dragModeSwitcherShortcut = GetConfiguredString(root, "dragModeSwitcherShortcut", DefaultDragModeSwitcherShortcut);
			var enableForcedModeToggleSound = GetConfiguredBoolean(root, "enableForcedModeToggleSound", DefaultEnableForcedModeToggleSound);
			var touchpadDragFingerCount = GetConfiguredInt32(root, "touchpadDragFingerCount", DefaultTouchpadDragFingerCount);
			var touchscreenDragFingerCount = GetConfiguredInt32(root, "touchscreenDragFingerCount", DefaultTouchscreenFingerCount);
			var touchDragButtons = GetConfiguredString(root, "touchDragButtons", DefaultTouchDragButtons);
			var mouseDragTriggerButton = GetConfiguredString(root, "mouseDragTriggerButton", DefaultMouseDragTriggerButton);
			var windowMoveModifierKey = GetConfiguredString(root, "windowMoveModifierKey", DefaultWindowMoveModifierKey);
			var windowMoveStepPixels = GetConfiguredInt32(root, "windowMoveStepPixels", DefaultWindowMoveStepPixels);
			var enableTouchHoldForceWindowDrag = GetConfiguredBoolean(root, "enableTouchHoldForceWindowDrag", DefaultEnableTouchHoldForceWindowDrag);
			var dragHoldDurationMs = GetConfiguredInt32(root, "dragHoldDurationMs", DefaultDragHoldDurationMs);
			var enableClickHoldForceWindowDrag = GetConfiguredBoolean(root, "enableClickHoldForceWindowDrag", DefaultEnableClickHoldForceWindowDrag);
			var enableStylus = GetConfiguredBoolean(root, "enableStylus", DefaultEnableStylus);
			var enableStylusHoldForceWindowDrag = GetConfiguredBoolean(root, "enableStylusHoldForceWindowDrag", DefaultEnableStylusHoldForceWindowDrag);
			var dragCancelKey = GetConfiguredString(root, "dragCancelKey", DefaultTouchpadCancelDragKey);
			var touchpadDragDropAnchorHoldMs = GetConfiguredInt32(root, "touchpadDragDropAnchorHoldMs", DefaultDragDropAnchorHoldMs);
			var touchpadPostLiftAnchorGraceMs = GetConfiguredInt32(root, "touchpadPostLiftAnchorGraceMs", DefaultPostLiftAnchorGraceMs);
			var enableTouchFlickToDrop = GetConfiguredBoolean(root, "enableTouchFlickToDrop", DefaultEnableFlickToDrop);
			var touchFlickToDropVelocityThreshold = GetConfiguredInt32(root, "touchFlickToDropVelocityThreshold", DefaultFlickToDropVelocityThreshold);
			var touchpadNaturalDragReleaseThreshold = GetConfiguredInt32(root, "touchpadNaturalDragReleaseThreshold", DefaultNaturalDragReleaseThreshold);
			var enableDragReleaseSound = GetConfiguredBoolean(root, "enableDragReleaseSound", DefaultEnableDragReleaseSound);
			var cursorPositionPollingIntervalMs = GetConfiguredInt32(root, "cursorPositionPollingIntervalMs", DefaultCursorPositionPollingIntervalMs);
			var enableDiagnosticLog = GetConfiguredBoolean(root, "enableDiagnosticLog", DefaultEnableDiagnosticLog);
			var enableSplitDiagnosticLogs = GetConfiguredBoolean(root, "enableSplitDiagnosticLogs", DefaultEnableSplitDiagnosticLogs);

			AddDuplicateSettingWarnings(root, warnings);
			var bindings = ResolveBindings(new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["dragCancelKey"] = ValidateBindingValue("dragCancelKey", dragCancelKey, false, warnings),
				["forceWindowDragKey"] = ValidateBindingValue("forceWindowDragKey", forceWindowDragKey, false, warnings),
				["forceNonWindowDragKey"] = ValidateBindingValue("forceNonWindowDragKey", forceNonWindowDragKey, false, warnings),
				["dragModeSwitcherShortcut"] = ValidateBindingValue("dragModeSwitcherShortcut", dragModeSwitcherShortcut, true, warnings),
				["windowMoveModifierKey"] = ValidateBindingValue("windowMoveModifierKey", windowMoveModifierKey, false, warnings),
			}, warnings);

			var clampedDragModeIndicatorDiameter = ClampConfiguredInt(root, "dragModeIndicatorDiameter", dragModeIndicatorDiameter, MinDragModeIndicatorDiameter, MaxDragModeIndicatorDiameter);

			return new Settings(
				ClampConfiguredInt(root, "cursorSpeed", cursorSpeed, MinCursorSpeed, MaxCursorSpeed),
				ClampConfiguredInt(root, "windowPositionSpeed", windowPositionSpeed, MinWindowPositionSpeed, MaxWindowPositionSpeed),
				ClampConfiguredInt(root, "solidColorDetectionRectangleSize", rectangleSize, MinSolidColorDetectionRectangleSize, MaxSolidColorDetectionRectangleSize, odd: true),
				bringToForeground,
				enableHitTestDetection,
				enableCursorShapeDetection,
				enableEmptyIBeamWindowDrag,
				ClampConfiguredInt(root, "iBeamTextDetectionAreaSize", iBeamTextDetectionAreaSize, MinIBeamTextDetectionAreaSize, MaxIBeamTextDetectionAreaSize, odd: true),
				enableUiAutomationDetection,
				enableControlClassNameDetection,
				enableSolidColorDetection,
				targetDetectionOrder,
				enableOcrCapture,
				ClampConfiguredInt(root, "ocrHoldDurationMs", ocrHoldDurationMs, MinOcrHoldDurationMs, MaxOcrHoldDurationMs),
				ClampConfiguredInt(root, "ocrCaptureRadius", ocrCaptureRadius, MinOcrCaptureRadius, MaxOcrCaptureRadius),
				ValidateOcrLanguageTag(ocrLanguageTag, warnings),
				ClampConfiguredInt(root, "ocrMaximumWordGapPercent", ocrMaximumWordGapPercent, MinOcrMaximumWordGapPercent, MaxOcrMaximumWordGapPercent),
				ocrCopyToClipboard,
				showSolidColorDetectionRectangle,
				ClampConfiguredInt(root, "solidColorDetectionRectangleDisplayMs", solidColorDetectionRectangleDisplayMs, MinOverlayDisplayMs, MaxOverlayDisplayMs),
				showDragModeIndicator,
				showDragModeIndicatorDetails,
				showTouchHoldForceWindowDragIndicator,
				windowDragIndicatorColor,
				otherDragIndicatorColor,
				windowPositionFallbackIndicatorColor,
				touchHoldForceWindowDragIndicatorColor,
				indicatorPanelBackgroundColor,
				indicatorPanelTextColor,
				solidColorUniformBorderColor,
				solidColorMixedBorderColor,
				ClampConfiguredInt(root, "touchHoldForceWindowDragIndicatorDisplayMs", touchHoldForceWindowDragIndicatorDisplayMs, MinOverlayDisplayMs, MaxOverlayDisplayMs),
				clampedDragModeIndicatorDiameter,
				ClampConfiguredInt(root, "dragModeIndicatorDisplayMs", dragModeIndicatorDisplayMs, MinOverlayDisplayMs, MaxOverlayDisplayMs),
				bindings["forceWindowDragKey"],
				bindings["forceNonWindowDragKey"],
				bindings["dragModeSwitcherShortcut"],
				enableForcedModeToggleSound,
				ClampConfiguredInt(root, "touchpadDragFingerCount", touchpadDragFingerCount, MinTouchpadDragFingerCount, MaxTouchpadDragFingerCount),
				ClampConfiguredInt(root, "touchscreenDragFingerCount", touchscreenDragFingerCount, MinTouchscreenDragFingerCount, MaxTouchscreenDragFingerCount),
				NormalizeDragMouseButtons(touchDragButtons),
				NormalizeMouseTriggerButton(mouseDragTriggerButton),
				bindings["windowMoveModifierKey"],
				ClampConfiguredInt(root, "windowMoveStepPixels", windowMoveStepPixels, MinWindowMoveStepPixels, MaxWindowMoveStepPixels),
				enableTouchHoldForceWindowDrag,
				ClampConfiguredInt(root, "dragHoldDurationMs", dragHoldDurationMs, MinDragHoldDurationMs, MaxDragHoldDurationMs),
				enableClickHoldForceWindowDrag,
				enableStylus,
				enableStylusHoldForceWindowDrag,
				bindings["dragCancelKey"],
				ClampConfiguredInt(root, "touchpadDragDropAnchorHoldMs", touchpadDragDropAnchorHoldMs, MinDragDropAnchorHoldMs, MaxDragDropAnchorHoldMs),
				ClampConfiguredInt(root, "touchpadPostLiftAnchorGraceMs", touchpadPostLiftAnchorGraceMs, MinPostLiftAnchorGraceMs, MaxPostLiftAnchorGraceMs),
				enableTouchFlickToDrop,
				ClampConfiguredInt(root, "touchFlickToDropVelocityThreshold", touchFlickToDropVelocityThreshold, MinFlickToDropVelocityThreshold, MaxFlickToDropVelocityThreshold),
				ClampConfiguredInt(root, "touchpadNaturalDragReleaseThreshold", touchpadNaturalDragReleaseThreshold, MinNaturalDragReleaseThreshold, MaxNaturalDragReleaseThreshold),
				enableDragReleaseSound,
				ClampConfiguredInt(root, "cursorPositionPollingIntervalMs", cursorPositionPollingIntervalMs, MinCursorPositionPollingIntervalMs, MaxCursorPositionPollingIntervalMs),
				enableDiagnosticLog,
				enableSplitDiagnosticLogs,
				keepWindowWithinScreen,
				ClampConfiguredInt(root, "ocrActivationDelayMs", ocrActivationDelayMs, MinOcrDropDelayMs, MaxOcrDropDelayMs),
				ClampConfiguredInt(root, "ocrClickDelayMs", ocrClickDelayMs, MinOcrDropDelayMs, MaxOcrDropDelayMs),
				ClampConfiguredInt(root, "ocrClipboardRestoreDelayMs", ocrClipboardRestoreDelayMs, MinOcrDropDelayMs, MaxOcrDropDelayMs),
				ClampConfiguredInt(root, "dragFallbackPollIntervalMs", dragFallbackPollIntervalMs, MinDragFallbackPollIntervalMs, MaxDragFallbackPollIntervalMs),
				ClampConfiguredInt(root, "windowDragSmoothingFrameMs", windowDragSmoothingFrameMs, MinWindowDragSmoothingFrameMs, MaxWindowDragSmoothingFrameMs),
				ClampConfiguredInt(root, "windowDragSmoothingMinTimeConstantMs", windowDragSmoothingMinTimeConstantMs, MinWindowDragSmoothingMinTimeConstantMs, MaxWindowDragSmoothingMinTimeConstantMs),
				ClampConfiguredInt(root, "windowDragSmoothingMaxTimeConstantMs", windowDragSmoothingMaxTimeConstantMs, MinWindowDragSmoothingMaxTimeConstantMs, MaxWindowDragSmoothingMaxTimeConstantMs),
				ClampConfiguredInt(root, "windowDragSmoothingMaxElapsedMs", windowDragSmoothingMaxElapsedMs, MinWindowDragSmoothingMaxElapsedMs, MaxWindowDragSmoothingMaxElapsedMs),
				ClampConfiguredInt(root, "settingsInputDebounceMs", settingsInputDebounceMs, MinSettingsInputDebounceMs, MaxSettingsInputDebounceMs),
				ClampConfiguredInt(root, "settingsStatIntervalMs", settingsStatIntervalMs, MinSettingsStatIntervalMs, MaxSettingsStatIntervalMs),
				ClampConfiguredInt(root, "mouseCancelDragPollIntervalMs", mouseCancelDragPollIntervalMs, MinMouseCancelDragPollIntervalMs, MaxMouseCancelDragPollIntervalMs),
				ClampConfiguredInt(root, "touchCancelDragPollIntervalMs", touchCancelDragPollIntervalMs, MinTouchCancelDragPollIntervalMs, MaxTouchCancelDragPollIntervalMs),
				ClampConfiguredInt(root, "touchStationaryDragDropHoldMs", touchStationaryDragDropHoldMs, MinTouchStationaryDragDropHoldMs, MaxTouchStationaryDragDropHoldMs),
				ClampConfiguredInt(root, "touchDragDropAnchorGraceMs", touchDragDropAnchorGraceMs, MinTouchDragDropAnchorGraceMs, MaxTouchDragDropAnchorGraceMs),
				ClampConfiguredInt(root, "touchFingerLiftGraceMs", touchFingerLiftGraceMs, MinTouchFingerLiftGraceMs, MaxTouchFingerLiftGraceMs),
				ClampConfiguredInt(root, "touchscreenCancelDragPollIntervalMs", touchscreenCancelDragPollIntervalMs, MinTouchscreenCancelDragPollIntervalMs, MaxTouchscreenCancelDragPollIntervalMs),
				ClampConfiguredInt(root, "touchscreenContactLostGraceMs", touchscreenContactLostGraceMs, MinTouchscreenContactLostGraceMs, MaxTouchscreenContactLostGraceMs),
				ClampConfiguredInt(root, "touchscreenRecentExternalCursorMovementMs", touchscreenRecentExternalCursorMovementMs, MinTouchscreenRecentExternalCursorMovementMs, MaxTouchscreenRecentExternalCursorMovementMs),
				ClampConfiguredInt(root, "hitTestTimeoutMs", hitTestTimeoutMs, MinHitTestTimeoutMs, MaxHitTestTimeoutMs),
				ClampConfiguredInt(root, "uiAutomationClassifyTimeoutMs", uiAutomationClassifyTimeoutMs, MinUiAutomationClassifyTimeoutMs, MaxUiAutomationClassifyTimeoutMs),
				ClampConfiguredInt(root, "titleBarHitTestTimeoutMs", titleBarHitTestTimeoutMs, MinTitleBarHitTestTimeoutMs, MaxTitleBarHitTestTimeoutMs),
				ClampConfiguredInt(root, "forcedDragModePollIntervalMs", forcedDragModePollIntervalMs, MinForcedDragModePollIntervalMs, MaxForcedDragModePollIntervalMs),
				ClampConfiguredInt(root, "windowMoveArrowSequenceTimeoutMs", windowMoveArrowSequenceTimeoutMs, MinWindowMoveArrowSequenceTimeoutMs, MaxWindowMoveArrowSequenceTimeoutMs),
				ClampConfiguredInt(root, "windowMoveNudgeIntervalMs", windowMoveNudgeIntervalMs, MinWindowMoveNudgeIntervalMs, MaxWindowMoveNudgeIntervalMs),
				ClampConfiguredInt(root, "notificationSafetyTimeoutMs", notificationSafetyTimeoutMs, MinNotificationSafetyTimeoutMs, MaxNotificationSafetyTimeoutMs),
				ClampConfiguredInt(root, "notificationBalloonTimeoutMs", notificationBalloonTimeoutMs, MinNotificationBalloonTimeoutMs, MaxNotificationBalloonTimeoutMs),
				ClampConfiguredInt(root, "settingsEventPollIntervalMs", settingsEventPollIntervalMs, MinSettingsEventPollIntervalMs, MaxSettingsEventPollIntervalMs),
				ClampConfiguredInt(root, "settingsCaptureRefreshIntervalMs", settingsCaptureRefreshIntervalMs, MinSettingsCaptureRefreshIntervalMs, MaxSettingsCaptureRefreshIntervalMs));
		}
		catch (IOException)
		{
		}
		catch (JsonException)
		{
		}
		catch (UnauthorizedAccessException)
		{
		}

		return Settings.Default;
	}
}
