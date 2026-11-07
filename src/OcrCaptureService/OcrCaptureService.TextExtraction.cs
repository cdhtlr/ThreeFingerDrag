using Windows.Media.Ocr;

namespace ThreeFingerDrag;

internal static partial class OcrCaptureService
{
	/// <summary>Returns the words around the cursor on one OCR line, or null if no word contains it.</summary>
	private static string? ExtractTextAtCursor(OcrResult result, Point cursor)
	{
		// Choose the nearest recognized word containing the cursor, then extend across nearby words.
		var gapTolerancePercent = PointerSettings.GetOcrMaximumWordGapPercent();
		List<OcrWord> candidates = [.. result.Lines
			.SelectMany(line => line.Words.Select(word => new OcrWord(line, word)))
			.Where(item => Contains(item, cursor))
			.OrderBy(item => DistanceToCenter(item, cursor))];
		if (candidates.Count == 0)
		{
			CursorDiagnosticLogger.Record("ocr_text_extraction", "not_found", details: new { lineCount = result.Lines.Count, candidateCount = 0 });
			return null;
		}

		var selected = candidates[0];
		// OCR returns words by line; sorting by X makes adjacent-word expansion deterministic.
		var bounds = selected.Word.BoundingRect;
		List<Windows.Media.Ocr.OcrWord> words = [.. selected.Line.Words.OrderBy(word => word.BoundingRect.X)];
		var selectedIndex = words.FindIndex(word => word.Text == selected.Word.Text
			&& word.BoundingRect.X == selected.Word.BoundingRect.X
			&& word.BoundingRect.Y == selected.Word.BoundingRect.Y);
		// Expand left and right from the selected word, stopping at the first large gap.
		var left = selectedIndex;
		var right = selectedIndex;
		// Walk left while gaps stay within the configured fraction of the word height.
		for (var i = selectedIndex - 1; i >= 0; i--)
		{
			var previous = words[i].BoundingRect;
			var gap = bounds.X - (previous.X + previous.Width);
			if (gap * 100 > Math.Max(bounds.Height, previous.Height) * gapTolerancePercent) break;

			left = i;
			bounds = Union(bounds, previous);
		}
		// Restart from the selected word before measuring gaps on its right side.
		bounds = selected.Word.BoundingRect;
		for (var i = selectedIndex + 1; i < words.Count; i++)
		{
			var next = words[i].BoundingRect;
			var gap = next.X - (bounds.X + bounds.Width);
			if (gap * 100 > Math.Max(bounds.Height, next.Height) * gapTolerancePercent) break;

			right = i;
			bounds = Union(bounds, next);
		}

		// Join the selected run with spaces, so text from separate OCR words reads naturally.
		var lineWords = words.Skip(left).Take(right - left + 1).Select(word => word.Text);
		var text = string.Join(" ", lineWords).Trim();
		CursorDiagnosticLogger.Record("ocr_text_extraction", "found", details: new { lineCount = result.Lines.Count, candidateCount = candidates.Count, lineWordCount = words.Count, selectedWordCount = right - left + 1, textLength = text.Length });
		return text;
	}

	/// <summary>True when the point lies inside the word bounding box, edges included.</summary>
	private static bool Contains(OcrWord word, Point point)
	{
		// OCR rectangles and the cursor point are both relative to the captured bitmap here.
		var rect = word.Word.BoundingRect;
		return point.X >= rect.X && point.X <= rect.X + rect.Width && point.Y >= rect.Y && point.Y <= rect.Y + rect.Height;
	}

	/// <summary>Squared distance from the point to the center of the word box.</summary>
	private static double DistanceToCenter(OcrWord word, Point point)
	{
		// Squared distance avoids a square root while preserving nearest-center ordering.
		var rect = word.Word.BoundingRect;
		var dx = point.X - (rect.Left + rect.Width / 2.0);
		var dy = point.Y - (rect.Top + rect.Height / 2.0);
		return dx * dx + dy * dy;
	}

	/// <summary>Pairs an OCR word with its line so word neighbors can be read.</summary>
	private sealed record OcrWord(OcrLine Line, Windows.Media.Ocr.OcrWord Word);

	/// <summary>Combines two word rectangles into one bounding rectangle.</summary>
	private static Windows.Foundation.Rect Union(Windows.Foundation.Rect first, Windows.Foundation.Rect second)
	{
		// Return the smallest rectangle that encloses both OCR word rectangles.
		var left = Math.Min(first.X, second.X);
		var top = Math.Min(first.Y, second.Y);
		var right = Math.Max(first.X + first.Width, second.X + second.Width);
		var bottom = Math.Max(first.Y + first.Height, second.Y + second.Height);
		return new Windows.Foundation.Rect(left, top, right - left, bottom - top);
	}
}
