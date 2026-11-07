using System.Globalization;

namespace ThreeFingerDrag;

/// <summary>Converts configured key names and shortcuts to Win32 virtual-key codes.</summary>
internal static class KeyNameParser
{
	// Named keys map to Win32 VK_ codes. Left and right modifier variants have their own codes
	// (for example LCtrl and RCtrl), while the generic names (Control, Shift, Alt) map to the shared code.
	private static readonly Dictionary<string, int> NamedVirtualKeys = new(StringComparer.OrdinalIgnoreCase)
	{
		["Control"] = 0x11,
		["Ctrl"] = 0x11,
		["LControl"] = 0xA2,
		["LCtrl"] = 0xA2,
		["RControl"] = 0xA3,
		["RCtrl"] = 0xA3,
		["Shift"] = 0x10,
		["LShift"] = 0xA0,
		["RShift"] = 0xA1,
		["Alt"] = 0x12,
		["Menu"] = 0x12,
		["LAlt"] = 0xA4,
		["LMenu"] = 0xA4,
		["RAlt"] = 0xA5,
		["RMenu"] = 0xA5,
		["Win"] = 0x5B,
		["LWin"] = 0x5B,
		["RWin"] = 0x5C,
		["Escape"] = 0x1B,
		["Esc"] = 0x1B,
		["Space"] = 0x20,
		["Tab"] = 0x09,
		["CapsLock"] = 0x14,
		["Enter"] = 0x0D,
		["Return"] = 0x0D,
		["Backspace"] = 0x08,
		["Insert"] = 0x2D,
		["Delete"] = 0x2E,
		["Del"] = 0x2E,
		["Home"] = 0x24,
		["End"] = 0x23,
		["PageUp"] = 0x21,
		["PageDown"] = 0x22,
		["Left"] = 0x25,
		["Up"] = 0x26,
		["Right"] = 0x27,
		["Down"] = 0x28,
		["PrintScreen"] = 0x2C,
		["Pause"] = 0x13,
		["ScrollLock"] = 0x91,
		["NumLock"] = 0x90,
		["Apps"] = 0x5D,
	};

	// Accepted forms, checked in this order: named keys, a single letter or digit, F1 to F24,
	// hex with a 0x prefix, and plain decimal numbers. Matching is case-insensitive.
	/// <summary>Parses a key name, such as <c>Control</c>, <c>F13</c>, or <c>0x11</c>.</summary>
	public static bool TryParseKey(string? keyName, out int virtualKeyCode)
	{
		virtualKeyCode = 0;
		if (string.IsNullOrWhiteSpace(keyName)) return false;

		var trimmed = keyName.Trim();
		// Named keys are checked first, so a name such as "Del" is never read as a number.
		if (NamedVirtualKeys.TryGetValue(trimmed, out var namedCode))
		{
			virtualKeyCode = namedCode;
			return true;
		}

		if (trimmed.Length == 1)
		{
			var upper = char.ToUpperInvariant(trimmed[0]);
			if (upper is (>= 'A' and <= 'Z') or (>= '0' and <= '9'))
			{
				// Letter and digit virtual-key codes match their ASCII values.
				virtualKeyCode = upper;
				return true;
			}
		}

		if (trimmed.Length is 2 or 3 && (trimmed[0] is 'F' or 'f') && int.TryParse(trimmed.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var functionKeyNumber) && functionKeyNumber is >= 1 and <= 24)
		{
			virtualKeyCode = 0x70 + (functionKeyNumber - 1); // VK_F1 = 0x70, VK_F2 = 0x71, ...
			return true;
		}

		// Hex is only accepted with a 0x prefix. Plain numbers are read as decimal, so "17" means VK 0x11 (Control).
		if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && int.TryParse(trimmed.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hexCode))
		{
			virtualKeyCode = hexCode;
			return true;
		}

		if (int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var decimalCode))
		{
			virtualKeyCode = decimalCode;
			return true;
		}

		return false;
	}

	/// <summary>Parses a shortcut such as <c>Control+Alt+W</c>; every key must be recognized.</summary>
	public static bool TryParseShortcut(string? shortcut, out int[] virtualKeyCodes)
	{
		virtualKeyCodes = [];
		if (string.IsNullOrWhiteSpace(shortcut))
		{
			CursorDiagnosticLogger.Record("key_name_parser", "shortcut_parse_failed", details: new { reason = "empty", input = shortcut });
			return false;
		}

		// Shortcut parts may be separated by "+" or ",". Duplicate keys are dropped.
		var parts = shortcut.Split(['+', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (parts.Length == 0)
		{
			CursorDiagnosticLogger.Record("key_name_parser", "shortcut_parse_failed", details: new { reason = "no_parts", input = shortcut });
			return false;
		}

		List<int> codes = new(parts.Length);
		foreach (var part in parts)
		{
			if (!TryParseKey(part, out var code))
			{
				CursorDiagnosticLogger.Record("key_name_parser", "shortcut_parse_failed", details: new { reason = "unknown_key", unknownPart = part, input = shortcut });
				return false;
			}
			if (!codes.Contains(code)) codes.Add(code);
		}

		virtualKeyCodes = [.. codes];
		return virtualKeyCodes.Length > 0;
	}
}
