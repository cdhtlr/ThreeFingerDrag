namespace ThreeFingerDrag;

internal static partial class ClipboardWriter
{
	// Win32 clipboard, movable-memory, and keyboard-injection constants.
	private const uint InputKeyboard = 1;
	private const uint KeyeventfKeyup = 0x0002;
	private const uint KeyeventfUnicode = 0x0004;
	private const ushort VkControl = 0x11;
	private const ushort VkV = 0x56;
	private const uint CfUnicodeText = 13;
	private const uint CfText = 1;
	private const uint CfOemText = 7;
	private const uint CfLocale = 16;
	private const uint GMemMoveable = 0x0002;
}
