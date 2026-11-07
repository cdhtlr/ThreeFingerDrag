using Windows.Media.Ocr;

namespace ThreeFingerDrag;

internal sealed partial class SettingsWindow : Form
{
	/// <summary>
	/// Config keys that are only shown in advanced mode. They are timing, polling, debounce, timeout,
	/// and sizing values that most users do not need to change.
	/// </summary>
	private static readonly HashSet<string> AdvancedOnlySettingKeys = new(StringComparer.Ordinal)
	{
		"dragFallbackPollIntervalMs", "windowDragSmoothingFrameMs", "windowDragSmoothingMinTimeConstantMs",
		"windowDragSmoothingMaxTimeConstantMs", "windowDragSmoothingMaxElapsedMs", "settingsInputDebounceMs",
		"settingsStatIntervalMs", "notificationSafetyTimeoutMs", "notificationBalloonTimeoutMs",
		"settingsEventPollIntervalMs", "settingsCaptureRefreshIntervalMs", "cursorPositionPollingIntervalMs",
		"touchFlickToDropVelocityThreshold", "touchpadNaturalDragReleaseThreshold", "touchpadDragDropAnchorHoldMs",
		"touchpadPostLiftAnchorGraceMs", "enableSplitDiagnosticLogs",
		"ocrMaximumWordGapPercent", "ocrActivationDelayMs", "ocrClickDelayMs", "ocrClipboardRestoreDelayMs",
		"hitTestTimeoutMs", "uiAutomationClassifyTimeoutMs", "titleBarHitTestTimeoutMs",
		"iBeamTextDetectionAreaSize", "solidColorDetectionRectangleSize",
		"touchCancelDragPollIntervalMs", "touchStationaryDragDropHoldMs", "touchDragDropAnchorGraceMs",
		"touchFingerLiftGraceMs", "touchscreenCancelDragPollIntervalMs", "touchscreenContactLostGraceMs",
		"touchscreenRecentExternalCursorMovementMs", "mouseCancelDragPollIntervalMs",
		"forcedDragModePollIntervalMs", "windowMoveArrowSequenceTimeoutMs", "windowMoveNudgeIntervalMs",
		"ocrCaptureRadius", "dragModeIndicatorDiameter", "dragModeIndicatorDisplayMs",
	};

