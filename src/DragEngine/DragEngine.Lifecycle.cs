namespace ThreeFingerDrag;

internal sealed partial class DragEngine
{
	/// <summary>
	/// Starts a drag for <paramref name="owner"/>. The gesture is claimed even when classification yields no session, so other owners stay blocked until it ends.
	/// </summary>
	/// <param name="owner">The object that claims the gesture.</param>
	/// <param name="startedSession">The session created for the drag, or null when classification yields none.</param>
	/// <param name="physicalButtonAlreadyHeld">True when the real mouse button is down, so no synthetic press is needed.</param>
	public bool TryStartDrag(object owner, out WindowDragController.DragSession? startedSession, bool physicalButtonAlreadyHeld = false)
	{
		lock (sync)
		{
			startedSession = null;
			if (state is not null || starting)
			{
				CursorDiagnosticLogger.Record("drag_lifecycle", "start_rejected", details: new { source = owner.GetType().Name, reason = state is not null ? "drag_already_active" : "start_in_progress" });
				return false;
			}
			// Apply queued fixes for out-of-range numeric settings before the drag begins.
			PointerSettings.ApplyPendingNumericCorrections();
			starting = true;
		}

		// Classification can wait on UI Automation, so it runs outside the lock. The starting flag
		// keeps other callers from starting a second drag meanwhile.
		WindowDragController.DragSession? session;
		try
		{
			session = WindowDragController.TryStartDrag();
		}
		catch (Exception ex)
		{
			lock (sync) starting = false;
			CursorDiagnosticLogger.Record("drag_lifecycle", "exception", details: new { phase = "classification", source = owner.GetType().Name, exceptionType = ex.GetType().FullName, message = ex.Message });
			throw;
		}

		lock (sync)
		{
			starting = false;
			// Store the result even when session is null. The gesture then still blocks other owners.
			state = new DragState(owner, session);
			startedSession = session;
			ownerSource = owner.GetType().Name;
			ResetStatistics();
			dragClock.Restart();
			CursorDiagnosticLogger.Record("drag_lifecycle", session is null ? "claimed_without_session" : "started", details: new { source = ownerSource, sessionStarted = session is not null, moveWithoutForeground = session?.MoveWithoutForeground, physicalButtonAlreadyHeld });
			// Press the synthetic drag buttons only when the real button is up and the window must be
			// dragged with the mouse. Position-only sessions never need a button press.
			if (!physicalButtonAlreadyHeld && session?.MoveWithoutForeground != true)
			{
				MouseInject.SetDragButtons(PointerSettings.GetTouchDragButtons(), true);
				syntheticButtonsDown = true;
				CursorDiagnosticLogger.Record("drag_lifecycle", "synthetic_buttons_pressed", details: new { source = ownerSource });
			}
			return true;
		}
	}

	/// <summary>Ends the drag owned by <paramref name="owner"/> and records why it stopped.</summary>
	public void EndDrag(object owner, string reason = "unspecified")
	{
		lock (sync)
		{
			var current = state;
			if (current is not null && ReferenceEquals(current.Owner, owner)) EndCurrentDrag(reason);
			else if (current is not null) CursorDiagnosticLogger.Record("drag_lifecycle", "end_ignored_not_owner", details: new { reason, source = owner.GetType().Name, activeSource = ownerSource });
		}
	}

	/// <summary>Clears the active drag and releases the synthetic buttons. Logs the totals first.</summary>
	private void EndCurrentDrag(string reason)
	{
		lock (sync)
		{
			var current = state;
			if (current is null) return;

			fallbackTimer.Stop();
			dragClock.Stop();
			// Drop queued window movement so the window stops where it was when the fingers lifted.
			if (current.Session is not null) WindowDragController.CancelSmoothedMovement(current.Session);
			var durationMs = dragClock.Elapsed.TotalMilliseconds;
			var seconds = durationMs / 1000.0;
			// Guard against a zero-length drag so the averages do not divide by zero.
			double AverageSpeed(double px) => seconds > 0 ? Math.Round(px / seconds, 1) : 0;
			// windowMoved is true only when a session existed and the window actually moved.
			CursorDiagnosticLogger.Record("drag_end", reason, details: new
			{
				source = ownerSource,
				reason,
				durationMs = Math.Round(durationMs, 1),
				sampleCount,
				totalInputPx = Math.Round(totalInputPx, 1),
				totalCursorPx = Math.Round(totalCursorPx, 1),
				totalWindowPx = Math.Round(totalWindowPx, 1),
				avgInputSpeedPxPerSec = AverageSpeed(totalInputPx),
				avgCursorSpeedPxPerSec = AverageSpeed(totalCursorPx),
				avgWindowSpeedPxPerSec = AverageSpeed(totalWindowPx),
				zeroCursorSamples,
				zeroWindowSamples,
				windowMoved = current.Session is not null && totalWindowPx > 0,
			});
			// Release the buttons before clearing state. A stuck button would keep a drag going
			// in the system after the engine has forgotten about it.
			ReleaseSyntheticDragButtons();
			state = null;
			// Play the release sound for every input source, so it does not depend on the touchpad path.
			if (PointerSettings.IsDragReleaseSoundEnabled()) SoundFeedback.PlayDragRelease();
		}
	}

	/// <summary>Releases the synthetic drag buttons if they are held.</summary>
	private void ReleaseSyntheticDragButtons()
	{
		if (!syntheticButtonsDown) return;
		MouseInject.SetDragButtons(PointerSettings.GetTouchDragButtons(), false);
		syntheticButtonsDown = false;
		CursorDiagnosticLogger.Record("drag_lifecycle", "synthetic_buttons_released", details: new { source = ownerSource });
	}
}
