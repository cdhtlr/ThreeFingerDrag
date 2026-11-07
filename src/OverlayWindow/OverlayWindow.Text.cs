using static ThreeFingerDrag.NativeMethods;

namespace ThreeFingerDrag;

internal static partial class OverlayWindow
{
	/// <summary>Draws the panel text inside the padded paint area, using the default GUI font.</summary>
	private static void DrawPanelText(nint hdc, RECT paintRect, string text, uint textColorRefBgr)
	{
		CursorDiagnosticLogger.Record("overlay_text", "draw", details: new { textLength = text.Length, paintRect });
		// Transparent background mode lets the window's solid brush show behind the text.
		var previousFont = SelectObject(hdc, GetStockObject(DefaultGuiFont));
		var previousBkMode = SetBkMode(hdc, TransparentBkMode);
		var previousColor = SetTextColor(hdc, textColorRefBgr);
		try
		{
			RECT textRect = new()
			{
				Left = paintRect.Left + TextPanelPaddingPx,
				Top = paintRect.Top + TextPanelPaddingPx,
				Right = paintRect.Right - TextPanelPaddingPx,
				Bottom = paintRect.Bottom - TextPanelPaddingPx,
			};
			_ = DrawTextW(hdc, text, -1, ref textRect, DtWordBreak | DtNoPrefix);
		}
		finally
		{
			_ = SetTextColor(hdc, previousColor);
			_ = SetBkMode(hdc, previousBkMode);
			_ = SelectObject(hdc, previousFont);
		}
	}

	/// <summary>Returns the size of <paramref name="text"/> when wrapped to <paramref name="maxWidth"/>, using the same font as the drawing code.</summary>
	private static Size MeasureText(string text, int maxWidth)
	{
		// Measure on the screen DC with the same font and wrapping flags used when drawing.
		var screenDc = GetDC(0);
		if (screenDc == 0)
		{
			CursorDiagnosticLogger.Record("overlay_text", "measure_no_screen_dc", details: new { maxWidth });
			return new Size(maxWidth, 40);
		}

		try
		{
			var previousFont = SelectObject(screenDc, GetStockObject(DefaultGuiFont));
			try
			{
				RECT rect = new() { Left = 0, Top = 0, Right = maxWidth, Bottom = 0 };
				_ = DrawTextW(screenDc, text, -1, ref rect, DtCalcRect | DtWordBreak | DtNoPrefix);
				var size = new Size(Math.Max(1, rect.Right - rect.Left), Math.Max(1, rect.Bottom - rect.Top));
				CursorDiagnosticLogger.Record("overlay_text", "measure_result", details: new { maxWidth, width = size.Width, height = size.Height });
				return size;
			}
			finally
			{
				_ = SelectObject(screenDc, previousFont);
			}
		}
		finally
		{
			_ = ReleaseDC(0, screenDc);
		}
	}
}
