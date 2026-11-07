using System.Runtime.InteropServices;

namespace ThreeFingerDrag;

internal static partial class NativeMethods
{
	// Bitmap capture: memory DC, BitBlt copy, and GetDIBits readback.
	[DllImport("gdi32.dll")]
	internal static extern nint CreateCompatibleDC(nint hdc);

	[DllImport("gdi32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool DeleteDC(nint hdc);

	[DllImport("gdi32.dll")]
	internal static extern nint CreateCompatibleBitmap(nint hdc, int width, int height);

	[DllImport("gdi32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool BitBlt(nint hdcDest, int xDest, int yDest, int width, int height, nint hdcSrc, int xSrc, int ySrc, uint rop);

	// Copies bitmap pixels into a byte array. The header describes the format we ask for.
	[DllImport("gdi32.dll")]
	internal static extern int GetDIBits(nint hdc, nint hbmp, uint startScanLine, uint scanLineCount, [Out] byte[]? bits, ref BITMAPINFO bitmapInfo, uint usage);

	/// <summary>BITMAPINFO holding only the header. A positive biHeight means bottom-up rows.</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct BITMAPINFO
	{
		public BITMAPINFOHEADER bmiHeader;
	}

	/// <summary>Win32 BITMAPINFOHEADER, 40 bytes. Describes size and pixel format.</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct BITMAPINFOHEADER
	{
		public uint biSize;
		public int biWidth;
		public int biHeight;
		public ushort biPlanes;
		public ushort biBitCount;
		public uint biCompression;
		public uint biSizeImage;
		public int biXPelsPerMeter;
		public int biYPelsPerMeter;
		public uint biClrUsed;
		public uint biClrImportant;
	}

	[DllImport("gdi32.dll")]
	internal static extern uint GetPixel(nint hdc, int nXPos, int nYPos);
}
