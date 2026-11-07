using static ThreeFingerDrag.NativeMethods;

namespace ThreeFingerDrag;

internal static partial class OverlayWindow
{
	/// <summary>Clips the window to a rectangular ring whose border is <paramref name="thicknessPx"/> wide.</summary>
	private static void ApplyRectangleFrameRegion(nint hwnd, int width, int height, int thicknessPx)
	{
		var innerLeft = Math.Min(thicknessPx, width / 2);
		var innerTop = Math.Min(thicknessPx, height / 2);
		// Outer rectangle minus inner rectangle leaves only the border. CombineRgn needs an existing destination region.
		var outerRgn = CreateRectRgn(0, 0, width, height);
		var innerRgn = CreateRectRgn(innerLeft, innerTop, Math.Max(innerLeft, width - innerLeft), Math.Max(innerTop, height - innerTop));
		var frameRgn = CreateRectRgn(0, 0, 0, 0);
		try
		{
			_ = CombineRgn(frameRgn, outerRgn, innerRgn, RgnDiff);
			if (SetWindowRgn(hwnd, frameRgn, false) != 0)
			{
				frameRgn = 0; // Ownership transferred to the window; do not delete it below.
				CursorDiagnosticLogger.Record("overlay_shape", "frame_region_applied", details: new { width, height, thicknessPx });
			}
			else
			{
				CursorDiagnosticLogger.Record("overlay_shape", "frame_region_set_failed", details: new { width, height, thicknessPx });
			}
		}
		finally
		{
			_ = DeleteObject(outerRgn);
			_ = DeleteObject(innerRgn);
			if (frameRgn != 0) _ = DeleteObject(frameRgn);
		}
	}

	/// <summary>Clips the window to a filled ellipse that fits the given size.</summary>
	private static void ApplyEllipseRegion(nint hwnd, int width, int height)
	{
		// SetWindowRgn takes ownership of the region on success. The region is deleted here only when it failed.
		var ellipseRgn = CreateEllipticRgn(0, 0, width, height);
		if (ellipseRgn == 0)
		{
			CursorDiagnosticLogger.Record("overlay_shape", "ellipse_region_create_failed", details: new { width, height });
			return;
		}

		if (SetWindowRgn(hwnd, ellipseRgn, false) == 0)
		{
			_ = DeleteObject(ellipseRgn);
			CursorDiagnosticLogger.Record("overlay_shape", "ellipse_region_set_failed", details: new { width, height });
			return;
		}

		CursorDiagnosticLogger.Record("overlay_shape", "ellipse_region_applied", details: new { width, height });
	}
}
