using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace ThreeFingerDrag;

internal static partial class CursorDiagnosticLogger
{
/// <summary>Writes one JSONL entry with the current monitor layout and cursor state. Does nothing when logging is off.</summary>
	public static void Record(string type, string? action = null, Point? before = null, Point? after = null, object? details = null, [CallerFilePath] string sourceFile = "")
	{
		// Logging is optional and should have no cost when the setting is off.
		if (!PointerSettings.IsDiagnosticLogEnabled()) return;

		try
		{
			var section = DiagnosticLogSection.FromSourceFile(sourceFile);
			// Anonymous properties define the stable JSONL shape without a separate DTO type.
			var entry = new
			{
				timestampUtc = DateTime.UtcNow,
				section,
				type,
				action,
				monitors = GetMonitorsSummary(),
				cursorBefore = ToCoordinates(before),
				cursorAfter = ToCoordinates(after),
				cursorMonitor = GetMonitorAtPoint(after ?? before),
				details,
			};
			// File I/O happens on a writer thread so callers on the low-level hook thread never block on disk.
			EnqueueLine(section, JsonSerializer.Serialize(entry, JsonOptions));
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
		{
			// Diagnostics must never interrupt input processing.
		}
	}

	/// <summary>Queue of JSON lines waiting for the writer thread. Unbounded.</summary>
	private static readonly BlockingCollection<(string Section, string Json)> PendingLines = [];
	// 0 until the first line is queued. The compare-exchange starts the writer thread exactly once.
	private static int writerStarted;

	/// <summary>Queues one JSON line for its section and starts the writer thread on first use.</summary>
	private static void EnqueueLine(string section, string json)
	{
		PendingLines.Add((section, json));
		if (Interlocked.CompareExchange(ref writerStarted, 1, 0) == 0){new Thread(WriteLoop) { IsBackground = true, Name = "Cursor diagnostics writer" }.Start();}
	}

	/// <summary>
	/// Returns the file for a section. When split logging is off, every section shares logs.jsonl.
	/// When it is on, each section gets its own file named after the section id.
	/// </summary>
	private static string ResolveLogPath(string section) => Path.Combine(LogDirectory, PointerSettings.IsSplitDiagnosticLogEnabled() ? $"{section}.jsonl" : SingleLogFileName);

	/// <summary>Background loop that appends queued lines to the section's JSONL file, one line per entry.</summary>
	private static void WriteLoop()
	{
		foreach (var (section, json) in PendingLines.GetConsumingEnumerable())
		{
			try
			{
				// The logs folder can be deleted while the app runs, so it is created before each write.
				_ = Directory.CreateDirectory(LogDirectory);
				// Open/write/flush on each event so another process or tailing viewer can read it immediately.
				using FileStream stream = new(ResolveLogPath(section), FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
				using StreamWriter writer = new(stream);
				writer.WriteLine(json);
				writer.Flush();
				stream.Flush(flushToDisk: true);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
			{
				// Dropping one diagnostic line is preferable to stopping the writer.
			}
		}
	}
}
