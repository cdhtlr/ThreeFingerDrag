using System.Runtime.InteropServices;
using static ThreeFingerDrag.NativeMethods;

namespace ThreeFingerDrag;

/// <summary>
/// Captures a region of the screen into a raw top-down 32bpp BGRA pixel buffer using plain GDI calls,
/// avoiding any dependency on <c>System.Drawing.Common</c>/GDI+.
/// </summary>
internal static partial class ScreenCapture
{
	// Each capture gets its own screen DC (window handle 0) and releases it when done.

	// BITMAPINFOHEADER mirrors the Win32 header used to request 32-bit uncompressed pixels.

	// GetDIBits only needs the header for BI_RGB data, so no color table is included.

	/// <summary>
	/// Captures <paramref name="region"/> (screen coordinates) into a top-down BGRA32 buffer.
	/// Returns <see langword="null"/> if any step fails.
	/// </summary>
	public static byte[]? CaptureRegion(Rectangle region, out int width, out int height)
	{
		// Preserve the requested dimensions for callers, even when capture cannot proceed.
		width = region.Width;
		height = region.Height;
		if (width <= 0 || height <= 0)
		{
			CursorDiagnosticLogger.Record("screen_capture_region", "invalid_size", details: new { width, height });
			return null;
		}

		// A compatible memory DC and bitmap provide a temporary destination for BitBlt.
		// GetDC(0) returns the device context of the whole virtual desktop, which BitBlt copies from.
		var screenDc = GetDC(0);
		if (screenDc == 0)
		{
			CursorDiagnosticLogger.Record("screen_capture_region", "get_dc_failed", details: new { error = Marshal.GetLastWin32Error() });
			return null;
		}

		try
		{
			var memoryDc = CreateCompatibleDC(screenDc);
			if (memoryDc == 0)
			{
				CursorDiagnosticLogger.Record("screen_capture_region", "create_dc_failed", details: new { error = Marshal.GetLastWin32Error() });
				return null;
			}

			try
			{
				var bitmap = CreateCompatibleBitmap(screenDc, width, height);
				if (bitmap == 0)
				{
					CursorDiagnosticLogger.Record("screen_capture_region", "create_bitmap_failed", details: new { width, height, error = Marshal.GetLastWin32Error() });
					return null;
				}

				try
				{
					// SelectObject returns the object that must be restored before deleting the bitmap.
					var previousBitmap = SelectObject(memoryDc, bitmap);
					try
					{
						if (!BitBlt(memoryDc, 0, 0, width, height, screenDc, region.Left, region.Top, SrcCopy))
						{
							CursorDiagnosticLogger.Record("screen_capture_region", "bitblt_failed", details: new { region.Left, region.Top, width, height, error = Marshal.GetLastWin32Error() });
							return null;
						}

						// Negative height requests top-down rows, matching ordinary image-buffer order.
						// BITMAPINFOHEADER asks for 32-bit uncompressed pixels (BI_RGB) with no color table.
						BITMAPINFO bitmapInfo = new()
						{
							bmiHeader = new BITMAPINFOHEADER
							{
								biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
								biWidth = width,
								biHeight = -height, // negative = top-down, avoids a manual row-flip.
								biPlanes = 1,
								biBitCount = BitsPerPixel,
								biCompression = 0, // BI_RGB
							},
						};

						// BGRA32 uses four bytes per pixel; GetDIBits fills this managed buffer.
						var buffer = new byte[width * height * BytesPerPixel];
						// GetDIBits copies the bitmap rows into the buffer. It returns 0 on failure.
						var copiedScanLines = GetDIBits(memoryDc, bitmap, 0, (uint)height, buffer, ref bitmapInfo, DibRgbColors);
						if (copiedScanLines == 0)
						{
							CursorDiagnosticLogger.Record("screen_capture_region", "get_dibits_failed", details: new { width, height, error = Marshal.GetLastWin32Error() });
							return null;
						}
						CursorDiagnosticLogger.Record("screen_capture_region", "success", details: new { width, height, scanLines = copiedScanLines });
						return buffer;
					}
					finally
					{
						_ = SelectObject(memoryDc, previousBitmap);
					}
				}
				finally
				{
					_ = DeleteObject(bitmap);
				}
			}
			finally
			{
				_ = DeleteDC(memoryDc);
			}
		}
		// Release the screen DC even when capture fails. Leaking it would exhaust GDI handles over time.
		finally
		{
			_ = ReleaseDC(0, screenDc);
		}
	}
}
