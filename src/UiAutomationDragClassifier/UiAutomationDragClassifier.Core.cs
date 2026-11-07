using System.Runtime.InteropServices;
using Interop.UIAutomationClient;

namespace ThreeFingerDrag;

internal static partial class UiAutomationDragClassifier
{
	/// <summary>Samples a grid of points across the square and returns true on the first text-like element.</summary>
	private static bool HasTextNearCore(Point cursorPoint, int areaSize)
	{
		try
		{
			IUIAutomation automation = (IUIAutomation)new CUIAutomationClass();
			var halfSize = areaSize / 2;
			// A bounded 5x5 hit-test grid checks the configured square, while avoiding a costly
			// full accessibility-tree walk for complex documents and browser pages.
			const int samplesPerAxis = 5;
			for (var row = 0; row < samplesPerAxis; row++)
			{
				for (var column = 0; column < samplesPerAxis; column++)
				{
					// Samples run from the square's near edge to its far edge, so both corners are included.
					var x = cursorPoint.X - halfSize + (areaSize * column / (samplesPerAxis - 1));
					var y = cursorPoint.Y - halfSize + (areaSize * row / (samplesPerAxis - 1));
					var element = automation.ElementFromPoint(new tagPOINT { x = x, y = y });
					if (element is null) continue;

					var controlType = element.CurrentControlType;
					if (controlType is UIA_ControlTypeIds.UIA_TextControlTypeId or UIA_ControlTypeIds.UIA_EditControlTypeId or UIA_ControlTypeIds.UIA_DocumentControlTypeId)
					{
						CursorDiagnosticLogger.Record("text_probe", "text_found", details: new { x, y, controlType, areaSize });
						return true;
					}
				}
			}

			CursorDiagnosticLogger.Record("text_probe", "no_text_found", details: new { areaSize });
			return false;
		}
		catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
		{
			CursorDiagnosticLogger.Record("text_probe", "exception", details: new { exceptionType = ex.GetType().FullName, message = ex.Message });
			return true;
		}
	}

	/// <summary>Maps the element under the cursor to a drag-target class. Checks run in order, and the first match wins.</summary>
	private static DragTargetClassification ClassifyCore(Point cursorPoint)
	{
		try
		{
			IUIAutomation automation = (IUIAutomation)new CUIAutomationClass();
			var element = automation.ElementFromPoint(new tagPOINT { x = cursorPoint.X, y = cursorPoint.Y });
			if (element is null)
			{
				CursorDiagnosticLogger.Record("classification", "no_element_at_point", details: new { result = nameof(DragTargetClassification.Undetermined) });
				return DragTargetClassification.Undetermined;
			}

			// A title bar is window chrome, so dragging it moves the window.
			var controlType = element.CurrentControlType;
			if (controlType == UIA_ControlTypeIds.UIA_TitleBarControlTypeId)
			{
				CursorDiagnosticLogger.Record("classification", "title_bar", details: new { controlType, result = nameof(DragTargetClassification.EmptyBackground) });
				return DragTargetClassification.EmptyBackground;
			}

			// A Drag pattern means the element is meant to be dragged, so it is not a window drag.
			if (IsDraggable(element))
			{
				CursorDiagnosticLogger.Record("classification", "draggable_element", details: new { controlType, result = nameof(DragTargetClassification.DraggableElement) });
				return DragTargetClassification.DraggableElement;
			}

			if (InteractiveControlTypes.Contains(controlType))
			{
				CursorDiagnosticLogger.Record("classification", "interactive_control_type", details: new { controlType, result = nameof(DragTargetClassification.InteractiveElement) });
				return DragTargetClassification.InteractiveElement;
			}

			if (HasSupplementaryInteractivePattern(element))
			{
				CursorDiagnosticLogger.Record("classification", "interactive_pattern", details: new { controlType, result = nameof(DragTargetClassification.InteractiveElement) });
				return DragTargetClassification.InteractiveElement;
			}

			var isControlElement = element.CurrentIsControlElement != 0;
			var isContentElement = element.CurrentIsContentElement != 0;
			if (!isControlElement && !isContentElement)
			{
				// Purely structural/decorative element (e.g. a plain background Pane) with no semantic
				// identity of its own: treat as empty background.
				CursorDiagnosticLogger.Record("classification", "structural_background", details: new { controlType, result = nameof(DragTargetClassification.EmptyBackground) });
				return DragTargetClassification.EmptyBackground;
			}

			CursorDiagnosticLogger.Record("classification", "undetermined", details: new { controlType, isControlElement, isContentElement, result = nameof(DragTargetClassification.Undetermined) });
			return DragTargetClassification.Undetermined;
		}
		catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
		{
			CursorDiagnosticLogger.Record("classification", "exception", details: new { exceptionType = ex.GetType().FullName, message = ex.Message, result = nameof(DragTargetClassification.Undetermined) });
			return DragTargetClassification.Undetermined;
		}
	}

	/// <summary>Returns true when the element exposes the UIA Drag pattern.</summary>
	private static bool IsDraggable(IUIAutomationElement element)
	{
		// UIA's Drag pattern is the strongest semantic signal where providers expose it. The
		// generated interop interface may not include the newer pattern method on every SDK build,
		// so probe through the generic pattern API and treat unsupported patterns as unavailable.
		// 10030 is UIA_DragPatternId. It is written as a literal because older SDK interop files do not define it.
		const int UiaDragPatternId = 10030;
		try
		{
			if (element.GetCurrentPattern(UiaDragPatternId) is not null) return true;
		}
		catch (COMException ex)
		{
			// The provider does not support this pattern or failed to answer. Treat the element as not draggable.
			CursorDiagnosticLogger.Record("pattern_probe", "exception", details: new { probe = "drag_pattern", patternId = UiaDragPatternId, exceptionType = ex.GetType().FullName, message = ex.Message });
		}

		return false;
	}

	/// <summary>Returns true when the element supports any pattern in <see cref="SupplementaryInteractivePatternIds"/>.</summary>
	private static bool HasSupplementaryInteractivePattern(IUIAutomationElement element)
	{
		foreach (var patternId in SupplementaryInteractivePatternIds)
		{
			try
			{
				if (element.GetCurrentPattern(patternId) is not null) return true;
			}
			catch (COMException ex)
			{
				// Ignore and try the next pattern; an unsupported/failing pattern query is not fatal.
				CursorDiagnosticLogger.Record("pattern_probe", "exception", details: new { probe = "supplementary_pattern", patternId, exceptionType = ex.GetType().FullName, message = ex.Message });
			}
		}

		return false;
	}
}
