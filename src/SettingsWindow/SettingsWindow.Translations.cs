using System.Text.Json;

namespace ThreeFingerDrag;

internal sealed partial class SettingsWindow : Form
{
	/// <summary>Looks up a translated string. Falls back to English, then to the key name itself.</summary>
	private string T(string key) => translations.TryGetValue(lang, out var values) && values.TryGetValue(key, out var text) ? text : translations["en"].GetValueOrDefault(key, key);

	/// <summary>
	/// Loads every *.json file in the Languages folder, keyed by file name. A file that cannot be read or parsed is skipped.
	/// English and Indonesian keys missing from their files are filled from the copies compiled into the executable.
	/// </summary>
	private static Dictionary<string, Dictionary<string, string>> LoadTranslations()
	{
		Dictionary<string, Dictionary<string, string>> result = new(StringComparer.OrdinalIgnoreCase);
		var directory = AppDataPaths.LanguageDirectory;
		try
		{
			if (Directory.Exists(directory))
			{
				foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
				{
					try
					{
						var code = Path.GetFileNameWithoutExtension(path);
						using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
						if (document.RootElement.ValueKind != JsonValueKind.Object) continue;

						Dictionary<string, string> values = new(StringComparer.Ordinal);
						foreach (var property in document.RootElement.EnumerateObject()) if (property.Value.ValueKind == JsonValueKind.String) values[property.Name] = property.Value.GetString() ?? "";

						values["languageName"] = values.GetValueOrDefault("languageName", code);
						result[code] = values;
					}
					catch (IOException ex) { CursorDiagnosticLogger.Record("settings_translations", "exception", details: new { file = path, exceptionType = ex.GetType().FullName, message = ex.Message }); }
					catch (JsonException ex) { CursorDiagnosticLogger.Record("settings_translations", "exception", details: new { file = path, exceptionType = ex.GetType().FullName, message = ex.Message }); }
					catch (UnauthorizedAccessException ex) { CursorDiagnosticLogger.Record("settings_translations", "exception", details: new { file = path, exceptionType = ex.GetType().FullName, message = ex.Message }); }
				}
			}
		}
		catch (IOException ex) { CursorDiagnosticLogger.Record("settings_translations", "exception", details: new { exceptionType = ex.GetType().FullName, message = ex.Message }); }
		catch (UnauthorizedAccessException ex) { CursorDiagnosticLogger.Record("settings_translations", "exception", details: new { exceptionType = ex.GetType().FullName, message = ex.Message }); }
		foreach (var code in new[] { "en", "id" })
		{
			var bundled = LoadBundledLanguage(code);
			if (bundled is null) continue;

			if (!result.TryGetValue(code, out var values))
			{
				result[code] = bundled;
				continue;
			}

			foreach (var pair in bundled) _ = values.TryAdd(pair.Key, pair.Value);
		}

		// These few built-in strings keep the window usable when the bundled copies are missing.
		if (!result.TryGetValue("en", out Dictionary<string, string>? value))
		{
			value = new Dictionary<string, string> { ["languageName"] = "English", ["settingsApply"] = "Settings apply immediately" };
			result["en"] = value;
		}

		if (!result.ContainsKey("id")) result["id"] = new Dictionary<string, string>(value) { ["languageName"] = "Bahasa Indonesia", ["settingsApply"] = "Pengaturan diterapkan langsung" };

		return result;
	}

	/// <summary>Returns the language file compiled into the executable, or null when it is not bundled.</summary>
	private static Dictionary<string, string>? LoadBundledLanguage(string code)
	{
		using var resource = typeof(SettingsWindow).Assembly.GetManifestResourceStream($"ThreeFingerDrag.Languages.{code}.json");
		if (resource is null) return null;

		using JsonDocument document = JsonDocument.Parse(resource);
		return document.RootElement.EnumerateObject()
			.Where(property => property.Value.ValueKind == JsonValueKind.String)
			.ToDictionary(property => property.Name, property => property.Value.GetString() ?? "", StringComparer.Ordinal);
	}
}
