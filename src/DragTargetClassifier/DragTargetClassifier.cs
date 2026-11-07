namespace ThreeFingerDrag;

/// <summary>
/// Checks the target under the cursor once at drag start. Detectors run in the order set in
/// PointerSettings, and the first decisive result wins. If none is decisive, the target is
/// treated as non-window.
/// </summary>
internal static partial class DragTargetClassifier
{
	// HTCLIENT (1) and anything else (HTNOWHERE/HTTRANSPARENT/HTERROR/failure) are inconclusive.

	// Classic Win32 control classes used as a fallback when UI Automation has no answer.
	// Matching is case-insensitive. Any hit here means the point is on a control, not on chrome.
	private static readonly string[] InteractiveWindowClassNames =
	[
		"Button",
		"Edit",
		"RichEdit",
		"RichEdit20A",
		"RichEdit20W",
		"RICHEDIT50W",
		"ComboBox",
		"ComboBoxEx32",
		"ScrollBar",
		"msctls_trackbar32",
		"msctls_updown32",
		"msctls_hotkey32",
		"msctls_progress32",
		"SysListView32",
		"SysTreeView32",
		"SysTabControl32",
		"SysLink",
		"SysDateTimePick32",
		"SysAnimate32",
		"ToolbarWindow32",
		"Static",
	];

	/// <summary>Runs the configured detectors in order and returns the first decisive result.</summary>
	/// <param name="rootWindowHandle">The top-level window under the cursor (used for hit-testing).</param>
	/// <param name="cursorPoint">The cursor position in screen coordinates.</param>
	public static DragClassificationOutcome Classify(nint rootWindowHandle, Point cursorPoint)
	{
		// Order matters. The first decisive answer ends the chain, so a slow or broad detector
		// placed early can hide a more precise one further down.
		foreach (var detection in PointerSettings.GetTargetDetectionOrder())
		{
			var (classification, method) = detection switch
			{
				TargetDetection.UiAutomation => ClassifyByUiAutomation(cursorPoint),
				TargetDetection.Ocr => ClassifyByOcr(cursorPoint),
				TargetDetection.HitTest => (ClassifyByHitTest(rootWindowHandle, cursorPoint), "Hit-test (WM_NCHITTEST)"),
				TargetDetection.SolidColor => ClassifyBySolidColor(cursorPoint),
				TargetDetection.CursorShape => (ClassifyByCursorShape(cursorPoint), "Cursor shape"),
				TargetDetection.ControlClassName => (ClassifyByControlClassName(cursorPoint), "Control class name"),
				_ => (DragTargetClassification.Undetermined, "Undetermined"),
			};

			var enabled = IsDetectionEnabled(detection);
			var selected = classification != DragTargetClassification.Undetermined;
			CursorDiagnosticLogger.Record("target_detection", "attempt", cursorPoint, cursorPoint,
				new
				{
					targetDetection = detection.ToString(),
					enabled,
					classification = classification.ToString(),
					method,
					selected,
					reason = selected ? GetSelectedReason(detection, classification) : GetUndeterminedReason(detection, enabled),
					inputTarget = CursorDiagnosticLogger.IsEnabled ? WindowDragController.DescribeWindowAtPoint(cursorPoint) : null,
					accessibleElement = CursorDiagnosticLogger.IsEnabled ? UiAutomationDragClassifier.DescribeElementAtPoint(cursorPoint) : null
				});

			if (selected)
			{
				CursorDiagnosticLogger.Record("target_detection", "selected", cursorPoint, cursorPoint, new { targetDetection = detection.ToString(), classification = classification.ToString(), method });
				return new DragClassificationOutcome(classification, method);
			}
		}

		CursorDiagnosticLogger.Record("target_detection", "unresolved", cursorPoint, cursorPoint, new { reason = "Every configured detector returned Undetermined; caller treats the target as non-window/interactive." });
		return DragClassificationOutcome.Undetermined;
	}

	/// <summary>Returns whether the user has turned on the given detector in settings.</summary>
	private static bool IsDetectionEnabled(TargetDetection detection) => detection switch
	{
		TargetDetection.UiAutomation => PointerSettings.IsUiAutomationDetectionEnabled(),
		TargetDetection.Ocr => PointerSettings.IsOcrCaptureEnabled(),
		TargetDetection.HitTest => PointerSettings.IsHitTestDetectionEnabled(),
		TargetDetection.SolidColor => PointerSettings.IsSolidColorDetectionEnabled(),
		TargetDetection.CursorShape => PointerSettings.IsCursorShapeDetectionEnabled(),
		TargetDetection.ControlClassName => PointerSettings.IsControlClassNameDetectionEnabled(),
		_ => false,
	};

