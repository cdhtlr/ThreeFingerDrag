using System.Runtime.InteropServices;
using Linearstar.Windows.RawInput;
using static ThreeFingerDrag.NativeMethods;

namespace ThreeFingerDrag;

/// <summary>Maps touchscreen contacts to screen pixels using the digitizer and display rectangles of each touch device.</summary>
internal static partial class TouchscreenDeviceMapper
{
	// Win32 helpers used to enumerate touch devices and read their raw input names.

	// Keep in sync with Windows tagPOINTER_DEVICE_INFO (winuser.h).
	// Explicit layout ensures correct field alignment on x64.

	/// <summary>One touch digitizer: its path, its digitizer units rectangle, and the screen rectangle it maps to.</summary>
	private sealed class TouchDeviceInfo
	{
		public IntPtr DeviceHandle { get; set; }
		public string DevicePath { get; set; } = string.Empty;
		public NativeRect DigitizerRect { get; set; }
		public Rectangle DisplayBounds { get; set; }
	}

	// Cache of touch devices and their screen rectangles. Guarded by DisplayRectCacheLock.
	// Taken by every mapping call, so the work done under it must stay short.
	private static readonly object DisplayRectCacheLock = new();
	private static List<TouchDeviceInfo>? cachedTouchDevices;
	// Raw device path to touch device. Speeds up lookups for devices already seen.
	private static readonly Dictionary<string, TouchDeviceInfo> pathToDeviceMap = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Invalidates the cached touchscreen → display-rect mapping.
	/// Call when the display configuration changes (WM_DISPLAYCHANGE).
	/// </summary>
	internal static void InvalidateDisplayRectCache()
	{
		lock (DisplayRectCacheLock)
		{
			CursorDiagnosticLogger.Record("touchscreen_mapper", "display_rect_cache_invalidated", details: new { cachedDeviceCount = cachedTouchDevices?.Count ?? 0, cachedPathCount = pathToDeviceMap.Count });
			cachedTouchDevices = null;
			pathToDeviceMap.Clear();
		}
	}

	/// <summary>Reads the device interface path for a raw input handle. Returns an empty string on failure.</summary>
	private static string GetDevicePathFromHandle(IntPtr hDevice)
	{
		if (hDevice == IntPtr.Zero) return string.Empty;

		uint size = 0;
		_ = GetRawInputDeviceInfo(hDevice, RidiDevicename, IntPtr.Zero, ref size);
		if (size == 0) return string.Empty;

		var buffer = Marshal.AllocHGlobal((int)size * sizeof(char));
		try
		{
			if (GetRawInputDeviceInfo(hDevice, RidiDevicename, buffer, ref size) != unchecked((uint)-1)) return Marshal.PtrToStringUni(buffer) ?? string.Empty;
		}
		finally
		{
			Marshal.FreeHGlobal(buffer);
		}
		return string.Empty;
	}

	/// <summary>Enumerates touch pointer devices and records each digitizer and display rectangle.</summary>
	private static List<TouchDeviceInfo> BuildTouchDeviceList()
	{
		List<TouchDeviceInfo> result = [];
		uint count = 0;
		if (!GetPointerDevices(ref count, null))
		{
			CursorDiagnosticLogger.Record("touchscreen_mapper", "pointer_device_count_failed");
			return result;
		}
		if (count == 0)
		{
			CursorDiagnosticLogger.Record("touchscreen_mapper", "pointer_device_enumeration_empty");
			return result;
		}

		PointerDeviceInfo[] devices = new PointerDeviceInfo[count];
		if (!GetPointerDevices(ref count, devices))
		{
			CursorDiagnosticLogger.Record("touchscreen_mapper", "pointer_device_enumeration_failed", details: new { count });
			return result;
		}

		foreach (var dev in devices)
		{
			if (dev.PointerDeviceType != PointerDeviceType.Touch) continue;

			// Skip devices without rectangles. They cannot be mapped to a display.
			if (!GetPointerDeviceRects(dev.Device, out var digRect, out var dispRect))
			{
				CursorDiagnosticLogger.Record("touchscreen_mapper", "touch_device_rects_unavailable", details: new { handle = dev.Device.ToInt64() });
				continue;
			}

			// The path tells identical touchscreen models apart.
			var devPath = GetDevicePathFromHandle(dev.Device);
			if (string.IsNullOrEmpty(devPath)) CursorDiagnosticLogger.Record("touchscreen_mapper", "touch_device_path_unavailable", details: new { handle = dev.Device.ToInt64() });
			Rectangle displayBounds = new(
				dispRect.Left, dispRect.Top,
				dispRect.Right - dispRect.Left,
				dispRect.Bottom - dispRect.Top);

			result.Add(new TouchDeviceInfo
			{
				DeviceHandle = dev.Device,
				DevicePath = devPath,
				DigitizerRect = digRect,
				DisplayBounds = displayBounds
			});
		}
		CursorDiagnosticLogger.Record("touchscreen_mapper", "touch_devices_enumerated", details: new { pointerDeviceCount = devices.Length, touchDeviceCount = result.Count });
		return result;
	}

