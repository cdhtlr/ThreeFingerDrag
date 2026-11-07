using static ThreeFingerDrag.NativeMethods;

namespace ThreeFingerDrag;

internal static partial class WindowDragController
{
	/// <summary>
	/// Decides whether a title-bar drag that did not respond should be replaced by SetWindowPos.
	/// Movement is queued during a short response window. Once it expires, the queue is applied.
	/// </summary>
	/// <returns>True when the fallback was activated by this call.</returns>
	public static bool TryActivateWindowPositionFallback(DragSession session, Point distance)
	{
		if (session.UsingWindowPositionFallback || session.HasMovedNatively) return false;

		// If the title-bar drag already moved the window, no fallback is needed.
		if (CheckWindowMoved(session))
		{
			session.HasMovedNatively = true;
			session.FallbackCheckPending = false;
			session.PendingFallbackHorizontalMovement = 0;
			session.PendingFallbackVerticalMovement = 0;
			CursorDiagnosticLogger.Record("window_position_fallback", "not_needed_window_already_moved", null, null, new { windowHandle = session.WindowHandle.ToInt64(), dx = distance.X, dy = distance.Y });
			return false;
		}

		var now = DateTime.UtcNow;
		if (!session.FallbackCheckPending)
		{
			// Give the title-bar drag a short window to respond. Scaled with the swipe so fast motions get enough time.
			var speed = Math.Sqrt((double)distance.X * distance.X + (double)distance.Y * distance.Y);
			var waitMs = (int)Math.Clamp(90 + speed * 3, 90, 220);
			session.FallbackCheckPending = true;
			session.FallbackCheckDeadlineUtc = now.AddMilliseconds(waitMs);
			session.PendingFallbackHorizontalMovement = distance.X;
			session.PendingFallbackVerticalMovement = distance.Y;
			session.PendingFallbackStartPosition = TryGetWindowBounds(session.WindowHandle, out var startBounds) ? new Point(startBounds.Left, startBounds.Top) : Point.Empty;
			CursorDiagnosticLogger.Record("window_position_fallback", "waiting_for_title_bar_drag", null, null, new { windowHandle = session.WindowHandle.ToInt64(), dx = distance.X, dy = distance.Y, waitMs });
			return false;
		}

		session.PendingFallbackHorizontalMovement += distance.X;
		session.PendingFallbackVerticalMovement += distance.Y;
		if (now < session.FallbackCheckDeadlineUtc) return false;

		// The title-bar drag did not respond. End any native move loop so it does not run alongside SetWindowPos.
		// The timeout keeps a hung app from blocking.
		SendMessageTimeout(session.WindowHandle, WM_CANCELMODE, 0, 0, SMTO_ABORTIFHUNG, 50, out _);
		MouseInject.ReleaseNativeTitleBarDrag();
		if ((GetWindowLong(session.WindowHandle, GwlStyle) & GwsMaximize) != 0)
		{
			_ = ShowWindow(session.WindowHandle, SwRestore);
			if ((GetWindowLong(session.WindowHandle, GwlStyle) & GwsMaximize) == 0 && TryGetWindowBounds(session.WindowHandle, out var restoredBounds))
			{
				session.InitialBounds = restoredBounds;
				session.LastObservedBounds = restoredBounds;
				session.PendingFallbackStartPosition = new Point(restoredBounds.Left, restoredBounds.Top);
				CursorDiagnosticLogger.Record("window_position_fallback", "maximized_window_restore_requested", null, null, new { windowHandle = session.WindowHandle.ToInt64(), bounds = restoredBounds.ToString() });
			}
		}
		session.UsingWindowPositionFallback = true;
		session.FallbackCheckPending = false;
		CursorDiagnosticLogger.Record("window_position_fallback", "activated", null, null,
			new
			{
				windowHandle = session.WindowHandle.ToInt64(),
				dx = distance.X,
				dy = distance.Y,
				pendingDx = session.PendingFallbackHorizontalMovement,
				pendingDy = session.PendingFallbackVerticalMovement
			});
		// The title-bar drag may already have moved the window during the wait. Replay only the part it did not move.
		var nativeDx = 0;
		var nativeDy = 0;
		if (TryGetWindowBounds(session.WindowHandle, out var nowBounds))
		{
			nativeDx = nowBounds.Left - session.PendingFallbackStartPosition.X;
			nativeDy = nowBounds.Top - session.PendingFallbackStartPosition.Y;
		}
		Point pendingMovement = new(
			(int)Math.Round(session.PendingFallbackHorizontalMovement, MidpointRounding.AwayFromZero) - nativeDx,
			(int)Math.Round(session.PendingFallbackVerticalMovement, MidpointRounding.AwayFromZero) - nativeDy);
		if (pendingMovement.X != 0 || pendingMovement.Y != 0) MoveWindow(session, pendingMovement);
		session.PendingFallbackHorizontalMovement = 0;
		session.PendingFallbackVerticalMovement = 0;
		return true;
	}

	/// <summary>True while a fallback decision is waiting for its response window to expire.</summary>
	public static bool IsWindowPositionFallbackPending(DragSession session) => session.FallbackCheckPending && !session.UsingWindowPositionFallback;

	/// <summary>Makes the fallback decision once the response window has expired. Does nothing before then.</summary>
	public static bool TryCompleteWindowPositionFallback(DragSession session)
	{
		if (!session.FallbackCheckPending || DateTime.UtcNow < session.FallbackCheckDeadlineUtc) return false;
		return TryActivateWindowPositionFallback(session, Point.Empty);
	}
}
