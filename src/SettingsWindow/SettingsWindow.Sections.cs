namespace ThreeFingerDrag;

internal sealed partial class SettingsWindow : Form
{
	/// <summary>
	/// Clears and recreates every section from the current language and config values.
	/// <see cref="loading"/> is set during the rebuild so the new controls do not write their
	/// initial values back to the config.
	/// </summary>
	private void Rebuild()
	{
		CursorDiagnosticLogger.Record("settings_sections", "rebuild", details: new { language = lang, tabbedView = tabbedViewToggle.Checked });
		loading = true;
		language.SelectedItem = language.Items.Cast<LanguageChoice>().FirstOrDefault(choice => choice.Code == lang);
		content.SuspendLayout();
		content.Controls.Clear();
		foreach (var (_, _, group) in sections) group.Dispose();
		ClearTabPages();

		tabbedContent.TabPages.Clear();
		sections.Clear();
		AddSection("global", T("global"),
			Int("dragFallbackPollIntervalMs", T("dragFallbackPollInterval"), T("dragFallbackPollIntervalHelp"), PointerSettings.MinDragFallbackPollIntervalMs, PointerSettings.MaxDragFallbackPollIntervalMs),
			Int("cursorSpeed", T("cursorSpeed"), T("cursorSpeedHelp"), 1, 20),
			Int("cursorPositionPollingIntervalMs", T("polling"), T("pollingHelp"), 1, 1000),
			Int("windowPositionSpeed", T("windowPositionSpeed"), T("windowPositionSpeedHelp"), 1, 100, fallback: 5),
			Int("dragHoldDurationMs", T("dragHoldDuration"), T("dragHoldDurationHelp"), 500, 10000, fallback: 1000),
			KeyCaptureSetting("dragCancelKey", T("cancelKey"), T("cancelKeyHelp"), "Escape"),
			KeyCaptureSetting("forceWindowDragKey", T("forceWindowKey"), T("forceWindowKeyHelp"), "LControl", isShortcut: false),
			KeyCaptureSetting("forceNonWindowDragKey", T("forceNonWindowKey"), T("forceNonWindowKeyHelp"), "RControl", isShortcut: false),
			KeyCaptureSetting("dragModeSwitcherShortcut", T("dragModeSwitcherShortcut"), T("dragModeSwitcherShortcutHelp"), "LControl+LAlt+W", isShortcut: true),
			Bool("enableTouchFlickToDrop", T("flickDrop"), T("flickDropHelp")),
			Int("touchFlickToDropVelocityThreshold", T("flickVelocity"), T("flickVelocityHelp"), 6, 100),
			Int("touchpadNaturalDragReleaseThreshold", T("releaseThreshold"), T("releaseThresholdHelp"), 2, 30),
			Int("touchpadDragDropAnchorHoldMs", T("anchorHold"), T("anchorHoldHelp"), 50, 1000),
			Int("touchpadPostLiftAnchorGraceMs", T("grace"), T("graceHelp"), 50, 1000),
			Bool("bringDraggedWindowToForeground", T("bringFront"), T("bringFrontHelp")),
			Bool("keepWindowWithinScreen", T("keepWithinScreen"), T("keepWithinScreenHelp"), onEnabled: WindowDragController.ReturnOutOfScreenWindowsToCursorScreen),
			Bool("enableDragReleaseSound", T("releaseSound"), T("releaseSoundHelp")),
			Bool("enableForcedModeToggleSound", T("modeSound"), T("modeSoundHelp")));
		AddSection("ocr", T("ocr"),
			Int("ocrHoldDurationMs", T("ocrHold"), T("ocrHoldHelp"), 150, 3000),
			Int("ocrCaptureRadius", T("ocrRadius"), T("ocrRadiusHelp"), 50, 1000),
			Int("ocrMaximumWordGapPercent", T("ocrGap"), T("ocrGapHelp"), PointerSettings.MinOcrMaximumWordGapPercent, PointerSettings.MaxOcrMaximumWordGapPercent),
			OcrLanguageSetting(),
			Bool("ocrCopyToClipboard", T("ocrClipboard"), T("ocrClipboardHelp")),
			Int("ocrActivationDelayMs", T("ocrActivationDelay"), T("ocrActivationDelayHelp"), PointerSettings.MinOcrDropDelayMs, PointerSettings.MaxOcrDropDelayMs),
			Int("ocrClickDelayMs", T("ocrClickDelay"), T("ocrClickDelayHelp"), PointerSettings.MinOcrDropDelayMs, PointerSettings.MaxOcrDropDelayMs),
			Int("ocrClipboardRestoreDelayMs", T("ocrClipboardRestoreDelay"), T("ocrClipboardRestoreDelayHelp"), PointerSettings.MinOcrDropDelayMs, PointerSettings.MaxOcrDropDelayMs));
		AddSection("detection", T("detection"),
			Bool("enableSolidColorDetection", T("solidColorDetection"), T("solidColorDetectionHelp"), true),
			Int("solidColorDetectionRectangleSize", T("colorRect"), T("colorRectHelp"), 1, 51, true),
			Bool("enableUiAutomationDetection", T("uia"), T("uiaHelp"), true),
			Int("uiAutomationClassifyTimeoutMs", T("uiAutomationClassifyTimeout"), T("uiAutomationClassifyTimeoutHelp"), PointerSettings.MinUiAutomationClassifyTimeoutMs, PointerSettings.MaxUiAutomationClassifyTimeoutMs),
			Bool("enableOcrCapture", T("ocrDetection"), T("ocrDetectionHelp"), true),
			Bool("enableCursorShapeDetection", T("cursorShape"), T("cursorShapeHelp"), true),
			Bool("enableEmptyIBeamWindowDrag", T("emptyIBeam"), T("emptyIBeamHelp"), true),
			Int("iBeamTextDetectionAreaSize", T("iBeamArea"), T("iBeamAreaHelp"), 1, 501, true),
			Bool("enableControlClassNameDetection", T("className"), T("classNameHelp"), true),
			Bool("enableHitTestDetection", T("hitTest"), T("hitTestHelp"), true),
			Int("titleBarHitTestTimeoutMs", T("titleBarHitTestTimeout"), T("titleBarHitTestTimeoutHelp"), PointerSettings.MinTitleBarHitTestTimeoutMs, PointerSettings.MaxTitleBarHitTestTimeoutMs),
			Int("hitTestTimeoutMs", T("hitTestTimeout"), T("hitTestTimeoutHelp"), PointerSettings.MinHitTestTimeoutMs, PointerSettings.MaxHitTestTimeoutMs));
		AddSection("indicators", T("indicators"),
			Int("dragModeIndicatorDiameter", T("indicatorDiameter"), T("indicatorDiameterHelp"), 6, 80),
			Int("dragModeIndicatorDisplayMs", T("indicatorTime"), T("indicatorTimeHelp"), 100, 5000),
			null,
			Bool("showTouchHoldForceWindowDragIndicator", T("holdIndicator"), T("holdIndicatorHelp")),
			ColorSetting("touchHoldForceWindowDragIndicatorColor", T("touchHoldColor"), T("touchHoldColorHelp"), PointerSettings.GetDefaultIndicatorColor("touchHoldForceWindowDragIndicatorColor")),
			null,
			Bool("showDragModeIndicator", T("showIndicator"), T("showIndicatorHelp")),
			ColorSetting("windowDragIndicatorColor", T("windowDragColor"), T("windowDragColorHelp"), PointerSettings.GetDefaultIndicatorColor("windowDragIndicatorColor")),
			ColorSetting("windowPositionFallbackIndicatorColor", T("fallbackWindowDragColor"), T("fallbackWindowDragColorHelp"), PointerSettings.GetDefaultIndicatorColor("windowPositionFallbackIndicatorColor")),
			ColorSetting("otherDragIndicatorColor", T("otherDragColor"), T("otherDragColorHelp"), PointerSettings.GetDefaultIndicatorColor("otherDragIndicatorColor")),
			null,
			Bool("showDragModeIndicatorDetails", T("indicatorDetails"), T("indicatorDetailsHelp")),
			ColorSetting("indicatorPanelBackgroundColor", T("panelBackgroundColor"), T("panelBackgroundColorHelp"), PointerSettings.GetDefaultIndicatorColor("indicatorPanelBackgroundColor")),
			ColorSetting("indicatorPanelTextColor", T("panelTextColor"), T("panelTextColorHelp"), PointerSettings.GetDefaultIndicatorColor("indicatorPanelTextColor")),
			null,
			Bool("showSolidColorDetectionRectangle", T("showColorRect"), T("showColorRectHelp")),
			ColorSetting("solidColorUniformBorderColor", T("uniformSampleColor"), T("uniformSampleColorHelp"), PointerSettings.GetDefaultIndicatorColor("solidColorUniformBorderColor")),
			ColorSetting("solidColorMixedBorderColor", T("mixedSampleColor"), T("mixedSampleColorHelp"), PointerSettings.GetDefaultIndicatorColor("solidColorMixedBorderColor")));
		AddSection("touch", T("touch"),
			Int("touchpadDragFingerCount", T("fingerCount"), T("fingerCountHelp"), PointerSettings.MinTouchpadDragFingerCount, PointerSettings.MaxTouchpadDragFingerCount),
			Int("touchscreenDragFingerCount", T("touchscreenFingerCount"), T("touchscreenFingerCountHelp"), PointerSettings.MinTouchscreenDragFingerCount, PointerSettings.MaxTouchscreenDragFingerCount),
			Bool("enableTouchHoldForceWindowDrag", T("touchForce"), T("touchForceHelp"), true),
			Int("touchCancelDragPollIntervalMs", T("touchCancelDragPollInterval"), T("touchCancelDragPollIntervalHelp"), PointerSettings.MinTouchCancelDragPollIntervalMs, PointerSettings.MaxTouchCancelDragPollIntervalMs),
			Int("touchStationaryDragDropHoldMs", T("touchStationaryDragDropHold"), T("touchStationaryDragDropHoldHelp"), PointerSettings.MinTouchStationaryDragDropHoldMs, PointerSettings.MaxTouchStationaryDragDropHoldMs),
			Int("touchDragDropAnchorGraceMs", T("touchDragDropAnchorGrace"), T("touchDragDropAnchorGraceHelp"), PointerSettings.MinTouchDragDropAnchorGraceMs, PointerSettings.MaxTouchDragDropAnchorGraceMs),
			Int("touchFingerLiftGraceMs", T("touchFingerLiftGrace"), T("touchFingerLiftGraceHelp"), PointerSettings.MinTouchFingerLiftGraceMs, PointerSettings.MaxTouchFingerLiftGraceMs),
			Int("touchscreenCancelDragPollIntervalMs", T("touchscreenCancelDragPollInterval"), T("touchscreenCancelDragPollIntervalHelp"), PointerSettings.MinTouchscreenCancelDragPollIntervalMs, PointerSettings.MaxTouchscreenCancelDragPollIntervalMs),
			Int("touchscreenContactLostGraceMs", T("touchscreenContactLostGrace"), T("touchscreenContactLostGraceHelp"), PointerSettings.MinTouchscreenContactLostGraceMs, PointerSettings.MaxTouchscreenContactLostGraceMs),
			Int("touchscreenRecentExternalCursorMovementMs", T("touchscreenRecentExternalCursorMovement"), T("touchscreenRecentExternalCursorMovementHelp"), PointerSettings.MinTouchscreenRecentExternalCursorMovementMs, PointerSettings.MaxTouchscreenRecentExternalCursorMovementMs));
		AddSection("stylus", T("stylus"),
			Bool("enableStylus", T("enableStylus"), T("enableStylusHelp"), false),
			Bool("enableStylusHoldForceWindowDrag", T("stylusHoldForceWindow"), T("stylusHoldForceWindowHelp"), true));
		AddSection("mouse", T("mouse"),
			MouseButtonCaptureSetting("mouseDragTriggerButton", T("dragButtons"), T("dragButtonsHelp")),
			Bool("enableClickHoldForceWindowDrag", T("clickHoldForceWindow"), T("clickHoldForceWindowHelp"), true, forceStacked: true),
			Int("mouseCancelDragPollIntervalMs", T("mouseCancelDragPollInterval"), T("mouseCancelDragPollIntervalHelp"), PointerSettings.MinMouseCancelDragPollIntervalMs, PointerSettings.MaxMouseCancelDragPollIntervalMs));
		clickLockCheck = null;
		clickLockTimeInput = null;
		if (ClickLock.IsSupported()) AddSection("clickLock", T("clickLock"), ClickLockEnabledSetting(), ClickLockTimeSetting());

		AddSection("keyboard", T("keyboard"),
			ModifierCaptureSetting("windowMoveModifierKey", T("moveModifier"), T("moveModifierHelp"), "LAlt"),
			Int("windowMoveStepPixels", T("moveStep"), T("moveStepHelp"), PointerSettings.MinWindowMoveStepPixels, PointerSettings.MaxWindowMoveStepPixels, forceStacked: true),
			Int("forcedDragModePollIntervalMs", T("forcedDragModePollInterval"), T("forcedDragModePollIntervalHelp"), PointerSettings.MinForcedDragModePollIntervalMs, PointerSettings.MaxForcedDragModePollIntervalMs),
			Int("windowMoveArrowSequenceTimeoutMs", T("windowMoveArrowSequenceTimeout"), T("windowMoveArrowSequenceTimeoutHelp"), PointerSettings.MinWindowMoveArrowSequenceTimeoutMs, PointerSettings.MaxWindowMoveArrowSequenceTimeoutMs),
			Int("windowMoveNudgeIntervalMs", T("windowMoveNudgeInterval"), T("windowMoveNudgeIntervalHelp"), PointerSettings.MinWindowMoveNudgeIntervalMs, PointerSettings.MaxWindowMoveNudgeIntervalMs));
		AddSection("windowDragSmoothing", T("windowDragSmoothingSection"),
			Int("windowDragSmoothingFrameMs", T("windowDragSmoothingFrame"), T("windowDragSmoothingFrameHelp"), PointerSettings.MinWindowDragSmoothingFrameMs, PointerSettings.MaxWindowDragSmoothingFrameMs),
			Int("windowDragSmoothingMinTimeConstantMs", T("windowDragSmoothingMinTimeConstant"), T("windowDragSmoothingMinTimeConstantHelp"), PointerSettings.MinWindowDragSmoothingMinTimeConstantMs, PointerSettings.MaxWindowDragSmoothingMinTimeConstantMs),
			Int("windowDragSmoothingMaxTimeConstantMs", T("windowDragSmoothingMaxTimeConstant"), T("windowDragSmoothingMaxTimeConstantHelp"), PointerSettings.MinWindowDragSmoothingMaxTimeConstantMs, PointerSettings.MaxWindowDragSmoothingMaxTimeConstantMs),
			Int("windowDragSmoothingMaxElapsedMs", T("windowDragSmoothingMaxElapsed"), T("windowDragSmoothingMaxElapsedHelp"), PointerSettings.MinWindowDragSmoothingMaxElapsedMs, PointerSettings.MaxWindowDragSmoothingMaxElapsedMs));
		AddSection("advancedSettings", T("advancedSettings"),
			Int("settingsInputDebounceMs", T("settingsInputDebounce"), T("settingsInputDebounceHelp"), PointerSettings.MinSettingsInputDebounceMs, PointerSettings.MaxSettingsInputDebounceMs),
			Int("settingsStatIntervalMs", T("settingsStatInterval"), T("settingsStatIntervalHelp"), PointerSettings.MinSettingsStatIntervalMs, PointerSettings.MaxSettingsStatIntervalMs),
			Int("notificationSafetyTimeoutMs", T("notificationSafetyTimeout"), T("notificationSafetyTimeoutHelp"), PointerSettings.MinNotificationSafetyTimeoutMs, PointerSettings.MaxNotificationSafetyTimeoutMs),
			Int("notificationBalloonTimeoutMs", T("notificationBalloonTimeout"), T("notificationBalloonTimeoutHelp"), PointerSettings.MinNotificationBalloonTimeoutMs, PointerSettings.MaxNotificationBalloonTimeoutMs),
			Int("settingsEventPollIntervalMs", T("settingsEventPollInterval"), T("settingsEventPollIntervalHelp"), PointerSettings.MinSettingsEventPollIntervalMs, PointerSettings.MaxSettingsEventPollIntervalMs),
			Int("settingsCaptureRefreshIntervalMs", T("settingsCaptureRefreshInterval"), T("settingsCaptureRefreshIntervalHelp"), PointerSettings.MinSettingsCaptureRefreshIntervalMs, PointerSettings.MaxSettingsCaptureRefreshIntervalMs));
		AddSection("logging", T("logging"),
			Bool("enableDiagnosticLog", T("diagnosticLog"), T("diagnosticLogHelp")),
			Bool("enableSplitDiagnosticLogs", T("splitDiagnosticLog"), T("splitDiagnosticLogHelp")));
		ApplySavedSectionOrder();
		content.ResumeLayout(true);
		loading = false;
		LayoutSections();
	}

