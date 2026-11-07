using static ThreeFingerDrag.NativeMethods;

namespace ThreeFingerDrag;

internal sealed partial class SettingsWindow : Form
{
	/// <summary>Creates a row that records one modifier key, such as the window move modifier. Only the modifier name is stored.</summary>
	private Control ModifierCaptureSetting(string key, string label, string help, string fallback)
	{
		var row = Row(label, help);
		var editor = EditorHost(row);
		var currentValue = ReadString(key, fallback);
		var capturing = false;
		TextBox value = new() { ReadOnly = true, Multiline = true, WordWrap = true, ScrollBars = ScrollBars.None, TextAlign = HorizontalAlignment.Center, Text = FormatKeySettingForDisplay(currentValue), Margin = Padding.Empty };
		Button record = new() { Text = T("recordKey"), Font = new Font("Segoe UI", 8f), Margin = Padding.Empty, Padding = new Padding(5, 3, 5, 3), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, TextAlign = ContentAlignment.MiddleCenter };
		Button clear = new() { Text = T("clearKey"), Font = new Font("Segoe UI", 8f), Margin = Padding.Empty, Padding = new Padding(5, 3, 5, 3), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, TextAlign = ContentAlignment.MiddleCenter };
		_ = LayoutCaptureControls(editor, value, record, clear);

		void StopCapturing()
		{ capturing = false; record.Text = T("recordKey"); value.Text = FormatKeySettingForDisplay(currentValue); }
		record.Click += (_, _) =>
		{
			if (capturing)
			{ CursorDiagnosticLogger.Record("settings_modifier_capture", "capture_cancel", details: new { setting = key }); StopCapturing(); return; }
			CursorDiagnosticLogger.Record("settings_modifier_capture", "capture_start", details: new { setting = key });
			capturing = true;
			record.Text = T("stopRecordKey");
			value.Text = T("captureModifierPrompt");
			_ = value.Focus();
		};
		clear.Click += (_, _) =>
		{
			StopCapturing();
			currentValue = ResolveClearValue(key, fallback);
			value.Text = currentValue.Length == 0 ? T("keyOff") : FormatKeySettingForDisplay(currentValue);
			if (!loading) PointerSettings.SaveSetting(key, currentValue);
		};
		value.KeyDown += (_, e) =>
		{
			if (!capturing) return;

			e.Handled = true;
			e.SuppressKeyPress = true;
			var modifier = ToConfigModifierName(e.KeyCode);
			if (modifier is null)
			{ value.Text = T("captureModifierPrompt"); return; }
			if (FindBindingConflict(key, modifier) is { } conflict)
			{
				CursorDiagnosticLogger.Record("settings_modifier_capture", "rejected_conflict", details: new { setting = key, value = modifier, conflict });
				_ = MessageBox.Show(this, string.Format(T("bindingConflict"), T(SettingLabelKey(conflict) ?? conflict)), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
				value.Text = T("captureModifierPrompt");
				return;
			}
			currentValue = modifier;
			CursorDiagnosticLogger.Record("settings_modifier_capture", "capture_finish", details: new { setting = key, value = modifier });
			PointerSettings.SaveSetting(key, currentValue);
			StopCapturing();
		};
		return row;
	}

	/// <summary>Returns the name of another binding that clashes with <paramref name="candidate"/>, or null. Uses the same rules as config load.</summary>
	private static string? FindBindingConflict(string settingKey, string candidate)
	{
		Dictionary<string, string> values = new(StringComparer.Ordinal);
		foreach (var name in PointerSettings.BindingSettingNames) values[name] = ReadString(name, "");
		return PointerSettings.FindBindingConflict(settingKey, candidate, values);
	}

	/// <summary>Returns the fallback when it does not clash with another binding. Otherwise the binding is cleared to empty.</summary>
	private static string ResolveClearValue(string settingKey, string fallback) => string.IsNullOrEmpty(fallback) || FindBindingConflict(settingKey, fallback) is null ? fallback : "";

	/// <summary>Maps a pressed modifier to its config name. The physical key state decides left or right.</summary>
	private static string? ToConfigModifierName(Keys key) => (key & Keys.KeyCode) switch
	{
		Keys.ControlKey or Keys.LControlKey => IsKeyDown(0xA3) ? "RControl" : "LControl",
		Keys.RControlKey => "RControl",
		Keys.ShiftKey or Keys.LShiftKey => IsKeyDown(0xA1) ? "RShift" : "LShift",
		Keys.RShiftKey => "RShift",
		Keys.Menu or Keys.LMenu => IsKeyDown(0xA5) ? "RAlt" : "LAlt",
		Keys.RMenu => "RAlt",
		Keys.LWin => "LWin",
		Keys.RWin => "RWin",
		_ => null,
	};

	/// <summary>Returns the translation key for a setting's label, used in conflict messages.</summary>
	private static string? SettingLabelKey(string settingKey) => settingKey switch
	{
		"dragCancelKey" => "cancelKey",
		"forceWindowDragKey" => "forceWindowKey",
		"forceNonWindowDragKey" => "forceNonWindowKey",
		"dragModeSwitcherShortcut" => "dragModeSwitcherShortcut",
		"windowMoveModifierKey" => "moveModifier",
		_ => null,
	};

	/// <summary>True for any shift, control, alt, or Windows key, left or right.</summary>
	private static bool IsModifierKey(Keys key) => key is Keys.ControlKey or Keys.LControlKey or Keys.RControlKey
		or Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey or Keys.Menu or Keys.LMenu or Keys.RMenu or Keys.LWin or Keys.RWin;

	/// <summary>True when the key is held down right now, based on the physical key state.</summary>
	private static bool IsKeyDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

	/// <summary>Adds the left and/or right modifier that is physically held. If neither side reads as down, the left name is used.</summary>
	private static void AddCapturedModifier(List<string> parts, bool isPressed, int leftVk, int rightVk, string leftName, string rightName)
	{
		if (!isPressed) return;

		bool left = IsKeyDown(leftVk);
		bool right = IsKeyDown(rightVk);
		if (left) parts.Add(leftName);
		if (right) parts.Add(rightName);
		if (!left && !right) parts.Add(leftName); // Unknown side defaults to left.
	}

	/// <summary>Converts a key code to the name stored in the config. Keys without a name are stored as hex, such as "0x7B".</summary>
	private static string? ToConfigKeyName(Keys key)
	{
		// Strip modifier flags so only the base key is named.
		key &= Keys.KeyCode;
		return key switch
		{
			Keys.ControlKey or Keys.LControlKey => IsKeyDown(0xA3) ? "RControl" : "LControl",
			Keys.RControlKey => "RControl",
			Keys.ShiftKey or Keys.LShiftKey => IsKeyDown(0xA1) ? "RShift" : "LShift",
			Keys.RShiftKey => "RShift",
			Keys.Menu or Keys.LMenu => IsKeyDown(0xA5) ? "RAlt" : "LAlt",
			Keys.RMenu => "RAlt",
			Keys.LWin => "LWin",
			Keys.RWin => "RWin",
			Keys.Escape => "Escape",
			Keys.Space => "Space",
			Keys.Tab => "Tab",
			Keys.CapsLock => "CapsLock",
			Keys.Enter => "Enter",
			Keys.Back => "Backspace",
			Keys.Insert => "Insert",
			Keys.Delete => "Delete",
			Keys.Home => "Home",
			Keys.End => "End",
			Keys.PageUp => "PageUp",
			Keys.PageDown => "PageDown",
			Keys.Left => "Left",
			Keys.Up => "Up",
			Keys.Right => "Right",
			Keys.Down => "Down",
			_ when key is >= Keys.A and <= Keys.Z => key.ToString(),
			_ when key is >= Keys.D0 and <= Keys.D9 => ((int)key - (int)Keys.D0).ToString(),
			_ when key is >= Keys.F1 and <= Keys.F24 => key.ToString(),
			_ when key != Keys.None => $"0x{(int)key:X2}",
			_ => null,
		};
	}

	/// <summary>Turns a stored binding such as "LControl+W" into a readable label such as "Left Ctrl + W".</summary>
	private string FormatKeySettingForDisplay(string setting)
	{
		if (string.IsNullOrWhiteSpace(setting)) return T("keyOff");
		return string.Join(" + ", setting.Split(['+', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(part => KeyNameParser.TryParseKey(part, out var code) ? FormatVirtualKeyForDisplay(code) : part));
	}

	/// <summary>Gives a readable name for a virtual-key code. Codes without a name are shown in hex.</summary>
	private static string FormatVirtualKeyForDisplay(int key) => key switch
	{
		0x10 => "Shift",
		0xA0 => "Left Shift",
		0xA1 => "Right Shift",
		0x11 => "Ctrl",
		0xA2 => "Left Ctrl",
		0xA3 => "Right Ctrl",
		0x12 => "Alt",
		0xA4 => "Left Alt",
		0xA5 => "Right Alt",
		0x1B => "Esc",
		0x0D => "Enter",
		0x08 => "Backspace",
		0x20 => "Space",
		0x09 => "Tab",
		0x21 => "Page Up",
		0x22 => "Page Down",
		0x25 => "←",
		0x26 => "↑",
		0x27 => "→",
		0x28 => "↓",
		0x5B => "Left Windows",
		0x5C => "Right Windows",
		_ when key is >= 0x30 and <= 0x39 => ((char)key).ToString(),
		_ when key is >= 0x41 and <= 0x5A => ((char)key).ToString(),
		_ when key is >= 0x70 and <= 0x87 => $"F{key - 0x6F}",
		_ when key is >= 0x60 and <= 0x69 => $"Num {key - 0x60}",
		_ => $"Key 0x{key:X2}",
	};
}
