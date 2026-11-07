using System.Runtime.InteropServices;
using static ThreeFingerDrag.NativeMethods;

namespace ThreeFingerDrag;

internal static partial class ClipboardWriter
{
	/// <summary>Types Unicode text into the focused target without changing the clipboard.</summary>
	public static bool PasteText(string text)
	{
		// Empty input needs no injected key events and is already a successful no-op.
		if (string.IsNullOrEmpty(text))
		{
			CursorDiagnosticLogger.Record("clipboard_paste", "empty_text_skipped");
			return true;
		}

		// Many applications accept the normal paste command but ignore injected Unicode
		// characters. Use the clipboard only for the duration of Ctrl+V, then restore its prior
		// plain-text contents so the "do not copy OCR" option does not leave OCR in the clipboard.
		if (TryGetClipboardText(out var previousText, out var hadClipboardText))
		{
			if (SetText(text))
			{
				var pasted = PasteClipboard();
				// Wait so the target app reads the clipboard before it is restored.
				Thread.Sleep(PointerSettings.GetOcrClipboardRestoreDelayMs());
				var restored = hadClipboardText ? SetText(previousText!) : ClearClipboardReturning();
				CursorDiagnosticLogger.Record("clipboard_paste", "clipboard_restore", details: new { hadClipboardText, restored });
				if (pasted)
				{
					CursorDiagnosticLogger.Record("clipboard_paste", "clipboard_path_succeeded", details: new { length = text.Length });
					return true;
				}
				CursorDiagnosticLogger.Record("clipboard_paste", "clipboard_ctrl_v_failed", details: new { length = text.Length });
			}
			else
			{
				CursorDiagnosticLogger.Record("clipboard_paste", "clipboard_set_failed", details: new { length = text.Length });
			}
		}
		else
		{
			CursorDiagnosticLogger.Record("clipboard_paste", "clipboard_read_unavailable");
		}

		// Each UTF-16 code unit becomes a Unicode key-down followed by key-up.
		INPUT[] inputs = new INPUT[text.Length * 2];
		var n = 0;
		foreach (var ch in text)
		{
			inputs[n++] = CreateUnicodeInput(ch, 0);
			inputs[n++] = CreateUnicodeInput(ch, KeyeventfKeyup);
		}

		CursorDiagnosticLogger.Record("clipboard_paste", "unicode_fallback", details: new { length = text.Length });
		var sentFallback = SendInputs(inputs);
		CursorDiagnosticLogger.Record("clipboard_paste", "unicode_fallback_result", details: new { success = sentFallback });
		return sentFallback;
	}

	/// <summary>Invokes the focused application's normal paste command using the current clipboard.</summary>
	public static bool PasteClipboard()
	{
		var inputs = new[]
		{
			CreateVirtualKeyInput(VkControl, 0),
			CreateVirtualKeyInput(VkV, 0),
			CreateVirtualKeyInput(VkV, KeyeventfKeyup),
			CreateVirtualKeyInput(VkControl, KeyeventfKeyup),
		};
		var sent = SendInputs(inputs);
		CursorDiagnosticLogger.Record("clipboard_paste", "ctrl_v_sent", details: new { success = sent });
		return sent;
	}

	/// <summary>Reads the current clipboard text. Returns false when the clipboard cannot be opened or holds non-text data.</summary>
	private static bool TryGetClipboardText(out string? text, out bool hadText)
	{
		text = null;
		hadText = false;
		if (!OpenClipboard(0))
		{
			CursorDiagnosticLogger.Record("clipboard_read", "open_failed", details: new { error = Marshal.GetLastWin32Error() });
			return false;
		}

		try
		{
			var hasAnyFormat = false;
			// Only text formats are restored later. Anything else would be lost, so skip the clipboard path.
			for (var format = EnumClipboardFormats(0); format != 0; format = EnumClipboardFormats(format))
			{
				hasAnyFormat = true;
				if (format is not (CfUnicodeText or CfText or CfOemText or CfLocale))
				{
					CursorDiagnosticLogger.Record("clipboard_read", "non_text_format_rejected", details: new { format });
					return false;
				}
			}

			var handle = GetClipboardData(CfUnicodeText);
			if (handle == 0)
			{
				CursorDiagnosticLogger.Record("clipboard_read", "no_unicode_text", details: new { hasAnyFormat });
				return !hasAnyFormat;
			}

			var pointer = GlobalLock(handle);
			if (pointer == 0)
			{
				CursorDiagnosticLogger.Record("clipboard_read", "lock_failed", details: new { error = Marshal.GetLastWin32Error() });
				return false;
			}

			try
			{
				text = Marshal.PtrToStringUni(pointer);
				hadText = text is not null;
				return hadText;
			}
			finally
			{
				_ = GlobalUnlock(handle);
			}
		}
		finally
		{
			_ = CloseClipboard();
		}
	}

	/// <summary>Empties the clipboard and reports whether it could be opened and emptied.</summary>
	private static bool ClearClipboardReturning()
	{
		if (!OpenClipboard(0))
		{
			CursorDiagnosticLogger.Record("clipboard_clear", "open_failed", details: new { error = Marshal.GetLastWin32Error() });
			return false;
		}

		try
		{
			var emptied = EmptyClipboard();
			CursorDiagnosticLogger.Record("clipboard_clear", emptied ? "cleared" : "empty_failed");
			return emptied;
		}
		finally { _ = CloseClipboard(); }
	}
}
