using static ThreeFingerDrag.NativeMethods;

namespace ThreeFingerDrag;

internal sealed partial class SettingsWindow : Form
{
	/// <summary>
	/// Creates a row that records mouse buttons. The user presses Record, then clicks. Recording polls the
	/// buttons every 25 ms and first waits for all buttons to be released, so the click on Record is not captured.
	/// Only one button may be recorded. The primary button is refused for the drag trigger.
	/// </summary>
	private Control MouseButtonCaptureSetting(string settingKey, string label, string help)
	{
		var row = Row(label, help);
		var editor = EditorHost(row);
		Label value = new() { Height = 24, AutoSize = false, AutoEllipsis = false, TextAlign = ContentAlignment.MiddleCenter, Margin = Padding.Empty, BorderStyle = BorderStyle.FixedSingle };
		Button record = new() { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, TextAlign = ContentAlignment.MiddleCenter, Margin = Padding.Empty };
		Button clear = new() { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, TextAlign = ContentAlignment.MiddleCenter, Margin = Padding.Empty };
		_ = LayoutCaptureControls(editor, value, record, clear);
		System.Windows.Forms.Timer timer = new() { Interval = PointerSettings.GetSettingsCaptureRefreshIntervalMs() };
		string current = ReadString(settingKey, settingKey == "mouseDragTriggerButton" ? "Secondary" : "Left+Right");
		bool recording = false;
		bool waitUntilReleased = false;
		var fallback = settingKey == "mouseDragTriggerButton" ? "Secondary" : "Left+Right";

		// "Secondary" is the Windows name for the right button, which swaps for left-handed setups.
		string Display(string setting) => setting == "Secondary" ? T("secondaryMouseButton") : string.IsNullOrWhiteSpace(setting) ? T("keyOff") : string.Join(" + ", setting.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(button => button switch
		{
			"Left" => T("left"),
			"Right" => T("right"),
			"Middle" => T("middle"),
			"X1" => T("mouseBack"),
			"X2" => T("mouseForward"),
			_ => button,
		}));
		void RefreshValue() => value.Text = Display(current);
		void SetRecording(bool active)
		{
			recording = active;
			CursorDiagnosticLogger.Record("settings_mouse_capture", active ? "capture_start" : "capture_finish", details: new { setting = settingKey, current });
			record.Text = active ? T("recordingMouseButtons") : T("recordMouseButtons");
			if (active)
			{
				waitUntilReleased = true;
				_ = record.Focus();
				timer.Start();
			} else
			{
				timer.Stop();
			}
		}

		record.Click += (_, _) => SetRecording(!recording);
		clear.Click += (_, _) => { current = fallback; RefreshValue(); PointerSettings.SaveSetting(settingKey, current); };
		timer.Tick += (_, _) =>
		{
			if (!recording) return;

			if (waitUntilReleased)
			{
				// Virtual-key codes 1, 2, 4, 5, and 6 are the mouse buttons. Code 3 (Cancel) is not checked.
				var anyDown = Enumerable.Range(1, 6).Where(key => key is 1 or 2 or 4 or 5 or 6).Any(key => (GetAsyncKeyState(key) & 0x8000) != 0);
				if (anyDown) return;

				waitUntilReleased = false;
				return;
			}
			// Collect every button that is down now. Two or more at once are rejected.
			List<string> buttons = [];
			if ((GetAsyncKeyState(0x01) & 0x8000) != 0) buttons.Add("Left");
			if ((GetAsyncKeyState(0x02) & 0x8000) != 0) buttons.Add("Right");
			if ((GetAsyncKeyState(0x04) & 0x8000) != 0) buttons.Add("Middle");
			if ((GetAsyncKeyState(0x05) & 0x8000) != 0) buttons.Add("X1");
			if ((GetAsyncKeyState(0x06) & 0x8000) != 0) buttons.Add("X2");

			if (buttons.Count == 0) return;

			if (buttons.Count > 1)
			{
				CursorDiagnosticLogger.Record("settings_mouse_capture", "rejected_multiple_buttons", details: new { setting = settingKey, buttons = string.Join('+', buttons) });
				_ = MessageBox.Show(this, T("oneMouseButtonOnly"), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}
			if (settingKey == "mouseDragTriggerButton" && UsesPrimaryMouseButton(buttons))
			{
				CursorDiagnosticLogger.Record("settings_mouse_capture", "rejected_primary_button", details: new { setting = settingKey, buttons = string.Join('+', buttons) });
				_ = MessageBox.Show(this, T("primaryMouseButtonNotAllowed"), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}
			current = string.Join('+', buttons);
			CursorDiagnosticLogger.Record("settings_mouse_capture", "captured", details: new { setting = settingKey, value = current });
			RefreshValue();
			PointerSettings.SaveSetting(settingKey, current);
			SetRecording(false);
		};
		record.LostFocus += (_, _) => { if (recording) { SetRecording(false); } };
		RefreshValue();
		record.Text = T("recordMouseButtons");
		clear.Text = T("clear");
		row.Disposed += (_, _) => timer.Dispose();
		return row;
	}

	/// <summary>True when the buttons include the one Windows currently treats as primary (left, unless swapped).</summary>
	private static bool UsesPrimaryMouseButton(IEnumerable<string> buttons)
	{
		var primary = GetSystemMetrics(23) != 0 ? "Right" : "Left"; // SM_SWAPBUTTON
		return buttons.Contains(primary, StringComparer.OrdinalIgnoreCase);
	}
	/// <summary>
	/// Creates a row that records a key or shortcut. After Record is pressed, the next key press is read in the
	/// text box KeyDown handler. For shortcuts, modifiers come from the physical key state, so left and right
	/// are stored separately. Escape cancels a shortcut recording.
	/// </summary>
	private Control KeyCaptureSetting(string key, string label, string help, string fallback, bool isShortcut = false)
	{
		var row = Row(label, help);
		var editor = EditorHost(row);
		var currentValue = ReadString(key, fallback);
		var capturing = false;
		TextBox value = new()
		{
			ReadOnly = true,
			Multiline = true,
			WordWrap = true,
			ScrollBars = ScrollBars.None,
			TextAlign = HorizontalAlignment.Center,
			Text = FormatKeySettingForDisplay(currentValue),
			Margin = Padding.Empty,
		};
		Button record = new() { Text = T("recordKey"), Font = new Font("Segoe UI", 8f), Margin = Padding.Empty, Padding = new Padding(5, 3, 5, 3), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, TextAlign = ContentAlignment.MiddleCenter };
		Button clear = new() { Text = T("clearKey"), Font = new Font("Segoe UI", 8f), Margin = Padding.Empty, Padding = new Padding(5, 3, 5, 3), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, TextAlign = ContentAlignment.MiddleCenter };
		_ = LayoutCaptureControls(editor, value, record, clear);

		void StopCapturing()
		{
			capturing = false;
			record.Text = T("recordKey");
			value.Text = FormatKeySettingForDisplay(currentValue);
		}

		record.Click += (_, _) =>
		{
			if (capturing)
			{
				CursorDiagnosticLogger.Record("settings_key_capture", "capture_cancel", details: new { setting = key, reason = "stop_button" });
				StopCapturing();
				return;
			}
			capturing = true;
			CursorDiagnosticLogger.Record("settings_key_capture", "capture_start", details: new { setting = key, isShortcut });
			record.Text = T("stopRecordKey");
			value.Text = T("captureKeyPrompt");
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

			// Plain Escape cancels a shortcut recording, so Escape can be used as a single key elsewhere.
			if (isShortcut && e.KeyCode == Keys.Escape && e.Modifiers == Keys.None)
			{
				CursorDiagnosticLogger.Record("settings_key_capture", "capture_cancel", details: new { setting = key, reason = "escape" });
				StopCapturing();
				return;
			}

			// A shortcut needs a main key. Pressing only a modifier keeps the prompt on screen.
			if (IsModifierKey(e.KeyCode))
			{
				if (isShortcut)
				{
					value.Text = T("captureMainKeyPrompt");
					return;
				}
			}

			var keyName = ToConfigKeyName(e.KeyCode);
			if (string.IsNullOrEmpty(keyName))
			{
				value.Text = T("unsupportedKeyPrompt");
				return;
			}

			// The stored string lists modifiers first, then the main key, for example "LControl+LAlt+W".
			List<string> parts = [];
			if (isShortcut)
			{
				AddCapturedModifier(parts, e.Control, 0xA2, 0xA3, "LControl", "RControl");
				AddCapturedModifier(parts, e.Alt, 0xA4, 0xA5, "LAlt", "RAlt");
				AddCapturedModifier(parts, e.Shift, 0xA0, 0xA1, "LShift", "RShift");
				AddCapturedModifier(parts, IsKeyDown(0x5B) || IsKeyDown(0x5C), 0x5B, 0x5C, "LWin", "RWin");
			}
			parts.Add(keyName);
			currentValue = string.Join("+", parts.Distinct(StringComparer.OrdinalIgnoreCase));
			// A conflicting binding is refused and recording stays open. Nothing is saved.
			if (FindBindingConflict(key, currentValue) is { } conflict)
			{
				CursorDiagnosticLogger.Record("settings_key_capture", "capture_conflict", details: new { setting = key, binding = currentValue, conflictSetting = conflict });
				_ = MessageBox.Show(this, string.Format(T("bindingConflict"), T(SettingLabelKey(conflict) ?? conflict)), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
				value.Text = T("captureKeyPrompt");
				return;
			}
			CursorDiagnosticLogger.Record("settings_key_capture", "capture_finish", details: new { setting = key, binding = currentValue });
			PointerSettings.SaveSetting(key, currentValue);
			StopCapturing();
		};
		return row;
	}
}
