namespace ThreeFingerDrag;

internal static partial class ForcedDragModeService
{
	private static DateTime lastWindowNudgeUtc;
	private static DateTime lastArrowPressUtc;
	private static int arrowPressCount;
	private static bool leftArrowWasDown;
	private static bool rightArrowWasDown;
	private static bool upArrowWasDown;
	private static bool downArrowWasDown;
	private static bool windowMoveModifierHeld;
	private static bool lastWindowNudgeSucceeded = true;
	private static TimeSpan ArrowSequenceTimeout => TimeSpan.FromMilliseconds(PointerSettings.GetWindowMoveArrowSequenceTimeoutMs());
	private static TimeSpan WindowNudgeInterval => TimeSpan.FromMilliseconds(PointerSettings.GetWindowMoveNudgeIntervalMs());

	/// <summary>
	/// Moves the foreground window with the arrow keys while the window-move modifier is held.
	/// Repeated presses within 500 ms speed up the step, up to 10 presses.
	/// </summary>
	private static void PollWindowMoveKeys()
	{
		var modifierParsed = KeyNameParser.TryParseKey(PointerSettings.GetWindowMoveModifierKey(), out var modifier);
		var modifierHeld = modifierParsed && IsKeyDown(modifier);
		if (modifierHeld != windowMoveModifierHeld)
		{
			windowMoveModifierHeld = modifierHeld;
			CursorDiagnosticLogger.Record("window_move", modifierHeld ? "modifier_pressed" : "modifier_released", details: new { modifierParsed });
		}

		if (!modifierHeld)
		{
			ResetArrowSequence();
			return;
		}

		var now = DateTime.UtcNow;
		// Virtual-key codes for the arrow keys: left 0x25, up 0x26, right 0x27, down 0x28.
		var leftDown = IsKeyDown(0x25);
		var upDown = IsKeyDown(0x26);
		var rightDown = IsKeyDown(0x27);
		var downDown = IsKeyDown(0x28);
		// Count only key-down edges, so a held arrow does not add to the acceleration count.
		var newPressCount = (leftDown && !leftArrowWasDown ? 1 : 0) + (rightDown && !rightArrowWasDown ? 1 : 0) + (upDown && !upArrowWasDown ? 1 : 0) + (downDown && !downArrowWasDown ? 1 : 0);

		// The sequence restarts after a pause longer than 500 ms. Otherwise the step keeps growing.
		if (newPressCount > 0)
		{
			if (now - lastArrowPressUtc > ArrowSequenceTimeout && arrowPressCount != 0)
			{
				CursorDiagnosticLogger.Record("window_move", "arrow_sequence_restarted", details: new { previousPressCount = arrowPressCount });
				arrowPressCount = 0;
			}

			arrowPressCount = Math.Min(MaximumArrowAcceleration, arrowPressCount + newPressCount);
			lastArrowPressUtc = now;
		} else if (!leftDown && !rightDown && !upDown && !downDown && now - lastArrowPressUtc > ArrowSequenceTimeout)
		{
			if (arrowPressCount != 0)
			{
				CursorDiagnosticLogger.Record("window_move", "arrow_sequence_expired", details: new { previousPressCount = arrowPressCount });
			}

			arrowPressCount = 0;
		}

		leftArrowWasDown = leftDown;
		rightArrowWasDown = rightDown;
		upArrowWasDown = upDown;
		downArrowWasDown = downDown;

		var dx = (leftDown ? -1 : 0) + (rightDown ? 1 : 0);
		var dy = (upDown ? -1 : 0) + (downDown ? 1 : 0);
		if (dx == 0 && dy == 0) return;

		// Limit nudges to about 22 per second. Faster polling would make the window move in jerks.
		if (now - lastWindowNudgeUtc < WindowNudgeInterval) return;

		lastWindowNudgeUtc = now;
		// The step grows with the press count, so a quick run of presses covers more distance.
		var step = PointerSettings.GetWindowMoveStepPixels() * Math.Max(arrowPressCount, 1);
		var nudged = WindowDragController.NudgeForegroundWindow(dx * step, dy * step);
		if (nudged != lastWindowNudgeSucceeded)
		{
			lastWindowNudgeSucceeded = nudged;
			CursorDiagnosticLogger.Record("window_move", nudged ? "nudge_recovered" : "nudge_failed", details: new { dx = dx * step, dy = dy * step });
		}
	}

	/// <summary>Clears the arrow-key acceleration state.</summary>
	private static void ResetArrowSequence()
	{
		if (arrowPressCount != 0)
		{
			CursorDiagnosticLogger.Record("window_move", "arrow_sequence_reset", details: new { previousPressCount = arrowPressCount });
		}

		arrowPressCount = 0;
		lastArrowPressUtc = DateTime.MinValue;
		leftArrowWasDown = false;
		rightArrowWasDown = false;
		upArrowWasDown = false;
		downArrowWasDown = false;
	}
}