	/// <summary>Builds the diagnostic text explaining why a detector gave no answer.</summary>
	private static string GetUndeterminedReason(TargetDetection detection, bool enabled)
	{
		if (!enabled) return "Detector is disabled in settings.";

		return detection switch
		{
			TargetDetection.UiAutomation => "UI Automation returned no decisive pattern/control result, timed out, or its provider was unavailable.",
			TargetDetection.Ocr => "No recognized OCR text was cached at this exact point; OCR is a hold-triggered detector.",
			TargetDetection.HitTest => "The target returned a client/unknown hit-test value, did not respond, or the call failed.",
			TargetDetection.SolidColor => "Pixel sampling was inconclusive/unavailable and no usable touch-start sample existed.",
			TargetDetection.CursorShape => "The current cursor shape was unavailable or did not identify a draggable/interactive target.",
			TargetDetection.ControlClassName => "The point was not inside a recognized Win32 control class.",
			_ => "No classification was available.",
		};
	}

	/// <summary>Returns the log text explaining which detector produced the final classification.</summary>
	private static string GetSelectedReason(TargetDetection detection, DragTargetClassification classification) => detection switch
	{
		TargetDetection.UiAutomation when classification == DragTargetClassification.DraggableElement => "UI Automation found a drag pattern on the element.",
		TargetDetection.UiAutomation when classification == DragTargetClassification.EmptyBackground => "UI Automation found no control/content element at the point.",
		TargetDetection.UiAutomation => "UI Automation identified an interactive control or pattern.",
		TargetDetection.Ocr => "Previously captured OCR text matches this exact point.",
		TargetDetection.HitTest => "The window returned a decisive non-client or interactive hit-test code.",
		TargetDetection.SolidColor => "The sampled pixels classified the point as uniform background or mixed interactive content.",
		TargetDetection.CursorShape => "The current cursor shape indicates an interactive control or empty text area.",
		TargetDetection.ControlClassName => "The child window class matched a known interactive Win32 control.",
		_ => "This detector produced the classification.",
	};

	/// <summary>Reports interactive text only when OCR has cached recognized text at this exact point.</summary>
	private static (DragTargetClassification Classification, string Method) ClassifyByOcr(Point cursorPoint)
	{
		var recognized = PointerSettings.IsOcrCaptureEnabled() && OcrCaptureService.HasRecognizedTextAt(cursorPoint);
		return (recognized ? DragTargetClassification.InteractiveElement : DragTargetClassification.Undetermined, "OCR text");
	}

	/// <summary>Asks UI Automation for the element at the cursor and reports drag or interactive support.</summary>
	private static (DragTargetClassification Classification, string Method) ClassifyByUiAutomation(Point cursorPoint)
	{
		var classification = UiAutomationDragClassifier.Classify(cursorPoint);
		var method = classification == DragTargetClassification.DraggableElement ? "UI Automation drag pattern" : "UI Automation";
		return (classification, method);
	}

	/// <summary>Uses the solid-color sample taken at touch start, or samples now, to tell background from content.</summary>
	private static (DragTargetClassification Classification, string Method) ClassifyBySolidColor(Point cursorPoint)
	{
		if (!PointerSettings.IsSolidColorDetectionEnabled()) return (DragTargetClassification.Undetermined, "Solid-color sampling");

		// A sample taken now is tried first. The touch-start sample is only used when this one is inconclusive.
		if (WindowDragController.TryClassifyBySolidColor(cursorPoint, out var isSingleColor)) return (isSingleColor ? DragTargetClassification.EmptyBackground : DragTargetClassification.InteractiveElement, "Solid-color sampling");

		if (WindowDragController.HasTouchStartSolidColorResult) return (WindowDragController.TouchStartSolidColorIsSingleColor ? DragTargetClassification.EmptyBackground : DragTargetClassification.InteractiveElement, "Solid-color sampling (touch start)");

		return (DragTargetClassification.Undetermined, "Solid-color sampling");
	}

}
