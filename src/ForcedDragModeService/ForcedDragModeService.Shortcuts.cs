namespace ThreeFingerDrag;

internal static partial class ForcedDragModeService
{
	private static bool previousSwitcherShortcutAllKeysDown;

	/// <summary>Polls the window-move arrows and the mode-switcher shortcut once per tick.</summary>
	private static void PollShortcuts()
	{
		try
		{
			PollWindowMoveKeys();
			if (KeyNameParser.TryParseShortcut(PointerSettings.GetDragModeSwitcherShortcut(), out var switcherKeys))
			{
				var allDown = AreAllKeysDown(switcherKeys);
				// Toggle on the rising edge only. Holding the shortcut must not flip the mode on every tick.
				var isNewPress = allDown && !previousSwitcherShortcutAllKeysDown;
				previousSwitcherShortcutAllKeysDown = allDown;
				if (isNewPress)
				{
					CursorDiagnosticLogger.Record("forced_drag_mode", "switcher_shortcut_pressed", details: new { keyCount = switcherKeys.Length });
					ToggleDragModeSwitcher();
				}
			}
		}
		catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
		{
			CursorDiagnosticLogger.Record("forced_drag_mode", "exception", details: new { phase = "poll_shortcuts", exceptionType = ex.GetType().FullName, message = ex.Message });
		}
	}

	/// <summary>True when every key in the shortcut is down. An empty shortcut never matches.</summary>
	private static bool AreAllKeysDown(int[] keys)
	{
		if (keys.Length == 0) return false;

		foreach (var key in keys) if (!IsKeyDown(key)) return false;

		return true;
	}

	/// <summary>Advances the switcher None, Window, NonWindow, then back to None, and plays feedback.</summary>
	private static void ToggleDragModeSwitcher()
	{
		bool newState;
		lock (StateLock)
		{
			shortcutMode = shortcutMode switch
			{
				ForcedDragMode.None => ForcedDragMode.Window,
				ForcedDragMode.Window => ForcedDragMode.NonWindow,
				_ => ForcedDragMode.None,
			};
			newState = shortcutMode != ForcedDragMode.None;
		}

		CursorDiagnosticLogger.Record("forced_drag_mode", "switcher_mode_changed", details: new { mode = shortcutMode.ToString(), active = newState });
		NotifyToggle(newState);
	}

	/// <summary>Plays the toggle sound when the user has enabled it.</summary>
	private static void NotifyToggle(bool isNowActive)
	{
		if (PointerSettings.IsForcedModeToggleSoundEnabled()) SoundFeedback.PlayForcedModeToggle(isNowActive);
	}
}
