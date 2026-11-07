using System.Runtime.InteropServices;

namespace ThreeFingerDrag;

internal static partial class NativeMethods
{
	// Injects synthetic keyboard and mouse input. The structs below must match the Win32 layout exactly.
	// The last argument is the size of one INPUT element.
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern uint SendInput(uint count, INPUT[] inputs, int size);

	/// <summary>Mouse part of INPUT. Dx and Dy are relative moves unless the ABSOLUTE flag is set.</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct MOUSEINPUT
	{
		public int Dx;
		public int Dy;
		public uint MouseData;
		public uint Flags;
		public uint Time;
		public nint ExtraInfo;
	}

	/// <summary>Keyboard part of INPUT. Either VirtualKey or ScanCode identifies the key.</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct KEYBDINPUT
	{
		public ushort VirtualKey;
		public ushort ScanCode;
		public uint Flags;
		public uint Time;
		public nint ExtraInfo;
	}

	/// <summary>Overlays the keyboard and mouse structs on the same bytes. INPUT.Type says which one is valid.</summary>
	[StructLayout(LayoutKind.Explicit)]
	internal struct InputUnion
	{
		[FieldOffset(0)] public KEYBDINPUT Keyboard;
		// INPUT is sized to its largest union member. Without MOUSEINPUT here the x64
		// structure is too small and SendInput rejects the call even for keyboard input.
		[FieldOffset(0)] public MOUSEINPUT Mouse;
	}

	/// <summary>Win32 INPUT. Type is 0 for a mouse event and 1 for a keyboard event.</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct INPUT
	{
		public uint Type;
		public InputUnion Data;
	}

	/// <summary>Flags for mouse_event and INPUT.mi.dwFlags. Values match WinUser.h.</summary>
	internal enum MouseEventFlags : uint
	{
		MOVE = 0x00000001,
		LEFTDOWN = 0x00000002,
		LEFTUP = 0x00000004,
		RIGHTDOWN = 0x00000008,
		RIGHTUP = 0x00000010,
		MIDDLEDOWN = 0x00000020,
		MIDDLEUP = 0x00000040,
		XDOWN = 0x00000080,
		XUP = 0x00000100,
		WHEEL = 0x00000800,
		ABSOLUTE = 0x00008000,
	}

	// Legacy mouse injection. MouseInject uses it for button and relative move events.
	[DllImport("user32.dll")]
	internal static extern void mouse_event(MouseEventFlags dwFlags, int dx = 0, int dy = 0, uint dwData = 1, UIntPtr dwExtraInfo = default);
}
