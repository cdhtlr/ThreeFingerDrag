using static ThreeFingerDrag.NativeMethods;

namespace ThreeFingerDrag;

/// <summary>Sends pointer input and guards the cursor against conflicting physical movement.</summary>
internal static partial class MouseInject
{
	/// <summary>Bit flags for the buttons that can be held during a drag. One bit per button.</summary>
	[Flags]
	private enum DragButtons : uint
	{
		None = 0,
		Left = 1,
		Right = 2,
		Middle = 4,
		X1 = 8,
		X2 = 16,
	}

	private static bool leftButtonPressed;
	// Buttons currently held by SetDragButtons. Only these are released later.
	private static DragButtons pressedDragButtons;
	// Sub-pixel movement carried between Move calls, so slow motion is not lost to rounding.
	private static int pendingHorizontalTenths;
	private static int pendingVerticalTenths;


	/// <summary>Presses or releases the left button. Setting the current value again does nothing.</summary>
	public static bool LeftButtonPressed
	{
		get => leftButtonPressed;
		set
		{
			if (leftButtonPressed == value) return;

			mouse_event(value ? MouseEventFlags.LEFTDOWN : MouseEventFlags.LEFTUP);
			CursorDiagnosticLogger.Record("mouse_inject_left_button", value ? "press_sent" : "release_sent");
			leftButtonPressed = value;

			// Releasing also drops the pending sub-pixel movement and the cursor anchor, so the next drag starts clean.
			if (!value)
			{
				pendingHorizontalTenths = 0;
				pendingVerticalTenths = 0;
				LastAppliedCursorPoint = null;
			}
		}
	}

	/// <summary>Presses or releases the buttons named in a configuration such as Left+Right.</summary>
	public static void SetDragButtons(string configuration, bool pressed)
	{
		var buttons = ParseDragButtons(configuration);
		if (pressed)
		{
			// Send down events only for buttons not already held, so a repeated call does not double-press.
			var newlyPressed = buttons & ~pressedDragButtons;
			SendDragButtonEvents(newlyPressed, true);
			CursorDiagnosticLogger.Record("mouse_inject_drag_buttons", "press", details: new { configuration, buttons = buttons.ToString(), newlyPressed = newlyPressed.ToString() });
			pressedDragButtons |= buttons;
			leftButtonPressed = buttons.HasFlag(DragButtons.Left);
		} else
		{
			// Release everything we pressed, not only the buttons named in this configuration.
			var released = pressedDragButtons;
			SendDragButtonEvents(released, false);
			CursorDiagnosticLogger.Record("mouse_inject_drag_buttons", "release", details: new { configuration, released = released.ToString() });
			pressedDragButtons = DragButtons.None;
			leftButtonPressed = false;
			pendingHorizontalTenths = pendingVerticalTenths = 0;
			LastAppliedCursorPoint = null;
		}
	}

	/// <summary>Sends a left button release so a native title-bar move loop ends. Used when SetWindowPos takes over.</summary>
	public static void ReleaseNativeTitleBarDrag()
	{
		mouse_event(MouseEventFlags.LEFTUP);
		CursorDiagnosticLogger.Record("mouse_inject_title_bar_release", "left_up_sent");
	}

	/// <summary>Replays a trigger click that ended without becoming a drag.</summary>
	public static void ReplayMouseClick(string button)
	{
		CursorDiagnosticLogger.Record("mouse_inject_replay_click", "start", details: new { button });
		switch (button)
		{
			case "Left":
				mouse_event(MouseEventFlags.LEFTDOWN);
				mouse_event(MouseEventFlags.LEFTUP);
				break;
			case "Right":
				mouse_event(MouseEventFlags.RIGHTDOWN);
				mouse_event(MouseEventFlags.RIGHTUP);
				break;
			case "Middle":
				mouse_event(MouseEventFlags.MIDDLEDOWN);
				mouse_event(MouseEventFlags.MIDDLEUP);
				break;
			case "X1":
			case "X2":
				// mouse_event takes the X button number (1 or 2) in dwData.
				var data = button == "X1" ? 1u : 2u;
				mouse_event(MouseEventFlags.XDOWN, dwData: data);
				mouse_event(MouseEventFlags.XUP, dwData: data);
				break;
			default:
				CursorDiagnosticLogger.Record("mouse_inject_replay_click", "unsupported_button", details: new { button });
				break;
		}
	}

