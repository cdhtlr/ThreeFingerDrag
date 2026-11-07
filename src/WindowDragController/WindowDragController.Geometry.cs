using System.Runtime.InteropServices;
using static ThreeFingerDrag.NativeMethods;

namespace ThreeFingerDrag;

internal static partial class WindowDragController
{
	/// <summary>Returns the combined bounds of all monitors, not only the primary one.</summary>
	internal static Rectangle GetDesktopBounds()
	{
		var screens = Screen.AllScreens;
		var left = screens.Min(s => s.Bounds.Left);
		var top = screens.Min(s => s.Bounds.Top);
		var right = screens.Max(s => s.Bounds.Right);
		var bottom = screens.Max(s => s.Bounds.Bottom);
		return Rectangle.FromLTRB(left, top, right, bottom);
	}

	// Windows larger than the desktop keep their minimum position instead of throwing.
	private static int ClampToRange(int value, int min, int max) => Math.Max(min, Math.Min(value, max));

	/// <summary>
	/// Finds the deepest window at a point, then walks up to its top-level window. Controls
	/// inside an app are not movable, so the root is the window that gets moved.
	/// </summary>
	private static bool TryGetRootWindowAtPoint(Point point, out nint windowHandle)
	{
		var windowAtPoint = WindowFromPoint(new POINT { X = point.X, Y = point.Y });
		if (windowAtPoint == 0)
		{
			windowHandle = 0;
			return false;
		}

		var rootWindow = GetAncestor(windowAtPoint, GaRoot);
		windowHandle = rootWindow == 0 ? windowAtPoint : rootWindow;
		return true;
	}

	/// <summary>Returns a diagnostic description of the top-level window under a point, for logging.</summary>
	internal static object? DescribeWindowAtPoint(Point point) => CursorDiagnosticLogger.DescribeWindow(0, point);

	/// <summary>Brings the window under the cursor to the foreground and gives it keyboard focus.</summary>
	internal static bool ActivateWindowAtCursor()
	{
		if (!GetCursorPos(out var point)) return false;

		var childWindow = WindowFromPoint(point);
		if (childWindow == 0 || !TryGetRootWindowAtPoint(new Point(point.X, point.Y), out var rootWindow) || rootWindow == GetDesktopWindow() || rootWindow == GetShellWindow()) return false;

		var targetThread = GetWindowThreadProcessId(rootWindow, out _);
		var currentThread = GetCurrentThreadId();
		// Attach to the target's input queue. Windows then allows SetForegroundWindow to succeed.
		var attached = targetThread != 0 && targetThread != currentThread && AttachThreadInput(currentThread, targetThread, true);
		try
		{
			_ = ShowWindow(rootWindow, SwRestore);
			_ = BringWindowToTop(rootWindow);
			var foregroundSet = SetForegroundWindow(rootWindow);
			_ = SetActiveWindow(rootWindow);
			_ = SetFocus(childWindow);
			CursorDiagnosticLogger.Record("ocr_drop", "target_window_activated", new Point(point.X, point.Y), null, new { windowHandle = rootWindow.ToInt64(), childHandle = childWindow.ToInt64(), foregroundSet });
			return foregroundSet;
		}
		finally
		{
			if (attached) _ = AttachThreadInput(currentThread, targetThread, false);
		}
	}

	/// <summary>True when the window has the WS_EX_TOPMOST style.</summary>
	private static bool IsTopMostWindow(nint windowHandle)
	{
		var exStyle = GetWindowLongPtr(windowHandle, GwlExStyle);
		return exStyle != 0 && (exStyle & WsExTopMost) != 0;
	}

	/// <summary>Reads the window rectangle in screen coordinates. Returns false if the window is gone.</summary>
	internal static bool TryGetWindowBounds(nint windowHandle, out Rectangle bounds)
	{
		if (!GetWindowRect(windowHandle, out var rect))
		{
			bounds = default;
			return false;
		}

		bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
		return true;
	}

