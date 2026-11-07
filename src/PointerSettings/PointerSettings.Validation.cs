using System.Text.Json;
using System.Text.RegularExpressions;

namespace ThreeFingerDrag;

internal static partial class PointerSettings
{
	/// <summary>Binding settings that must not clash. The order decides which setting keeps its value when two clash at load.</summary>
	internal static readonly string[] BindingSettingNames = ["dragCancelKey", "forceWindowDragKey", "forceNonWindowDragKey", "dragModeSwitcherShortcut", "windowMoveModifierKey"];

	/// <summary>Accepts a BCP-47 style tag such as "en-US" or "zh-Hant-TW". Empty means the user profile language.</summary>
	private static readonly Regex OcrLanguageTagPattern = new(@"^[A-Za-z]{2,3}(-[A-Za-z0-9]{1,8})*$", RegexOptions.CultureInvariant);

	/// <summary>Returns the built-in value for a binding setting.</summary>
	private static string GetDefaultBinding(string settingName) => settingName switch
	{
		"dragCancelKey" => DefaultTouchpadCancelDragKey,
		"forceWindowDragKey" => DefaultForceWindowDragKey,
		"forceNonWindowDragKey" => DefaultForceNonWindowDragKey,
		"dragModeSwitcherShortcut" => DefaultDragModeSwitcherShortcut,
		"windowMoveModifierKey" => DefaultWindowMoveModifierKey,
		_ => "",
	};

	/// <summary>
	/// Checks a key or shortcut name. Empty turns the binding off. A name that cannot be parsed is replaced by the default.
	/// </summary>
	private static string ValidateBindingValue(string settingName, string value, bool isShortcut, List<string> warnings)
	{
		var trimmed = value.Trim();
		if (trimmed.Length == 0) return "";

		var isValid = isShortcut ? KeyNameParser.TryParseShortcut(trimmed, out _) : KeyNameParser.TryParseKey(trimmed, out _);
		if (isValid) return trimmed;

		var fallback = GetDefaultBinding(settingName);
		warnings.Add($"{settingName}: \"{value}\" is not a valid key name. Using \"{fallback}\".");
		return fallback;
	}

	/// <summary>Checks the OCR language tag. Empty is allowed, and any other value that is not a language tag falls back to the default.</summary>
	private static string ValidateOcrLanguageTag(string value, List<string> warnings)
	{
		var trimmed = value.Trim();
		if (trimmed.Length == 0 || OcrLanguageTagPattern.IsMatch(trimmed)) return trimmed;

		warnings.Add($"ocrLanguageTag: \"{value}\" is not a valid language tag. Using \"{DefaultOcrLanguageTag}\".");
		return DefaultOcrLanguageTag;
	}

	/// <summary>
	/// Resolves all binding settings at load. Each binding is checked against the ones resolved before it.
	/// A clashing binding falls back to its default, or is cleared when the default clashes too.
	/// </summary>
	private static Dictionary<string, string> ResolveBindings(Dictionary<string, string> requested, List<string> warnings)
	{
		Dictionary<string, string> resolved = new(StringComparer.Ordinal);
		foreach (var name in BindingSettingNames)
		{
			var value = requested[name];
			if (value.Length == 0 || FindBindingConflict(name, value, resolved) is not { } conflict)
			{
				resolved[name] = value;
				continue;
			}

			var fallback = GetDefaultBinding(name);
			var replacement = FindBindingConflict(name, fallback, resolved) is null ? fallback : "";
			warnings.Add($"{name}: \"{value}\" clashes with {conflict}. Using \"{replacement}\".");
			resolved[name] = replacement;
		}
		return resolved;
	}

	/// <summary>
	/// Returns the name of another binding that clashes with <paramref name="candidate"/>, or null.
	/// Two shortcuts clash only when they use exactly the same keys. A shortcut and a single key clash
	/// when their keys overlap, except where <see cref="AllowsShortcutOverlap"/> permits it.
	/// </summary>
	internal static string? FindBindingConflict(string settingKey, string candidate, IReadOnlyDictionary<string, string> values)
	{
		var candidateKeys = ParseBindingKeys(candidate);
		if (candidateKeys.Count == 0) return null;

		bool candidateIsShortcut = IsShortcutBinding(settingKey);
		foreach (var other in BindingSettingNames)
		{
			if (other == settingKey || !values.TryGetValue(other, out var otherValue)) continue;

			var otherKeys = ParseBindingKeys(otherValue);
			bool otherIsShortcut = IsShortcutBinding(other);
			if (candidateIsShortcut && otherIsShortcut)
			{
				if (candidateKeys.SetEquals(otherKeys)) return other;
				continue;
			}
			if ((candidateIsShortcut && AllowsShortcutOverlap(other)) || (otherIsShortcut && AllowsShortcutOverlap(settingKey))) continue;
			if (BindingKeysOverlap(candidateKeys, otherKeys)) return other;
		}
		return null;
	}

	/// <summary>True only for the drag mode switcher, which records a full shortcut instead of a single key.</summary>
	private static bool IsShortcutBinding(string key) => key is "dragModeSwitcherShortcut";

	/// <summary>
	/// Bindings that may share keys with a shortcut. These are the force-drag keys and the move modifier,
	/// which are held together with other keys during a drag.
	/// </summary>
	private static bool AllowsShortcutOverlap(string key) => key is "forceWindowDragKey" or "forceNonWindowDragKey" or "windowMoveModifierKey";

	/// <summary>
	/// True when two key sets share a key. A generic modifier code (Ctrl 0x11, Shift 0x10, Alt 0x12) matches
	/// its left or right code, and the left and right Windows keys match each other.
	/// </summary>
	private static bool BindingKeysOverlap(HashSet<int> first, HashSet<int> second) => first.Any(a => second.Any(b =>
		a == b
		|| (a == 0x11 && b is 0xA2 or 0xA3) || (b == 0x11 && a is 0xA2 or 0xA3)
		|| (a == 0x10 && b is 0xA0 or 0xA1) || (b == 0x10 && a is 0xA0 or 0xA1)
		|| (a == 0x12 && b is 0xA4 or 0xA5) || (b == 0x12 && a is 0xA4 or 0xA5)
		|| (a == 0x5B && b == 0x5C) || (b == 0x5B && a == 0x5C)));

	/// <summary>Turns a stored binding string into virtual-key codes. Invalid text gives an empty set.</summary>
	private static HashSet<int> ParseBindingKeys(string value)
	{
		if (!KeyNameParser.TryParseShortcut(value, out int[] keys)) return [];
		return [.. keys];
	}

	/// <summary>
	/// Adds a warning for each setting stored more than once in the config, counting legacy names as the current name.
	/// Only the first match is read, so the other copies have no effect.
	/// </summary>
	private static void AddDuplicateSettingWarnings(JsonElement root, List<string> warnings)
	{
		Dictionary<string, int> counts = new(StringComparer.Ordinal);
		void Count(string name)
		{
			var canonical = LegacyToCurrentSettingName.TryGetValue(name, out var current) ? current : name;
			counts[canonical] = counts.GetValueOrDefault(canonical) + 1;
		}

		foreach (var property in root.EnumerateObject())
		{
			if (!IsSettingsSection(property.Name))
			{
				Count(property.Name);
				continue;
			}
			if (property.Value.ValueKind != JsonValueKind.Object) continue;
			foreach (var setting in property.Value.EnumerateObject()) Count(setting.Name);
		}

		foreach (var (name, count) in counts)
		{
			if (count > 1) warnings.Add($"{name}: stored {count} times in the config (legacy names included). Only the first one is used.");
		}
	}
}
