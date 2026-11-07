using System.Text.Json;

namespace ThreeFingerDrag;

internal static partial class PointerSettings
{
	/// <summary>Reads a boolean from the config JSON. Non-boolean values are ignored and the default is used.</summary>
	private static bool GetConfiguredBoolean(JsonElement root, string propertyName, bool defaultValue)
	{
		if (TryGetSettingProperty(root, propertyName, out var property) && property.ValueKind is JsonValueKind.True or JsonValueKind.False) return property.GetBoolean();
		return defaultValue;
	}

	/// <summary>Reads an int32 setting, or returns the default when it is missing or not an integer.</summary>
	private static int GetConfiguredInt32(JsonElement root, string propertyName, int defaultValue)
	{
		if (TryGetSettingProperty(root, propertyName, out var property) && property.TryGetInt32(out var value)) return value;
		return defaultValue;
	}

	/// <summary>Reads a string setting, or returns the default when it is missing or not a string.</summary>
	private static string GetConfiguredString(JsonElement root, string propertyName, string defaultValue)
	{
		if (TryGetSettingProperty(root, propertyName, out var property) && property.ValueKind == JsonValueKind.String) return property.GetString() ?? defaultValue;
		return defaultValue;
	}

	/// <summary>Reads a color setting written as "#RRGGBB". Any other format returns the default.</summary>
	private static Color GetConfiguredColor(JsonElement root, string propertyName, Color defaultValue)
	{
		if (!TryGetSettingProperty(root, propertyName, out var property) || property.ValueKind != JsonValueKind.String) return defaultValue;

		// Each channel is two hex digits, so the string must be exactly 7 characters long.
		var value = property.GetString();
		if (value is not { Length: 7 } || value[0] != '#'
			|| !byte.TryParse(value.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber, null, out var red)
			|| !byte.TryParse(value.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber, null, out var green)
			|| !byte.TryParse(value.AsSpan(5, 2), System.Globalization.NumberStyles.HexNumber, null, out var blue))
		{
			return defaultValue;
		}

		return Color.FromArgb(red, green, blue);
	}

	/// <summary>Older setting names, kept for backward compatibility, mapped to the current name they are read as.</summary>
	private static readonly Dictionary<string, string[]> LegacySettingNames = new()
	{
		["touchpadDragFingerCount"] = ["dragFingerCount"],
		["touchscreenDragFingerCount"] = ["touchscreenFingerCount"],
		["touchDragButtons"] = ["touchDragMouseButtons", "dragMouseButtons"],
		["enableTouchHoldForceWindowDrag"] = ["enableTouchpadHoldForceWindowDrag"],
		["dragHoldDurationMs"] = ["touchHoldForceWindowDragMs"],
		["dragCancelKey"] = ["cancelDragKey"],
		["touchpadDragDropAnchorHoldMs"] = ["dragDropAnchorHoldMs"],
		["touchpadPostLiftAnchorGraceMs"] = ["postLiftAnchorGraceMs"],
		["enableTouchFlickToDrop"] = ["enableTouchpadFlickToDrop", "enableFlickToDrop"],
		["touchFlickToDropVelocityThreshold"] = ["touchpadFlickToDropVelocityThreshold", "flickToDropVelocityThreshold"],
		["touchpadNaturalDragReleaseThreshold"] = ["naturalDragReleaseThreshold"],
		["showTouchHoldForceWindowDragIndicator"] = ["showTouchpadHoldForceWindowDragIndicator"],
		["touchHoldForceWindowDragIndicatorDisplayMs"] = ["touchpadHoldForceWindowDragIndicatorDisplayMs"],
		["dragModeSwitcherShortcut"] = ["forceNonWindowDragShortcut"],
		["enableStylusHoldForceWindowDrag"] = ["enableStylusHoldToMoveWindow"],
	};

	/// <summary>Reverse of <see cref="LegacySettingNames"/>: each legacy name mapped to its current name.</summary>
	private static readonly Dictionary<string, string> LegacyToCurrentSettingName = LegacySettingNames
		.SelectMany(pair => pair.Value.Select(legacyName => (LegacyName: legacyName, CurrentName: pair.Key)))
		.ToDictionary(pair => pair.LegacyName, pair => pair.CurrentName, StringComparer.Ordinal);