	/// <summary>
	/// Adds a titled group box with its controls to the settings layout. A null control adds a spacer row between sub-groups.
	/// Rows for advanced-only settings are left out while advanced mode is off. A section with no rows left is not added.
	/// </summary>
	private void AddSection(string id, string title, params Control?[] allControls)
	{
		bool advancedMode = advancedViewToggle.Checked;
		List<Control?> visibleControls = [];
		foreach (var control in allControls)
		{
			if (control is not null && !advancedMode && control.Name is { Length: > 0 } key && AdvancedOnlySettingKeys.Contains(key))
			{ control.Dispose(); continue; }
			// Spacers that would lead, repeat, or trail after the hidden rows are removed.
			if (control is null && (visibleControls.Count == 0 || visibleControls[^1] is null)) continue;

			visibleControls.Add(control);
		}
		while (visibleControls.Count > 0 && visibleControls[^1] is null) visibleControls.RemoveAt(visibleControls.Count - 1);
		if (visibleControls.Count == 0) return;

		Control?[] controls = [.. visibleControls];
		GroupBox group = new() { Text = title, Width = 600, Height = controls.Length * 84 + 46, AutoSize = false, Padding = new Padding(12), Margin = new Padding(0, 0, 0, 18) };
		TableLayoutPanel stack = new() { Dock = DockStyle.Fill, AutoSize = false, ColumnCount = 1, RowCount = controls.Length, Padding = Padding.Empty, Margin = Padding.Empty };
		_ = stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
		for (int i = 0; i < controls.Length; i++)
		{
			if (controls[i] is null) { _ = stack.RowStyles.Add(new RowStyle(SizeType.Absolute, SpacerRowHeight)); continue; }
			_ = stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
			Control control = controls[i]!;
			control.Dock = DockStyle.Fill;
			stack.Controls.Add(control, 0, i);
		}
		group.Controls.Add(stack);
		sections.Add((id, title, group));
		group.Resize += (_, _) => FitSectionGroup(group);
		FitSectionGroup(group);
	}

