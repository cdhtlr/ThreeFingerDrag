using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ThreeFingerDrag;

internal static partial class CursorDiagnosticLogger
{
// Monotonic clock used to age the raw-input hint. Wall-clock changes do not affect it.
	private static readonly Stopwatch RawInputClock = Stopwatch.StartNew();
	// Device class of the most recent raw-input event, or "none" before any arrives.
	private static string lastRawInputKind = "none";
	// Clock time of the last raw-input event. -1 means none has arrived.
	private static long lastRawInputMs = -1;

	// Monotonic clock for measuring the time between speed samples.
	private static readonly Stopwatch SpeedClock = Stopwatch.StartNew();
	// Time of the previous sample for each source, so every drag path keeps its own delta time.
	private static readonly Dictionary<string, double> LastSpeedMsBySource = [];

	/// <summary>
	/// Logs one movement sample for a drag path that does not go through DragEngine.Move,
	/// so every drag method reports comparable speed fields.
	/// </summary>
	public static void RecordSpeedSample(string source, Point inputDelta, Point? cursorBefore, Point? cursorAfter, Rectangle? windowBefore, Rectangle? windowAfter, [CallerFilePath] string sourceFile = "")
	{
		if (!IsEnabled) return;

		double nowMs = SpeedClock.Elapsed.TotalMilliseconds;
		double dtMs;
		lock (Sync)
		{
			// The first sample for a source has no earlier time, so its dtMs is 0 and its speeds report as 0.
			dtMs = LastSpeedMsBySource.TryGetValue(source, out var last) ? nowMs - last : 0;
			LastSpeedMsBySource[source] = nowMs;
		}

		// Each distance is the Euclidean length, in pixels, between the two points.
		double inputPx = Math.Sqrt((double)inputDelta.X * inputDelta.X + (double)inputDelta.Y * inputDelta.Y);
		double cursorPx = cursorBefore is { } cb && cursorAfter is { } ca ? Math.Sqrt(Math.Pow(ca.X - cb.X, 2) + Math.Pow(ca.Y - cb.Y, 2)) : 0;
		double windowPx = windowBefore is { } wb && windowAfter is { } wa ? Math.Sqrt(Math.Pow(wa.X - wb.X, 2) + Math.Pow(wa.Y - wb.Y, 2)) : 0;
		// Converts pixels over dtMs into pixels per second. Returns 0 when no interval is known.
		double Speed(double px) => dtMs > 0 ? Math.Round(px / dtMs * 1000, 1) : 0;

		Record("drag_speed", "sample", cursorBefore, cursorAfter, sourceFile: sourceFile, details: new
		{
			source,
			dtMs = Math.Round(dtMs, 2),
			inputDelta = new { inputDelta.X, inputDelta.Y },
			inputPx = Math.Round(inputPx, 2),
			cursorPx = Math.Round(cursorPx, 2),
			windowPx = Math.Round(windowPx, 2),
			inputSpeedPxPerSec = Speed(inputPx),
			cursorSpeedPxPerSec = Speed(cursorPx),
			windowSpeedPxPerSec = Speed(windowPx),
			windowBefore = windowBefore is { } b ? new { b.X, b.Y, b.Width, b.Height } : null,
			windowAfter = windowAfter is { } a ? new { a.X, a.Y, a.Width, a.Height } : null,
		});
	}

	/// <summary>Remembers which HID class produced the latest raw input, to attribute the next click.</summary>
	public static void NoteRawInput(string kind)
	{
		lock (Sync)
		{
			lastRawInputKind = kind;
			lastRawInputMs = RawInputClock.ElapsedMilliseconds;
		}
	}

	/// <summary>Returns the latest raw-input hint and its age in ms. Low-level hooks cannot identify the device.</summary>
	public static (string Kind, long? MsSince) GetRawInputHint()
	{
		lock (Sync)
		{
			// No raw input has arrived yet, so the age is unknown and returned as null.
			if (lastRawInputMs < 0) return (lastRawInputKind, null);
			return (lastRawInputKind, RawInputClock.ElapsedMilliseconds - lastRawInputMs);
		}
	}

