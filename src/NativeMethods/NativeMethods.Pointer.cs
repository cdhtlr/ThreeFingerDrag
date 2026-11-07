using System.Runtime.InteropServices;

namespace ThreeFingerDrag;

internal static partial class NativeMethods
{
	// Touch and pen device enumeration. Call once with null to get the count, then again with an array.
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool GetPointerDevices(ref uint deviceCount, [Out] PointerDeviceInfo[]? devices);

	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool GetPointerDeviceRects(IntPtr device, out NativeRect pointerDeviceRect, out NativeRect displayRect);

	/// <summary>Layout of POINTER_DEVICE_INFO. The explicit offsets assume 64-bit pointers.</summary>
	[StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
	internal struct PointerDeviceInfo
	{
		[FieldOffset(0)] public uint DisplayIntegrated;
		[FieldOffset(8)] public IntPtr Device;
		[FieldOffset(16)] public PointerDeviceType PointerDeviceType;
		[FieldOffset(24)] public IntPtr Monitor;
		[FieldOffset(32)] public uint StartingRange;
		[FieldOffset(36)] public uint SupportedPointerTypes;
		[FieldOffset(40), MarshalAs(UnmanagedType.ByValTStr, SizeConst = 520)]
		public string ProductString;
	}

	/// <summary>Only Touch is used. The value 3 is POINTER_DEVICE_TYPE_TOUCH.</summary>
	internal enum PointerDeviceType : uint { Touch = 3 }

	/// <summary>Win32 RECT. Right and bottom are exclusive, so width is Right - Left.</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct RECT { public int Left, Top, Right, Bottom; }

	/// <summary>Win32 POINT in screen coordinates.</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct POINT { public int X; public int Y; }

	/// <summary>Rectangle returned by GetPointerDeviceRects. Same layout as RECT.</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct NativeRect { public int Left, Top, Right, Bottom; }

	// Monitor and window geometry helpers. They map points, rectangles, and windows to displays.
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern nint MonitorFromPoint(POINT pt, uint dwFlags);

	[DllImport("user32.dll", SetLastError = true)]
	internal static extern nint MonitorFromRect(ref RECT lprc, uint dwFlags);

	[DllImport("user32.dll", SetLastError = true)]
	internal static extern nint MonitorFromWindow(nint hwnd, uint dwFlags);

	// Window geometry, title, owning process, and hit testing.
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

	[DllImport("user32.dll", SetLastError = true)]
	internal static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

	[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	internal static extern int GetWindowText(nint hWnd, System.Text.StringBuilder text, int maxCount);

	// Finds the window under a point. GetAncestor then climbs to its top-level owner.
	[DllImport("user32.dll")]
	internal static extern nint WindowFromPoint(POINT point);

	[DllImport("user32.dll")]
	internal static extern nint GetAncestor(nint hWnd, uint gaFlags);

	[DllImport("user32.dll")]
	internal static extern nint GetDesktopWindow();

	[DllImport("user32.dll")]
	internal static extern nint GetShellWindow();
}