	/// <summary>
	/// Returns the range of outer-rectangle positions that keep the visible frame inside the desktop.
	/// Windows 10/11 add invisible borders around the frame, so the outer rectangle can extend past the screen edge.
	/// </summary>
	internal static (int MinLeft, int MinTop, int MaxLeft, int MaxTop) GetWindowPositionLimits(nint windowHandle, int width, int height)
	{
		var desktop = GetDesktopBounds();
		if (!GetWindowRect(windowHandle, out var rect) || DwmGetWindowAttribute(windowHandle, DwmwaExtendedFrameBounds, out var frame, (uint)Marshal.SizeOf<RECT>()) != 0)
			return (desktop.Left, desktop.Top, desktop.Right - width, desktop.Bottom - height);

		var insetLeft = frame.Left - rect.Left;
		var insetTop = frame.Top - rect.Top;
		var insetRight = rect.Right - frame.Right;
		var insetBottom = rect.Bottom - frame.Bottom;
		return (desktop.Left - insetLeft, desktop.Top - insetTop, desktop.Right + insetRight - width, desktop.Bottom + insetBottom - height);
	}

	/// <summary>Checks the point against the virtual screen, which spans all monitors. The right and bottom edges are exclusive.</summary>
	private static bool IsPointOnVirtualScreen(Point point)
	{
		var left = GetSystemMetrics(SmXVirtualScreen);
		var top = GetSystemMetrics(SmYVirtualScreen);
		var width = GetSystemMetrics(SmCxVirtualScreen);
		var height = GetSystemMetrics(SmCyVirtualScreen);
		return point.X >= left && point.X < left + width && point.Y >= top && point.Y < top + height;
	}

	/// <summary>
	/// Computes a grab point on the caption from window metrics, without probing the window.
	/// Used when no caption point can be confirmed.
	/// </summary>
	private static Point GetTitleBarPoint(Rectangle windowBounds, Point cursorPoint)
	{
		var horizontalBorder = Math.Max(0, GetSystemMetrics(SmCxFrame));
		var verticalBorder = Math.Max(0, GetSystemMetrics(SmCyFrame));
		var paddedBorder = Math.Max(0, GetSystemMetrics(SmCxPaddedBorder));
		var captionHeight = Math.Max(1, GetSystemMetrics(SmCyCaption) + verticalBorder + paddedBorder);
		// Middle of the caption, capped at half the window height so very short windows stay on-screen.
		var titleBarY = windowBounds.Top + Math.Min(Math.Max(1, captionHeight / 2), Math.Max(1, windowBounds.Height / 2));
		var dragInsetX = Math.Max(1, horizontalBorder + paddedBorder);
		var minX = windowBounds.Left + dragInsetX;
		var maxX = windowBounds.Right - dragInsetX - 1;
		if (maxX < minX)
		{
			var middleX = windowBounds.Left + (windowBounds.Width / 2);
			minX = middleX;
			maxX = middleX;
		}

		var titleBarX = Math.Clamp(cursorPoint.X, minX, maxX);
		return new Point(titleBarX, titleBarY);
	}

