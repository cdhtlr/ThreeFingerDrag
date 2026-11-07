namespace ThreeFingerDrag;

internal static partial class OverlayWindow
{
	private const uint WsPopup = 0x80000000;
	private const uint WsExTransparent = 0x00000020;
	private const uint WsExToolWindow = 0x00000080;
	private const uint WsExTopMost = 0x00000008;
	private const uint WsExNoActivate = 0x08000000;
	private const int SwShowNoActivate = 4;
	private const uint WmPaint = 0x000F;
	private const uint WmTimer = 0x0113;
	private const int RgnDiff = 4;
	private const int DefaultGuiFont = 17;
	private const int TransparentBkMode = 1;
	private const uint DtWordBreak = 0x00000010;
	private const uint DtNoPrefix = 0x00000800;
	private const uint DtCalcRect = 0x00000400;
	private const int SmXVirtualScreen = 76;
	private const int SmYVirtualScreen = 77;
	private const int SmCxVirtualScreen = 78;
	private const int SmCyVirtualScreen = 79;
	// Layout limits for the text panel. The panel wraps at 320 px and keeps a 6 px margin on every side.
	private const int TextPanelMaxWidth = 320;
	private const int TextPanelPaddingPx = 6;
}
