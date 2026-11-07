using static ThreeFingerDrag.NativeMethods;

namespace ThreeFingerDrag;

internal static partial class WindowDragController
{
	/// <summary>Scales an input distance by the window speed setting and queues it for smoothed movement.</summary>
	public static void MoveWindow(DragSession session, Point distance, string? inputSource = null, Point? inputPoint = null)
	{
		if (distance.X == 0 && distance.Y == 0) return;

		// Window position moves use their own speed setting, separate from the system cursor speed.
		var speed = PointerSettings.GetWindowPositionSpeed();
		var horizontalMovement = distance.X * speed;
		var verticalMovement = distance.Y * speed;
		if (horizontalMovement == 0 && verticalMovement == 0) return;

		QueueSmoothedMovement(session, horizontalMovement, verticalMovement);
	}

	/// <summary>Moves the foreground window by a fixed offset. Used by the arrow-key window move.</summary>
	public static bool NudgeForegroundWindow(int dx, int dy)
	{
		var hwnd = GetForegroundWindow();
		// Skip the shell, topmost windows and maximized windows. Moving them by hand is not expected.
		if (hwnd == 0 || hwnd == GetShellWindow() || (GetWindowLong(hwnd, GwlExStyle) & WsExTopMost) != 0) return false;
		if (!GetWindowRect(hwnd, out var rect) || (GetWindowLong(hwnd, GwlStyle) & GwsMaximize) != 0) return false;

		var targetLeft = rect.Left + dx;
		var targetTop = rect.Top + dy;
		if (PointerSettings.ShouldKeepWindowWithinScreen())
		{
			// Same limit as smoothed mouse movement: the combined bounds of all monitors.
			var limits = GetWindowPositionLimits(hwnd, rect.Right - rect.Left, rect.Bottom - rect.Top);
			targetLeft = ClampToRange(targetLeft, limits.MinLeft, limits.MaxLeft);
			targetTop = ClampToRange(targetTop, limits.MinTop, limits.MaxTop);
		}

		var succeeded = SetWindowPos(hwnd, 0, targetLeft, targetTop, 0, 0, SwpNoSize | SwpNoZOrder | SwpNoActivate);
		if (succeeded) CursorDiagnosticLogger.Record("window_move", "keyboard_nudge", null, null, new { windowHandle = hwnd.ToInt64(), dx, dy, targetLeft, targetTop });

		return succeeded;
	}
}
