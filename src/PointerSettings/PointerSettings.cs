namespace ThreeFingerDrag;

/// <summary>Target classifiers whose precedence follows their order in the detection settings.</summary>
internal enum TargetDetection
{
	UiAutomation,
	Ocr,
	HitTest,
	SolidColor,
	CursorShape,
	ControlClassName,
}

/// <summary>Loads, validates, caches, and persists settings stored beside the executable.</summary>
internal static partial class PointerSettings
{
	// Default precedence used when the config has no "detection" section or omits some entries.
	private static readonly TargetDetection[] DefaultTargetDetectionOrder =
	[
		TargetDetection.SolidColor,
		TargetDetection.UiAutomation,
		TargetDetection.Ocr,
		TargetDetection.CursorShape,
		TargetDetection.ControlClassName,
		TargetDetection.HitTest,
	];
	private static readonly Color DefaultWindowDragIndicatorColor = Color.FromArgb(64, 96, 255);
	private static readonly Color DefaultOtherDragIndicatorColor = Color.FromArgb(255, 64, 64);
	private static readonly Color DefaultWindowPositionFallbackIndicatorColor = Color.FromArgb(128, 0, 128);
	private static readonly Color DefaultTouchHoldForceWindowDragIndicatorColor = Color.FromArgb(0, 200, 200);
	private static readonly Color DefaultIndicatorPanelBackgroundColor = Color.FromArgb(32, 32, 32);
	private static readonly Color DefaultIndicatorPanelTextColor = Color.FromArgb(255, 255, 255);
	private static readonly Color DefaultSolidColorUniformBorderColor = Color.FromArgb(64, 200, 120);
	private static readonly Color DefaultSolidColorMixedBorderColor = Color.FromArgb(255, 176, 32);
}
