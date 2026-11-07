namespace ThreeFingerDrag;

internal static partial class PointerSettings
{
	public static bool IsOcrCaptureEnabled() => GetConfiguredSettings().EnableOcrCapture;
	public static int GetOcrHoldDurationMs() => GetConfiguredSettings().OcrHoldDurationMs;
	public static int GetOcrCaptureRadius() => GetConfiguredSettings().OcrCaptureRadius;
	public static string GetOcrLanguageTag() => GetConfiguredSettings().OcrLanguageTag;
	public static int GetOcrMaximumWordGapPercent() => GetConfiguredSettings().OcrMaximumWordGapPercent;
	public static bool IsOcrCopyToClipboardEnabled() => GetConfiguredSettings().OcrCopyToClipboard;
	public static int GetOcrActivationDelayMs() => GetConfiguredSettings().OcrActivationDelayMs;
	public static int GetOcrClickDelayMs() => GetConfiguredSettings().OcrClickDelayMs;
	public static int GetOcrClipboardRestoreDelayMs() => GetConfiguredSettings().OcrClipboardRestoreDelayMs;
}