	/// <summary>Records a physical mouse-button transition with the window under the pointer.</summary>
	public static void RecordClick(string button, bool pressed, Point position, uint mouseData, [CallerFilePath] string sourceFile = "")
	{
		if (!PointerSettings.IsDiagnosticLogEnabled()) return;
		var (kind, msSince) = GetRawInputHint();
		// Window lookup runs off the hook thread so a slow API call cannot trip the hook timeout.
		_ = Task.Run(() =>
		{
			try
			{
				// For X1 and X2, the hook puts the button number in the high word of mouseData.
				Record("click", pressed ? "press" : "release", after: position, sourceFile: sourceFile, details: new
				{
					button,
					state = pressed ? "down" : "up",
					lastRawInputKind = kind,
					msSinceRawInput = msSince,
					xButton = button is "X1" or "X2" ? (int)((mouseData >> 16) & 0xFFFF) : (int?)null,
					target = DescribeWindow(0, position),
				});
			}
			catch (Exception)
			{
				// Diagnostics must never affect input handling.
			}
		});
	}

	/// <summary>Records an input-device sample independently from the shared Windows cursor.</summary>
	public static void RecordInputSample(string source, string action, Point? inputPosition,
		Point? cursorPosition, object? details = null, [CallerFilePath] string sourceFile = "")
	{
		if (!PointerSettings.IsDiagnosticLogEnabled()) return;

		Record("input_sample", action, sourceFile: sourceFile, details: new
		{
			source,
			inputPosition = ToCoordinates(inputPosition),
			inputMonitor = GetMonitorAtPoint(inputPosition),
			cursorPosition = ToCoordinates(cursorPosition),
			cursorMonitor = GetMonitorAtPoint(cursorPosition),
			details
		});
	}

	/// <summary>Records position changes reported by the low-level mouse hook. Duplicate positions are dropped unless suppressed.</summary>
	public static void RecordCursorEvent(Point position, string source, string action, bool suppressed = false, bool movedByProgram = false, [CallerFilePath] string sourceFile = "")
	{
		// Runs on every mouse move; skip all work (including monitor lookups) when logging is off.
		if (!IsEnabled) return;

		Point? before;
		lock (Sync)
		{
			// Ignore duplicate unsuppressed positions, but retain suppressed events for diagnosis.
			before = lastPosition;
			if (before == position && !suppressed) return;
			lastPosition = position;
		}

		// Moves made by this app are tagged so they are not counted as user movement.
		var movedBy = movedByProgram ? "program" : "user";
		Record(suppressed ? "cursor_movement_suppressed" : "cursor_movement", action, before, position,
			sourceFile: sourceFile,
			details: new
			{
				source,
				suppressed,
				cursorMovedBy = movedBy,
				monitor = GetMonitorAtPoint(position)
			});
	}

	/// <summary>Poll fallback for cursor changes not surfaced through the mouse hook.</summary>
	public static void PollCursorPosition([CallerFilePath] string sourceFile = "")
	{
		// Polled every few milliseconds; nothing is recorded unless diagnostics are enabled.
		if (!IsEnabled) return;

		// The hook is primary; polling catches movement paths that do not produce a hook callback.
		if (!MouseInject.TryGetCursorPoint(out var position)) return;

		Point? before;
		lock (Sync)
		{
			// The first poll establishes a baseline and is not itself a movement event.
			before = lastPosition;
			if (isPositionBaselineInitialized && before == position) return;

			lastPosition = position;
			isPositionBaselineInitialized = true;
		}

		if (before != position)
		{
			Record("cursor_movement", "poll_detected_position_change", before, position,
				sourceFile: sourceFile,
				details: new
				{
					source = "OS cursor-position polling fallback",
					cursorMovedBy = "user",
					attribution = "unknown",
					monitor = GetMonitorAtPoint(position)
				});
		}
	}

	/// <summary>Converts an optional point to an anonymous x/y object for JSON logging. Returns null when the point is missing.</summary>
	private static object? ToCoordinates(Point? point) => point is { } p ? new { x = p.X, y = p.Y } : null;
}
