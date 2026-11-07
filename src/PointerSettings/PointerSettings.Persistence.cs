using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using static ThreeFingerDrag.NativeMethods;

namespace ThreeFingerDrag;

internal static partial class PointerSettings
{
	/// <summary>Reads the allowed min/max for a numeric setting from the config file, falling back to the given defaults.</summary>
	internal static (int Minimum, int Maximum) GetNumericValueBounds(string key, int defaultMinimum, int defaultMaximum)
	{
		try
		{
			var path = AppDataPaths.ConfigurationFilePath;
			using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
			return GetNumericValueBounds(document.RootElement, key, defaultMinimum, defaultMaximum);
		}
		catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
		{
			CursorDiagnosticLogger.Record("config", "exception", details: new { key, exceptionType = ex.GetType().FullName, message = ex.Message });
			return (defaultMinimum, defaultMaximum);
		}
	}

	/// <summary>Returns the UI language code, "en" or "id". Any other value falls back to "id".</summary>
	public static string GetLanguage() => GetConfiguredStringFromFile("language", "id") is "en" ? "en" : "id";

	/// <summary>Restores editable files in the user data directory without replacing user values.</summary>
	public static void Initialize()
	{
		EnsureConfigurationFileComplete();
		EnsureDefaultLanguageFilesExist();
		ApplyCursorSpeed();
		CursorDiagnosticLogger.Record("config", "initialized");
	}

	/// <summary>Applies the configured cursor speed to the whole system. This changes the Windows pointer speed, not only this app.</summary>
	public static void ApplyCursorSpeed() => SystemParametersInfo(SpiSetMouseSpeed, 0, GetConfiguredCursorSpeed(), 0);

	/// <summary>Finds a setting either at the top level or inside one of the known sections.</summary>
	private static JsonNode? FindSettingNode(JsonObject root, string name)
	{
		if (root[name] is JsonNode direct) return direct;
		foreach (var sectionName in SettingsSections) if (root[sectionName] is JsonObject section && section[name] is JsonNode nested) return nested;
		return null;
	}

	// Guards pendingNumericCorrections, which is written from the settings UI and read on drag start.
	private static readonly object pendingNumericLock = new();
	// Corrected values waiting to be written to the config file. Keyed by setting name.
	private static readonly Dictionary<string, int> pendingNumericCorrections = [];

	/// <summary>Keeps the entered value in the configuration until the next drag, which writes the corrected value.</summary>
	public static void SaveOutOfRangeNumericValue(string name, int enteredValue, int correctedValue)
	{
		SaveSetting(name, enteredValue);
		lock (pendingNumericLock) pendingNumericCorrections[name] = correctedValue;
	}

	/// <summary>Writes corrected values queued by <see cref="SaveOutOfRangeNumericValue"/>; called when a drag starts.</summary>
	public static void ApplyPendingNumericCorrections()
	{
		KeyValuePair<string, int>[] pending;
		lock (pendingNumericLock)
		{
			if (pendingNumericCorrections.Count == 0) return;

			pending = [.. pendingNumericCorrections];
			pendingNumericCorrections.Clear();
		}

		CursorDiagnosticLogger.Record("config", "pending_corrections_apply", details: new { count = pending.Length });
		foreach (var (name, value) in pending) SaveSetting(name, value);
	}

