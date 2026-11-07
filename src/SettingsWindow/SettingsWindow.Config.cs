using System.Text.Json;

namespace ThreeFingerDrag;

internal sealed partial class SettingsWindow : Form
{
	/// <summary>
	/// Finds a config key at the top level first, then inside each known section object.
	/// Both the flat and the sectioned file layouts are supported.
	/// </summary>
	private static bool TryGetConfigProperty(JsonElement root, string key, out JsonElement value)
	{
		if (root.TryGetProperty(key, out value)) return true;
			foreach (var section in new[] { "global", "logging", "detection", "indicators", "ocr", "touch", "mouse", "stylus", "keyboard" }) if (root.TryGetProperty(section, out var group) && group.ValueKind == JsonValueKind.Object && group.TryGetProperty(key, out value)) return true;
		value = default;
		return false;
	}
	/// <summary>Reads an integer from the config. Returns the fallback when the file or value is missing or invalid.</summary>
	private static int ReadInt(string k, int fallback) { try { using JsonDocument d = JsonDocument.Parse(File.ReadAllText(AppDataPaths.ConfigurationFilePath)); return TryGetConfigProperty(d.RootElement, k, out var x) && x.TryGetInt32(out var v) ? v : fallback; } catch (Exception ex) { CursorDiagnosticLogger.Record("settings_window_config", "exception", details: new { key = k, exceptionType = ex.GetType().FullName, message = ex.Message }); return fallback; } }
	/// <summary>Reads a boolean from the config. Other JSON types return the fallback.</summary>
	private static bool ReadBool(string k, bool fallback = false) { try { using JsonDocument d = JsonDocument.Parse(File.ReadAllText(AppDataPaths.ConfigurationFilePath)); return TryGetConfigProperty(d.RootElement, k, out var x) && x.ValueKind is JsonValueKind.True or JsonValueKind.False ? x.GetBoolean() : fallback; } catch (Exception ex) { CursorDiagnosticLogger.Record("settings_window_config", "exception", details: new { key = k, exceptionType = ex.GetType().FullName, message = ex.Message }); return fallback; } }
	/// <summary>Reads a string from the config. Returns the fallback when the value is missing or not a string.</summary>
	private static string ReadString(string k, string fallback) { try { using JsonDocument d = JsonDocument.Parse(File.ReadAllText(AppDataPaths.ConfigurationFilePath)); return TryGetConfigProperty(d.RootElement, k, out var x) && x.ValueKind == JsonValueKind.String ? x.GetString() ?? fallback : fallback; } catch (Exception ex) { CursorDiagnosticLogger.Record("settings_window_config", "exception", details: new { key = k, exceptionType = ex.GetType().FullName, message = ex.Message }); return fallback; } }
	/// <summary>Reads a top-level array of strings from the config. Non-string items are skipped.</summary>
	private static string[] ReadStringArray(string key) { try { using JsonDocument d = JsonDocument.Parse(File.ReadAllText(AppDataPaths.ConfigurationFilePath)); return d.RootElement.TryGetProperty(key, out var x) && x.ValueKind == JsonValueKind.Array ? [.. x.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!)] : []; } catch (Exception ex) { CursorDiagnosticLogger.Record("settings_window_config", "exception", details: new { key, exceptionType = ex.GetType().FullName, message = ex.Message }); return []; } }
	/// <summary>Returns the known section ids that appear as top-level keys in the config, in file order.</summary>
	private static string[] ReadConfiguredSectionOrder() { try { using JsonDocument d = JsonDocument.Parse(File.ReadAllText(AppDataPaths.ConfigurationFilePath)); return [.. d.RootElement.EnumerateObject().Select(item => item.Name).Where(name => sourceArray.Contains(name, StringComparer.Ordinal))]; } catch (Exception ex) { CursorDiagnosticLogger.Record("settings_window_config", "exception", details: new { action = "read_section_order", exceptionType = ex.GetType().FullName, message = ex.Message }); return []; } }
}
