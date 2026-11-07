using System.Runtime.InteropServices;
using System.Text;
using static ThreeFingerDrag.NativeMethods;

namespace ThreeFingerDrag;

internal static partial class DragTargetClassifier
{
	/// <summary>
	/// Asks the window itself what is at the cursor via <c>WM_NCHITTEST</c> - the exact mechanism
	/// Windows uses to decide title-bar/caption areas when dragging a window with a real mouse, so it
	/// is automatically compatible with custom-title-bar apps as long as they answer it correctly.
	/// </summary>
	private static DragTargetClassification ClassifyByHitTest(nint rootWindowHandle, Point cursorPoint)
	{
		if (!PointerSettings.IsHitTestDetectionEnabled() || rootWindowHandle == 0) return DragTargetClassification.Undetermined;

		try
		{
			var lParam = MakeLParam(cursorPoint.X, cursorPoint.Y);
			// The timeout keeps a frozen window from blocking the gesture start for long.
			var messageSent = SendMessageTimeout(rootWindowHandle, WmNcHitTest, 0, lParam, SmtoAbortIfHung, HitTestTimeoutMilliseconds, out var result);
			if (messageSent == 0)
			{
				// The target did not respond (hung, blocked by UIPI on an elevated window, etc.).
				CursorDiagnosticLogger.Record("target_detection_detail", "hit_test_no_response", cursorPoint, cursorPoint, new { windowHandle = rootWindowHandle.ToInt64(), error = Marshal.GetLastWin32Error(), reason = "WM_NCHITTEST timed out or was blocked." });
				return DragTargetClassification.Undetermined;
			}

			// Only chrome codes allow a window move. Interactive codes block it. Client and unknown
			// codes return Undetermined so the next detector can decide.
			var classification = (int)result switch
			{
				HtCaption or HtSysMenu or HtBorder or HtGrowBox or HtLeft or HtRight or HtTop or HtTopLeft or HtTopRight or HtBottom or HtBottomLeft or HtBottomRight => DragTargetClassification.EmptyBackground,
				HtMenu or HtHScroll or HtVScroll or HtMinButton or HtMaxButton or HtClose or HtHelp => DragTargetClassification.InteractiveElement,
				_ => DragTargetClassification.Undetermined,
			};
			CursorDiagnosticLogger.Record("target_detection_detail", "hit_test_result", cursorPoint, cursorPoint, new { windowHandle = rootWindowHandle.ToInt64(), hitTestCode = result.ToInt64(), classification = classification.ToString() });
			return classification;
		}
		catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
		{
			CursorDiagnosticLogger.Record("target_detection_detail", "hit_test_exception", cursorPoint, cursorPoint, new { windowHandle = rootWindowHandle.ToInt64(), exceptionType = ex.GetType().FullName, message = ex.Message });
			return DragTargetClassification.Undetermined;
		}
	}

	/// <summary>
	/// Reads the currently displayed cursor shape (set by the target app/OS via <c>WM_SETCURSOR</c>)
	/// as a free, already-computed signal: an I-beam means a text area, a hand means a link/button, and
	/// size handles mean a resize/splitter control.
	/// </summary>
	private static DragTargetClassification ClassifyByCursorShape(Point cursorPoint)
	{
		if (!PointerSettings.IsCursorShapeDetectionEnabled()) return DragTargetClassification.Undetermined;

		try
		{
			CURSORINFO cursorInfo = new() { cbSize = Marshal.SizeOf<CURSORINFO>() };
			if (!GetCursorInfo(ref cursorInfo) || cursorInfo.hCursor == 0) return DragTargetClassification.Undetermined;

			// Each shape is compared by handle. The system returns shared cursor handles, so
			// comparing handles is reliable.
			nint[] interactiveCursors =
			[
				LoadCursor(0, IdcIBeam),
				LoadCursor(0, IdcHand),
				LoadCursor(0, IdcSizeNwSe),
				LoadCursor(0, IdcSizeNeSw),
				LoadCursor(0, IdcSizeWe),
				LoadCursor(0, IdcSizeNs),
				LoadCursor(0, IdcSizeAll),
			];

			foreach (var interactiveCursor in interactiveCursors)
			{
				if (interactiveCursor != 0 && interactiveCursor == cursorInfo.hCursor)
				{
					// An I-beam over empty space (no text nearby) can drag the window when the user allows it.
					// Otherwise the I-beam means editable text, so the drag is blocked.
					if (interactiveCursor == LoadCursor(0, IdcIBeam) && PointerSettings.IsEmptyIBeamWindowDragEnabled() && !UiAutomationDragClassifier.HasTextNear(cursorPoint, PointerSettings.GetIBeamTextDetectionAreaSize())) return DragTargetClassification.EmptyBackground;

					return DragTargetClassification.InteractiveElement;
				}
			}

			return DragTargetClassification.Undetermined;
		}
		catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
		{
			return DragTargetClassification.Undetermined;
		}
	}

	/// <summary>
	/// Legacy fallback for apps that do not implement UI Automation: compares the window class name of
	/// the deepest child window at the cursor point against well-known Win32/common-controls class
	/// names. Cannot see custom-drawn/canvas controls, but is cheap and still useful before giving up to
	/// solid-color pixel sampling.
	/// </summary>
	private static DragTargetClassification ClassifyByControlClassName(Point cursorPoint)
	{
		if (!PointerSettings.IsControlClassNameDetectionEnabled()) return DragTargetClassification.Undetermined;

		try
		{
			// WindowFromPoint returns the deepest child under the cursor, which is the control itself.
			var windowAtPoint = WindowFromPoint(new POINT { X = cursorPoint.X, Y = cursorPoint.Y });
			if (windowAtPoint == 0)
			{
				CursorDiagnosticLogger.Record("target_detection_detail", "control_class_window_not_found", cursorPoint, cursorPoint, new { reason = "WindowFromPoint returned zero." });
				return DragTargetClassification.Undetermined;
			}

			StringBuilder classNameBuilder = new(256);
			if (GetClassName(windowAtPoint, classNameBuilder, classNameBuilder.Capacity) == 0)
			{
				CursorDiagnosticLogger.Record("target_detection_detail", "control_class_read_failed", cursorPoint, cursorPoint, new { childWindow = windowAtPoint.ToInt64(), error = Marshal.GetLastWin32Error() });
				return DragTargetClassification.Undetermined;
			}

			var className = classNameBuilder.ToString();
			var matchedClassName = InteractiveWindowClassNames.FirstOrDefault(name => string.Equals(className, name, StringComparison.OrdinalIgnoreCase));
			CursorDiagnosticLogger.Record("target_detection_detail", "control_class_result", cursorPoint, cursorPoint, new { childWindow = windowAtPoint.ToInt64(), className, matched = matchedClassName is not null, matchedClassName });
			foreach (var interactiveClassName in InteractiveWindowClassNames) if (string.Equals(className, interactiveClassName, StringComparison.OrdinalIgnoreCase)) return DragTargetClassification.InteractiveElement;

			return DragTargetClassification.Undetermined;
		}
		catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
		{
			return DragTargetClassification.Undetermined;
		}
	}

	/// <summary>Packs screen coordinates into an LPARAM the way the native MAKELPARAM macro does.</summary>
	private static nint MakeLParam(int x, int y)
	{
		// Low word holds x and high word holds y. The DWORD is zero-extended to nint,
		// matching the native (LPARAM)(DWORD) cast, so negative coordinates on secondary monitors still work.
		var packed = unchecked((uint)(((y & 0xFFFF) << 16) | (x & 0xFFFF)));
		return (nint)packed;
	}
}
