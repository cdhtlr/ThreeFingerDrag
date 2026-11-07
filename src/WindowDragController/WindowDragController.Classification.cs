using static ThreeFingerDrag.NativeMethods;

namespace ThreeFingerDrag;

internal static partial class WindowDragController
{
	/// <summary>Classifies the cursor target and reports which check made the decision.</summary>
	private static bool IsEmptyBackgroundAtCursor(nint windowHandle, Point cursorPoint, out string detectionMethod)
	{
		var outcome = DragTargetClassifier.Classify(windowHandle, cursorPoint);
		switch (outcome.Classification)
		{
			case DragTargetClassification.EmptyBackground:
				detectionMethod = outcome.Method ?? "Classifier";
				return true;
			case DragTargetClassification.InteractiveElement:
				detectionMethod = outcome.Method ?? "Classifier";
				return false;
			// Undetermined is not treated as background, so an unclear target does not start a window move.
			default:
				detectionMethod = "Undetermined";
				return false;
		}
	}

	/// <summary>
	/// Samples a square of screen pixels around the cursor. If every pixel matches the center,
	/// the spot is treated as flat background. The result is also drawn as an overlay.
	/// </summary>
	internal static bool TryClassifyBySolidColor(Point cursorPoint, out bool isSingleColor)
	{
		isSingleColor = false;
		var rectangleSize = PointerSettings.GetSolidColorDetectionRectangleSize();
		// An odd square size keeps the cursor pixel exactly at the center of the sample.
		var startX = cursorPoint.X - ((rectangleSize - 1) / 2);
		var startY = cursorPoint.Y - ((rectangleSize - 1) / 2);
		var endX = startX + rectangleSize - 1;
		var endY = startY + rectangleSize - 1;
		Rectangle sampledBounds = Rectangle.FromLTRB(startX, startY, endX + 1, endY + 1);
		var dc = GetDC(0);
		if (dc == 0) return false;

		try
		{
			var centerColor = GetPixel(dc, cursorPoint.X, cursorPoint.Y);
			if (centerColor == ClrInvalid)
			{
				CursorDiagnosticLogger.Record("color_sampling", "GetPixel_failed", cursorPoint, cursorPoint, new { method = "Solid-color sampling", bounds = sampledBounds.ToString() });
				return false;
			}
			// COLORREF is 0x00BBGGRR, so the hex string reverses the byte order to read as #RRGGBB.
			var centerRgb = $"#{centerColor & 0xFF:X2}{(centerColor >> 8) & 0xFF:X2}{(centerColor >> 16) & 0xFF:X2}";

			for (var y = startY; y <= endY; y++)
			{
				for (var x = startX; x <= endX; x++)
				{
					var pixelColor = GetPixel(dc, x, y);
					if (pixelColor == ClrInvalid) return false;

					// One differing pixel is enough. The spot has text, an icon or an edge.
					if (pixelColor != centerColor)
					{
						SolidColorDetectionOverlay.Show(sampledBounds, isSingleColor: false);
						CursorDiagnosticLogger.Record("color_sampling", "mixed_colors", cursorPoint, cursorPoint, new { centerColor = centerRgb, sampledColor = $"#{pixelColor & 0xFF:X2}{(pixelColor >> 8) & 0xFF:X2}{(pixelColor >> 16) & 0xFF:X2}", rectangleSize });
						return true;
					}
				}
			}

			SolidColorDetectionOverlay.Show(sampledBounds, isSingleColor: true);
			CursorDiagnosticLogger.Record("color_sampling", "single_color", cursorPoint, cursorPoint, new { centerColor = centerRgb, rectangleSize });
			isSingleColor = true;
			return true;
		}
		finally
		{
			_ = ReleaseDC(0, dc);
		}
	}

	/// <summary>Runs the solid-color check at touch start and stores the result for later use.</summary>
	internal static bool TryGetTouchStartSolidColor(Point cursorPoint, out bool isSingleColor)
	{
		var succeeded = TryClassifyBySolidColor(cursorPoint, out isSingleColor);
		touchStartSolidColorIsSingleColor = succeeded ? isSingleColor : null;
		return succeeded;
	}

	/// <summary>Forgets the solid-color sample taken at touch start, so a stale result is not reused.</summary>
	internal static void ClearTouchStartSolidColorResult() => touchStartSolidColorIsSingleColor = null;

	/// <summary>Reads one screen pixel as a #RRGGBB string for diagnostics. Returns null if it cannot be read.</summary>
	private static string? GetColorHexAt(Point cursorPoint)
	{
		var dc = GetDC(0);
		if (dc == 0) return null;

		try
		{
			var color = GetPixel(dc, cursorPoint.X, cursorPoint.Y);
			return color == ClrInvalid ? null : $"#{color & 0xFF:X2}{(color >> 8) & 0xFF:X2}{(color >> 16) & 0xFF:X2}";
		}
		finally { _ = ReleaseDC(0, dc); }
	}
}
