using System.Timers;

namespace ThreeFingerDrag;

internal sealed partial class StylusProcessor
{
	/// <summary>Arms forced window mode if the pen has stayed within tolerance for the full hold duration.</summary>
	private void HoldTimerElapsed(object? sender, ElapsedEventArgs e)
	{
		lock (sync)
		{
			if (isDisposed || !penDown || holdCanceled || holdArmed
				|| !PointerSettings.IsStylusHoldForceWindowDragEnabled()
				|| !MouseInject.TryGetCursorPoint(out var point)
				|| DistanceExceedsTolerance(holdStartPoint, point))
			{
				CursorDiagnosticLogger.Record("stylus_hold", "hold_timer_elapsed_ignored", holdStartPoint, null, new { isDisposed, penDown, holdCanceled, holdArmed });
				return;
			}

			holdArmed = true;
			ForcedDragModeService.SetStylusHoldForced(true);
			DragModeIndicatorOverlay.ShowHoldForceWindowDrag(point, "Stylus hold forced");
			CursorDiagnosticLogger.Record("stylus_hold", "window_drag_armed", holdStartPoint, point, new { holdMs = PointerSettings.GetDragHoldDurationMs() });
		}
	}

	/// <summary>True when the point is farther from the start than the tolerance. Compares squared distances in integer math.</summary>
	private static bool DistanceExceedsTolerance(Point start, Point current)
	{
		var dx = current.X - start.X;
		var dy = current.Y - start.Y;
		return (long)dx * dx + (long)dy * dy > (long)HoldStillTolerancePixels * HoldStillTolerancePixels;
	}
}