	/// <summary>Clicks the current cursor location to focus the precise drop target.</summary>
	public static bool ClickAtCursor(string source = "cursor", Point? inputPoint = null)
	{
		if (!TryGetCursorPoint(out var point))
		{
			CursorDiagnosticLogger.Record("mouse_inject_click_at_cursor", "cursor_read_failed", details: new { source });
			return false;
		}
		// Use the button the system treats as primary, so swapped-button settings are respected.
		var primaryButton = GetSystemMetrics(23) != 0 ? "right" : "left"; // SM_SWAPBUTTON
		var down = primaryButton == "right" ? MouseEventFlags.RIGHTDOWN : MouseEventFlags.LEFTDOWN;
		var up = primaryButton == "right" ? MouseEventFlags.RIGHTUP : MouseEventFlags.LEFTUP;
		mouse_event(down);
		mouse_event(up);
		CursorDiagnosticLogger.Record("ocr_drop", "target_clicked_at_cursor", point, point, new { button = primaryButton, source, inputPosition = inputPoint });
		return true;
	}

	/// <summary>Parses +-separated button names into flags. Unknown names are ignored.</summary>
	private static DragButtons ParseDragButtons(string configuration)
	{
		var buttons = DragButtons.None;
		foreach (var button in configuration.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			var flag = button.ToLowerInvariant() switch
			{
				"left" => DragButtons.Left,
				"right" => DragButtons.Right,
				// Secondary is the non-primary button, taking the swap setting into account.
				"secondary" => GetSystemMetrics(23) != 0 ? DragButtons.Left : DragButtons.Right,
				"middle" => DragButtons.Middle,
				"x1" or "back" or "button4" => DragButtons.X1,
				"x2" or "forward" or "button5" => DragButtons.X2,
				_ => DragButtons.None,
			};
			if (flag == DragButtons.None) CursorDiagnosticLogger.Record("mouse_inject_parse_buttons", "unknown_name_ignored", details: new { configuration, name = button });
			buttons |= flag;
		}
		// Nothing recognized: fall back to the left button, the usual drag button.
		if (buttons != DragButtons.None) return buttons;
		CursorDiagnosticLogger.Record("mouse_inject_parse_buttons", "fallback_left", details: new { configuration });
		return DragButtons.Left;
	}

	/// <summary>Sends down or up events for a set of buttons. Each X button needs its own call.</summary>
	private static void SendDragButtonEvents(DragButtons buttons, bool pressed)
	{
		MouseEventFlags flags = (MouseEventFlags)0;
		if (buttons.HasFlag(DragButtons.Left)) flags |= pressed ? MouseEventFlags.LEFTDOWN : MouseEventFlags.LEFTUP;
		if (buttons.HasFlag(DragButtons.Right))
		if (buttons.HasFlag(DragButtons.Right)) flags |= pressed ? MouseEventFlags.RIGHTDOWN : MouseEventFlags.RIGHTUP;
		if (buttons.HasFlag(DragButtons.Middle))
		if (buttons.HasFlag(DragButtons.Middle)) flags |= pressed ? MouseEventFlags.MIDDLEDOWN : MouseEventFlags.MIDDLEUP;
		// X buttons need their own dwData value, so each one is sent alone and the flags are reset after it.
		if (buttons.HasFlag(DragButtons.X1))
		{ flags |= pressed ? MouseEventFlags.XDOWN : MouseEventFlags.XUP; mouse_event(flags, dwData: 1); flags = 0; }
		if (buttons.HasFlag(DragButtons.X2))
		{ flags |= pressed ? MouseEventFlags.XDOWN : MouseEventFlags.XUP; mouse_event(flags, dwData: 2); flags = 0; }
		// Left, right, and middle events share one call.
		if (flags != 0) mouse_event(flags);
		CursorDiagnosticLogger.Record("mouse_inject_drag_events", pressed ? "press_sent" : "release_sent", details: new { buttons = buttons.ToString() });
	}
}
