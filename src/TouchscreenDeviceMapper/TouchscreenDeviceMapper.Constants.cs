namespace ThreeFingerDrag;

internal static partial class TouchscreenDeviceMapper
{
	// Fallback primary-screen metrics used only when GetPointerDeviceRects is unavailable.
	private const int SmCxScreen = 0;
	private const int SmCyScreen = 1;

	/// <summary>RIDI_DEVICENAME. Asks GetRawInputDeviceInfo for the device path string.</summary>
	private const uint RidiDevicename = 0x20000007;
}