	/// <summary>Resizes a group box so it fits all of its child controls.</summary>
	private void FitSectionGroup(GroupBox group)
	{
		if (group.Controls.OfType<TableLayoutPanel>().FirstOrDefault() is not { } stack) return;
		int stackWidth = Math.Max(1, group.Width - group.Padding.Horizontal - 12);
		int totalHeight = 0;
		for (int i = 0; i < stack.RowCount; i++)
		{
			if (stack.GetControlFromPosition(0, i) is not TableLayoutPanel row)
			{
				// Spacer rows keep their fixed height. A search collapses them to zero.
				totalHeight += (int)stack.RowStyles[i].Height;
				continue;
			}
			if (filteredOut.Contains(row))
			{
				stack.RowStyles[i].Height = 0;
				continue;
			}

			var labels = row.Controls.OfType<Panel>().FirstOrDefault(panel => panel.Controls.OfType<Label>().Any(label => label.Dock == DockStyle.Fill));
			var editor = row.Controls.OfType<Panel>().FirstOrDefault(panel => panel != labels);
			var description = labels?.Controls.OfType<Label>().FirstOrDefault(label => label.Dock == DockStyle.Fill);
			var title = labels?.Controls.OfType<Label>().FirstOrDefault(label => label.Dock == DockStyle.Top);
			if (labels is null || editor is null || description is null || title is null)
			{
				// Keep a visible row from staying collapsed to zero height.
				stack.RowStyles[i].SizeType = SizeType.Absolute;
				stack.RowStyles[i].Height = Math.Max(64, row.PreferredSize.Height);
				totalHeight += (int)stack.RowStyles[i].Height;
				continue;
			}

			bool stacked = stackWidth < 760;
			if (row.ColumnCount != (stacked ? 1 : 2))
			{
				row.SuspendLayout();
				row.ColumnCount = stacked ? 1 : 2;
				row.RowCount = stacked ? 2 : 1;
				row.ColumnStyles.Clear();
				row.RowStyles.Clear();
				_ = row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, stacked ? 100 : 42));
				if (!stacked) _ = row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));

				row.SetCellPosition(labels, new TableLayoutPanelCellPosition(0, 0));
				row.SetCellPosition(editor, stacked ? new TableLayoutPanelCellPosition(0, 1) : new TableLayoutPanelCellPosition(1, 0));
				row.ResumeLayout(true);
			}
			int labelWidth = stacked ? Math.Max(80, stackWidth - 12) : Math.Max(80, (int)(stackWidth * 0.42) - 8);
			int titleHeight = TextRenderer.MeasureText(title.Text, title.Font, new Size(labelWidth, 10000), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding).Height + 4;
			title.Height = Math.Max(25, titleHeight);
			int helpHeight = TextRenderer.MeasureText(description.Text, description.Font, new Size(labelWidth, 10000), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding).Height;
			int labelsHeight = title.Height + helpHeight + 6;
			int editorHeight = editor.Controls.Cast<Control>().Select(control => control.Height).DefaultIfEmpty(32).Max();
			editorHeight = Math.Max(32, editorHeight + 4);
			labels.Height = labelsHeight;
			editor.Height = editorHeight;
			int rowHeight = stacked ? labelsHeight + editorHeight + row.Margin.Vertical + 8 : Math.Max(labelsHeight, editorHeight) + row.Margin.Vertical + 8;
			rowHeight = Math.Max(64, rowHeight);
			row.RowStyles.Clear();
			if (stacked)
			{
				_ = row.RowStyles.Add(new RowStyle(SizeType.Absolute, labelsHeight));
				_ = row.RowStyles.Add(new RowStyle(SizeType.Absolute, editorHeight));
			} else
			{
				_ = row.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));
			}

			stack.RowStyles[i].SizeType = SizeType.Absolute;
			stack.RowStyles[i].Height = rowHeight;
			totalHeight += rowHeight;
		}
		int desiredHeight = totalHeight + 46;
		if (group.Height != desiredHeight) group.Height = desiredHeight;
	}
	/// <summary>Lets the user drag tabs to reorder sections. The new order is saved to the config.</summary>
	private void EnableTabReordering()
	{
		// A drag starts only after the pointer moves past half the system drag size, so plain tab clicks still work.
		tabbedContent.AllowDrop = true;
		tabbedContent.SelectedIndexChanged += (_, _) =>
		{
			CursorDiagnosticLogger.Record("settings_sections", "section_switch", details: new { section = tabbedContent.SelectedTab?.Tag as string });
			RequestResponsiveLayout();
		};
		tabbedContent.MouseDown += (_, e) =>
		{
			dragTabIndex = e.Button == MouseButtons.Left ? Enumerable.Range(0, tabbedContent.TabPages.Count).FirstOrDefault(i => tabbedContent.GetTabRect(i).Contains(e.Location), -1) : -1;
			dragStart = e.Location;
		};
		tabbedContent.MouseMove += (_, e) =>
		{
			if (dragTabIndex < 0 || e.Button != MouseButtons.Left || tabbedContent.TabPages.Count == 0) return;
			if (Math.Abs(e.X - dragStart.X) < SystemInformation.DragSize.Width / 2 && Math.Abs(e.Y - dragStart.Y) < SystemInformation.DragSize.Height / 2) return;

			var page = tabbedContent.TabPages[dragTabIndex];
			_ = tabbedContent.DoDragDrop(page, DragDropEffects.Move);
			dragTabIndex = -1;
		};
		tabbedContent.MouseUp += (_, _) => dragTabIndex = -1;
		tabbedContent.DragEnter += (_, e) => e.Effect = e.Data?.GetData(typeof(TabPage)) is TabPage ? DragDropEffects.Move : DragDropEffects.None;
		tabbedContent.DragOver += (_, e) => e.Effect = e.Data?.GetData(typeof(TabPage)) is TabPage ? DragDropEffects.Move : DragDropEffects.None;
		tabbedContent.DragDrop += (_, e) =>
		{
			if (e.Data?.GetData(typeof(TabPage)) is not TabPage source) return;

			var sourceIndex = tabbedContent.TabPages.IndexOf(source);
			if (sourceIndex < 0) return;

			var point = tabbedContent.PointToClient(new Point(e.X, e.Y));
			var targetIndex = Enumerable.Range(0, tabbedContent.TabPages.Count).FirstOrDefault(i => tabbedContent.GetTabRect(i).Contains(point), -1);
			if (targetIndex < 0) return;

			var insertionIndex = targetIndex + (point.X > tabbedContent.GetTabRect(targetIndex).Left + tabbedContent.GetTabRect(targetIndex).Width / 2 ? 1 : 0);
			if (insertionIndex > sourceIndex) insertionIndex--;

			insertionIndex = Math.Clamp(insertionIndex, 0, tabbedContent.TabPages.Count - 1);
			if (insertionIndex == sourceIndex) return;

			var selected = tabbedContent.SelectedTab;
			tabbedContent.TabPages.Remove(source);
			tabbedContent.TabPages.Insert(insertionIndex, source);
			if (selected is not null) tabbedContent.SelectedTab = selected;

			SaveSectionOrder([.. tabbedContent.TabPages.Cast<TabPage>().Select(page => (string)page.Tag!)]);
			CursorDiagnosticLogger.Record("settings_sections", "tab_reordered", details: new { from = sourceIndex, to = insertionIndex, tab = (string)source.Tag! });
		};
	}
	/// <summary>
	/// Sorts <see cref="sections"/> by the saved order. Unknown ids are ignored, and sections missing from
	/// the saved list keep their original relative order at the end.
	/// </summary>
	private void ApplySavedSectionOrder()
	{
		// Prefer the saved settingsSectionOrder array, since it also lists sections without a config object (such as clickLock).
		// Fall back to the key order inside the config object, then the default.
		var savedOrder = ReadStringArray("settingsSectionOrder");
		if (savedOrder.Length == 0) savedOrder = ReadConfiguredSectionOrder();
		if (savedOrder.Length == 0) savedOrder = ["global", "logging", "detection", "indicators", "ocr", "touch", "mouse", "stylus", "clickLock", "keyboard"];

		Dictionary<string, int> positions = savedOrder.Distinct(StringComparer.Ordinal).Select((id, index) => (id, index)).ToDictionary(item => item.id, item => item.index, StringComparer.Ordinal);
		var original = sections.Select((section, index) => (section, index)).ToArray();
		List<string> normalizedOrder = [.. savedOrder.Where(positions.ContainsKey).Distinct(StringComparer.Ordinal)];
		normalizedOrder.AddRange(original.Where(item => !normalizedOrder.Contains(item.section.Id, StringComparer.Ordinal)).Select(item => item.section.Id));
		Dictionary<string, int> normalizedPositions = normalizedOrder.Select((id, index) => (id, index)).ToDictionary(item => item.id, item => item.index, StringComparer.Ordinal);
		sections.Clear();
		sections.AddRange(original.OrderBy(item => normalizedPositions.GetValueOrDefault(item.section.Id, int.MaxValue)).ThenBy(item => item.index).Select(item => item.section));
	}
	/// <summary>Applies a new section order to the list and saves it to the config.</summary>
	private void SaveSectionOrder(string[] order)
	{
		// Ids that are not in the current section list are skipped.
		Dictionary<string, (string Id, string Title, GroupBox Group)> byId = sections.ToDictionary(section => section.Id, StringComparer.Ordinal);
		sections.Clear();
		sections.AddRange(order.Where(byId.ContainsKey).Select(id => byId[id]));
		PointerSettings.SaveSetting("settingsSectionOrder", order);
	}
	/// <summary>
	/// Shows either the tab view or the scrollable column view, depending on the toggle.
	/// The tab view keeps one group box per tab and only resizes it to the tab width.
	/// The column view places each group in the shortest column.
	/// </summary>
	private void LayoutSections()
	{
		// Rebuilding the layout can move keyboard focus away from the search box, so it is restored afterwards.
		bool searchFocused = searchHadFocus || searchBox.Focused;
		searchHadFocus = false;
		int caret = searchBox.SelectionStart;
		LayoutSectionsCore();
		if (searchFocused && !searchBox.Focused)
		{
			_ = searchBox.Focus();
			searchBox.SelectionStart = Math.Min(caret, searchBox.TextLength);
		}
	}

	private void LayoutSectionsCore()
	{
		if (sections.Count == 0 || content.ClientSize.Width <= 0) return;

		ApplySearchFilter();
		// A search always uses the column view, so the tab view is only used when the search box is empty.
		if (tabbedViewToggle.Checked && !searchWasActive)
		{
			content.Visible = false;
			tabbedContent.Visible = true;
			if (tabbedContent.TabPages.Count != sections.Count)
			{
				ClearTabPages();

				foreach (var (id, title, group) in sections)
				{
					TabPage page = new(title) { AutoScroll = true, Padding = new Padding(14), Tag = id };
					group.Location = Point.Empty;
					group.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
					page.Controls.Add(group);
					page.Resize += (_, _) => group.Width = Math.Max(360, page.ClientSize.Width - page.Padding.Horizontal - 4);
					tabbedContent.TabPages.Add(page);
					group.Width = Math.Max(360, page.ClientSize.Width - page.Padding.Horizontal - 4);
				}
			}
			foreach (TabPage page in tabbedContent.TabPages)
			{
				if (page.Controls.Count > 0 && page.Controls[0] is GroupBox group) group.Width = Math.Max(360, page.ClientSize.Width - page.Padding.Horizontal - 4);
			}
			if (restoreTabId is { } tabId && tabbedContent.TabPages.Cast<TabPage>().FirstOrDefault(page => (string?)page.Tag == tabId) is { } restored)
			{ tabbedContent.SelectedTab = restored; }
			restoreTabId = null;

			return;
		}
		content.Visible = true;
		tabbedContent.Visible = false;
		content.SuspendLayout();
		content.Controls.Clear();
		int available = Math.Max(440, content.ClientSize.Width - content.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
		int gap = 18;
		int columns = Math.Max(1, Math.Min(3, (available + gap) / (440 + gap)));
		int width = Math.Max(420, (available - (columns - 1) * gap) / columns);
		var columnGroups = Enumerable.Range(0, columns).Select(_ => new List<GroupBox>()).ToArray();
		var columnHeights = new int[columns];
		foreach (var (_, _, group) in sections.Where(section => !filteredOut.Contains(section.Group)))
		{
			group.Width = width;
			int target = Array.IndexOf(columnHeights, columnHeights.Min());
			columnGroups[target].Add(group);
			columnHeights[target] += group.Height + group.Margin.Vertical;
		}
		for (int column = 0; column < columns; column++)
		{
			int y = 0;
			foreach (var group in columnGroups[column])
			{
				group.Location = new Point(0, y);
				y += group.Height + group.Margin.Vertical;
			}
			Panel panel = new() { Location = new Point(column * (width + gap), 0), Size = new Size(width, Math.Max(1, y)), Margin = new Padding(0, 0, column == columns - 1 ? 0 : gap, 0) };
			foreach (var group in columnGroups[column]) panel.Controls.Add(group);

			content.Controls.Add(panel);
		}
		ClearTabPages();
		content.ResumeLayout(true);
	}

	/// <summary>Removes the tab pages without disposing the section groups, which are still needed for the column view.</summary>
	private void ClearTabPages()
	{
		foreach (TabPage page in tabbedContent.TabPages.Cast<TabPage>().ToArray())
		{
			page.Controls.Clear();
			page.Dispose();
		}
		tabbedContent.TabPages.Clear();
	}
	/// <summary>Re-runs the responsive layout after a resize.</summary>
	protected override void OnResize(EventArgs e) { base.OnResize(e); RequestResponsiveLayout(); }
	/// <summary>Re-runs the responsive layout after a DPI change.</summary>
	protected override void OnDpiChanged(DpiChangedEventArgs e) { base.OnDpiChanged(e); RequestResponsiveLayout(); }
	/// <summary>
	/// Queues one layout pass on the UI thread, so a burst of resize events runs the layout once.
	/// Before the handle exists, the layout runs right away because BeginInvoke needs a handle.
	/// </summary>
	private void RequestResponsiveLayout()
	{
		if (IsDisposed) return;

		if (!IsHandleCreated)
		{ LayoutSections(); return; }
		if (responsiveLayoutQueued) return;

		responsiveLayoutQueued = true;
		_ = BeginInvoke((MethodInvoker)(() =>
		{
			responsiveLayoutQueued = false;
			if (IsDisposed) return;

			LayoutHeader();
			PerformLayout();
			foreach (var (_, _, group) in sections) group.PerformLayout();

			LayoutSections();
		}));
	}
}
