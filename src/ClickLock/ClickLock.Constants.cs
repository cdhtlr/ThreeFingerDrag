namespace ThreeFingerDrag;

internal static partial class ClickLock
{
	public const int MinTimeMs = 200;
	public const int MaxTimeMs = 2200;

	// Win32 SystemParametersInfo action codes and flags for ClickLock.
	private const uint SpiGetMouseClickLock = 0x101E;
	private const uint SpiSetMouseClickLock = 0x101F;
	// ClickLock hold time is stored in milliseconds.
	private const uint SpiGetMouseClickLockTime = 0x2008;
	private const uint SpiSetMouseClickLockTime = 0x2009;
	// Write the change to the user profile and broadcast it so other apps see it immediately.
	private const uint SpifUpdateIniFile = 0x0001;
	private const uint SpifSendChange = 0x0002;
}