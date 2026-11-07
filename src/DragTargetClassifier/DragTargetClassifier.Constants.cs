namespace ThreeFingerDrag;

internal static partial class DragTargetClassifier
{
	private const uint WmNcHitTest = 0x0084;
	private const uint SmtoAbortIfHung = 0x0002;
	// Upper bound for one SendMessageTimeout call. A hung target must not stall the gesture start.
	private static uint HitTestTimeoutMilliseconds => (uint)PointerSettings.GetHitTestTimeoutMs();

	// Non-client hit-test results that represent window chrome: safe to move the window from here.
	private const int HtCaption = 2;
	private const int HtSysMenu = 3;
	private const int HtGrowBox = 4;
	private const int HtLeft = 10;
	private const int HtRight = 11;
	private const int HtTop = 12;
	private const int HtTopLeft = 13;
	private const int HtTopRight = 14;
	private const int HtBottom = 15;
	private const int HtBottomLeft = 16;
	private const int HtBottomRight = 17;
	private const int HtBorder = 18;

	// Non-client hit-test results that represent a specific interactive control: never move the window.
	private const int HtMenu = 5;
	private const int HtHScroll = 6;
	private const int HtVScroll = 7;
	private const int HtMinButton = 8;
	private const int HtMaxButton = 9;
	private const int HtClose = 20;
	private const int HtHelp = 21;

	private const nint IdcIBeam = 32513;
	private const nint IdcSizeNwSe = 32642;
	private const nint IdcSizeNeSw = 32643;
	private const nint IdcSizeWe = 32644;
	private const nint IdcSizeNs = 32645;
	private const nint IdcSizeAll = 32646;
	private const nint IdcHand = 32649;
}