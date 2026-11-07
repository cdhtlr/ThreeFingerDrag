using System.Diagnostics;

namespace ThreeFingerDrag;

internal static partial class WindowDragController
{
	/// <summary>
	/// Adds the movement to the target offset; a timer then glides the window toward it,
	/// so input bursts become a continuous motion instead of discrete jumps.
	/// </summary>
	private static void QueueSmoothedMovement(DragSession session, int horizontalMovement, int verticalMovement)
	{
		lock (session.SmoothingLock)
		{
			session.TargetHorizontalOffset += horizontalMovement;
			session.TargetVerticalOffset += verticalMovement;
			if (PointerSettings.ShouldKeepWindowWithinScreen())
			{
				// Keep the target inside the desktop so input past the edge does not build up and delay the reverse.
				var limits = GetWindowPositionLimits(session.WindowHandle, session.InitialBounds.Width, session.InitialBounds.Height);
				var minOffsetX = limits.MinLeft - session.InitialBounds.Left;
				var maxOffsetX = limits.MaxLeft - session.InitialBounds.Left;
				var minOffsetY = limits.MinTop - session.InitialBounds.Top;
				var maxOffsetY = limits.MaxTop - session.InitialBounds.Top;
				session.TargetHorizontalOffset = ClampToRange(session.TargetHorizontalOffset, minOffsetX, maxOffsetX);
				session.TargetVerticalOffset = ClampToRange(session.TargetVerticalOffset, minOffsetY, maxOffsetY);
			}
			// Speed comes from the input rate, not the last applied step, so easing does not feed back on itself.
			var now = Stopwatch.GetTimestamp();
			if (session.LastInputTimestamp != 0)
			{
				// Floor at one timer frame so a burst of input arriving in the same tick does not read as a huge speed.
				var inputTicks = Math.Max(now - session.LastInputTimestamp, SmoothingFrameTicks * 2);
				var inputPx = Math.Sqrt((double)horizontalMovement * horizontalMovement + (double)verticalMovement * verticalMovement);
				var instantSpeed = inputPx / inputTicks;
				session.InputSpeedPxPerTick = session.InputSpeedPxPerTick * 0.7 + instantSpeed * 0.3;
			}
			session.LastInputTimestamp = now;
			if (session.SmoothingTimer is not null) return;

			System.Timers.Timer timer = new(SmoothingFrameMs) { AutoReset = true };
			timer.Elapsed += (_, _) => StepSmoothedMovement(session, timer);
			session.SmoothingTimer = timer;
			session.LastSmoothingTimestamp = Stopwatch.GetTimestamp();
			timer.Start();
			CursorDiagnosticLogger.Record("smoothing_timer", "started", details: new
			{
				windowHandle = session.WindowHandle.ToInt64(),
				targetHorizontalOffset = session.TargetHorizontalOffset,
				targetVerticalOffset = session.TargetVerticalOffset,
				appliedHorizontalOffset = session.AppliedHorizontalOffset,
				appliedVerticalOffset = session.AppliedVerticalOffset,
			});
		}
	}
}
