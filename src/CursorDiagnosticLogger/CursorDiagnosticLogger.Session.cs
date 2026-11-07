using System.Text.Json;

namespace ThreeFingerDrag;

/// <summary>Append-only JSONL diagnostics for cursor, touch, and drag decisions.</summary>
internal static partial class CursorDiagnosticLogger
{

	// Guards the shared cursor baseline, raw-input hint, and speed state. File writes use the writer thread instead.
	private static readonly object Sync = new();
	// Keep diagnostics beside the executable so portable installs remain self-contained.
	private static readonly string LogDirectory = AppDataPaths.LogDirectory;
	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
	// Last cursor position seen by the recorders. Used to skip duplicate move events.
	private static Point? lastPosition;
	// False until the first poll sets the baseline. That first poll is not a movement.
	private static bool isPositionBaselineInitialized;

	public static string PathName => LogDirectory;
	/// <summary>True when the diagnostic log setting is on.</summary>
	public static bool IsEnabled => PointerSettings.IsDiagnosticLogEnabled();

	// ── Session Lifecycle ──────────────────────────────────────────────────────

	/// <summary>Marker file that exists while the app runs. A leftover marker means an unclean shutdown.</summary>
	private static readonly string SessionMarkerPath = Path.Combine(AppDataPaths.LogDirectory, "session.running");
	/// <summary>Random ID for this run, so log lines from one run can be grouped.</summary>
	private static readonly string SessionId = Guid.NewGuid().ToString("N");
	private static readonly DateTime SessionStartUtc = DateTime.UtcNow;
	// Set to 1 by the first EndSession call. Later calls return early.
	private static int sessionEnded;

	/// <summary>
	/// Logs the startup configuration and writes a marker file. The marker is removed on a clean exit,
	/// so a marker found at the next startup means the previous process was killed or crashed.
	/// </summary>
	public static void StartSession()
	{
		if (!IsEnabled) return;

		try
		{
			_ = Directory.CreateDirectory(Path.GetDirectoryName(SessionMarkerPath)!);
			if (File.Exists(SessionMarkerPath))
			{
				Record("lifecycle", "previous_session_unclean_shutdown", details: new
				{
					previousMarker = SafeReadText(SessionMarkerPath),
					explanation = "Marker file was not removed; previous process was force closed, killed, crashed, or the system lost power.",
				});
			}
			// The marker records who was running, so a crash report can name the dead process.
			File.WriteAllText(SessionMarkerPath, JsonSerializer.Serialize(new
			{
				sessionId = SessionId,
				pid = Environment.ProcessId,
				startedUtc = SessionStartUtc,
			}));
		}
		catch (IOException) { }
		catch (UnauthorizedAccessException) { }

		var configPath = AppDataPaths.ConfigurationFilePath;
		Record("lifecycle", "started", details: new
		{
			sessionId = SessionId,
			pid = Environment.ProcessId,
			version = typeof(CursorDiagnosticLogger).Assembly.GetName().Version?.ToString(),
			osVersion = Environment.OSVersion.VersionString,
			is64BitProcess = Environment.Is64BitProcess,
			machineName = Environment.MachineName,
			commandLine = Environment.CommandLine,
			configPath,
			config = ParseJsonOrRaw(SafeReadText(configPath)),
		});

		// ProcessExit covers normal exits. Killed or crashed runs are caught by the marker check above.
		AppDomain.CurrentDomain.ProcessExit += (_, _) => EndSession("process_exit");
		AppDomain.CurrentDomain.UnhandledException += (_, args) => Record("lifecycle", "unhandled_exception", details: new
		{
			exception = args.ExceptionObject?.ToString(),
			isTerminating = args.IsTerminating,
		});
		// Logoff and shutdown can end the process before ProcessExit runs, so the reason is logged here.
		Microsoft.Win32.SystemEvents.SessionEnding += (_, args) => Record("lifecycle", "session_ending", details: new { reason = args.Reason.ToString(), });
	}

	/// <summary>Logs the exit reason once and removes the marker file that signals an unclean shutdown.</summary>
	public static void EndSession(string reason)
	{
		// Exchange lets only the first caller continue, even if several exit events fire.
		if (Interlocked.Exchange(ref sessionEnded, 1) != 0) return;

		Record("lifecycle", "exited", details: new
		{
			sessionId = SessionId,
			reason,
			uptimeMs = (DateTime.UtcNow - SessionStartUtc).TotalMilliseconds,
		});
		try
		{
			if (File.Exists(SessionMarkerPath)) File.Delete(SessionMarkerPath);
		}
		catch (IOException) { }
		catch (UnauthorizedAccessException) { }
	}

	/// <summary>Reads a file as text. Returns null when it is missing or locked.</summary>
	private static string? SafeReadText(string path)
	{
		try
		{ return File.Exists(path) ? File.ReadAllText(path) : null; }
		catch (IOException) { return null; }
		catch (UnauthorizedAccessException) { return null; }
	}

	/// <summary>Parses text as JSON. Returns the raw text when it is not valid JSON, so the log still keeps it.</summary>
	private static object? ParseJsonOrRaw(string? text)
	{
		if (text is null) return null;

		try
		{ return System.Text.Json.Nodes.JsonNode.Parse(text); }
		catch (JsonException) { return text; }
	}
}