	/// <summary>Finds a setting by name, checking the current key first and then older names kept for backward compatibility.</summary>
	private static bool TryGetSettingProperty(JsonElement root, string name, out JsonElement property)
	{
		if (TryGetSettingPropertyDirect(root, name, out property)) return true;

		if (LegacySettingNames.TryGetValue(name, out var legacyNames))
		{
			foreach (var legacyName in legacyNames) if (TryGetSettingPropertyDirect(root, legacyName, out property)) return true;
		}

		property = default;
		return false;
	}

	/// <summary>Reads target classifier precedence from the order of detection settings in the config.</summary>
	private static TargetDetection[] GetTargetDetectionOrder(JsonElement root)
	{
		List<TargetDetection> order = new(DefaultTargetDetectionOrder.Length);
		HashSet<TargetDetection> seen = [];
		if (root.TryGetProperty("detection", out var detectionSettings) && detectionSettings.ValueKind == JsonValueKind.Object)
		{
			foreach (var setting in detectionSettings.EnumerateObject())
			{
				var detection = setting.Name switch
				{
					"enableUiAutomationDetection" => TargetDetection.UiAutomation,
					"enableOcrCapture" => TargetDetection.Ocr,
					"enableHitTestDetection" => TargetDetection.HitTest,
					"enableSolidColorDetection" => TargetDetection.SolidColor,
					"enableCursorShapeDetection" => TargetDetection.CursorShape,
					"enableControlClassNameDetection" => TargetDetection.ControlClassName,
					_ => (TargetDetection?)null,
				};

				if (detection.HasValue && seen.Add(detection.Value)) order.Add(detection.Value);
			}
		}

		foreach (var detection in DefaultTargetDetectionOrder) if (seen.Add(detection)) order.Add(detection);

		return [.. order];
	}

	/// <summary>Looks up a setting in each known section, then at the top level. Does not check legacy names.</summary>
	private static bool TryGetSettingPropertyDirect(JsonElement root, string name, out JsonElement property)
	{
		foreach (var section in SettingsSections) if (root.TryGetProperty(section, out var group) && group.ValueKind == JsonValueKind.Object && group.TryGetProperty(name, out property)) return true;

		if (root.TryGetProperty(name, out property)) return true;

		property = default;
		return false;
	}

	/// <summary>
	/// Reads the allowed min/max for one key from the "numericValueBounds" block. The block can only narrow the built-in
	/// range passed in as the defaults, never widen it. Invalid or empty ranges fall back to the defaults.
	/// </summary>
	private static (int Minimum, int Maximum) GetNumericValueBounds(JsonElement root, string key, int defaultMinimum, int defaultMaximum)
	{
		if (!root.TryGetProperty("numericValueBounds", out var allBounds)
			|| allBounds.ValueKind != JsonValueKind.Object
			|| !allBounds.TryGetProperty(key, out var bounds)
			|| bounds.ValueKind != JsonValueKind.Object)
		{
			return (defaultMinimum, defaultMaximum);
		}

		var minimum = bounds.TryGetProperty("min", out var minValue) && minValue.TryGetInt32(out var parsedMinimum) ? Math.Max(defaultMinimum, parsedMinimum) : defaultMinimum;
		var maximum = bounds.TryGetProperty("max", out var maxValue) && maxValue.TryGetInt32(out var parsedMaximum) ? Math.Min(defaultMaximum, parsedMaximum) : defaultMaximum;
		return minimum <= maximum ? (minimum, maximum) : (defaultMinimum, defaultMaximum);
	}

	/// <summary>Clamps a value to the bounds from the config file. When <paramref name="odd"/> is true, the result is forced to an odd number.</summary>
	private static int ClampConfiguredInt(JsonElement root, string key, int value, int defaultMinimum, int defaultMaximum, bool odd = false)
	{
		var (minimum, maximum) = GetNumericValueBounds(root, key, defaultMinimum, defaultMaximum);
		if (odd)
		{
			// Odd bounds keep the clamp range odd at both ends, so an odd value always exists in it.
			if (minimum % 2 == 0) minimum++;
			if (maximum % 2 == 0) maximum--;
			if (minimum > maximum) (minimum, maximum) = (defaultMinimum, defaultMaximum);
		}

		var normalized = Math.Clamp(value, minimum, maximum);
		// If clamping produced an even number, step toward the range interior to make it odd.
		if (odd && normalized % 2 == 0) normalized = normalized < maximum ? normalized + 1 : normalized - 1;
		return normalized;
	}
}
