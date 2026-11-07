using System.Runtime.InteropServices;

namespace ThreeFingerDrag;

internal static partial class NativeMethods
{
	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool BringWindowToTop(nint hWnd);

	[DllImport("user32.dll")]
	internal static extern int GetWindowLong(nint hWnd, int nIndex);

	internal delegate bool EnumWindowsProc(nint hWnd, nint lParam);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool IsWindowVisible(nint hWnd);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool IsIconic(nint hWnd);

	// Visible frame of a window. Unlike GetWindowRect, it excludes the invisible resize border on Windows 10 and 11.
	[DllImport("dwmapi.dll")]
	internal static extern int DwmGetWindowAttribute(nint hwnd, uint dwAttribute, out RECT pvAttribute, uint cbAttribute);

	[DllImport("user32.dll", SetLastError = true)]
	internal static extern nint GetWindowLongPtr(nint hWnd, int nIndex);
}
