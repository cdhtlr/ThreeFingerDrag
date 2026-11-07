using System.Diagnostics;
using static ThreeFingerDrag.NativeMethods;

namespace ThreeFingerDrag;

internal static partial class WindowDragController
{
	/// <summary>
	/// Stops the smoothing timer and drops any queued movement. Called when the drag ends so the window
	/// does not keep gliding toward input that arrived before the finger lifted.
	/// </summary>
	internal static void CancelSmoothedMovement(DragSession session)
	{
		lock (session.SmoothingLock)
		{
			var wasRunning = session.SmoothingTimer is not null;
			var droppedHorizontal = session.TargetHorizontalOffset - session.AppliedHorizontalOffset;
			var droppedVertical = session.TargetVerticalOffset - session.AppliedVerticalOffset;
			session.SmoothingTimer?.Stop();
			session.SmoothingTimer = null;
			session.TargetHorizontalOffset = session.AppliedHorizontalOffset;
			session.TargetVerticalOffset = session.AppliedVerticalOffset;
			if (wasRunning)
			{
				CursorDiagnosticLogger.Record("smoothing_lifecycle", "cancelled", details: new
				{
					windowHandle = session.WindowHandle.ToInt64(),
					droppedHorizontal,
					droppedVertical,
				});
			}
		}
	}

	/// <summary>
	/// Runs on each timer tick. It moves the window a fraction of the remaining distance,
	/// stops the timer when the target is reached, and stops it on any SetWindowPos failure.
	/// </summary>
	private static void StepSmoothedMovement(DragSession session, System.Timers.Timer timer)
	{
		lock (session.SmoothingLock)
		{
			var remainingX = session.TargetHorizontalOffset - session.AppliedHorizontalOffset;
			var remainingY = session.TargetVerticalOffset - session.AppliedVerticalOffset;
			if (remainingX == 0 && remainingY == 0)
			{
				timer.Stop();
				session.SmoothingTimer = null;
				CursorDiagnosticLogger.Record("smoothing_lifecycle", "completed", details: new
				{
					windowHandle = session.WindowHandle.ToInt64(),
					appliedHorizontalOffset = session.AppliedHorizontalOffset,
					appliedVerticalOffset = session.AppliedVerticalOffset,
				});
				return;
			}

			// Compare the window with the last position we sent. A difference means another source moved it.
			if (CursorDiagnosticLogger.IsEnabled && session.LastRequestedPosition is { } expected && TryGetWindowBounds(session.WindowHandle, out var actualBounds))
			{
				if (actualBounds.Left != expected.X || actualBounds.Top != expected.Y)
				{
					CursorDiagnosticLogger.Record("window_position_check", "external_move", null, null, new
					{
						windowHandle = session.WindowHandle.ToInt64(),
						expectedLeft = expected.X,
						expectedTop = expected.Y,
						actualLeft = actualBounds.Left,
						actualTop = actualBounds.Top,
						deltaX = actualBounds.Left - expected.X,
						deltaY = actualBounds.Top - expected.Y
					});
				}
			}

			// Use real elapsed time so uneven timer ticks do not show up as stutter.
			var now = Stopwatch.GetTimestamp();
			var elapsedTicks = Math.Min(now - session.LastSmoothingTimestamp, MaxSmoothingElapsedTicks);
			session.LastSmoothingTimestamp = now;
			if (elapsedTicks <= 0) return;

			// Time constant so that the steady-state lag is SmoothingLagPx: tau = lag / speed.
			var speed = session.InputSpeedPxPerTick;
			var timeConstantTicks = speed > 0 ? Math.Clamp(SmoothingLagPx / speed, MinSmoothingTimeConstantTicks, MaxSmoothingTimeConstantTicks) : MaxSmoothingTimeConstantTicks;
			// Exponential easing: close the fraction of the gap that matches the real elapsed time.
			var follow = 1.0 - Math.Exp(-elapsedTicks / timeConstantTicks);
			var stepX = Math.Clamp(SmoothingStep(remainingX, follow), -MaxSmoothingStepPx, MaxSmoothingStepPx);
			var stepY = Math.Clamp(SmoothingStep(remainingY, follow), -MaxSmoothingStepPx, MaxSmoothingStepPx);
			var requestedLeft = session.InitialBounds.Left + session.AppliedHorizontalOffset + stepX;
			var requestedTop = session.InitialBounds.Top + session.AppliedVerticalOffset + stepY;
			if (PointerSettings.ShouldKeepWindowWithinScreen())
			{
				// The limit is the combined bounds of all monitors, so the window can still cross between extended screens.
				var limits = GetWindowPositionLimits(session.WindowHandle, session.InitialBounds.Width, session.InitialBounds.Height);
				var clampedLeft = ClampToRange(requestedLeft, limits.MinLeft, limits.MaxLeft);
				var clampedTop = ClampToRange(requestedTop, limits.MinTop, limits.MaxTop);
				if (clampedLeft != requestedLeft)
				{
					// Drop the excess target so the timer stops instead of pushing against the edge.
					stepX = clampedLeft - (session.InitialBounds.Left + session.AppliedHorizontalOffset);
					session.TargetHorizontalOffset = session.AppliedHorizontalOffset + stepX;
					requestedLeft = clampedLeft;
				}
				if (clampedTop != requestedTop)
				{
					stepY = clampedTop - (session.InitialBounds.Top + session.AppliedVerticalOffset);
					session.TargetVerticalOffset = session.AppliedVerticalOffset + stepY;
					requestedTop = clampedTop;
				}
			}

			// SWP_ASYNC avoids waiting on the target app. On failure, stop smoothing and drop the
			// queued target so the timer does not keep retrying a window that cannot be moved.
			var callStart = Stopwatch.GetTimestamp();
			var moved = SetWindowPos(session.WindowHandle, 0, requestedLeft, requestedTop, 0, 0, SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpAsyncWindowPos);
			if (CursorDiagnosticLogger.IsEnabled)
			{
				var callMs = (Stopwatch.GetTimestamp() - callStart) * 1000.0 / Stopwatch.Frequency;
				CursorDiagnosticLogger.Record("smoothing_step", moved ? "applied" : "set_window_pos_failed", null, null, new
				{
					windowHandle = session.WindowHandle.ToInt64(),
					stepX,
					stepY,
					remainingX,
					remainingY,
					elapsedMs = Math.Round(elapsedTicks * 1000.0 / Stopwatch.Frequency, 2),
					timeConstantMs = Math.Round(timeConstantTicks * 1000.0 / Stopwatch.Frequency, 2),
					speedPxPerSec = Math.Round(speed * Stopwatch.Frequency, 1),
					setWindowPosMs = Math.Round(callMs, 3),
					requestedLeft,
					requestedTop
				});
			}
			if (!moved)
			{
				timer.Stop();
				session.SmoothingTimer = null;
				session.TargetHorizontalOffset = session.AppliedHorizontalOffset;
				session.TargetVerticalOffset = session.AppliedVerticalOffset;
				return;
			}

			session.LastRequestedPosition = new Point(requestedLeft, requestedTop);
			session.AppliedHorizontalOffset += stepX;
			session.AppliedVerticalOffset += stepY;
		}
	}
}