	/// <summary>
	/// Creates a numeric row bound to a config key. Bounds come from <see cref="PointerSettings.GetNumericValueBounds(string, int, int)"/>,
	/// so the config can widen them. When <paramref name="odd"/> is set, only odd values are allowed.
	/// Changes are saved immediately, except while the form is loading.
	/// </summary>
	private Control Int(string key, string label, string help, int min, int max, bool odd = false, int? fallback = null, bool forceStacked = false)
	{
		// Odd-only mode: even bounds move inward so steps of 2 always land on odd numbers.
		var (Minimum, Maximum) = PointerSettings.GetNumericValueBounds(key, min, max);
		min = Minimum;
		max = Maximum;
		var row = Row(label, help, forceStacked);
		row.Name = key;
		var editor = EditorHost(row);
		int current = ReadInt(key, fallback ?? min);
		if (odd)
		{
			if (min % 2 == 0) min++;
			if (max % 2 == 0) max--;
			if (current % 2 == 0)
			current = current < max ? current + 1 : current - 1;
		}
		// The odd adjustment can empty the range (for example min == max == 2), so the original bounds are restored.
		if (min > max)
		{ min = Minimum; max = Maximum; }
		NumericUpDown input = new() { Minimum = min, Maximum = max, Increment = odd ? 2 : 1, Value = Math.Clamp(current, min, max), Width = 112, Height = 30, Anchor = AnchorStyles.Left, TextAlign = HorizontalAlignment.Right, ThousandsSeparator = false };
		CenterVertically(editor, input);
		var typedState = SyncTypedNumericValue(input);
		// An out-of-range typed value is saved with the number the user entered, not just the clamped value.
		input.ValueChanged += (_, _) =>
		{
			if (loading) return;

			var value = decimal.ToInt32(input.Value);
			if (typedState.OutOfRange is { } entered) PointerSettings.SaveOutOfRangeNumericValue(key, entered, value); else PointerSettings.SaveSetting(key, value);
		};
		editor.Controls.Add(input);
		return row;
	}
	// NumericUpDown only commits typed text on focus loss; in-range typed values are applied after a short pause in typing (debounce).
	// Out-of-range text is clamped to the nearest bound when the edit is committed (Enter or focus loss).
	/// <summary>Holds the out-of-range number the user typed, or null when the typed value is valid.</summary>
	private sealed class TypedNumericState
	{
		public int? OutOfRange { get; set; }
	}
	/// <summary>Hooks the text box inside a NumericUpDown so typed text is applied shortly after the user stops typing.</summary>
	private static TypedNumericState SyncTypedNumericValue(NumericUpDown input)
	{
		TypedNumericState state = new();
		// syncingText stops the programmatic text update from re-entering this handler.
		var editBox = input.Controls.OfType<TextBox>().FirstOrDefault();
		if (editBox == null) return state;

		bool syncingText = false;
		// Each keystroke restarts the timer, so the value is applied only after the user pauses typing.
		var applyTimer = new System.Windows.Forms.Timer { Interval = PointerSettings.GetSettingsInputDebounceMs() };
		editBox.Disposed += (_, _) => applyTimer.Dispose();
		void ApplyTypedValue()
		{
			if (!int.TryParse(editBox.Text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.CurrentCulture, out var typed)) return;
			if (typed < input.Minimum || typed > input.Maximum || typed == input.Value) return;

			syncingText = true;
			try
			{ input.Value = typed; }
			finally { syncingText = false; }
		}
		applyTimer.Tick += (_, _) =>
		{
			applyTimer.Stop();
			ApplyTypedValue();
		};
		editBox.TextChanged += (_, _) =>
		{
			if (syncingText) return;

			applyTimer.Stop();
			if (!int.TryParse(editBox.Text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.CurrentCulture, out var typed))
			{
				state.OutOfRange = null;
				return;
			}
			state.OutOfRange = typed < input.Minimum || typed > input.Maximum ? typed : null;
			if (state.OutOfRange is null) applyTimer.Start();
		};
		// Runs on Enter or focus loss. Unparseable text falls back to the current value. Out-of-range text is clamped.
		void CommitTypedValue()
		{
			applyTimer.Stop();
			var typed = int.TryParse(editBox.Text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.CurrentCulture, out var parsed) ? parsed : decimal.ToInt32(input.Value);
			var clamped = Math.Clamp(typed, (int)input.Minimum, (int)input.Maximum);
			syncingText = true;
			try
			{
				input.Value = clamped;
				editBox.Text = clamped.ToString(System.Globalization.CultureInfo.CurrentCulture);
			}
			finally { syncingText = false; }
			state.OutOfRange = null;
		}
		editBox.Leave += (_, _) => CommitTypedValue();
		editBox.KeyDown += (_, e) =>
		{
			if (e.KeyCode != Keys.Enter) return;
			e.SuppressKeyPress = true;
			CommitTypedValue();
		};
		return state;
	}
	private CheckBox? clickLockCheck;
	private NumericUpDown? clickLockTimeInput;
	internal static readonly string[] sourceArray = new[] { "global", "logging", "detection", "indicators", "ocr", "touch", "mouse", "stylus", "keyboard" };

