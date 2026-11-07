using System.Runtime.InteropServices;

namespace ThreeFingerDrag;

internal static partial class NativeMethods
{
	// Focus helpers. Windows limits SetForegroundWindow, so AttachThreadInput can share input state with the foreground thread.
	[DllImport("user32.dll")]
	internal static extern nint GetForegroundWindow();

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool SetForegroundWindow(nint hWnd);

	[DllImport("user32.dll")]
	internal static extern nint SetFocus(nint hWnd);

	[DllImport("user32.dll")]
	internal static extern nint SetActiveWindow(nint hWnd);

	// Thread ID used with AttachThreadInput.
	[DllImport("kernel32.dll")]
	internal static extern uint GetCurrentThreadId();

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool attach);
}
