using System.Diagnostics;

namespace ThreeFingerDrag;

/// <summary>
/// Separate, opt-in feature triggered by a stationary hold from a supported input gesture:
/// captures the screen area around the cursor, runs on-device OCR
/// (<see cref="Windows.Media.Ocr"/>), and retains recognized text for insertion at drop time. This
/// exists for text that cannot be selected/copied normally (text baked into
/// images/video/protected documents, games, etc.). It intentionally has nothing to do with the
/// window-move vs. mouse-passthrough classifier chain in <see cref="DragTargetClassifier"/>: OCR takes
/// tens to hundreds of milliseconds, far too slow to run on every drag-start, so it only runs when
/// its own hold gesture fires. Recognized text is copied from the word beneath the cursor, extending
/// across nearby words until a configurable whitespace gap is reached.
/// </summary>
internal static partial class OcrCaptureService
{
	// 1 while a capture is running, 0 when idle. Used as an atomic try-lock so only one capture runs at a time.
	private static int isCapturing;
	// These values connect a completed capture to the gesture that may later drop its text.
	// Cursor position of the last successful recognition. Drop is only valid at this exact point.
	private static Point? lastRecognizedTextPoint;
	// Text waiting for a drop gesture. Cleared once it is pasted or superseded.
	private static string? pendingDropText;
	// True when the pending text was also placed on the clipboard, so paste can use Ctrl+V.
	private static bool pendingTextIsOnClipboard;
	// Where the originating input gesture happened. Used for logging and the target click.
	private static Point? captureInputPoint;
	// Input device label ("cursor", touch, etc.) recorded in diagnostics.
	private static string captureInputSource = "cursor";

	/// <summary>True when OCR text was recognized at exactly this point.</summary>
	internal static bool HasRecognizedTextAt(Point point) => lastRecognizedTextPoint == point;
	/// <summary>True when recognized text is waiting for a drop.</summary>
	internal static bool HasPendingDropText => !string.IsNullOrEmpty(pendingDropText);

	/// <summary>
	/// Ends the pending OCR drop. When paste is true, activates the window under the cursor,
	/// clicks the drop point, and pastes the text on a background thread.
	/// </summary>
	internal static void CompleteDrop(bool paste)
	{
		// Consume the pending text once, whether it is pasted or intentionally discarded.
		var text = pendingDropText;
		pendingDropText = null;
		var useClipboard = pendingTextIsOnClipboard;
		pendingTextIsOnClipboard = false;
		if (paste && !string.IsNullOrEmpty(text))
		{
			CursorDiagnosticLogger.Record("ocr_capture_drop", "paste_start", details: new { useClipboard, length = text.Length });
			// The delays below wait for the target app to react; keep them off the input thread that called us.
			var inputSource = captureInputSource;
			var inputPoint = captureInputPoint;
			_ = Task.Run(() =>
			{
				try
				{
					_ = WindowDragController.ActivateWindowAtCursor();
					// Activation alone may leave focus in the previously active control. Click at the
					// exact drop point so the target places its caret there before receiving the paste.
					Thread.Sleep(PointerSettings.GetOcrActivationDelayMs());
					if (!MouseInject.ClickAtCursor(inputSource, inputPoint))
					{
						Debug.WriteLine("OCR drop: could not read cursor position for target click.");
						CursorDiagnosticLogger.Record("ocr_capture_drop", "click_failed");
						return;
					}
					Thread.Sleep(PointerSettings.GetOcrClickDelayMs());
					var pasted = useClipboard ? ClipboardWriter.PasteClipboard() : ClipboardWriter.PasteText(text);
					if (!pasted) Debug.WriteLine($"OCR drop: input injection failed (clipboard paste: {useClipboard}).");
					CursorDiagnosticLogger.Record("ocr_capture_drop", pasted ? "paste_succeeded" : "paste_failed", details: new { useClipboard });
				}
				catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
				{
					Debug.WriteLine($"OCR drop failed: {ex}");
					CursorDiagnosticLogger.Record("ocr_capture_drop", "exception", details: new { exceptionType = ex.GetType().FullName, message = ex.Message });
				}
			});
		}
		else
		{
			CursorDiagnosticLogger.Record("ocr_capture_drop", "discarded", details: new { paste, hadText = !string.IsNullOrEmpty(text) });
		}
	}




	/// <summary>
	/// Fires a fire-and-forget OCR capture centered on the current cursor position. Safe to call
	/// repeatedly; overlapping requests while a capture is already running are ignored.
	/// </summary>
	public static void TriggerCapture(Point? inputPoint = null, string inputSource = "cursor")
	{
		captureInputPoint = inputPoint;
		captureInputSource = inputSource;
		_ = MouseInject.TryGetCursorPoint(out var requestCursor);
		CursorDiagnosticLogger.RecordInputSample(inputSource, "ocr_capture_requested", inputPoint, requestCursor,
			new { enabled = PointerSettings.IsOcrCaptureEnabled() });
		// A new attempt invalidates any result tied to a previous cursor location.
		lastRecognizedTextPoint = null;
		pendingDropText = null;
		pendingTextIsOnClipboard = false;
		if (!PointerSettings.IsOcrCaptureEnabled())
		{
			CursorDiagnosticLogger.Record("ocr_capture_trigger", "skipped_disabled");
			return;
		}

		// Claim the single capture slot atomically so slow OCR requests never overlap.
		if (Interlocked.CompareExchange(ref isCapturing, 1, 0) != 0)
		{
			CursorDiagnosticLogger.Record("ocr_capture_trigger", "skipped_busy");
			return;
		}

		// OCR may take noticeable time, so keep it off the raw-input/message-loop thread.
		_ = Task.Run(() =>
		{
			try
			{
				CaptureAndCopyCore();
			}
			catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
			{
				Debug.WriteLine($"OCR capture failed: {ex}");
				CursorDiagnosticLogger.RecordInputSample(captureInputSource, "ocr_capture_failed", captureInputPoint,
					MouseInject.TryGetCursorPoint(out var failedAt) ? failedAt : null,
					new { exceptionType = ex.GetType().FullName, message = ex.Message });
			}
			finally
			{
				Volatile.Write(ref isCapturing, 0);
			}
		});
	}
}