	/// <summary>
	/// Writes one setting to the config file. Also migrates legacy top-level keys into their sections,
	/// removes old shortcut keys, and rewrites the file atomically (temp file, then move).
	/// The settings cache is cleared so the next read sees the new value.
	/// </summary>
	public static void SaveSetting(string name, object value)
	{
		var path = AppDataPaths.ConfigurationFilePath;
		JsonObject root = [];
		try { if (File.Exists(path)) root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? [];}
		// A corrupt or unreadable file is replaced by an empty object instead of blocking the save.
		catch (JsonException ex)
		{
			CursorDiagnosticLogger.Record("config", "exception", details: new { name, exceptionType = ex.GetType().FullName, message = ex.Message, action = "replaced_with_empty_object" });
		}
		var previousValueJson = FindSettingNode(root, name)?.ToJsonString();
		// The old single shortcut key is replaced by dragModeSwitcherShortcut, so drop the stale copies.
		if (name == "dragModeSwitcherShortcut")
		{
			foreach (var sectionName in SettingsSections)
			{
				if (root[sectionName] is JsonObject sectionObject)
				{
					_ = sectionObject.Remove("forceWindowDragShortcut");
					_ = sectionObject.Remove("forceNonWindowDragShortcut");
				}
			}
			_ = root.Remove("forceWindowDragShortcut");
			_ = root.Remove("forceNonWindowDragShortcut");
		}
		// Move any top-level key that now belongs in a section into that section.
		foreach (var pair in root.ToArray())
		{
			var legacySection = GetSettingSection(pair.Key);
			if (legacySection is null || pair.Value is null) continue;

			var legacyValue = pair.Value.DeepClone();
			_ = root.Remove(pair.Key);
			JsonObject? legacyGroup = root[legacySection] as JsonObject;
			if (legacyGroup is null) root[legacySection] = legacyGroup = [];

			legacyGroup[pair.Key] = legacyValue;
		}
		// Keep the same setting from appearing in two sections at once.
		var section = GetSettingSection(name);
		{
			foreach (var sectionName in SettingsSections) if (sectionName != section && root[sectionName] is JsonObject otherSection) _ = otherSection.Remove(name);
		}

		if (section is null)
		{
			root[name] = JsonSerializer.SerializeToNode(value);
		} else
		{
			var sectionNode = root[section] as JsonObject ?? [];
			sectionNode[name] = JsonSerializer.SerializeToNode(value);
			if (root[section] is null) root[section] = sectionNode;
		}
		// Rewrite the file with sections in the order the user chose. Unknown top-level keys are kept first.
		if (name == "settingsSectionOrder" && value is IEnumerable<string> sectionOrder)
		{
			JsonObject ordered = [];
			foreach (var pair in root) if (pair.Key is not "settingsSectionOrder" && !IsSettingsSection(pair.Key)) ordered[pair.Key] = pair.Value?.DeepClone();
			foreach (var id in sectionOrder) if (root[id] is JsonObject sectionObject) ordered[id] = sectionObject.DeepClone();
			foreach (var pair in root)
			{
				if (IsSettingsSection(pair.Key) && !ordered.ContainsKey(pair.Key))
				if (IsSettingsSection(pair.Key) && !ordered.ContainsKey(pair.Key)) ordered[pair.Key] = pair.Value?.DeepClone();
			}

			ordered["settingsSectionOrder"] = JsonSerializer.SerializeToNode(sectionOrder);
			root = ordered;
		}
		CursorDiagnosticLogger.Record("config", "setting_changed", details: new
		{
			name,
			previousValue = previousValueJson,
			newValue = JsonSerializer.Serialize(value),
		});
		// Write to a temp file first, then move it over the config. A crash mid-write cannot leave a half-written file.
		var tempPath = path + ".tmp";
		File.WriteAllText(tempPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
		File.Move(tempPath, path, true);
		// Force the next read to reparse the file.
		lock (SettingsLock) isSettingsCacheInitialized = false;
		// cursorSpeed is a system setting, so it must be pushed to Windows right away.
		if (name == "cursorSpeed") ApplyCursorSpeed();
	}
	/// <summary>Known section names in the config file. Settings can be stored at the top level (legacy) or inside these.</summary>
	private static readonly string[] SettingsSections = ["global", "logging", "detection", "indicators", "ocr", "touch", "mouse", "stylus", "keyboard"];
	/// <summary>True when the key is one of the section names.</summary>
	private static bool IsSettingsSection(string name) => SettingsSections.Contains(name, StringComparer.Ordinal);
	/// <summary>Maps a setting name to the section it belongs in. Returns null for unknown names, which stay at the top level.</summary>
	private static string? GetSettingSection(string name) => name switch
	{
		"cursorSpeed" or "windowPositionSpeed" or "cursorPositionPollingIntervalMs" or "bringDraggedWindowToForeground" or "keepWindowWithinScreen" or "dragHoldDurationMs" or "forceWindowDragKey" or "forceNonWindowDragKey" or "dragModeSwitcherShortcut" or "enableDragReleaseSound" or "enableForcedModeToggleSound" => "global",
		"enableDiagnosticLog" or "enableSplitDiagnosticLogs" => "logging",
		"dragCancelKey" or "enableTouchFlickToDrop" or "touchFlickToDropVelocityThreshold" or "touchpadNaturalDragReleaseThreshold" or "touchpadDragDropAnchorHoldMs" or "touchpadPostLiftAnchorGraceMs" => "global",
		"enableHitTestDetection" or "enableCursorShapeDetection" or "enableUiAutomationDetection" or "enableControlClassNameDetection" or "enableEmptyIBeamWindowDrag" or "enableSolidColorDetection" or "enableOcrCapture" or "iBeamTextDetectionAreaSize" or "solidColorDetectionRectangleSize" => "detection",
		"showTouchHoldForceWindowDragIndicator" or "touchHoldForceWindowDragIndicatorDisplayMs" or "showDragModeIndicator" or "dragModeIndicatorDiameter" or "dragModeIndicatorDisplayMs" or "windowDragIndicatorColor" or "otherDragIndicatorColor" or "windowPositionFallbackIndicatorColor" or "touchHoldForceWindowDragIndicatorColor" or "showSolidColorDetectionRectangle" or "solidColorUniformBorderColor" or "solidColorMixedBorderColor" or "solidColorDetectionRectangleDisplayMs" or "showDragModeIndicatorDetails" or "indicatorPanelBackgroundColor" or "indicatorPanelTextColor" => "indicators",
		"ocrHoldDurationMs" or "ocrCaptureRadius" or "ocrLanguageTag" or "ocrMaximumWordGapPercent" or "ocrCopyToClipboard" or "ocrActivationDelayMs" or "ocrClickDelayMs" or "ocrClipboardRestoreDelayMs" => "ocr",
		"touchpadDragFingerCount" or "touchscreenDragFingerCount" or "enableTouchHoldForceWindowDrag" or "touchDragButtons" => "touch",
		"mouseDragTriggerButton" or "enableClickHoldForceWindowDrag" => "mouse",
		"enableStylus" or "enableStylusHoldForceWindowDrag" => "stylus",
		"windowMoveModifierKey" or "windowMoveStepPixels" => "keyboard",
		"dragFallbackPollIntervalMs" or "windowDragSmoothingFrameMs" or "windowDragSmoothingMinTimeConstantMs" or "windowDragSmoothingMaxTimeConstantMs" or "windowDragSmoothingMaxElapsedMs" or "settingsInputDebounceMs" or "settingsStatIntervalMs" or "notificationSafetyTimeoutMs" or "notificationBalloonTimeoutMs" or "settingsEventPollIntervalMs" or "settingsCaptureRefreshIntervalMs" => "global",
		"hitTestTimeoutMs" or "uiAutomationClassifyTimeoutMs" or "titleBarHitTestTimeoutMs" => "detection",
		"touchCancelDragPollIntervalMs" or "touchStationaryDragDropHoldMs" or "touchDragDropAnchorGraceMs" or "touchFingerLiftGraceMs" or "touchscreenCancelDragPollIntervalMs" or "touchscreenContactLostGraceMs" or "touchscreenRecentExternalCursorMovementMs" => "touch",
		"mouseCancelDragPollIntervalMs" => "mouse",
		"forcedDragModePollIntervalMs" or "windowMoveArrowSequenceTimeoutMs" or "windowMoveNudgeIntervalMs" => "keyboard",
		_ => null
	};

	/// <summary>
	/// Adds missing config keys from the bundled template. Existing user values and legacy install-directory
	/// config are kept. If neither config exists, the template is written to the user data directory.
	/// </summary>
	private static void EnsureConfigurationFileComplete()
	{
		var configPath = AppDataPaths.ConfigurationFilePath;
		var legacyConfigPath = Path.Combine(AppContext.BaseDirectory, ConfigFileName);

		try
		{
			using var template = typeof(PointerSettings).Assembly.GetManifestResourceStream("ThreeFingerDrag.config.json");
			if (template is null) return;

			using StreamReader reader = new(template);
			var defaultJson = reader.ReadToEnd();
			if (!File.Exists(configPath))
			{
				_ = Directory.CreateDirectory(AppDataPaths.DataDirectory);
				if (File.Exists(legacyConfigPath))
				{
					File.Copy(legacyConfigPath, configPath);
					CursorDiagnosticLogger.Record("config", "migrated_from_install_directory");
				}
				else
				{
					WriteTextAtomically(configPath, defaultJson);
					CursorDiagnosticLogger.Record("config", "created_from_template");
					return;
				}
			}
			if (JsonNode.Parse(File.ReadAllText(configPath)) is not JsonObject current || JsonNode.Parse(defaultJson) is not JsonObject defaults || !MergeMissingDefaults(current, defaults)) return;

			WriteTextAtomically(configPath, current.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
			CursorDiagnosticLogger.Record("config", "missing_defaults_merged");
		}
		catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
		{
			// Keep in-memory defaults available when the configuration cannot be repaired.
			// Not logged here: Record reads settings, which would call this method again when the file is missing.
		}
	}

	/// <summary>Recursively copies keys that exist in <paramref name="defaults"/> but not in <paramref name="current"/>. Returns true if anything changed.</summary>
	private static bool MergeMissingDefaults(JsonObject current, JsonObject defaults)
	{
		var changed = false;
		foreach (var pair in current.ToArray())
		{
			if (!defaults.TryGetPropertyValue(pair.Key, out var defaultValue)) continue;

			if (defaultValue is JsonObject defaultSection)
			{
				if (pair.Value is JsonObject currentSection)
				{
					changed |= MergeMissingDefaults(currentSection, defaultSection);
				} else
				{
					current[pair.Key] = defaultSection.DeepClone();
					changed = true;
				}
			} else if (pair.Value is null)
			{
				current[pair.Key] = defaultValue?.DeepClone();
				changed = true;
			}
		}

		foreach (var pair in defaults)
		{
			if (current.ContainsKey(pair.Key)) continue;
			current[pair.Key] = pair.Value?.DeepClone();
			changed = true;
		}

		return changed;
	}

	/// <summary>
	/// Restores bundled language files in the user data directory. Existing install-directory translations
	/// are migrated once, and existing values are kept while missing keys are added from the bundle.
	/// </summary>
	private static void EnsureDefaultLanguageFilesExist()
	{
		const string resourcePrefix = "ThreeFingerDrag.Languages.";
		var assembly = typeof(PointerSettings).Assembly;
		var languageDirectory = AppDataPaths.LanguageDirectory;
		JsonSerializerOptions options = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
		foreach (var resourceName in assembly.GetManifestResourceNames().Where(name => name.StartsWith(resourcePrefix, StringComparison.Ordinal)))
		{
			var fileName = resourceName[resourcePrefix.Length..];
			var languagePath = Path.Combine(languageDirectory, fileName);
			var legacyLanguagePath = Path.Combine(AppContext.BaseDirectory, "Languages", fileName);
			try
			{
				using var resource = assembly.GetManifestResourceStream(resourceName);
				if (resource is null) continue;

				using StreamReader reader = new(resource);
				var defaultJson = reader.ReadToEnd();
				if (!File.Exists(languagePath))
				{
					_ = Directory.CreateDirectory(languageDirectory);
					if (File.Exists(legacyLanguagePath))
					{
						File.Copy(legacyLanguagePath, languagePath);
						CursorDiagnosticLogger.Record("language_files", "migrated_from_install_directory", details: new { fileName });
					}
					else
					{
						WriteTextAtomically(languagePath, defaultJson);
						CursorDiagnosticLogger.Record("language_files", "created_from_bundle", details: new { fileName });
						continue;
					}
				}

				if (JsonNode.Parse(File.ReadAllText(languagePath)) is not JsonObject current || JsonNode.Parse(defaultJson) is not JsonObject defaults || !MergeMissingDefaults(current, defaults)) continue;

				WriteTextAtomically(languagePath, current.ToJsonString(options));
				CursorDiagnosticLogger.Record("language_files", "missing_keys_merged", details: new { fileName });
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
			{
				CursorDiagnosticLogger.Record("language_files", "exception", details: new { fileName, exceptionType = ex.GetType().FullName, message = ex.Message });
				// The language stays usable from the bundled copy when the file cannot be read or written.
			}
		}
	}

	/// <summary>Writes a file through a uniquely named temp file so readers never see a partial write.</summary>
	private static void WriteTextAtomically(string path, string contents)
	{
		_ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
		try
		{
			File.WriteAllText(temporaryPath, contents);
			File.Move(temporaryPath, path, overwrite: true);
		}
		finally
		{
			if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
		}
	}
}
