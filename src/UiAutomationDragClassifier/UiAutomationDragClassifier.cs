using Interop.UIAutomationClient;

namespace ThreeFingerDrag;

/// <summary>Uses UI Automation to classify the accessible element under the cursor.</summary>
/// <remarks>
/// The generated interop assembly avoids hand-written COM declarations and a WPF dependency.
/// </remarks>
internal static partial class UiAutomationDragClassifier
{

	/// <summary>Classifies the element at the cursor with a bounded wait for UI Automation.</summary>
	public static DragTargetClassification Classify(Point cursorPoint)
	{
		// Detection can be turned off in settings. Then the result is undetermined and the chain moves on.
		if (!PointerSettings.IsUiAutomationDetectionEnabled()) return DragTargetClassification.Undetermined;

		try
		{
			// UIA runs on a thread-pool thread. If it does not finish within the timeout, the result is
			// dropped. A hung provider can keep that worker thread busy, but the caller is never blocked.
			var task = Task.Run(() => ClassifyCore(cursorPoint));
			if (task.Wait(ClassifyTimeoutMilliseconds)) return task.Result;
			CursorDiagnosticLogger.Record("uia_classifier", "timeout", cursorPoint, cursorPoint, new { timeoutMs = ClassifyTimeoutMilliseconds });
		}
		catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
		{
			// Any COM failure (elevated/UIPI-blocked window, misbehaving provider, marshaling error,
			// etc.) is treated as inconclusive so the chain can fall through to the next classifier.
			CursorDiagnosticLogger.Record("uia_classifier", "exception", cursorPoint, cursorPoint, new { phase = "classify", exceptionType = ex.GetType().FullName, message = ex.Message });
		}

		return DragTargetClassification.Undetermined;
	}

	/// <summary>Captures a bounded description of the accessible element under an input point for diagnostics.</summary>
	internal static object? DescribeElementAtPoint(Point point)
	{
		try
		{
			// Diagnostics only: the result is logged and never used for classification.
			// Each call creates its own COM automation object, since those objects are not shared across threads.
			var task = Task.Run(() =>
			{
				IUIAutomation automation = (IUIAutomation)new CUIAutomationClass();
				var element = automation.ElementFromPoint(new tagPOINT { x = point.X, y = point.Y });
				return element is null ? null : new
				{
					controlType = element.CurrentControlType,
					name = element.CurrentName,
					automationId = element.CurrentAutomationId
				};
			});
			return task.Wait(ClassifyTimeoutMilliseconds) ? task.Result : null;
		}
		catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
		{
			CursorDiagnosticLogger.Record("uia_classifier", "exception", point, point, new { phase = "describe_element", exceptionType = ex.GetType().FullName, message = ex.Message });
			return null;
		}
	}

	/// <summary>Checks whether any UI Automation text element intersects a square around the cursor.</summary>
	public static bool HasTextNear(Point cursorPoint, int areaSize)
	{
		// When detection is off, assume there is text. That keeps the caller on the safe path.
		if (!PointerSettings.IsUiAutomationDetectionEnabled()) return true;

		try
		{
			var task = Task.Run(() => HasTextNearCore(cursorPoint, areaSize));
			// A timeout also counts as text, so a slow provider never allows a drag start by mistake.
			if (task.Wait(ClassifyTimeoutMilliseconds)) return task.Result;
			CursorDiagnosticLogger.Record("uia_classifier", "text_near_timeout", cursorPoint, cursorPoint, new { areaSize, timeoutMs = ClassifyTimeoutMilliseconds });
			return true;
		}
		catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
		{
			// If text detection is unavailable, preserve the safe behavior and treat the area as text.
			CursorDiagnosticLogger.Record("uia_classifier", "exception", cursorPoint, cursorPoint, new { phase = "has_text_near", exceptionType = ex.GetType().FullName, message = ex.Message });
			return true;
		}
	}
}
