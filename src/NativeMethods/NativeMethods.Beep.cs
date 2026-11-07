using System.Runtime.InteropServices;

namespace ThreeFingerDrag;

internal static partial class NativeMethods
{
	[DllImport("user32.dll")]
	internal static extern bool MessageBeep(uint uType);
}
