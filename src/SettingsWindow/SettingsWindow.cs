namespace ThreeFingerDrag;

/// <summary>
/// Settings window for ThreeFingerDrag. Every control writes its value straight to the config file
/// through <see cref="PointerSettings"/>, so there is no Save button. The window is built from
/// the setting definitions below and can show them either as tabs or as a scrollable multi-column page.
/// </summary>
internal sealed partial class SettingsWindow : Form
{
	/// <summary>Scrollable page that shows setting sections as wrapped columns. Used when tabbed view is off.</summary>
	private readonly FlowLayoutPanel content = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoScroll = true, Padding = new Padding(18) };
	/// <summary>Tab view of the sections. Shown instead of <see cref="content"/> when tabbed view is on.</summary>
	private readonly TabControl tabbedContent = new() { Dock = DockStyle.Fill, Visible = false, Padding = new Point(10, 4) };
	/// <summary>
	/// Every section in display order. Id is the stable key used in the saved section order,
	/// Title is the localized heading, and Group is the box that holds the section's rows.
	/// </summary>
	private readonly List<(string Id, string Title, GroupBox Group)> sections = [];
	/// <summary>Top banner with the app name, subtitle, view toggles, and language picker.</summary>
	private readonly Panel header = new() { Dock = DockStyle.Top, Height = 128 };
	/// <summary>Stacks the title, subtitle, and toggles inside the header on the left side.</summary>
	private readonly FlowLayoutPanel titleStack = new() { FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = Padding.Empty };
	private readonly CheckBox tabbedViewToggle = new() { AutoSize = true, Margin = Padding.Empty };
	/// <summary>When off, settings marked as advanced (timing, tuning, and timeout values) are hidden.</summary>
	private readonly CheckBox advancedViewToggle = new() { AutoSize = true, Margin = Padding.Empty };
	private readonly ComboBox language = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
	/// <summary>Language code to (string key to text). Loaded once from the Languages folder at startup.</summary>
	private readonly Dictionary<string, Dictionary<string, string>> translations = LoadTranslations();
	private readonly Label subtitle = new() { AutoSize = true, Location = new Point(20, 48), ForeColor = Color.DimGray };
	private readonly Label languageLabel = new() { AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Right, TextAlign = ContentAlignment.MiddleRight };
	/// <summary>
	/// True while controls are being filled from the config. Change handlers check this flag so that
	/// programmatic updates do not save values back to the file.
	/// </summary>
	private bool loading;
	/// <summary>True while a responsive layout pass is queued on the UI thread. Prevents duplicate passes.</summary>
	private bool responsiveLayoutQueued;
	/// <summary>Index of the tab that the user pressed the left mouse button on, or -1 when no tab is being dragged.</summary>
	private int dragTabIndex = -1;
	/// <summary>Mouse position where the tab drag started. Used to ignore small movements.</summary>
	private Point dragStart;
	/// <summary>Code of the language in use, such as "en". Set from the config or from Windows at startup.</summary>
	private string lang = ResolveInitialLanguage();

	/// <summary>Creates the window, wires the header controls, and builds the setting sections.</summary>
	public SettingsWindow()
	{
		// Controls scale with the DPI of the monitor the window opens on.
		AutoScaleMode = AutoScaleMode.Dpi;
		Text = "ThreeFingerDrag — Settings";
		Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
		// Open at half the work area of the screen under the cursor, so the window fits on any monitor.
		var workArea = Screen.FromPoint(Cursor.Position).WorkingArea;
		Size = new Size(workArea.Width / 2, workArea.Height / 2);
		MinimumSize = new Size(Math.Min(600, Size.Width), Math.Min(500, Size.Height));
		StartPosition = FormStartPosition.CenterScreen;
		Label title = new() { Text = "ThreeFingerDrag", Font = new Font("Segoe UI", 18, FontStyle.Bold), AutoSize = true, Margin = Padding.Empty };
		subtitle.Text = T("settingsApply");
		subtitle.Margin = new Padding(0, 2, 0, 0);
		tabbedViewToggle.Text = T("tabbedView");
		tabbedViewToggle.Checked = ReadBool("useTabbedSettings", true);
		// Switching the view only changes layout. The saved value is written here, then the layout is refreshed.
		tabbedViewToggle.CheckedChanged += (_, _) =>
		{
			if (loading) return;

			PointerSettings.SaveSetting("useTabbedSettings", tabbedViewToggle.Checked);
			CursorDiagnosticLogger.Record("settings_window", "tabbed_view_changed", details: new { tabbed = tabbedViewToggle.Checked });
			RequestResponsiveLayout();
		};
		advancedViewToggle.Text = T("advancedView");
		advancedViewToggle.Checked = ReadBool("useAdvancedSettings", false);
		// Advanced settings are added or removed by rebuilding the sections, so the toggle does a full rebuild.
		advancedViewToggle.CheckedChanged += (_, _) =>
		{
			if (loading) return;

			PointerSettings.SaveSetting("useAdvancedSettings", advancedViewToggle.Checked);
			CursorDiagnosticLogger.Record("settings_window", "advanced_view_changed", details: new { advanced = advancedViewToggle.Checked });
			Rebuild();
			RequestResponsiveLayout();
		};
		titleStack.Controls.AddRange([title, subtitle, tabbedViewToggle, advancedViewToggle]);
		languageLabel.Text = T("languageLabel");
		languageLabel.TextAlign = ContentAlignment.MiddleLeft;
		// Languages are listed by their own display name (languageName), sorted by code.
		var choices = translations.Keys.OrderBy(code => code, StringComparer.OrdinalIgnoreCase).Select(code => new LanguageChoice(code, translations[code].GetValueOrDefault("languageName", code))).ToArray();
		language.Items.AddRange(choices);
		// Fall back to English when the saved or system language has no matching file.
		language.SelectedItem = choices.FirstOrDefault(choice => string.Equals(choice.Code, lang, StringComparison.OrdinalIgnoreCase)) ?? choices.First(choice => choice.Code == "en");
		lang = ((LanguageChoice)language.SelectedItem).Code;
		// Changing the language saves it, then re-translates the header and rebuilds every section.
		language.SelectedIndexChanged += (_, _) => { if (loading || language.SelectedItem is not LanguageChoice choice) { return; } lang = choice.Code; CursorDiagnosticLogger.Record("settings_window", "language_changed", details: new { language = lang }); PointerSettings.SaveSetting("language", lang); subtitle.Text = T("settingsApply"); languageLabel.Text = T("languageLabel"); tabbedViewToggle.Text = T("tabbedView"); advancedViewToggle.Text = T("advancedView"); searchLabel.Text = T("searchSettings"); LayoutHeader(); Rebuild(); RequestResponsiveLayout(); };
		searchLabel.Text = T("searchSettings");
		searchBox.TextChanged += (_, _) => OnSearchTextChanged();
		header.Controls.AddRange([titleStack, languageLabel, language, searchLabel, searchBox]);
		header.Resize += (_, _) => LayoutHeader();
		// The fill panels are added first and the top-docked header last, so the header
		// takes the top edge before the content panels use the remaining space.
		Controls.Add(content);
		Controls.Add(tabbedContent);
		Controls.Add(header);
		LayoutHeader();
		EnableTabReordering();
		Rebuild();
	}

	/// <summary>One entry in the language picker. The display text is the language's own name.</summary>
	private sealed record LanguageChoice(string Code, string Name) { public override string ToString() => Name; }
	/// <summary>One entry in the OCR language picker. IsInstalled is false for languages that Windows has not installed.</summary>
	private sealed record OcrLanguageChoice(string Tag, string Name, bool IsInstalled = true) { public override string ToString() => Name; }

	/// <summary>
	/// Places the header controls. Below 900 px the language picker drops under the title on its own row.
	/// At 900 px and above the title and picker sit side by side.
	/// </summary>
	private void LayoutHeader()
	{
		int width = header.ClientSize.Width;
		if (width <= 0) return;

		bool compact = width < 900;
		// Compact header: the header is taller, and the picker spans the full width under the title.
		if (compact)
		{
			if (header.Height != 240) header.Height = 240;

			titleStack.SetBounds(18, 10, Math.Max(120, width - 36), 106);
			languageLabel.SetBounds(18, 120, Math.Max(80, width - 36), 20);
			language.SetBounds(18, 142, Math.Max(120, width - 36), 28);
			language.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			searchLabel.SetBounds(18, 178, Math.Max(80, width - 36), 20);
			searchLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			searchBox.SetBounds(18, 200, Math.Max(120, width - 36), 28);
			searchBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		} else
		{
			if (header.Height != 128) header.Height = 128;
			// The title gets about 58% of the width. The picker group (label, gap, combo) is right-aligned.
			int titleWidth = Math.Max(200, (int)(width * 0.58) - 24);
			titleStack.SetBounds(18, 10, titleWidth, 106);
			int comboWidth = Math.Min(190, Math.Max(140, width / 4));
			int labelWidth = TextRenderer.MeasureText(languageLabel.Text, languageLabel.Font).Width;
			// Measure the label text so the picker group ends 18 px from the right edge, whatever the language.
			int left = width - 18 - comboWidth - labelWidth - 16;
			languageLabel.SetBounds(left, 34, labelWidth, 24);
			language.SetBounds(left + labelWidth + 12, 32, comboWidth, 28);
			language.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			searchLabel.SetBounds(left, 34 + 40, labelWidth + 12 + comboWidth, 20);
			searchLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			searchBox.SetBounds(left, 98, labelWidth + 12 + comboWidth, 28);
			searchBox.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		}
	}

	/// <summary>
	/// Picks the starting language. A language saved in the config wins. Otherwise the Windows UI
	/// language is used, reduced to its base code (for example "pt-BR" becomes "pt").
	/// </summary>
	private static string ResolveInitialLanguage()
	{
		var configuredLanguage = ReadString("language", "").Trim();
		if (configuredLanguage.Length > 0) return configuredLanguage;

		// Some systems report an empty UI culture. The regular culture is used in that case.
		var windowsCulture = System.Globalization.CultureInfo.CurrentUICulture;
		if (string.IsNullOrWhiteSpace(windowsCulture.Name)) windowsCulture = System.Globalization.CultureInfo.CurrentCulture;

		var windowsLanguage = windowsCulture.Name;
		return string.IsNullOrWhiteSpace(windowsLanguage) ? "en" : windowsLanguage.Split('-', '_')[0];
	}

	/// <summary>
	/// Hides the window when the user clicks its close button, so it can be shown again quickly
	/// by starting the program a second time. Other close reasons (such as application exit) close the form normally.
	/// </summary>
	protected override void OnFormClosing(FormClosingEventArgs e)
	{
		if (e.CloseReason == CloseReason.UserClosing)
		{ CursorDiagnosticLogger.Record("settings_window", "close", details: new { reason = e.CloseReason.ToString() }); e.Cancel = true; Hide(); return; }
		base.OnFormClosing(e);
	}

	/// <summary>Restores, shows, and brings the window to the front. Called when the user opens settings.</summary>
	public void ShowAndActivate()
	{
		CursorDiagnosticLogger.Record("settings_window", "open", details: new { visible = Visible, minimized = WindowState == FormWindowState.Minimized });
		if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
		if (!Visible) Show();

		ShowInTaskbar = true;
		BringToFront();
		Activate();
		// A launch from Explorer or a shortcut can leave Windows' foreground lock in place.
		// Temporarily making the form topmost requests an actual foreground raise; revert it
		// immediately so the settings window does not stay above other applications.
		TopMost = true;
		TopMost = false;
		BringToFront();
		Activate();
	}
	/// <summary>Runs the first responsive layout and moves focus to the language picker.</summary>
	protected override void OnShown(EventArgs e) { base.OnShown(e); RequestResponsiveLayout(); _ = language.Focus(); }
}
