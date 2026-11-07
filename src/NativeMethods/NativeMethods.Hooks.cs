using System.Runtime.InteropServices;
using System.Text;

namespace ThreeFingerDrag;

internal static partial class NativeMethods
{
	// Window messaging, cursor info, and key state.
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern nint SendMessageTimeout(nint hWnd, uint msg, nint wParam, nint lParam, uint fuFlags, uint timeoutMilliseconds, out nint result);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool GetCursorInfo(ref CURSORINFO cursorInfo);

	[DllImport("user32.dll")]
	internal static extern nint LoadCursor(nint hInstance, nint cursorResourceId);

	[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	internal static extern int GetClassName(nint hWnd, StringBuilder className, int maxCount);

	/// <summary>Win32 CURSORINFO. cbSize must be set to this struct size before the call.</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct CURSORINFO
	{
		public int cbSize;
		public int flags;
		public nint hCursor;
		public POINT ptScreenPos;
	}

	// Key state. The high bit of the result is set while the key is down.
	[DllImport("user32.dll")]
	internal static extern short GetAsyncKeyState(int vKey);

	/// <summary>Low-level hook callback. Return a nonzero value to block the event, or CallNextHookEx to pass it on.</summary>
	internal delegate nint HookProc(int nCode, nint wParam, nint lParam);

	/// <summary>Event data for WH_MOUSE_LL. flags includes LLMHF_INJECTED for synthetic input.</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct MSLLHOOKSTRUCT
	{
		public POINT pt;
		public uint mouseData;
		public uint flags;
		public uint time;
		public nuint dwExtraInfo;
	}

	// Installs and removes low-level hooks. CallNextHookEx forwards the event to the next hook.
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern nint SetWindowsHookEx(int idHook, HookProc lpfn, nint hMod, uint dwThreadId);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool UnhookWindowsHookEx(nint hhk);

	[DllImport("user32.dll")]
	internal static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool PostMessage(nint hWnd, uint msg, nint wParam, nint lParam);

	// Cursor position and display metrics.
	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool GetCursorPos(out POINT lpPoint);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool SetCursorPos(int x, int y);

	[DllImport("user32.dll")]
	internal static extern int GetSystemMetrics(int index);
}
