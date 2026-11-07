namespace ThreeFingerDrag;

/// <summary>
/// Shared infrastructure for small, transient, click-through, topmost heads-up overlays (a bordered
/// rectangle "frame", a filled circle, or a short text panel) used by
/// <see cref="SolidColorDetectionOverlay"/> and <see cref="DragModeIndicatorOverlay"/>. Deliberately
/// avoids any dependency on <c>System.Drawing.Common</c>/GDI+ (consistent with <see cref="ScreenCapture"/>):
/// shapes are produced by clipping a plain popup window to a region with <c>SetWindowRgn</c> (a
/// rectangle-frame region or an ellipse) and letting the window's own background brush paint the solid
/// fill color, so no manual pixel/alpha blending is needed; the text panel variant additionally draws
/// with plain <c>DrawTextW</c> in response to <c>WM_PAINT</c>. Each call runs on its own short-lived
/// background thread with a minimal message loop and self-destructs after <c>durationMs</c>, so callers
/// (the drag-start classifier and the OCR capture service) are never blocked by showing one.
/// </summary>
internal static partial class OverlayWindow
{
	/// <summary>Builds a Win32 <c>COLORREF</c> (<c>0x00BBGGRR</c>) from individual RGB components.</summary>
	public static uint Rgb(byte r, byte g, byte b) => (uint)(r | (g << 8) | (b << 16));

	/// <summary>Shows a border around the sampled screen bounds for the specified duration.</summary>
	public static void ShowRectangleFrame(Rectangle sampledBounds, int thicknessPx, uint colorRefBgr, int durationMs)
	{
		// The window is grown by the border thickness on each side, so the frame sits outside the sampled area.
		var thickness = Math.Max(1, thicknessPx);
		var windowBounds = Rectangle.Inflate(sampledBounds, thickness, thickness);
		CursorDiagnosticLogger.Record("overlay_window", "show_rectangle_frame", details: new { sampledBounds, windowBounds, thickness, durationMs });
		RunOnBackgroundThread(() => RunOverlay(
			windowBounds,
			colorRefBgr,
			hwnd => ApplyRectangleFrameRegion(hwnd, windowBounds.Width, windowBounds.Height, thickness),
			drawContent: null,
			durationMs));
	}

	/// <summary>Shows a filled circle within the screen bounds for the specified duration.</summary>
	public static void ShowFilledEllipse(Rectangle bounds, uint colorRefBgr, int durationMs)
	{
		Rectangle safeBounds = new(bounds.X, bounds.Y, Math.Max(1, bounds.Width), Math.Max(1, bounds.Height));
		CursorDiagnosticLogger.Record("overlay_window", "show_filled_ellipse", details: new { bounds = safeBounds, durationMs });
		RunOnBackgroundThread(() => RunOverlay(
			safeBounds,
			colorRefBgr,
			hwnd => ApplyEllipseRegion(hwnd, safeBounds.Width, safeBounds.Height),
			drawContent: null,
			durationMs));
	}
	/// <summary>Shows a text panel near the anchor point. Colors are passed as COLORREF values in BGR order.</summary>
	public static void ShowTextPanel(Point anchor, string text, uint backgroundColorRefBgr, uint textColorRefBgr, int durationMs)
	{
		if (string.IsNullOrEmpty(text))
		{
			CursorDiagnosticLogger.Record("overlay_text", "skipped_empty_text");
			return;
		}

		CursorDiagnosticLogger.Record("overlay_text", "show_text_panel", details: new { anchor, textLength = text.Length, durationMs });
		RunOnBackgroundThread(() =>
		{
			// Measure first so the window can be sized to fit the wrapped text.
			var contentSize = MeasureText(text, TextPanelMaxWidth);
			var windowWidth = contentSize.Width + (TextPanelPaddingPx * 2);
			var windowHeight = contentSize.Height + (TextPanelPaddingPx * 2);
			var bounds = ClampToVirtualScreen(new Rectangle(anchor.X, anchor.Y, windowWidth, windowHeight));
			CursorDiagnosticLogger.Record("overlay_text", "measured", details: new { contentSize, bounds });

			RunOverlay(
				bounds,
				backgroundColorRefBgr,
				shapeWindow: null,
				drawContent: (hdc, paintRect) => DrawPanelText(hdc, paintRect, text, textColorRefBgr),
				durationMs);
		});
	}
}
