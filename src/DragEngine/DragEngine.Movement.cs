namespace ThreeFingerDrag;

internal sealed partial class DragEngine
{
	/// <summary>
	/// Applies movement from an input adapter. If its device already moved the cursor, the shared
	/// title-bar drag is observed for a short interval before the same SetWindowPos fallback is used.
	/// </summary>
	/// <returns>True when this session is moving its window directly.</returns>
	public bool Move(object owner, Point distance, bool cursorAlreadyMoved)
	{
		lock (sync)
		{
			var current = state;
			if (current is null || !ReferenceEquals(current.Owner, owner)) return false;

			var session = current.Session;
			if (session is null) return false;

			if (!CursorDiagnosticLogger.IsEnabled) return MoveSession(owner, session, distance, cursorAlreadyMoved);

			// Sample the cursor and window positions before and after the move, for speed diagnostics.
			var cursorBefore = MouseInject.TryGetCursorPoint(out var cursorPoint) ? cursorPoint : Point.Empty;
			var hasWindowBefore = WindowDragController.TryGetWindowBounds(session.WindowHandle, out var windowBefore);
			var moved = MoveSession(owner, session, distance, cursorAlreadyMoved);
			// Only successful engine moves are sampled, so a fallback path is not counted twice.
			if (moved)
			{
				var cursorAfter = MouseInject.TryGetCursorPoint(out var afterCursor) ? afterCursor : cursorBefore;
				var hasWindowAfter = WindowDragController.TryGetWindowBounds(session.WindowHandle, out var windowAfter);
				TrackMovement(distance, cursorBefore, cursorAfter, hasWindowBefore && hasWindowAfter ? windowBefore : null, hasWindowAfter ? windowAfter : null, cursorAlreadyMoved);
			}
			return moved;
		}
	}

	/// <summary>
	/// Picks the movement path. Position-only sessions and sessions already on fallback move directly.
	/// Otherwise a cursor-driven move waits for the title-bar drag, then uses SetWindowPos if it stays unresponsive.
	/// </summary>
	private bool MoveSession(object owner, WindowDragController.DragSession session, Point distance, bool cursorAlreadyMoved)
	{
		if (session.MoveWithoutForeground || session.UsingWindowPositionFallback)
		{
			fallbackTimer.Stop();
			WindowDragController.MoveWindow(session, distance);
			return true;
		}

		// Try the title-bar fallback when the device moved the cursor, the move has a vertical part,
		// or a fallback check is already pending.
		if ((cursorAlreadyMoved || distance.Y != 0 || WindowDragController.IsWindowPositionFallbackPending(session)) && WindowDragController.TryActivateWindowPositionFallback(session, distance))
		{
			fallbackTimer.Stop();
			ShowForcedWindowModeIndicator();
			CursorDiagnosticLogger.Record("drag_movement", "position_fallback_engaged", details: new { source = ownerSource, distanceX = distance.X, distanceY = distance.Y, cursorAlreadyMoved });
			return true;
		}
		// While the response window is open, the timer checks its deadline.
		if (WindowDragController.IsWindowPositionFallbackPending(session))
		{
			if (!fallbackTimer.Enabled) CursorDiagnosticLogger.Record("drag_movement", "fallback_timer_started", details: new { source = ownerSource });
			fallbackTimer.Start();
		}

		return CompletePendingFallback(owner);
	}

	/// <summary>
	/// Finishes a fallback whose response window has expired. Returns true when the session is
	/// on the SetWindowPos path, either already or after this call.
	/// </summary>
	public bool CompletePendingFallback(object owner)
	{
		lock (sync)
		{
			var current = state;
			if (current is null || !ReferenceEquals(current.Owner, owner)) return false;

			var session = current.Session;
			if (session is null) return false;

			if (session.UsingWindowPositionFallback) return true;

			if (!WindowDragController.TryCompleteWindowPositionFallback(session)) return false;
			fallbackTimer.Stop();
			ReleaseSyntheticDragButtons();
			ShowForcedWindowModeIndicator();
			CursorDiagnosticLogger.Record("drag_movement", "fallback_completed_pending", details: new { source = ownerSource });
			return true;
		}
	}

	/// <summary>Shows the forced window mode indicator when the drag switches to direct window positioning.</summary>
	private static void ShowForcedWindowModeIndicator()
	{
		var point = MouseInject.TryGetCursorPoint(out var cursorPoint) ? cursorPoint : Point.Empty;
		DragModeIndicatorOverlay.ShowForcedWindowMode(point);
	}
}