	/// <summary>Row with a checkbox that turns Windows ClickLock on or off. The state is kept by Windows, not the config.</summary>
	private Control ClickLockEnabledSetting()
	{
		var row = Row(T("clickLockEnabled"), T("clickLockEnabledHelp"));
		var editor = EditorHost(row);
		clickLockCheck = new CheckBox { Text = T("enabled"), AutoSize = true, Padding = new Padding(6, 0, 0, 0), Anchor = AnchorStyles.Left };
		_ = ClickLock.TryGetEnabled(out var enabled);
		clickLockCheck.Checked = enabled;
		CenterVertically(editor, clickLockCheck);
		clickLockCheck.CheckedChanged += (_, _) => { if (!loading) { _ = ClickLock.SetEnabled(clickLockCheck.Checked); } };
		editor.Controls.Add(clickLockCheck);
		return row;
	}
	/// <summary>Row with the ClickLock hold time. Like the checkbox, the value is read from and written to Windows.</summary>
	private Control ClickLockTimeSetting()
	{
		var row = Row(T("clickLockTime"), T("clickLockTimeHelp"));
		var editor = EditorHost(row);
		_ = ClickLock.TryGetTimeMs(out var time);
		clickLockTimeInput = new NumericUpDown { Minimum = ClickLock.MinTimeMs, Maximum = ClickLock.MaxTimeMs, Increment = 100, Value = Math.Clamp(time, ClickLock.MinTimeMs, ClickLock.MaxTimeMs), Width = 112, Height = 30, Anchor = AnchorStyles.Left, TextAlign = HorizontalAlignment.Right, ThousandsSeparator = false };
		CenterVertically(editor, clickLockTimeInput);
		clickLockTimeInput.ValueChanged += (_, _) => { if (!loading) { _ = ClickLock.SetTimeMs(decimal.ToInt32(clickLockTimeInput.Value)); } };
		_ = SyncTypedNumericValue(clickLockTimeInput);
		editor.Controls.Add(clickLockTimeInput);
		return row;
	}
	// Windows may change ClickLock outside this window, so refresh the controls from Windows when shown again.
	private void SyncClickLockControls()
	{
		if (clickLockCheck == null || clickLockTimeInput == null) return;

		bool wasLoading = loading;
		loading = true;
		try
		{
			if (ClickLock.TryGetEnabled(out var enabled)) clickLockCheck.Checked = enabled;
			if (ClickLock.TryGetTimeMs(out var time)) clickLockTimeInput.Value = Math.Clamp(time, ClickLock.MinTimeMs, ClickLock.MaxTimeMs);
		}
		finally { loading = wasLoading; }
	}
	protected override void OnActivated(EventArgs e) { base.OnActivated(e); SyncClickLockControls(); }
	/// <summary>Creates a checkbox row bound to a true/false config key. Changes are saved right away, except while loading.</summary>
	private Control Bool(string key, string label, string help, bool fallback = false, bool forceStacked = false, Action? onEnabled = null)
	{
		var row = Row(label, help, forceStacked);
		row.Name = key;
		var editor = EditorHost(row);
		CheckBox check = new() { Text = T("enabled"), AutoSize = true, Checked = ReadBool(key, fallback), Padding = new Padding(6, 0, 0, 0), Anchor = AnchorStyles.Left };
		CenterVertically(editor, check);
		check.CheckedChanged += (_, _) =>
		{
			if (loading) return;

			PointerSettings.SaveSetting(key, check.Checked);
			if (check.Checked) onEnabled?.Invoke();
		};
		EditorHost(row).Controls.Add(check);
		return row;
	}
	/// <summary>
	/// Creates a color button bound to a "#RRGGBB" config key. The dialog rejects a color that another
	/// indicator already uses, so the indicator colors stay distinct.
	/// </summary>
	private Control ColorSetting(string key, string label, string help, Color fallback)
	{
		var row = Row(label, help);
		Button button = new()
		{
			Dock = DockStyle.None,
			Width = 150,
			Height = 42,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			TextAlign = ContentAlignment.MiddleCenter,
			FlatStyle = FlatStyle.Flat,
			Padding = new Padding(10, 4, 10, 4),
		};

		// Text is white on dark colors and black on light ones, so the label stays readable.
		void SetPreview(Color color)
		{
			button.BackColor = color;
			button.ForeColor = color.GetBrightness() < 0.5f ? Color.White : Color.Black;
			button.Text = FormatColor(color);
		}

		var savedValue = ReadString(key, FormatColor(fallback));
		SetPreview(TryParseColor(savedValue, out var current) ? current : fallback);
		button.Click += (_, _) =>
		{
			using ColorDialog dialog = new() { Color = TryParseColor(savedValue, out var selected) ? selected : fallback, FullOpen = true };
			if (dialog.ShowDialog(this) != DialogResult.OK) return;

			foreach (var (Key, LabelKey, Default) in PointerSettings.IndicatorColorSpecs)
			{
				if (Key == key) continue;

				var otherValue = ReadString(Key, FormatColor(Default));
				var other = TryParseColor(otherValue, out var parsed) ? parsed : Default;
				if (other.ToArgb() != dialog.Color.ToArgb()) continue;

				_ = MessageBox.Show(this, string.Format(T("colorConflict"), T(LabelKey)), Text,
					MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

			savedValue = FormatColor(dialog.Color);
			SetPreview(dialog.Color);
			PointerSettings.SaveSetting(key, savedValue);
		};

		var editor = EditorHost(row);
		CenterVertically(editor, button);
		editor.Controls.Add(button);
		return row;
	}

	/// <summary>Formats a color as "#RRGGBB". Alpha is not stored.</summary>
	private static string FormatColor(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

	/// <summary>Parses a strict "#RRGGBB" string. Any other text returns false.</summary>
	private static bool TryParseColor(string value, out Color color)
	{
		color = Color.Empty;
		if (value.Length != 7 || value[0] != '#'
			|| !byte.TryParse(value.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber, null, out var red)
			|| !byte.TryParse(value.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber, null, out var green)
			|| !byte.TryParse(value.AsSpan(5, 2), System.Globalization.NumberStyles.HexNumber, null, out var blue))
		{
			return false;
		}

		color = Color.FromArgb(red, green, blue);
		return true;
	}

	/// <summary>Creates a drop-down row. Stored values are English names such as "Left", and the labels are translated.</summary>
	private Control Choice(string key, string label, string help, string[] values)
	{
		var row = Row(label, help);
		var editor = EditorHost(row);
		ComboBox combo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = editor.ClientSize.Width, Anchor = AnchorStyles.Left | AnchorStyles.Right };
		CenterVertically(editor, combo);
		editor.Resize += (_, _) => combo.Width = Math.Max(150, editor.ClientSize.Width);
		combo.Items.AddRange([.. values.Select(v => v switch
		{
			"Left" => T("left"),
			"Right" => T("right"),
			"LeftRight" => T("leftRight"),
			"LeftMiddleRight" => T("allButtons"),
			"Control" => T("keyControl"),
			"Alt" => T("keyAlt"),
			"Shift" => T("keyShift"),
			"Win" => T("keyWindows"),
			_ => v,
		})]);
		var existing = ReadString(key, values[0]);
		combo.SelectedIndex = Math.Max(0, Array.IndexOf(values, existing));
		combo.SelectedIndexChanged += (_, _) => { if (!loading && combo.SelectedIndex >= 0) { PointerSettings.SaveSetting(key, values[combo.SelectedIndex]); } };
		editor.Controls.Add(combo);
		return row;
	}

	/// <summary>
	/// Creates the OCR language picker. The first entry means the system default, stored as an empty tag.
	/// Languages without an installed recognizer are drawn in red and marked unavailable.
	/// </summary>
	private Control OcrLanguageSetting()
	{
		const string settingKey = "ocrLanguageTag";
		var row = Row(T("ocrLanguage"), T("ocrLanguageHelp"));
		var editor = EditorHost(row);
		ComboBox combo = new()
		{
			DropDownStyle = ComboBoxStyle.DropDownList,
			DrawMode = DrawMode.OwnerDrawFixed,
			Width = editor.ClientSize.Width,
			Anchor = AnchorStyles.Left | AnchorStyles.Right,
		};
		// Owner-draw is used only to color unavailable languages red.
		combo.DrawItem += (_, e) =>
		{
			e.DrawBackground();
			if (e.Index >= 0 && combo.Items[e.Index] is OcrLanguageChoice choice)
			{
				TextRenderer.DrawText(e.Graphics, choice.Name, e.Font, e.Bounds,
					choice.IsInstalled ? e.ForeColor : Color.Red,
					TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
			}
			e.DrawFocusRectangle();
		};
		CenterVertically(editor, combo);
		editor.Resize += (_, _) => combo.Width = Math.Max(150, editor.ClientSize.Width);

		// A saved tag that is missing from the list is added at the end, so the saved value is never lost.
		var currentTag = ReadString(settingKey, "").Trim();
		List<OcrLanguageChoice> choices = [new("", T("ocrDefaultLanguage"))];
		try
		{
			Dictionary<string, string> installed = OcrEngine.AvailableRecognizerLanguages.ToDictionary(
				language => language.LanguageTag,
				language => language.DisplayName,
				StringComparer.OrdinalIgnoreCase);

			// Windows exposes installed OCR languages, not the full language catalog. Seed
			// the selector with the standard Windows OCR language tags, then merge installed
			// recognizers so their localized display names are used where available.
			(string Tag, string Name)[] supportedLanguages =
			[
				("af-ZA", "Afrikaans"), ("am-ET", "አማርኛ"), ("ar-SA", "العربية"), ("az-Latn-AZ", "azərbaycan"),
				("be-BY", "Беларуская"), ("bg-BG", "Български"), ("bn-BD", "বাংলা"), ("bs-Latn-BA", "Bosanski"),
				("ca-ES", "Català"), ("cs-CZ", "Čeština"), ("cy-GB", "Cymraeg"), ("da-DK", "Dansk"),
				("de-DE", "Deutsch"), ("el-GR", "Ελληνικά"), ("en-GB", "English (United Kingdom)"), ("en-US", "English (United States)"),
				("es-ES", "Español"), ("es-MX", "Español (México)"), ("et-EE", "Eesti"), ("eu-ES", "Euskara"),
				("fa-IR", "فارسی"), ("fi-FI", "Suomi"), ("fil-PH", "Filipino"), ("fr-CA", "Français (Canada)"),
				("fr-FR", "Français"), ("ga-IE", "Gaeilge"), ("gd-GB", "Gàidhlig"), ("gl-ES", "Galego"),
				("gu-IN", "ગુજરાતી"), ("he-IL", "עברית"), ("hi-IN", "हिन्दी"), ("hr-HR", "Hrvatski"),
				("hu-HU", "Magyar"), ("hy-AM", "Հայերեն"), ("id-ID", "Bahasa Indonesia"), ("is-IS", "Íslenska"),
				("it-IT", "Italiano"), ("ja-JP", "日本語"), ("ka-GE", "ქართული"), ("kk-KZ", "Қазақша"),
				("km-KH", "ខ្មែរ"), ("kn-IN", "ಕನ್ನಡ"), ("ko-KR", "한국어"), ("lo-LA", "ລາວ"),
				("lt-LT", "Lietuvių"), ("lv-LV", "Latviešu"), ("mi-NZ", "Māori"), ("mk-MK", "Македонски"),
				("ml-IN", "മലയാളം"), ("mn-MN", "Монгол"), ("mr-IN", "मराठी"), ("ms-MY", "Bahasa Melayu"),
				("mt-MT", "Malti"), ("my-MM", "မြန်မာ"), ("nb-NO", "Norsk Bokmål"), ("ne-NP", "नेपाली"),
				("nl-NL", "Nederlands"), ("or-IN", "ଓଡ଼ିଆ"), ("pa-IN", "ਪੰਜਾਬੀ"), ("pl-PL", "Polski"),
				("ps-AF", "پښتو"), ("pt-BR", "Português (Brasil)"), ("pt-PT", "Português"), ("ro-RO", "Română"),
				("ru-RU", "Русский"), ("si-LK", "සිංහල"), ("sk-SK", "Slovenčina"), ("sl-SI", "Slovenščina"),
				("sq-AL", "Shqip"), ("sr-Cyrl-RS", "Српски (ћирилица)"), ("sr-Latn-RS", "Srpski (latinica)"), ("sv-SE", "Svenska"),
				("sw-KE", "Kiswahili"), ("ta-IN", "தமிழ்"), ("te-IN", "తెలుగు"), ("th-TH", "ไทย"),
				("tr-TR", "Türkçe"), ("uk-UA", "Українська"), ("ur-PK", "اردو"), ("uz-Latn-UZ", "O‘zbek"),
				("vi-VN", "Tiếng Việt"), ("zh-CN", "中文 (简体)"), ("zh-TW", "中文 (繁體)")
			];

			choices.AddRange(supportedLanguages
				.Select(language => new OcrLanguageChoice(
					language.Tag,
					$"{installed.GetValueOrDefault(language.Tag, language.Name)} ({language.Tag}){(installed.ContainsKey(language.Tag) ? "" : $" ({T("ocrUnavailable")})")}",
					installed.ContainsKey(language.Tag)))
				.Concat(installed
					.Where(language => supportedLanguages.All(item => !string.Equals(item.Tag, language.Key, StringComparison.OrdinalIgnoreCase)))
					.Select(language => new OcrLanguageChoice(language.Key, $"{language.Value} ({language.Key})", true)))
				.GroupBy(language => language.Tag, StringComparer.OrdinalIgnoreCase)
				.Select(group => group.First())
				.OrderBy(language => language.Name, StringComparer.CurrentCultureIgnoreCase));
		}
		catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
		{
			CursorDiagnosticLogger.Record("settings_controls", "exception", details: new { exceptionType = ex.GetType().FullName, message = ex.Message });
			// If Windows cannot list OCR languages, the picker still shows the default entry and any saved tag.
		}

		if (currentTag.Length > 0 && choices.All(choice => !string.Equals(choice.Tag, currentTag, StringComparison.OrdinalIgnoreCase))) choices.Add(new OcrLanguageChoice(currentTag, $"{currentTag} ({T("ocrUnavailable")})", false));

		combo.Items.AddRange([.. choices]);
		combo.SelectedItem = choices.FirstOrDefault(choice => string.Equals(choice.Tag, currentTag, StringComparison.OrdinalIgnoreCase)) ?? choices[0];
		combo.SelectedIndexChanged += (_, _) => { if (!loading && combo.SelectedItem is OcrLanguageChoice choice) PointerSettings.SaveSetting(settingKey, choice.Tag); };
		editor.Controls.Add(combo);
		return row;
	}
}
