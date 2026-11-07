namespace ThreeFingerDrag;

internal static partial class PointerSettings
{
	/// <summary>An indicator color setting: its config key, the label key used in messages, and its default color.</summary>
	internal readonly record struct IndicatorColorSpec(string Key, string LabelKey, Color Default);

	/// <summary>
	/// Every indicator color setting, in the order used by <see cref="EnsureDistinctIndicatorColors"/>.
	/// Each color must be unique across this list, both when loading config and when picking colors in the UI.
	/// </summary>
	internal static IndicatorColorSpec[] IndicatorColorSpecs =>
	[
		new("windowDragIndicatorColor", "windowDragColor", DefaultWindowDragIndicatorColor),
		new("otherDragIndicatorColor", "otherDragColor", DefaultOtherDragIndicatorColor),
		new("windowPositionFallbackIndicatorColor", "fallbackWindowDragColor", DefaultWindowPositionFallbackIndicatorColor),
		new("touchHoldForceWindowDragIndicatorColor", "touchHoldColor", DefaultTouchHoldForceWindowDragIndicatorColor),
		new("indicatorPanelBackgroundColor", "panelBackgroundColor", DefaultIndicatorPanelBackgroundColor),
		new("indicatorPanelTextColor", "panelTextColor", DefaultIndicatorPanelTextColor),
		new("solidColorUniformBorderColor", "uniformSampleColor", DefaultSolidColorUniformBorderColor),
		new("solidColorMixedBorderColor", "mixedSampleColor", DefaultSolidColorMixedBorderColor),
	];

	// Replacement candidates used only when a configured color collides and its own default is already taken.
	private static readonly Color[] IndicatorColorPalette =
	[
		Color.FromArgb(0, 0, 255), Color.FromArgb(0, 128, 0), Color.FromArgb(255, 255, 0), Color.FromArgb(0, 255, 255),
		Color.FromArgb(255, 0, 255), Color.FromArgb(255, 128, 0), Color.FromArgb(128, 0, 128), Color.FromArgb(139, 69, 19),
		Color.FromArgb(128, 128, 128), Color.FromArgb(0, 0, 0), Color.FromArgb(255, 255, 255), Color.FromArgb(255, 192, 203),
		Color.FromArgb(0, 128, 128), Color.FromArgb(128, 0, 0), Color.FromArgb(173, 216, 230), Color.FromArgb(144, 238, 144),
		Color.FromArgb(255, 215, 0), Color.FromArgb(75, 0, 130), Color.FromArgb(240, 230, 140), Color.FromArgb(165, 42, 42),
	];

	/// <summary>Returns the default color for an indicator color key.</summary>
	internal static Color GetDefaultIndicatorColor(string key) =>
		IndicatorColorSpecs.First(spec => spec.Key == key).Default;

	/// <summary>
	/// Makes the indicator colors unique. <paramref name="values"/> must follow <see cref="IndicatorColorSpecs"/> order.
	/// The first occurrence keeps its color. A duplicate uses its default if free, otherwise the first free palette color.
	/// </summary>
	internal static Color[] EnsureDistinctIndicatorColors(Color[] values)
	{
		var specs = IndicatorColorSpecs;
		if (values.Length != specs.Length) throw new ArgumentException("Indicator color count does not match the color specs.", nameof(values));

		var result = new Color[values.Length];
		var used = new HashSet<int>();
		var duplicates = new List<int>();
		for (var i = 0; i < values.Length; i++)
		{
			if (used.Add(values[i].ToArgb())) result[i] = values[i];
			else duplicates.Add(i);
		}

		foreach (var i in duplicates)
		{
			var fallback = used.Contains(specs[i].Default.ToArgb())
				? IndicatorColorPalette.First(color => !used.Contains(color.ToArgb()))
				: specs[i].Default;
			used.Add(fallback.ToArgb());
			result[i] = fallback;
		}

		return result;
	}
}
