namespace ThreeFingerDrag;

/// <summary>
/// Requirement 1's opt-in visual feedback for the solid-color pixel-sampling fallback in
/// <see cref="WindowDragController"/>: briefly outlines the exact rectangle of pixels that was sampled
/// around the cursor, in blue if every sampled pixel was the same color (so the drag was/would be
/// treated as empty background) or red otherwise (mixed colors found, so the drag was/would be passed
/// through as a normal element drag instead). Gated behind <c>showSolidColorDetectionRectangle</c> so it
/// stays invisible unless a user opts in.
/// </summary>
internal static partial class SolidColorDetectionOverlay
{
	/// <summary>Shows the sampled-rectangle frame overlay if enabled in configuration; otherwise does nothing.</summary>
	public static void Show(Rectangle sampledBounds, bool isSingleColor)
	{
		// This diagnostic frame is deliberately opt-in to avoid adding UI during normal drags.
		if (!PointerSettings.ShouldShowSolidColorDetectionRectangle())
		{
			CursorDiagnosticLogger.Record("solid_color_overlay", "skipped_disabled");
			return;
		}

		var configuredColor = isSingleColor ? PointerSettings.GetSolidColorUniformBorderColor() : PointerSettings.GetSolidColorMixedBorderColor();
		var color = OverlayWindow.Rgb(configuredColor.R, configuredColor.G, configuredColor.B);
		var displayMs = PointerSettings.GetSolidColorDetectionRectangleDisplayMs();
		CursorDiagnosticLogger.Record("solid_color_overlay", "frame_shown", details: new { sampledBounds, isSingleColor, displayMs });
		OverlayWindow.ShowRectangleFrame(sampledBounds, BorderThicknessPx, color, displayMs);
	}
}
