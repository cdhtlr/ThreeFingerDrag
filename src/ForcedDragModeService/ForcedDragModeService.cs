using static ThreeFingerDrag.NativeMethods;

namespace ThreeFingerDrag;

/// <summary>
/// Resolves hold keys, mouse buttons, touch holds, and shortcut toggles that override drag classification.
/// Hold states are checked on demand; shortcuts are polled to detect each new press.
/// </summary>
internal static partial class ForcedDragModeService
{
	private static readonly object StateLock = new();
	private static System.Timers.Timer? shortcutPollTimer;
	private static bool isInitialized;
	private static ForcedDragMode shortcutMode;
	// Hold flags are set by input adapters while the touch, mouse, or stylus button is down.
	// Volatile, because adapters and the UI thread read them without a lock.
	private static volatile bool touchHoldForced;
	private static volatile bool mouseHoldForced;
	private static volatile bool stylusHoldForced;

	/// <summary>Starts shortcut polling once, if a shortcut or move modifier is configured.</summary>
	public static void Initialize()
	{
		lock (StateLock)
		{
			if (isInitialized) return;

			isInitialized = true;

			// Polling is needed only when a switcher shortcut or a move modifier exists. Otherwise the
			// timer stays off.
			var hasSwitcherShortcut = KeyNameParser.TryParseShortcut(PointerSettings.GetDragModeSwitcherShortcut(), out _);
			if (!hasSwitcherShortcut && string.IsNullOrWhiteSpace(PointerSettings.GetWindowMoveModifierKey()))
			{
				CursorDiagnosticLogger.Record("forced_drag_mode", "initialize_polling_skipped", details: new { hasSwitcherShortcut });
				return;
			}

			shortcutPollTimer = new System.Timers.Timer(PointerSettings.GetForcedDragModePollIntervalMs()) { AutoReset = true };
			shortcutPollTimer.Elapsed += (_, _) => PollShortcuts();
			shortcutPollTimer.Start();
			CursorDiagnosticLogger.Record("forced_drag_mode", "initialize_polling_started", details: new { hasSwitcherShortcut, pollIntervalMs = PointerSettings.GetForcedDragModePollIntervalMs() });
		}
	}

	/// <summary>Updates the touch-hold override state and plays the mode toggle sound on each change.</summary>
	public static void SetTouchHoldForced(bool forced)
	{
		if (touchHoldForced == forced) return;
		touchHoldForced = forced;
		CursorDiagnosticLogger.Record("forced_drag_mode", "touch_hold_changed", details: new { forced });
		NotifyToggle(forced);
	}

	/// <summary>Updates the mouse-hold override state and plays the mode toggle sound on each change.</summary>
	public static void SetMouseHoldForced(bool forced)
	{
		if (mouseHoldForced == forced) return;
		mouseHoldForced = forced;
		CursorDiagnosticLogger.Record("forced_drag_mode", "mouse_hold_changed", details: new { forced });
		NotifyToggle(forced);
	}

	/// <summary>Updates the stylus-hold override state and plays the mode toggle sound on each change.</summary>
	public static void SetStylusHoldForced(bool forced)
	{
		if (stylusHoldForced == forced) return;
		stylusHoldForced = forced;
		CursorDiagnosticLogger.Record("forced_drag_mode", "stylus_hold_changed", details: new { forced });
		NotifyToggle(forced);
	}

	/// <summary>
	/// Returns the highest-priority active override. Order: hold keys (window first), then
	/// touch/mouse/stylus holds, then the shortcut mode. The first match wins.
	/// </summary>
	public static ForcedDragModeResult GetForcedMode()
	{
		// Window hold key is checked before the non-window hold key, so holding both forces Window.
		if (KeyNameParser.TryParseKey(PointerSettings.GetForceWindowDragKey(), out var windowKey) && IsKeyDown(windowKey)) return new ForcedDragModeResult(ForcedDragMode.Window, "forced hold-key");

		if (KeyNameParser.TryParseKey(PointerSettings.GetForceNonWindowDragKey(), out var nonWindowKey) && IsKeyDown(nonWindowKey)) return new ForcedDragModeResult(ForcedDragMode.NonWindow, "forced hold-key");

		if (touchHoldForced || mouseHoldForced || stylusHoldForced)
		{
			// Label priority is stylus, then touch, then mouse. It only changes the text shown in diagnostics.
			var reason = stylusHoldForced ? "forced stylus hold" : touchHoldForced ? "forced touch hold" : "forced mouse hold";
			return new ForcedDragModeResult(ForcedDragMode.Window, reason, FromHold: true);
		}

		lock (StateLock)
		{
			if (shortcutMode == ForcedDragMode.Window) return new ForcedDragModeResult(ForcedDragMode.Window, "forced shortcut");
			if (shortcutMode == ForcedDragMode.NonWindow) return new ForcedDragModeResult(ForcedDragMode.NonWindow, "forced shortcut");
		}

		return ForcedDragModeResult.None;
	}

	/// <summary>Checks whether the configured cancel-drag key is down.</summary>
	public static bool IsCancelDragKeyPressed()
	{
		var pressed = KeyNameParser.TryParseKey(PointerSettings.GetDragCancelKey(), out var key) && IsKeyDown(key);
		if (pressed) CursorDiagnosticLogger.Record("forced_drag_mode", "cancel_key_pressed");
		return pressed;
	}

	/// <summary>Returns true when the key's high bit is set, meaning it is down now.</summary>
	private static bool IsKeyDown(int virtualKeyCode)
	{
		try
		{
			return (GetAsyncKeyState(virtualKeyCode) & KeyCurrentlyDownFlag) != 0;
		}
		catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
		{
			CursorDiagnosticLogger.Record("forced_drag_mode", "exception", details: new { exceptionType = ex.GetType().FullName, message = ex.Message, virtualKeyCode });
			return false;
		}
	}
}
