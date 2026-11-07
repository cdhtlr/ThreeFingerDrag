namespace ThreeFingerDrag;

internal static partial class NativeMethods
{
	// Hook and window message constants from WinUser.h.
	internal const int WH_MOUSE_LL = 14;
	internal const int HC_ACTION = 0;
	internal const uint WM_QUIT = 0x0012;
	internal const uint WM_CANCELMODE = 0x001F;
	internal const uint SMTO_ABORTIFHUNG = 0x0002;
	internal const uint WM_INPUT = 0x00FF;
	internal const uint WM_MOUSEMOVE = 0x0200;
}