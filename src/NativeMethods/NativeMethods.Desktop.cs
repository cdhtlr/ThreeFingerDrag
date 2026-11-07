using System.Runtime.InteropServices;

namespace ThreeFingerDrag;

internal static partial class NativeMethods
{
	// Desktop settings. SystemParametersInfo reads or writes values such as pointer speed.
	// The three overloads differ only in the type of the output parameter.
	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool SystemParametersInfo(uint uiAction, uint uiParam, out int pvParam, uint fWinIni);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool SystemParametersInfo(uint uiAction, uint uiParam, out uint pvParam, uint fWinIni);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool SystemParametersInfo(uint uiAction, uint uiParam, nint pvParam, uint fWinIni);

	// Clipboard access. Every OpenClipboard must be paired with CloseClipboard.
	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool OpenClipboard(nint hWndNewOwner);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool CloseClipboard();

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool EmptyClipboard();

	[DllImport("user32.dll", SetLastError = true)]
	internal static extern nint SetClipboardData(uint uFormat, nint hMem);

	[DllImport("user32.dll", SetLastError = true)]
	internal static extern nint GetClipboardData(uint uFormat);

	[DllImport("user32.dll", SetLastError = true)]
	internal static extern uint EnumClipboardFormats(uint format);

	// Global memory blocks (GMEM_*) that carry clipboard data. After SetClipboardData succeeds, the system owns the block.
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern nint GlobalAlloc(uint uFlags, nuint dwBytes);

	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern nint GlobalLock(nint hMem);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool GlobalUnlock(nint hMem);

	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern nint GlobalFree(nint hMem);
}
