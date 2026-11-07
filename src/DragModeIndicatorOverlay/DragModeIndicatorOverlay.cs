namespace ThreeFingerDrag;

/// <summary>Shows optional drag-mode feedback beside the cursor, with classifier details when enabled.</summary>
internal static partial class DragModeIndicatorOverlay
{
	/// <summary>Shows the classification result and optional details near the cursor.</summary>
	/// <param name="cursorPoint">Cursor position in screen coordinates at the moment of classification.</param>
	/// <param name="isWindowDrag">Whether the gesture will move the window.</param>
	/// <param name="detectionMethod">Classifier or override that made the decision.</param>
	/// <param name="recognizedText">Optional OCR preview for the details panel.</param>
	public static void Show(Point cursorPoint, bool isWindowDrag, string detectionMethod, string? recognizedText)
	{
		if (!PointerSettings.ShouldShowDragModeIndicator())
		{
			CursorDiagnosticLogger.Record("drag_mode_indicator", "skipped_disabled");
			return;
		}

		var diameter = PointerSettings.GetDragModeIndicatorDiameter();
		Rectangle circleBounds = new(cursorPoint.X + CursorOffsetPx, cursorPoint.Y + CursorOffsetPx, diameter, diameter);
		var fillColor = GetDragColor(isWindowDrag);
		var displayMs = PointerSettings.GetDragModeIndicatorDisplayMs();
		CursorDiagnosticLogger.Record("drag_mode_indicator", "circle_shown", cursorPoint, details: new { isWindowDrag, detectionMethod, circleBounds, displayMs });
		OverlayWindow.ShowFilledEllipse(circleBounds, fillColor, displayMs);

		// The details panel is optional. It sits to the right of the circle, top-aligned with it.
		if (!PointerSettings.ShouldShowDragModeIndicatorDetails()) return;

		var text = BuildDetailsText(isWindowDrag, detectionMethod, recognizedText);
		Point panelAnchor = new(circleBounds.Right + PanelGapPx, circleBounds.Top);
		CursorDiagnosticLogger.Record("drag_mode_indicator", "details_panel_shown", panelAnchor, details: new { text });
		OverlayWindow.ShowTextPanel(panelAnchor, text, GetColorRef(PointerSettings.GetIndicatorPanelBackgroundColor()), GetColorRef(PointerSettings.GetIndicatorPanelTextColor()), displayMs);
	}

	/// <summary>Shows a window-drag indicator when the drag moves the window with set window position (forced window mode).</summary>
	public static void ShowForcedWindowMode(Point cursorPoint)
	{
		if (!PointerSettings.ShouldShowDragModeIndicator())
		{
			CursorDiagnosticLogger.Record("drag_mode_indicator", "forced_window_skipped_disabled");
			return;
		}

		var diameter = PointerSettings.GetDragModeIndicatorDiameter();
		Rectangle circleBounds = new(cursorPoint.X + CursorOffsetPx, cursorPoint.Y + CursorOffsetPx, diameter, diameter);
		var displayMs = PointerSettings.GetDragModeIndicatorDisplayMs();
		CursorDiagnosticLogger.Record("drag_mode_indicator", "forced_window_shown", cursorPoint, details: new { circleBounds, displayMs });
		OverlayWindow.ShowFilledEllipse(circleBounds, GetColorRef(PointerSettings.GetWindowPositionFallbackIndicatorColor()), displayMs);
		if (!PointerSettings.ShouldShowDragModeIndicatorDetails()) return;

		Point panelAnchor = new(circleBounds.Right + PanelGapPx, circleBounds.Top);
		CursorDiagnosticLogger.Record("drag_mode_indicator", "forced_window_details_panel_shown", panelAnchor);
		OverlayWindow.ShowTextPanel(panelAnchor, "Window drag" + Environment.NewLine + "Method: Forced window mode", GetColorRef(PointerSettings.GetIndicatorPanelBackgroundColor()), GetColorRef(PointerSettings.GetIndicatorPanelTextColor()), displayMs);
	}

	/// <summary>Shows a window-drag indicator when a touch, mouse, or stylus hold forced window dragging.</summary>
	/// <param name="cursorPoint">Cursor position in screen coordinates.</param>
	/// <param name="method">Label shown in the details panel, for example "Mouse hold forced".</param>
	public static void ShowHoldForceWindowDrag(Point cursorPoint, string method)
	{
		if (!PointerSettings.ShouldShowTouchHoldForceWindowDragIndicator())
		{
			CursorDiagnosticLogger.Record("drag_mode_indicator", "hold_force_skipped_disabled", details: new { method });
			return;
		}

		var diameter = PointerSettings.GetDragModeIndicatorDiameter();
		Rectangle circleBounds = new(cursorPoint.X + CursorOffsetPx, cursorPoint.Y + CursorOffsetPx, diameter, diameter);
		var displayMs = PointerSettings.GetTouchHoldForceWindowDragIndicatorDisplayMs();
		CursorDiagnosticLogger.Record("drag_mode_indicator", "hold_force_shown", cursorPoint, details: new { method, circleBounds, displayMs });
		OverlayWindow.ShowFilledEllipse(circleBounds, GetColorRef(PointerSettings.GetTouchHoldForceWindowDragIndicatorColor()), displayMs);
		if (!PointerSettings.ShouldShowDragModeIndicatorDetails()) return;

		Point panelAnchor = new(circleBounds.Right + PanelGapPx, circleBounds.Top);
		CursorDiagnosticLogger.Record("drag_mode_indicator", "hold_force_details_panel_shown", panelAnchor, details: new { method });
		OverlayWindow.ShowTextPanel(panelAnchor, "Window drag" + Environment.NewLine + "Method: " + method, GetColorRef(PointerSettings.GetIndicatorPanelBackgroundColor()), GetColorRef(PointerSettings.GetIndicatorPanelTextColor()), displayMs);
	}

	/// <summary>Builds the multi-line details text shown in the indicator panel.</summary>
	private static string BuildDetailsText(bool isWindowDrag, string detectionMethod, string? recognizedText)
	{
		var modeLabel = isWindowDrag ? "Window drag" : "Other drag";
		var text = $"{modeLabel}{Environment.NewLine}Method: {detectionMethod}";
		if (!string.IsNullOrWhiteSpace(recognizedText))
		{
			// Line breaks become spaces so the OCR preview stays on one line in the panel.
			var preview = recognizedText.Replace('\r', ' ').Replace('\n', ' ').Trim();
			if (preview.Length > MaxOcrPreviewLength)
			{
				CursorDiagnosticLogger.Record("drag_mode_indicator", "ocr_preview_truncated", details: new { originalLength = preview.Length, maxLength = MaxOcrPreviewLength });
				preview = string.Concat(preview.AsSpan(0, MaxOcrPreviewLength), "...");
			}
			text += $"{Environment.NewLine}OCR: {preview}";
		}

		return text;
	}

	/// <summary>Returns the COLORREF for the indicator. Window drags and other drags use separate settings.</summary>
	private static uint GetDragColor(bool isWindowDrag)
	{
		var color = isWindowDrag ? PointerSettings.GetWindowDragIndicatorColor() : PointerSettings.GetOtherDragIndicatorColor();
		return GetColorRef(color);
	}

	/// <summary>Converts a managed color to the Win32 COLORREF layout used by the overlay.</summary>
	private static uint GetColorRef(Color color) => OverlayWindow.Rgb(color.R, color.G, color.B);
}
