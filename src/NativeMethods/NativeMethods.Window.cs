using System.Runtime.InteropServices;

namespace ThreeFingerDrag;

internal static partial class NativeMethods
{
	/// <summary>Window procedure signature. Used as the callback in WNDCLASSEX.</summary>
	internal delegate nint WndProcDelegate(nint hWnd, uint msg, nint wParam, nint lParam);

	// DPI awareness. Set it early, before windows are created, so coordinates are not scaled.
	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool SetProcessDpiAwarenessContext(nint value);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool SetProcessDPIAware();

	// Window class registration and window lifetime for helper windows.
	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	internal static extern nint GetModuleHandle(string? lpModuleName);

	[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "RegisterClassExW")]
	internal static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

	[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "UnregisterClassW")]
	internal static extern bool UnregisterClass(string lpClassName, nint hInstance);

	[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CreateWindowExW")]
	internal static extern nint CreateWindowEx(
		uint dwExStyle,
		string lpClassName,
		string? lpWindowName,
		uint dwStyle,
		int x, int y, int nWidth, int nHeight,
		nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool DestroyWindow(nint hWnd);

	[DllImport("user32.dll")]
	internal static extern bool ShowWindow(nint hWnd, int nCmdShow);

	[DllImport("user32.dll", SetLastError = true)]
	internal static extern int SetWindowRgn(nint hWnd, nint hRgn, bool bRedraw);

	[DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "DefWindowProcW")]
	internal static extern nint DefWindowProc(nint hWnd, uint msg, nint wParam, nint lParam);

	// Timers and the message loop.
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern nuint SetTimer(nint hWnd, nuint nIDEvent, uint uElapse, nint lpTimerFunc);

	[DllImport("user32.dll")]
	internal static extern bool KillTimer(nint hWnd, nuint uIDEvent);

	[DllImport("user32.dll", SetLastError = true)]
	internal static extern int GetMessage(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

	[DllImport("user32.dll")]
	internal static extern bool TranslateMessage(ref MSG lpMsg);

	[DllImport("user32.dll")]
	internal static extern nint DispatchMessage(ref MSG lpMsg);

	// Painting calls for a window client area.
	[DllImport("user32.dll")]
	internal static extern nint BeginPaint(nint hWnd, out PAINTSTRUCT lpPaint);

	[DllImport("user32.dll")]
	internal static extern bool EndPaint(nint hWnd, ref PAINTSTRUCT lpPaint);

	[DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "DrawTextW")]
	internal static extern int DrawTextW(nint hDC, string lpchText, int cchText, ref RECT lprc, uint format);

	// GDI drawing: device contexts, brushes, regions, and text.
	[DllImport("user32.dll")]
	internal static extern nint GetDC(nint hWnd);

	[DllImport("user32.dll")]
	internal static extern int ReleaseDC(nint hWnd, nint hDC);

	[DllImport("gdi32.dll")]
	internal static extern nint CreateSolidBrush(uint crColor);

	[DllImport("gdi32.dll")]
	internal static extern bool DeleteObject(nint hObject);

	[DllImport("gdi32.dll")]
	internal static extern nint CreateRectRgn(int x1, int y1, int x2, int y2);

	[DllImport("gdi32.dll")]
	internal static extern nint CreateEllipticRgn(int x1, int y1, int x2, int y2);

	[DllImport("gdi32.dll")]
	internal static extern int CombineRgn(nint hrgnDest, nint hrgnSrc1, nint hrgnSrc2, int fnCombineMode);

	[DllImport("gdi32.dll")]
	internal static extern nint SelectObject(nint hdc, nint hgdiobj);

	[DllImport("gdi32.dll")]
	internal static extern nint GetStockObject(int fnObject);

	[DllImport("gdi32.dll")]
	internal static extern uint SetTextColor(nint hdc, uint color);

	[DllImport("gdi32.dll")]
	internal static extern int SetBkMode(nint hdc, int mode);

	/// <summary>Win32 WNDCLASSEX passed to RegisterClassEx.</summary>
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	internal struct WNDCLASSEX
	{
		public uint cbSize;
		public uint style;
		[MarshalAs(UnmanagedType.FunctionPtr)]
		public WndProcDelegate lpfnWndProc;
		public int cbClsExtra;
		public int cbWndExtra;
		public nint hInstance;
		public nint hIcon;
		public nint hCursor;
		public nint hbrBackground;
		public string? lpszMenuName;
		public string lpszClassName;
		public nint hIconSm;
	}

	/// <summary>Win32 PAINTSTRUCT filled by BeginPaint.</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct PAINTSTRUCT
	{
		public nint hdc;
		public int fErase;
		public RECT rcPaint;
		public int fRestore;
		public int fIncUpdate;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public byte[] rgbReserved;
	}

	/// <summary>Win32 MSG filled by GetMessage.</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct MSG
	{
		public nint hwnd;
		public uint message;
		public nint wParam;
		public nint lParam;
		public uint time;
		public int ptX;
		public int ptY;
	}
}
