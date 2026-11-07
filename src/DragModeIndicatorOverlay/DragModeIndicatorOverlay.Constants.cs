namespace ThreeFingerDrag;

internal static partial class DragModeIndicatorOverlay
{
	// Offset from the cursor tip, so the circle does not cover the pointer.
	private const int CursorOffsetPx = 14;
	// Gap between the circle's right edge and the details panel.
	private const int PanelGapPx = 6;
	// Long OCR text is cut to this many characters to keep the panel small.
	private const int MaxOcrPreviewLength = 160;
}