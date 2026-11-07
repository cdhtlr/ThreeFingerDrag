using System.Runtime.InteropServices;
using static ThreeFingerDrag.NativeMethods;

namespace ThreeFingerDrag;

internal static partial class OverlayWindow
{
	/// <summary>Runs <paramref name="action"/> on a new background thread and swallows any failure, since overlays are cosmetic.</summary>
	private static void RunOnBackgroundThread(Action action)
	{
		// Each overlay gets its own thread and message loop, so the caller never waits for it.
		try
		{
			Thread thread = new(() =>
			{
				try
				{
					action();
				}
				// Out-of-memory and stack overflow are rethrown; a failed overlay is ignored.
				catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
				{
					CursorDiagnosticLogger.Record("overlay_host", "exception", details: new { exceptionType = ex.GetType().FullName, message = ex.Message });
				}
			})
			{
				IsBackground = true,
				Name = "ThreeFingerDrag-Overlay",
			};
			thread.Start();
			CursorDiagnosticLogger.Record("overlay_host", "thread_started", details: new { threadName = thread.Name });
		}
		catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
		{
			CursorDiagnosticLogger.Record("overlay_host", "thread_start_failed", details: new { exceptionType = ex.GetType().FullName, message = ex.Message });
		}
	}

	/// <summary>Creates the overlay window, shows it, and pumps messages until the duration timer fires.</summary>
	private static void RunOverlay(Rectangle screenBounds, uint backgroundColorRefBgr, Action<nint>? shapeWindow, Action<nint, RECT>? drawContent, int durationMs)
	{
		var bounds = ClampToVirtualScreen(screenBounds);
		var hInstance = GetModuleHandle(null);
		// The background brush paints the solid fill. The window is clipped to a shape by shapeWindow, if one is given.
		var brush = CreateSolidBrush(backgroundColorRefBgr);
		// A unique class name per overlay, so each class can be unregistered when its window is gone.
		var className = "TFD_Overlay_" + Guid.NewGuid().ToString("N");
		var classRegistered = false;
		var hwnd = nint.Zero;

		try
		{
			// Handles WM_PAINT for text panels. All other messages go to the default procedure.
			WndProcDelegate wndProc = (hWnd, msg, wParam, lParam) =>
			{
				if (msg == WmPaint && drawContent is not null)
				{
					// BeginPaint and EndPaint must be paired. EndPaint runs in finally, even if drawing throws.
					var hdc = BeginPaint(hWnd, out var paintStruct);
					try
					{
						drawContent(hdc, paintStruct.rcPaint);
					}
					catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
					{
						CursorDiagnosticLogger.Record("overlay_text", "paint_exception", details: new { exceptionType = ex.GetType().FullName, message = ex.Message });
					}
					finally
					{
						_ = EndPaint(hWnd, ref paintStruct);
					}

					return 0;
				}

				return DefWindowProc(hWnd, msg, wParam, lParam);
			};

			WNDCLASSEX wndClass = new()
			{
				cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
				lpfnWndProc = wndProc,
				hInstance = hInstance,
				hbrBackground = brush,
				lpszClassName = className,
			};

			// Register a class for this overlay. A failure here means the window cannot be created, so it returns.
			if (RegisterClassEx(ref wndClass) == 0)
			{
				CursorDiagnosticLogger.Record("overlay_host", "register_class_failed", details: new { className });
				return;
			}

			classRegistered = true;

			// Topmost, tool window (no taskbar or Alt+Tab entry), no activation, and click-through.
			// The transparent style makes hit tests skip the window, so clicks reach the windows below.
			hwnd = CreateWindowEx(
				WsExTopMost | WsExToolWindow | WsExNoActivate | WsExTransparent,
				className,
				null,
				WsPopup,
				bounds.Left, bounds.Top, bounds.Width, bounds.Height,
				0, 0, hInstance, 0);

			if (hwnd == 0)
			{
				CursorDiagnosticLogger.Record("overlay_host", "create_window_failed", details: new { className, bounds });
				return;
			}

			// Clip the window to its shape before showing it, so the first frame is already the right shape.
			shapeWindow?.Invoke(hwnd);
			_ = ShowWindow(hwnd, SwShowNoActivate);
			CursorDiagnosticLogger.Record("overlay_host", "shown", details: new { hwnd, bounds, durationMs, shaped = shapeWindow is not null, hasText = drawContent is not null });

			// A one-shot timer posts WM_TIMER after the duration. The loop below exits when it arrives.
			var timerId = SetTimer(hwnd, 1, (uint)Math.Max(1, durationMs), 0);
			while (GetMessage(out var msg, 0, 0, 0) > 0)
			{
				if (msg.message == WmTimer) break;

				_ = TranslateMessage(ref msg);
				_ = DispatchMessage(ref msg);
			}

			if (timerId != 0) _ = KillTimer(hwnd, timerId);
			CursorDiagnosticLogger.Record("overlay_host", "duration_elapsed", details: new { hwnd, timerId });
		}
		finally
		{
			// Destroy the window before unregistering its class and deleting the brush, since both are in use until then.
			if (hwnd != 0) _ = DestroyWindow(hwnd);
			if (classRegistered) _ = UnregisterClass(className, hInstance);
			if (brush != 0) _ = DeleteObject(brush);
			CursorDiagnosticLogger.Record("overlay_host", "destroyed", details: new { hwnd, classRegistered });
		}
	}

	/// <summary>Moves <paramref name="bounds"/> so it lies fully inside the virtual desktop, which can span several monitors.</summary>
	private static Rectangle ClampToVirtualScreen(Rectangle bounds)
	{
		// The virtual screen origin can be negative when a monitor sits left of or above the primary one.
		var left = GetSystemMetrics(SmXVirtualScreen);
		var top = GetSystemMetrics(SmYVirtualScreen);
		var width = Math.Max(GetSystemMetrics(SmCxVirtualScreen), 1);
		var height = Math.Max(GetSystemMetrics(SmCyVirtualScreen), 1);
		var right = left + width;
		var bottom = top + height;

		var x = Math.Clamp(bounds.X, left, Math.Max(left, right - bounds.Width));
		var y = Math.Clamp(bounds.Y, top, Math.Max(top, bottom - bounds.Height));
		if (x != bounds.X || y != bounds.Y)
		{
			CursorDiagnosticLogger.Record("overlay_host", "clamped_to_virtual_screen", bounds.Location, new Point(x, y));
		}

		return new Rectangle(x, y, bounds.Width, bounds.Height);
	}
}
