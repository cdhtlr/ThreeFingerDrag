using System.Runtime.InteropServices;
using static ThreeFingerDrag.NativeMethods;

namespace ThreeFingerDrag;

internal static partial class WindowDragController
{
	/// <summary>Starts a drag at the current cursor position.</summary>
	/// <returns>The new session, or null when the target is not a movable window.</returns>
	public static DragSession? TryStartDrag()
	{
		if (!TryGetCursorPoint(out var cursorPoint))
		{
			CursorDiagnosticLogger.Record("drag_classification", "cursor_position_unavailable", null, null, new { result = "failed", method = "GetCursorPos", error = Marshal.GetLastWin32Error() });
			return null;
		}

		return TryStartDragAt(cursorPoint, independentPosition: false);
	}

	/// <summary>Starts a window-only drag classified at a touch point without moving the system cursor.</summary>
	public static DragSession? TryStartTouchscreenWindowDrag(Point touchPoint) => TryStartDragAt(touchPoint, independentPosition: true);

	/// <summary>
	/// Runs the classifier chain at a point and builds a session if the point is an empty part of a window.
	/// Forced modes are checked first. Then the window is resolved. Then the target is classified.
	/// </summary>
	private static DragSession? TryStartDragAt(Point cursorPoint, bool independentPosition)
	{
		// User overrides take precedence over the normal classifier chain.
		var forcedMode = ForcedDragModeService.GetForcedMode();
		CursorDiagnosticLogger.Record("drag_classification", "forced_mode", cursorPoint, cursorPoint, new { mode = forcedMode.Mode.ToString(), reason = forcedMode.Reason, cursorColor = CursorDiagnosticLogger.IsEnabled ? GetColorHexAt(cursorPoint) : null });
		if (forcedMode.Mode == ForcedDragMode.NonWindow)
		{
			CursorDiagnosticLogger.Record("drag_classification", "rejected_by_forced_mode", cursorPoint, cursorPoint, new { inputSource = independentPosition ? "touchscreen" : "cursor", reason = forcedMode.Reason, forcedMode = forcedMode.Mode.ToString() });
			DragModeIndicatorOverlay.Show(cursorPoint, isWindowDrag: false, forcedMode.Reason ?? "Forced", recognizedText: null);
			return null;
		}

		// A hold-forced drag skips the title-bar wait and moves the window with SetWindowPos from the start.
		if (forcedMode.FromHold) independentPosition = true;

		if (!TryGetRootWindowAtPoint(cursorPoint, out var windowHandle))
		{
			CursorDiagnosticLogger.Record("drag_classification", "no_window_at_input_point", cursorPoint, cursorPoint, new { inputSource = independentPosition ? "touchscreen" : "cursor", reason = "WindowFromPoint returned no window." });
			return null;
		}

		if (windowHandle == GetDesktopWindow() || windowHandle == GetShellWindow())
		{
			CursorDiagnosticLogger.Record("drag_classification", "desktop_or_shell_target", cursorPoint, cursorPoint, new { inputSource = independentPosition ? "touchscreen" : "cursor", windowHandle = windowHandle.ToInt64(), reason = "Desktop and shell are not movable application windows." });
			return null;
		}

		if (forcedMode.Mode == ForcedDragMode.Window)
		{
			// Forced window mode still yields to actual draggable controls and recognized OCR
			// text, so it cannot turn those targets into a window-move gesture.
			var automationClassification = UiAutomationDragClassifier.Classify(cursorPoint);
			if (automationClassification == DragTargetClassification.DraggableElement || (PointerSettings.IsOcrCaptureEnabled() && OcrCaptureService.HasRecognizedTextAt(cursorPoint)))
			{
				CursorDiagnosticLogger.Record("drag_classification", "forced_window_mode_yielded", cursorPoint, cursorPoint, new { inputSource = independentPosition ? "touchscreen" : "cursor", reason = automationClassification == DragTargetClassification.DraggableElement ? "UI Automation reports a draggable element." : "OCR text is recognized at the input point." });
				DragModeIndicatorOverlay.Show(cursorPoint, isWindowDrag: false, automationClassification == DragTargetClassification.DraggableElement ? "Draggable UI element" : "OCR text", recognizedText: null);
				return null;
			}
		}

		bool isEmptyBackground;
		string detectionMethod;
		if (forcedMode.Mode == ForcedDragMode.Window)
		{
			isEmptyBackground = true;
			detectionMethod = forcedMode.Reason ?? "Forced";
		} else
		{
			isEmptyBackground = IsEmptyBackgroundAtCursor(windowHandle, cursorPoint, out detectionMethod);
		}

		// Keep the displayed reason consistent with the classifier's decision.
		if (forcedMode.Mode == ForcedDragMode.Window) DragModeIndicatorOverlay.ShowForcedWindowMode(cursorPoint);
		else DragModeIndicatorOverlay.Show(cursorPoint, isEmptyBackground, detectionMethod, recognizedText: null);
		CursorDiagnosticLogger.Record("drag_classification", "target_classified", cursorPoint, cursorPoint,
			new
			{
				result = isEmptyBackground ? "window_background" : "interactive_element",
				method = detectionMethod,
				windowHandle = windowHandle.ToInt64(),
				cursorColor = CursorDiagnosticLogger.IsEnabled ? GetColorHexAt(cursorPoint) : null
			});

		if (!isEmptyBackground)
		{
			CursorDiagnosticLogger.Record("drag_classification", "not_window_background", cursorPoint, cursorPoint, new { inputSource = independentPosition ? "touchscreen" : "cursor", method = detectionMethod, reason = "Classifier identified an interactive or draggable target." });
			return null;
		}

		if (forcedMode.Mode == ForcedDragMode.Window && (GetWindowLong(windowHandle, GwlStyle) & GwsMaximize) != 0)
		{
			_ = ShowWindow(windowHandle, SwRestore);
			CursorDiagnosticLogger.Record("drag_classification", "maximized_window_restore_requested", cursorPoint, cursorPoint, new { windowHandle = windowHandle.ToInt64(), reason = forcedMode.Reason });
		}

		if (!TryGetWindowBounds(windowHandle, out var bounds))
		{
			CursorDiagnosticLogger.Record("drag_classification", "window_bounds_unavailable", cursorPoint, cursorPoint, new { inputSource = independentPosition ? "touchscreen" : "cursor", windowHandle = windowHandle.ToInt64(), error = Marshal.GetLastWin32Error() });
			return null;
		}

		// Activation is skipped for touch-independent moves and for windows that are already topmost.
		var shouldBringDraggedWindowToForeground = !independentPosition && PointerSettings.ShouldBringDraggedWindowToForeground() && !IsTopMostWindow(windowHandle);
		// Look for a real caption point near the cursor first. The computed point is used only as a fallback.
		var titleBarPoint = FindDraggableTitleBarPoint(windowHandle, bounds, cursorPoint);
		var titleBarPointConfirmed = titleBarPoint.HasValue;

		// When the title bar is pushed above the screen, no cursor position can grab it. Position the
		// window directly instead so it stays draggable and can be pulled back on screen.
		if (!independentPosition && !titleBarPointConfirmed && !IsPointOnVirtualScreen(GetTitleBarPoint(bounds, cursorPoint)))
		{
			independentPosition = true;
			CursorDiagnosticLogger.Record("drag_classification", "title_bar_offscreen_direct_move", cursorPoint, cursorPoint, new { windowHandle = windowHandle.ToInt64(), bounds = bounds.ToString() });
		}

		if (shouldBringDraggedWindowToForeground) _ = SetForegroundWindow(windowHandle);

		// Ordinary drags use the title bar. Independent touchscreen movement never changes
		// global cursor/button state, so its session always uses direct window positioning.
		if (!independentPosition)
		{
			var dragPoint = titleBarPoint ?? GetTitleBarPoint(bounds, cursorPoint);
			if (!SetCursorPos(dragPoint.X, dragPoint.Y))
			{
				CursorDiagnosticLogger.Record("drag_session_start_failed", "SetCursorPos_title_bar", cursorPoint, null, new { windowHandle = windowHandle.ToInt64(), target = dragPoint, titleBarPointConfirmed });
				return null;
			}

			// Confirm the cursor really landed on the title bar. A mismatch means the drag would move the wrong thing.
			if (!TryGetCursorPoint(out var actualCursorPoint) || actualCursorPoint != dragPoint)
			{
				CursorDiagnosticLogger.Record("drag_session_start_failed", "title_bar_cursor_position_mismatch", cursorPoint, actualCursorPoint, new { windowHandle = windowHandle.ToInt64(), requested = dragPoint, titleBarPointConfirmed });
				return null;
			}
		}

		// Sessions without foreground activation move the window with SetWindowPos only.
		DragSession session = new()
		{
			WindowHandle = windowHandle,
			InitialBounds = bounds,
			LastObservedBounds = bounds,
			MoveWithoutForeground = independentPosition || !shouldBringDraggedWindowToForeground,
			TitleBarPointConfirmed = titleBarPointConfirmed,
		};
		CursorDiagnosticLogger.Record("drag_session_started", "window_drag", cursorPoint, cursorPoint,
			new
			{
				window = CursorDiagnosticLogger.DescribeWindow(windowHandle, cursorPoint),
				windowHandle = windowHandle.ToInt64(),
				mode = session.MoveWithoutForeground ? "BackgroundWindow" : "Mouse",
				bounds = bounds.ToString(),
				movedCursorToTitleBar = shouldBringDraggedWindowToForeground,
				titleBarPointConfirmed,
				titleBarPoint
			});
		return session;
	}

	/// <summary>Reads the current cursor position from Win32. Returns false if the call fails.</summary>
	private static bool TryGetCursorPoint(out Point point)
	{
		if (!GetCursorPos(out var cursorPosition))
		{
			point = default;
			return false;
		}

		point = new Point(cursorPosition.X, cursorPosition.Y);
		return true;
	}
}
