using System.Timers;

namespace ThreeFingerDrag;

public sealed partial class TouchProcessor
{
	/// <summary>
	/// Runs on every packet and mouse report. Pins the cursor as the drop anchor once it has
	/// stayed still for the hold time. Real movement releases the anchor.
	/// </summary>
	private void UpdateDragDropAnchorHoldState()
	{
		if (state != State.THREE_FINGER_DRAG && !dragDropAnchorCursorPoint.HasValue) return;
		if (!MouseInject.TryGetCursorPoint(out var cursorPoint)) return;

		// First sample. Start the stillness timer.
		if (!lastObservedCursorPointForAnchor.HasValue)
		{
			lastObservedCursorPointForAnchor = cursorPoint;
			dragDropAnchorStableSinceTickMs = Environment.TickCount64;
			return;
		}

		if (cursorPoint != lastObservedCursorPointForAnchor.Value)
		{
			// Compare squared values to avoid a square root.
			var distanceSquared = (cursorPoint.X - lastObservedCursorPointForAnchor.Value.X) * (cursorPoint.X - lastObservedCursorPointForAnchor.Value.X) + (cursorPoint.Y - lastObservedCursorPointForAnchor.Value.Y) * (cursorPoint.Y - lastObservedCursorPointForAnchor.Value.Y);

			// Real external movement releases the anchor.
			if (distanceSquared > DragDropAnchorMovementResetThresholdPx * DragDropAnchorMovementResetThresholdPx)
			{
				CursorDiagnosticLogger.Record("touch_anchor", "released_external_movement", before: dragDropAnchorCursorPoint, after: cursorPoint, details: new { distanceSquared, thresholdPx = DragDropAnchorMovementResetThresholdPx });
				ClearDragDropAnchorState();
				return;
			}

			lastObservedCursorPointForAnchor = cursorPoint;
			dragDropAnchorStableSinceTickMs = Environment.TickCount64;
			return;
		}

		if (dragDropAnchorStableSinceTickMs is null)
		{
			dragDropAnchorStableSinceTickMs = Environment.TickCount64;
			return;
		}

		// Pin only after the cursor has been still for the configured hold time.
		if (Environment.TickCount64 - dragDropAnchorStableSinceTickMs.Value < PointerSettings.GetTouchpadDragDropAnchorHoldMs()) return;

		if (!dragDropAnchorCursorPoint.HasValue)
		{
			dragDropAnchorCursorPoint = cursorPoint;
			// Measure finger travel from the centroid at the moment the anchor is set.
			anchorTouchCenter = previousCenterPoint;
			accumulatedAnchorTouchDistance = 0;
			CursorDiagnosticLogger.Record("touch_anchor", "pinned", after: cursorPoint, details: new { holdMs = PointerSettings.GetTouchpadDragDropAnchorHoldMs(), state = state.ToString() });
			ReapplyDragDropAnchor();
		}
	}

	/// <summary>Restores the pinned drop point while the post-lift grace period is active.</summary>
	private void ReapplyDragDropAnchor()
	{
		if (dragDropAnchorCursorPoint.HasValue) _ = MouseInject.TrySetCursorPoint(dragDropAnchorCursorPoint.Value);
	}

	/// <summary>Clears the drop anchor when all fingers leave the pad.</summary>
	private void ReleaseDragDropAnchor()
	{
		CursorDiagnosticLogger.Record("touch_anchor", "released_all_fingers_lifted", details: new { hadAnchor = dragDropAnchorCursorPoint.HasValue });
		dragDropAnchorHoldTimer.Stop();
		ClearDragDropAnchorState();
	}

	/// <summary>Timer callback. Releases the drop anchor when the post-lift grace time runs out.</summary>
	private void DragDropAnchorHoldTimerElapsed(object? sender, ElapsedEventArgs e)
	{
		CursorDiagnosticLogger.Record("touch_anchor", "hold_expired", details: new { hadAnchor = dragDropAnchorCursorPoint.HasValue });
		ClearDragDropAnchorState();
	}

	/// <summary>Clears all anchor, flick, and centroid tracking state.</summary>
	private void ClearDragDropAnchorState()
	{
		CursorDiagnosticLogger.Record("touch_anchor", "cleared", details: new { hadAnchor = dragDropAnchorCursorPoint.HasValue, accumulatedAnchorTouchDistance });
		dragDropAnchorCursorPoint = null;
		lastObservedCursorPointForAnchor = null;
		dragDropAnchorStableSinceTickMs = null;
		anchorTouchCenter = null;
		accumulatedAnchorTouchDistance = 0;
		recentCleanCursorPoint = null;
		previousAppliedCursorPoint = null;

		// Background-window drags do not release a mouse button, so clear the saved point explicitly.
		MouseInject.ClearLastAppliedCursorPoint();
	}
}
