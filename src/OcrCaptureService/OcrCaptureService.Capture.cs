using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using static ThreeFingerDrag.NativeMethods;

namespace ThreeFingerDrag;

internal static partial class OcrCaptureService
{
	/// <summary>
	/// Runs on a worker thread. Captures the area around the cursor, runs OCR, extracts the text
	/// under the cursor, and stores it for a later drop.
	/// </summary>
	private static void CaptureAndCopyCore()
	{
		// Capture position once so image bounds and OCR coordinates refer to the same point.
		if (!GetCursorPos(out var cursorPosition))
		{
			CursorDiagnosticLogger.Record("ocr_capture_core", "cursor_read_failed", details: new { error = Marshal.GetLastWin32Error() });
			return;
		}

		// Capture region is a square of PointerSettings.GetOcrCaptureRadius() around the cursor.
		var region = ComputeCaptureRegion(new Point(cursorPosition.X, cursorPosition.Y));
		CursorDiagnosticLogger.RecordInputSample(captureInputSource, "ocr_screen_capture", captureInputPoint, new Point(cursorPosition.X, cursorPosition.Y), new { captureBounds = region.ToString() });
		// Returns BGRA pixels for the region, or null when the screen grab fails.
		var buffer = ScreenCapture.CaptureRegion(region, out var width, out var height);
		if (buffer is null)
		{
			Debug.WriteLine("OCR capture: screen capture failed.");
			CursorDiagnosticLogger.RecordInputSample(captureInputSource, "ocr_screen_capture_failed", captureInputPoint, new Point(cursorPosition.X, cursorPosition.Y), new { captureBounds = region.ToString() });
			return;
		}

		var engine = CreateEngine();
		if (engine is null)
		{
			Debug.WriteLine("OCR capture: no OCR engine/language available.");
			CursorDiagnosticLogger.Record("ocr_capture_core", "engine_unavailable");
			return;
		}

		// WinRT OCR expects a SoftwareBitmap; the capture buffer already uses BGRA pixel order.
		using SoftwareBitmap softwareBitmap = SoftwareBitmap.CreateCopyFromBuffer(buffer.AsBuffer(), BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Ignore);

		OcrResult result;
		try
		{
			// WinRT returns an IAsyncOperation. AsTask and GetResult block this worker thread until OCR finishes.
			// This is safe only because the call runs on a Task.Run thread, never on the UI or input thread.
			result = engine.RecognizeAsync(softwareBitmap).AsTask().GetAwaiter().GetResult();
		}
		catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
		{
			Debug.WriteLine($"OCR recognition failed: {ex}");
			CursorDiagnosticLogger.Record("ocr_recognition", "exception", details: new { exceptionType = ex.GetType().FullName, message = ex.Message });
			return;
		}

		// OCR bounds are local to the captured image, while the cursor was read in screen coordinates.
		Point localCursor = new(cursorPosition.X - region.Left, cursorPosition.Y - region.Top);
		var selectedText = ExtractTextAtCursor(result, localCursor);
		if (string.IsNullOrWhiteSpace(selectedText))
		{
			lastRecognizedTextPoint = null;
			Debug.WriteLine("OCR capture: no text at cursor.");
			CursorDiagnosticLogger.RecordInputSample(captureInputSource, "ocr_no_text_at_point", captureInputPoint, new Point(cursorPosition.X, cursorPosition.Y), new { recognizedWordCount = result.Lines.Sum(line => line.Words.Count) });
			return;
		}

		// Keep a copy for the drop in either mode. Clipboard mode additionally makes the
		// recognized text available immediately, while drop insertion is a separate action.
		var copyToClipboard = PointerSettings.IsOcrCopyToClipboardEnabled();
		if (copyToClipboard)
		{
			if (!ClipboardWriter.SetText(selectedText))
			{
				Debug.WriteLine("OCR capture: clipboard write failed.");
				CursorDiagnosticLogger.Record("ocr_capture_core", "clipboard_write_failed");
				lastRecognizedTextPoint = null;
				return;
			}
		}
		pendingDropText = selectedText;
		pendingTextIsOnClipboard = copyToClipboard;

		lastRecognizedTextPoint = new Point(cursorPosition.X, cursorPosition.Y);
		CursorDiagnosticLogger.RecordInputSample(captureInputSource, "ocr_text_selected", captureInputPoint, lastRecognizedTextPoint, new { recognizedWordCount = result.Lines.Sum(line => line.Words.Count), copiedToClipboard = copyToClipboard });
		DragModeIndicatorOverlay.Show(lastRecognizedTextPoint.Value, isWindowDrag: false, "OCR copied", selectedText);
	}

	/// <summary>Returns the square capture region around the cursor, clipped to the virtual screen.</summary>
	private static Rectangle ComputeCaptureRegion(Point cursorPoint)
	{
		// SM_* virtual-screen metrics cover all monitors, including negative screen coordinates.
		// GetSystemMetrics indices for the virtual screen rectangle (SM_*XVIRTUALSCREEN).
		const int SmXVirtualScreen = 76;
		const int SmYVirtualScreen = 77;
		const int SmCxVirtualScreen = 78;
		const int SmCyVirtualScreen = 79;

		// Start with a square around the cursor and clip each edge to the virtual desktop.
		var radius = PointerSettings.GetOcrCaptureRadius();
		var left = cursorPoint.X - radius;
		var top = cursorPoint.Y - radius;
		var right = cursorPoint.X + radius;
		var bottom = cursorPoint.Y + radius;

		var virtualLeft = GetSystemMetrics(SmXVirtualScreen);
		var virtualTop = GetSystemMetrics(SmYVirtualScreen);
		var virtualWidth = GetSystemMetrics(SmCxVirtualScreen);
		var virtualHeight = GetSystemMetrics(SmCyVirtualScreen);
		// Width and height are at least 1 so the clamp range never becomes inverted.
		var virtualRight = virtualLeft + Math.Max(virtualWidth, 1);
		var virtualBottom = virtualTop + Math.Max(virtualHeight, 1);

		left = Math.Clamp(left, virtualLeft, virtualRight);
		top = Math.Clamp(top, virtualTop, virtualBottom);
		right = Math.Clamp(right, virtualLeft, virtualRight);
		bottom = Math.Clamp(bottom, virtualTop, virtualBottom);

		// Normalize the corners in case a clamp reordered them, so FromLTRB never gets an empty rectangle.
		return Rectangle.FromLTRB(Math.Min(left, right), Math.Min(top, bottom), Math.Max(left, right), Math.Max(top, bottom));
	}
}
