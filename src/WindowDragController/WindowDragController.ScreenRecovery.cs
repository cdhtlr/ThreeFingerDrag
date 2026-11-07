using static ThreeFingerDrag.NativeMethods;

namespace ThreeFingerDrag;

internal static partial class WindowDragController
{
	/// <summary>
	/// Moves visible windows that are outside the desktop back onto the screen under the cursor, centered.
	/// Minimized and maximized windows are skipped.
	/// </summary>
	public static void ReturnOutOfScreenWindowsToCursorScreen()
	{
		var workArea = Screen.FromPoint(Cursor.Position).WorkingArea;
		var desktop = GetDesktopBounds();
		var shellWindow = GetShellWindow();

		_ = EnumWindows((hwnd, _) =>
		{
			if (hwnd == shellWindow || !IsWindowVisible(hwnd) || IsIconic(hwnd)) return true;
			if ((GetWindowLong(hwnd, GwlStyle) & GwsMaximize) != 0) return true;
			if (DwmGetWindowAttribute(hwnd, DwmwaExtendedFrameBounds, out var frame, (uint)System.Runtime.InteropServices.Marshal.SizeOf<RECT>()) != 0) return true;

			var width = frame.Right - frame.Left;
			var height = frame.Bottom - frame.Top;
			if (width <= 0 || height <= 0) return true;

			var insideDesktop = frame.Left >= desktop.Left && frame.Top >= desktop.Top && frame.Right <= desktop.Right && frame.Bottom <= desktop.Bottom;
			if (insideDesktop || !GetWindowRect(hwnd, out var rect)) return true;

			// Center the visible frame, then shift the outer rectangle by the same amount so the frame lands in the right place.
			var targetLeft = Math.Max(workArea.Left, workArea.Left + (workArea.Width - width) / 2);
			var targetTop = Math.Max(workArea.Top, workArea.Top + (workArea.Height - height) / 2);
			var dx = targetLeft - frame.Left;
			var dy = targetTop - frame.Top;

			var moved = SetWindowPos(hwnd, 0, rect.Left + dx, rect.Top + dy, 0, 0, SwpNoSize | SwpNoZOrder | SwpNoActivate);
			if (CursorDiagnosticLogger.IsEnabled) CursorDiagnosticLogger.Record("window_move", moved ? "returned_to_cursor_screen" : "return_failed", null, null, new { windowHandle = hwnd.ToInt64(), dx, dy });
			return true;
		}, 0);
	}
}
