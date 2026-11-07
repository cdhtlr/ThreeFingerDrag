using System.Runtime.InteropServices;
using static ThreeFingerDrag.NativeMethods;

namespace ThreeFingerDrag;

/// <summary>
/// Sets plain Unicode text on the system clipboard using raw Win32 calls.
/// </summary>
internal static partial class ClipboardWriter
{
	public static bool SetText(string text)
	{
		var byteCount = (nuint)((text.Length + 1) * sizeof(char));
		CursorDiagnosticLogger.Record("clipboard_write", "start", details: new { length = text.Length });
		var globalMemory = GlobalAlloc(GMemMoveable, byteCount);
		if (globalMemory == 0)
		{
			CursorDiagnosticLogger.Record("clipboard_write", "alloc_failed", details: new { length = text.Length, error = Marshal.GetLastWin32Error() });
			return false;
		}

		// GlobalLock exposes the allocated block while its characters are copied.
		var lockedPointer = GlobalLock(globalMemory);
		if (lockedPointer == 0)
		{
			CursorDiagnosticLogger.Record("clipboard_write", "lock_failed", details: new { error = Marshal.GetLastWin32Error() });
			_ = GlobalFree(globalMemory);
			return false;
		}

		try
		{
			var chars = text.ToCharArray();
			Marshal.Copy(chars, 0, lockedPointer, chars.Length);
			Marshal.WriteInt16(lockedPointer, chars.Length * sizeof(char), 0);
		}
		finally
		{
			_ = GlobalUnlock(globalMemory);
		}

		// Clipboard access is exclusive; release the allocation if it cannot be opened.
		if (!OpenClipboard(0))
		{
			CursorDiagnosticLogger.Record("clipboard_write", "open_failed", details: new { error = Marshal.GetLastWin32Error() });
			_ = GlobalFree(globalMemory);
			return false;
		}

		try
		{
			_ = EmptyClipboard();
			if (SetClipboardData(CfUnicodeText, globalMemory) == 0)
			{
				CursorDiagnosticLogger.Record("clipboard_write", "set_data_failed", details: new { error = Marshal.GetLastWin32Error() });
				_ = GlobalFree(globalMemory);
				return false;
			}

			// Ownership of globalMemory has now transferred to the system; it must not be freed here.
			CursorDiagnosticLogger.Record("clipboard_write", "success", details: new { length = text.Length });
			return true;
		}
		finally
		{
			_ = CloseClipboard();
		}
	}
}