	/// <summary>
	/// Searches the caption band for a point the window reports as HTCAPTION or HTSYSMENU.
	/// Only those points start a native title-bar drag.
	/// </summary>
	/// <returns>A confirmed point, or null when no probe succeeded.</returns>
	private static Point? FindDraggableTitleBarPoint(nint windowHandle, Rectangle windowBounds, Point cursorPoint)
	{
		var horizontalBorder = Math.Max(0, GetSystemMetrics(SmCxFrame));
		var verticalBorder = Math.Max(0, GetSystemMetrics(SmCyFrame));
		var paddedBorder = Math.Max(0, GetSystemMetrics(SmCxPaddedBorder));
		var captionHeight = Math.Max(1, GetSystemMetrics(SmCyCaption) + verticalBorder + paddedBorder);
		var dragInsetX = Math.Max(1, horizontalBorder + paddedBorder);
		var minX = windowBounds.Left + dragInsetX;
		var maxX = windowBounds.Right - dragInsetX - 1;
		if (maxX < minX)
		{
			minX = windowBounds.Left;
			maxX = windowBounds.Right - 1;
		}

		var minY = windowBounds.Top + verticalBorder;
		var maxY = Math.Min(windowBounds.Bottom - 1, windowBounds.Top + captionHeight - 1);
		if (maxY < minY) return null;

		// Start from the cursor position, kept inside the caption band.
		var preferredX = Math.Clamp(cursorPoint.X, minX, maxX);
		var preferredY = Math.Clamp(cursorPoint.Y, minY, maxY);
		// Try the cursor's row first, then the middle, top and bottom of the band. Some captions
		// answer only at certain heights, such as the very top edge.
		var yCandidates = new[]
		{
			preferredY,
			minY + ((maxY - minY) / 2),
			minY,
			maxY,
		};

		// Check the closest horizontal locations first, then expand outward. This avoids moving the
		// cursor farther than necessary. The search is capped because every probe is a cross-window
		// message on the input thread; caption bars are contiguous, so a coarse step still finds them.
		var maxDistance = Math.Min(maxX - minX, TitleBarSearchRadiusPx);
		for (var distance = 0; distance <= maxDistance; distance += TitleBarSearchStepPx)
		{
			var leftX = preferredX - distance;
			var rightX = preferredX + distance;
			foreach (var x in distance == 0 ? new[] { preferredX } : [leftX, rightX])
			{
				if (x < minX || x > maxX) continue;

				foreach (var y in yCandidates)
				{
					// The hit test also answers off-screen, but the cursor cannot reach those points.
					if (!IsPointOnVirtualScreen(new Point(x, y))) continue;
					if (IsDraggableTitleBarPoint(windowHandle, x, y))
					{
						CursorDiagnosticLogger.Record("title_bar_search", "found", cursorPoint, new Point(x, y), new { windowHandle = windowHandle.ToInt64(), preferred = new Point(preferredX, preferredY), bounds = windowBounds.ToString() });
						return new Point(x, y);
					}
				}
			}
		}

		CursorDiagnosticLogger.Record("title_bar_search", "not_found", cursorPoint, null, new { windowHandle = windowHandle.ToInt64(), preferred = new Point(preferredX, preferredY), bounds = windowBounds.ToString(), reason = "No caption probe answered HTCAPTION or HTSYSMENU within the search radius." });
		return null;
	}

	/// <summary>Sends WM_NCHITTEST to the window at a screen point and checks whether the caption answers.</summary>
	private static bool IsDraggableTitleBarPoint(nint windowHandle, int x, int y)
	{
		try
		{
			// WM_NCHITTEST packs screen x in the low word and y in the high word of LPARAM.
			var lParam = unchecked((nint)(uint)(((y & 0xFFFF) << 16) | (x & 0xFFFF)));
			if (SendMessageTimeout(
					windowHandle,
					WmNcHitTest,
					0,
					lParam,
					SmtoAbortIfHung,
					TitleBarHitTestTimeoutMilliseconds,
					out var result) == 0)
			{
				if (CursorDiagnosticLogger.IsEnabled) CursorDiagnosticLogger.Record("title_bar_probe", "no_response", new Point(x, y), null, new { windowHandle = windowHandle.ToInt64(), error = Marshal.GetLastWin32Error() });
				return false;
			}

			var isDraggable = (int)result is HtCaption or HtSysMenu;
			if (CursorDiagnosticLogger.IsEnabled) CursorDiagnosticLogger.Record("title_bar_probe", isDraggable ? "draggable" : "not_draggable", new Point(x, y), null, new { windowHandle = windowHandle.ToInt64(), hitTestCode = result.ToInt64() });
			return isDraggable;
		}
		// Let out-of-memory and stack errors through. Any other failure means the point is not draggable.
		catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
		{
			if (CursorDiagnosticLogger.IsEnabled) CursorDiagnosticLogger.Record("title_bar_probe", "exception", new Point(x, y), null, new { windowHandle = windowHandle.ToInt64(), exceptionType = ex.GetType().FullName, message = ex.Message });
			return false;
		}
	}
}
