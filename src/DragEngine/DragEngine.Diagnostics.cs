using System.Diagnostics;

namespace ThreeFingerDrag;

internal sealed partial class DragEngine
{
	// Owner type name, used only in diagnostic records.
	private string? ownerSource;
	private readonly Stopwatch dragClock = new();
	// Diagnostic totals. They are updated only while CursorDiagnosticLogger is enabled.
	private double lastSampleMs;
	private long sampleCount;
	private double totalInputPx;
	private double totalCursorPx;
	private double totalWindowPx;
	private long zeroCursorSamples;
	private long zeroWindowSamples;

	/// <summary>Clears the diagnostic totals at the start of a drag.</summary>
	private void ResetStatistics()
	{
		sampleCount = 0;
		totalInputPx = totalCursorPx = totalWindowPx = 0;
		zeroCursorSamples = zeroWindowSamples = 0;
		lastSampleMs = 0;
	}

	/// <summary>Adds one movement sample to the drag totals and writes it to the diagnostic log.</summary>
	private void TrackMovement(Point inputDelta, Point cursorBefore, Point cursorAfter, Rectangle? windowBefore, Rectangle? windowAfter, bool cursorAlreadyMoved)
	{
		var nowMs = dragClock.Elapsed.TotalMilliseconds;
		var dtMs = nowMs - lastSampleMs;
		lastSampleMs = nowMs;
		sampleCount++;

		var inputPx = Math.Sqrt((double)inputDelta.X * inputDelta.X + (double)inputDelta.Y * inputDelta.Y);
		var cursorPx = Math.Sqrt(Math.Pow(cursorAfter.X - cursorBefore.X, 2) + Math.Pow(cursorAfter.Y - cursorBefore.Y, 2));
		var windowPx = windowBefore is { } wb && windowAfter is { } wa ? Math.Sqrt(Math.Pow(wa.X - wb.X, 2) + Math.Pow(wa.Y - wb.Y, 2)) : 0;
		totalInputPx += inputPx;
		totalCursorPx += cursorPx;
		totalWindowPx += windowPx;
		if (cursorPx == 0) zeroCursorSamples++;
		if (windowBefore is not null && windowPx == 0) zeroWindowSamples++;

		// Speed in px/s for this sample. Zero when no time has passed since the last sample.
		double Speed(double px) => dtMs > 0 ? Math.Round(px / dtMs * 1000, 1) : 0;
		CursorDiagnosticLogger.Record("drag_speed", "sample", cursorBefore, cursorAfter, new
		{
			source = ownerSource,
			sampleIndex = sampleCount,
			elapsedMs = Math.Round(nowMs, 1),
			dtMs = Math.Round(dtMs, 2),
			inputDelta = new { inputDelta.X, inputDelta.Y },
			inputPx = Math.Round(inputPx, 2),
			cursorPx = Math.Round(cursorPx, 2),
			windowPx = Math.Round(windowPx, 2),
			inputSpeedPxPerSec = Speed(inputPx),
			cursorSpeedPxPerSec = Speed(cursorPx),
			windowSpeedPxPerSec = Speed(windowPx),
			cursorAlreadyMoved,
			windowBefore = windowBefore is { } b ? new { b.X, b.Y, b.Width, b.Height } : null,
			windowAfter = windowAfter is { } a ? new { a.X, a.Y, a.Width, a.Height } : null,
		});
	}
}
