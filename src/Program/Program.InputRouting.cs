using Linearstar.Windows.RawInput;

namespace ThreeFingerDrag;

internal static partial class Program
{
	/// <summary>Decodes one WM_INPUT payload and sends it to the processor that owns that device type.</summary>
	private static void OnInputEvent(TouchProcessor touchProcessor, StylusProcessor stylusProcessor, TouchscreenProcessor touchscreenProcessor, nint lParam)
	{
		// Decode the raw input handle into a typed payload. Unknown payload types are ignored below.
		RawInputData data = RawInputData.FromHandle(lParam);
		if (data is RawInputDigitizerData digitizerData)
		{
			// The HID usage page and usage identify the digitizer: touch screen, pen, or touchpad.
			var usage = digitizerData.Device?.UsageAndPage;
			if (usage is not null && usage.Value.Equals(HidUsageAndPage.TouchScreen))
			{
				CursorDiagnosticLogger.NoteRawInput("touchscreen");
				touchscreenProcessor.Update(digitizerData);
			} else if (usage is not null && usage.Value.Equals(HidUsageAndPage.Pen))
			{
				CursorDiagnosticLogger.NoteRawInput("pen");
				stylusProcessor.Update(digitizerData);
			} else
			{
				CursorDiagnosticLogger.NoteRawInput("touchpad_digitizer");
				touchProcessor.Update(digitizerData);
			}
		}

		if (data is RawInputMouseData mouseData)
		{
			// Raw mouse reports cover both mice and precision touchpads in mouse mode; the device type is not separable here.
			CursorDiagnosticLogger.NoteRawInput("mouse_or_touchpad_mouse_mode");
			MouseInject.PhysicalMouseRawDelta?.Invoke(mouseData.Mouse.LastX, mouseData.Mouse.LastY);
			touchProcessor.Update(mouseData);
		}

		if (data is not (RawInputDigitizerData or RawInputMouseData))
		{
			CursorDiagnosticLogger.Record("app_input_routing", "unhandled_payload", details: new { payloadType = data?.GetType().Name ?? "null" });
		}
	}
}
