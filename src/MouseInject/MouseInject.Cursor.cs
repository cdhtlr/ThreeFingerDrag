using System.Runtime.InteropServices;
using static ThreeFingerDrag.NativeMethods;

namespace ThreeFingerDrag;

internal static partial class MouseInject
{
	// Sub-pixel movement carried between Move calls is kept in MouseInject.cs.

	/// <summary>
	/// Last cursor position set by this class. It cannot include touchpad-driver movement and remains
	/// useful as a stable drop point. Cleared when the drag button is released or the anchor hold ends.
	/// </summary>
	public static Point? LastAppliedCursorPoint { get; private set; }

	/// <summary>Clears the remembered position without changing button state.</summary>
	public static void ClearLastAppliedCursorPoint()
	{
		if (LastAppliedCursorPoint is null) return;
		LastAppliedCursorPoint = null;
		CursorDiagnosticLogger.Record("mouse_inject_anchor_clear", "cleared");
	}

	/// <summary>Reads the current cursor position. Returns false when GetCursorPos fails.</summary>
	public static bool TryGetCursorPoint(out Point point)
	{
		if (!GetCursorPos(out var cursorPosition))
		{
			CursorDiagnosticLogger.Record("mouse_inject_cursor_read", "get_cursor_failed", details: new { error = Marshal.GetLastWin32Error() });
			point = default;
			return false;
		}

		point = new Point(cursorPosition.X, cursorPosition.Y);
		return true;
	}

	/// <summary>Moves the cursor to an absolute screen point and logs the move or the failure.</summary>
	public static bool TrySetCursorPoint(Point point)
	{
		// Read the old position first, so the log can show where the cursor came from.
		var hasBefore = TryGetCursorPoint(out var before);
		if (!SetCursorPos(point.X, point.Y))
		{
			CursorDiagnosticLogger.Record("cursor_set_failed", "SetCursorPos", hasBefore ? before : null, point, new { error = Marshal.GetLastWin32Error() });
			return false;
		}

		LastAppliedCursorPoint = point;
		CursorDiagnosticLogger.Record("cursor_movement", "SetCursorPos", hasBefore ? before : null, point, new { source = "application", method = "SetCursorPos", cursorMovedBy = "program", monitor = CursorDiagnosticLogger.GetMonitorAtPoint(point) });
		return true;
	}

	/// <summary>Rounds a value in tenths of a pixel to whole pixels, halves away from zero.</summary>
	internal static int RoundTenthsAwayFromZero(int tenths) => (tenths + (tenths >= 0 ? 5 : -5)) / 10;

	/// <summary>Moves the cursor by a relative distance, scaled by the speed setting. Fractions carry over between calls.</summary>
	public static void Move(Point distance)
	{
		if (distance.X == 0 && distance.Y == 0) return;

		// Scale first. Fractions stay in the pending values until they add up to a whole pixel.
		// Speed is in tenths of a pixel, so pending values stay in tenths and the math is exact integer math.
		var speed = PointerSettings.GetCursorSpeed();
		pendingHorizontalTenths += distance.X * speed;
		pendingVerticalTenths += distance.Y * speed;

		// Round half away from zero, so a 0.5 px step moves 1 px instead of being rounded to even.
		var horizontalMovement = RoundTenthsAwayFromZero(pendingHorizontalTenths);
		var verticalMovement = RoundTenthsAwayFromZero(pendingVerticalTenths);
		if (horizontalMovement == 0 && verticalMovement == 0) return;

		// Keep only the fraction that was not moved this time.
		pendingHorizontalTenths -= horizontalMovement * 10;
		pendingVerticalTenths -= verticalMovement * 10;

		// Prefer an absolute move with SetCursorPos. Fall back to a relative mouse_event if that fails.
		if (TryGetCursorPoint(out var cursorPosition) && TrySetCursorPoint(new Point(cursorPosition.X + horizontalMovement, cursorPosition.Y + verticalMovement))) return;

		// dwData is ignored for MOVE, so the value 3 has no effect.
		mouse_event(MouseEventFlags.MOVE, horizontalMovement, verticalMovement, 3);
		_ = TryGetCursorPoint(out var afterMouseMoved);
		CursorDiagnosticLogger.Record("cursor_movement", "mouse_event_move", null, afterMouseMoved, new { source = "application", method = "mouse_event", dx = horizontalMovement, dy = verticalMovement, cursorMovedBy = "program", monitor = CursorDiagnosticLogger.GetMonitorAtPoint(afterMouseMoved) });
	}
}
