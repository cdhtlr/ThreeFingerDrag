using System.Diagnostics;
using System.Runtime.InteropServices;
using static ThreeFingerDrag.NativeMethods;

namespace ThreeFingerDrag;

internal static partial class ClipboardWriter
{
	/// <summary>Sends a batch of input events with SendInput. Returns true only if every event was accepted.</summary>
	private static bool SendInputs(INPUT[] inputs)
	{
		var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
		if (sent != inputs.Length)
		{
			Debug.WriteLine($"OCR paste: SendInput accepted {sent}/{inputs.Length} events; INPUT size={Marshal.SizeOf<INPUT>()}, error={Marshal.GetLastWin32Error()}.");
			CursorDiagnosticLogger.Record("clipboard_send_inputs", "partial_or_rejected", details: new { sent, requested = inputs.Length, inputSize = Marshal.SizeOf<INPUT>(), error = Marshal.GetLastWin32Error() });
			return false;
		}
		CursorDiagnosticLogger.Record("clipboard_send_inputs", "accepted", details: new { sent });
		return true;
	}

	// INPUT is a tagged union. Only its keyboard member is used here.
	/// <summary>Sends a Unicode key-down and key-up pair for each UTF-16 unit. Key-up is sent right after key-down.</summary>
	private static INPUT CreateUnicodeInput(char character, uint flags) => new()
	{
		Type = InputKeyboard,
		Data = new InputUnion { Keyboard = new KEYBDINPUT { ScanCode = character, Flags = KeyeventfUnicode | flags }, },
	};

	/// <summary>Builds a keyboard event for a virtual-key code, such as Ctrl+V.</summary>
	private static INPUT CreateVirtualKeyInput(ushort virtualKey, uint flags) => new()
	{
		Type = InputKeyboard,
		Data = new InputUnion { Keyboard = new KEYBDINPUT { VirtualKey = virtualKey, Flags = flags }, },
	};
}
