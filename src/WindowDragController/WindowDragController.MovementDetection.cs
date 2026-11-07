using System.Runtime.InteropServices;

namespace ThreeFingerDrag;

internal static partial class WindowDragController
{
	/// <summary>Compares the window's current position with the last one seen, and updates that record.</summary>
	public static bool CheckWindowMoved(DragSession session)
	{
		if (!TryGetWindowBounds(session.WindowHandle, out var currentBounds))
		{
			CursorDiagnosticLogger.Record("window_position_check", "GetWindowRect_failed", null, null, new { windowHandle = session.WindowHandle.ToInt64(), error = Marshal.GetLastWin32Error() });
			return false;
		}

		var moved = currentBounds.Left != session.LastObservedBounds.Left || currentBounds.Top != session.LastObservedBounds.Top;
		CursorDiagnosticLogger.Record("window_position_check", "CheckWindowMoved",
			new Point(session.LastObservedBounds.Left, session.LastObservedBounds.Top),
			new Point(currentBounds.Left, currentBounds.Top),
			new { windowHandle = session.WindowHandle.ToInt64(), moved, bounds = currentBounds.ToString() });
		session.LastObservedBounds = currentBounds;
		return moved;
	}
}
