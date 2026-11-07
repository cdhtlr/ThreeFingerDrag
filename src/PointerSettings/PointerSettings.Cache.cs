namespace ThreeFingerDrag;

internal static partial class PointerSettings
{
	// Guards the settings cache. Settings are read from many threads (hooks, timers, UI).
	private static readonly object SettingsLock = new();
	// Last write time of the config file when the cache was filled. A newer time forces a reparse.
	private static DateTime cachedSettingsLastWriteUtc = DateTime.MinValue;
	private static Settings cachedSettings = Settings.Default;
	private static bool isSettingsCacheInitialized;
	private static long lastSettingsStatTickMs;


	/// <summary>Returns the current cursor speed setting (1 to 20).</summary>
	private static int GetConfiguredCursorSpeed() => GetConfiguredSettings().CursorSpeed;

	/// <summary>
	/// Returns the cached settings, reparsing the config file only when its last write time has changed.
	/// </summary>
	private static Settings GetConfiguredSettings()
	{
		// Settings are read on every cursor and drag event; re-checking the file at most every
		// 250 ms avoids disk I/O on each call. SaveSetting invalidates the cache immediately.
		lock (SettingsLock) if (isSettingsCacheInitialized && Environment.TickCount64 - lastSettingsStatTickMs < cachedSettings.SettingsStatIntervalMs) return cachedSettings;

		var configPath = AppDataPaths.ConfigurationFilePath;
		if (!File.Exists(configPath))
		{
			EnsureConfigurationFileComplete();
			if (!File.Exists(configPath)) return Settings.Default;
		}

		DateTime lastWriteUtc;
		try
		{
			lastWriteUtc = File.GetLastWriteTimeUtc(configPath);
		}
		catch (IOException)
		{
			return Settings.Default;
		}
		catch (UnauthorizedAccessException)
		{
			return Settings.Default;
		}

		lock (SettingsLock)
		{
			lastSettingsStatTickMs = Environment.TickCount64;
			if (isSettingsCacheInitialized && cachedSettingsLastWriteUtc == lastWriteUtc) return cachedSettings;

			var warnings = new List<string>();
			cachedSettings = ParseConfiguredSettings(configPath, warnings);
			cachedSettingsLastWriteUtc = lastWriteUtc;
			isSettingsCacheInitialized = true;
			// Logged after the cache is set. Record checks the diagnostic setting, which reads this cache again.
			foreach (var warning in warnings) CursorDiagnosticLogger.Record("config", "validation-warning", details: warning);
			return cachedSettings;
		}
	}
}
