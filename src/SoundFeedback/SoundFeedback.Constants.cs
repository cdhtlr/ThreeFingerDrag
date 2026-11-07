namespace ThreeFingerDrag;

internal static partial class SoundFeedback
{
	// Standard "OK" sound type (value 0), used for release.
	private const uint MbOk = 0x00000000;
	// MessageBeep sound types used to distinguish activation and deactivation.
	private const uint MbIconAsterisk = 0x00000040;
	private const uint MbIconExclamation = 0x00000030;
}