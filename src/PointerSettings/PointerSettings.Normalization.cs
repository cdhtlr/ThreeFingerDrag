using static ThreeFingerDrag.NativeMethods;

namespace ThreeFingerDrag;

internal static partial class PointerSettings
{
	/// <summary>Normalizes the drag mouse button list. Keeps known names in a fixed order and returns the default when nothing valid remains.</summary>
	private static string NormalizeDragMouseButtons(string buttons)
	{
		HashSet<string> known = new(StringComparer.OrdinalIgnoreCase) { "Left", "Right", "Middle", "X1", "X2" };
		if (string.Equals(buttons.Trim(), "Secondary", StringComparison.OrdinalIgnoreCase)) return "Secondary";

		var parsed = buttons.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Where(known.Contains)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(button => button switch { "Left" => 0, "Right" => 1, "Middle" => 2, "X1" => 3, _ => 4 })
			.ToArray();
		return parsed.Length == 0 ? DefaultTouchDragButtons : string.Join('+', parsed);
	}
	/// <summary>
	/// Converts the trigger button to a stable name. The button that Windows treats as primary is stored
	/// as "Secondary" so the setting keeps working when the user swaps mouse buttons.
	/// </summary>
	private static string NormalizeMouseTriggerButton(string button)
	{
		var normalized = button.Trim();
		if (string.Equals(normalized, "Secondary", StringComparison.OrdinalIgnoreCase)) return "Secondary";
		if (string.Equals(normalized, "Left", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, "Right", StringComparison.OrdinalIgnoreCase))
		{
			// SM_SWAPBUTTON (23) is nonzero when the buttons are swapped. Then the physical right button is primary.
			var primary = GetSystemMetrics(23) != 0 ? "Right" : "Left";
			return string.Equals(normalized, primary, StringComparison.OrdinalIgnoreCase) ? "Secondary" : normalized;
		}
		if (string.Equals(normalized, "Middle", StringComparison.OrdinalIgnoreCase)) return "Middle";
		if (string.Equals(normalized, "X1", StringComparison.OrdinalIgnoreCase)) return "X1";
		if (string.Equals(normalized, "X2", StringComparison.OrdinalIgnoreCase)) return "X2";

		return DefaultMouseDragTriggerButton;
	}
}
