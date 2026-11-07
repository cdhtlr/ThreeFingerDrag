using System.Diagnostics;
using System.Text;
using static ThreeFingerDrag.NativeMethods;

namespace ThreeFingerDrag;

internal static partial class CursorDiagnosticLogger
{
/// <summary>Resolves detailed window, process, and monitor info for a window handle or at a point.</summary>
	public static object? DescribeWindow(nint windowHandle, Point? inputPoint = null)
	{
		// Without a handle, find the window under the point and climb to its top-level owner.
		if (windowHandle == 0 && inputPoint.HasValue)
		{
			POINT pt = new() { X = inputPoint.Value.X, Y = inputPoint.Value.Y };
			var winAtPt = WindowFromPoint(pt);
			if (winAtPt != 0)
			{
				var root = GetAncestor(winAtPt, GaRoot);
				windowHandle = root == 0 ? winAtPt : root;
			}
		}

		if (windowHandle == 0) return null;

		try
		{
			Rectangle? bounds = null;
			if (GetWindowRect(windowHandle, out var rect)) bounds = new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);

			// GetWindowText truncates titles longer than the buffer.
			StringBuilder titleBuf = new(512);
			_ = GetWindowText(windowHandle, titleBuf, titleBuf.Capacity);
			_ = GetWindowThreadProcessId(windowHandle, out uint pid);

			string processName = "Unknown";
			string processPath = "";
			if (pid != 0)
			{
				try
				{
					// The process may exit or be protected. Then the name stays "Unknown" and the path stays empty.
					using Process proc = Process.GetProcessById((int)pid);
					processName = proc.ProcessName;
					try
					{ processPath = proc.MainModule?.FileName ?? ""; }
					catch { }
				}
				catch { }
			}

			return new
			{
				windowHandle = windowHandle.ToInt64(),
				title = titleBuf.ToString(),
				processId = pid,
				processName,
				processPath,
				bounds = bounds.HasValue ? new { bounds.Value.X, bounds.Value.Y, bounds.Value.Width, bounds.Value.Height } : null,
				monitor = bounds.HasValue ? GetMonitorForRect(bounds) : null,
				// Desktop and shell windows are flagged so they are not read as app windows.
				isDesktop = windowHandle == GetDesktopWindow() || windowHandle == GetShellWindow()
			};
		}
		catch
		{
			return null;
		}
	}
}
