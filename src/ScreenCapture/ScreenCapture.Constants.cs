namespace ThreeFingerDrag;

internal static partial class ScreenCapture
{
	// GDI flags for a direct pixel copy and the default RGB color table.
	private const uint SrcCopy = 0x00CC0020;
	private const uint DibRgbColors = 0;
	private const int BitsPerPixel = 32;
	private const int BytesPerPixel = BitsPerPixel / 8;
}