	/// <summary>Finds the digitizer and display rectangles for a raw input device. Matches by device path first, then by digitizer size.</summary>
	private static bool TryGetDisplayRect(RawInputDigitizerData? data, int digitizerW, int digitizerH, out NativeRect digitizerRect, out Rectangle displayRect)
	{
		lock (DisplayRectCacheLock)
		{
			cachedTouchDevices ??= BuildTouchDeviceList();

			var rawDevicePath = data?.Device?.DevicePath ?? string.Empty;

			// 1. Try match by device path (differentiates identical models with distinct device instance paths)
			if (!string.IsNullOrEmpty(rawDevicePath))
			{
				if (pathToDeviceMap.TryGetValue(rawDevicePath, out var mappedDev))
				{
					digitizerRect = mappedDev.DigitizerRect;
					displayRect = mappedDev.DisplayBounds;
					return true;
				}

				// Paths may differ in case or in trailing parts, so match in either direction.
				var pathMatch = cachedTouchDevices.FirstOrDefault(d =>
					!string.IsNullOrEmpty(d.DevicePath) &&
					(string.Equals(d.DevicePath, rawDevicePath, StringComparison.OrdinalIgnoreCase) ||
					 rawDevicePath.Contains(d.DevicePath, StringComparison.OrdinalIgnoreCase) ||
					 d.DevicePath.Contains(rawDevicePath, StringComparison.OrdinalIgnoreCase)));

				if (pathMatch != null)
				{
					// Remember the match, so the next report from this device skips the search.
					pathToDeviceMap[rawDevicePath] = pathMatch;
					CursorDiagnosticLogger.Record("touchscreen_mapper", "device_path_matched", details: new { rawDevicePath, matchedDevicePath = pathMatch.DevicePath });
					digitizerRect = pathMatch.DigitizerRect;
					displayRect = pathMatch.DisplayBounds;
					return true;
				}
			}

			// 2. Fallback: match by digitizer dimensions
			// Fallback for devices with no usable path. Two screens can share a digitizer size, so this can be ambiguous.
			var sizeMatch = cachedTouchDevices.FirstOrDefault(d => (d.DigitizerRect.Right - d.DigitizerRect.Left) == digitizerW && (d.DigitizerRect.Bottom - d.DigitizerRect.Top) == digitizerH);

			if (sizeMatch != null)
			{
				// Logged only when a path is cached. Without a path the match repeats per report, so logging it would flood the log.
				if (!string.IsNullOrEmpty(rawDevicePath))
				{
					pathToDeviceMap[rawDevicePath] = sizeMatch;
					CursorDiagnosticLogger.Record("touchscreen_mapper", "device_size_matched", details: new { rawDevicePath, digitizerW, digitizerH });
				}
				digitizerRect = sizeMatch.DigitizerRect;
				displayRect = sizeMatch.DisplayBounds;
				return true;
			}
		}

		digitizerRect = default;
		displayRect = default;
		return false;
	}

	/// <summary>Average screen position of the given contacts, rounded to whole pixels.</summary>
	internal static Point CalculateCentroid(RawInputDigitizerData data, RawInputDigitizerContact[] contacts)
	{
		var points = contacts.Select(c => MapDigitizerPoint(data, c)).ToArray();
		return new Point((int)Math.Round(points.Average(point => point.X)), (int)Math.Round(points.Average(point => point.Y)));
	}

	/// <summary>Maps a contact without device data. Uses the primary screen fallback.</summary>
	internal static Point MapDigitizerPoint(RawInputDigitizerContact contact) => MapDigitizerPoint(null, contact);

	/// <summary>Maps a digitizer contact to screen pixels. Uses the matching touch device's display rectangle, or the primary screen when none matches.</summary>
	internal static Point MapDigitizerPoint(RawInputDigitizerData? data, RawInputDigitizerContact contact)
	{
		// Logical range of the contact. Positions are scaled against it.
		var width = Math.Max(1, contact.MaxX - contact.MinX);
		var height = Math.Max(1, contact.MaxY - contact.MinY);

		// Look up the display rectangle for this device.
		// Uses device handle / device path matching so identical touchscreen models
		// with identical HID coordinate ranges are correctly mapped to their respective monitors.
		// contact.X/Y are HID logical units, so normalize them against the contact's own
		// logical range (MinX/MaxX), not the pixel-based digitizer rect.
		// Scale into the matched monitor's pixel rectangle.
		if (TryGetDisplayRect(data, width, height, out _, out var display))
		{
			var x = display.Left + (int)Math.Round((contact.X - contact.MinX) * (display.Width - 1d) / width);
			var y = display.Top + (int)Math.Round((contact.Y - contact.MinY) * (display.Height - 1d) / height);
			return new Point(x, y);
		}

		// Fallback: map to primary screen (single-monitor / API-unavailable case).
		var screenWidth = Math.Max(1, GetSystemMetrics(SmCxScreen));
		var screenHeight = Math.Max(1, GetSystemMetrics(SmCyScreen));
		return new Point((int)Math.Round((contact.X - contact.MinX) * (screenWidth - 1d) / width), (int)Math.Round((contact.Y - contact.MinY) * (screenHeight - 1d) / height));
	}

}
