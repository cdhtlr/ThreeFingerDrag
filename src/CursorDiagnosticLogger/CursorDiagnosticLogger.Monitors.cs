using static ThreeFingerDrag.NativeMethods;

namespace ThreeFingerDrag;

internal static partial class CursorDiagnosticLogger
{
// ── Monitor and Display Diagnostics ────────────────────────────────────────

	/// <summary>Returns the collection of displays with touchscreen capability status.</summary>
	public static object GetMonitorsSummary()
	{
		try
		{
			List<Rectangle> touchRects = [];
			uint count = 0;
			// The first call with a null buffer returns the device count. The second call fills the array.
			if (GetPointerDevices(ref count, null) && count > 0)
			{
				PointerDeviceInfo[] devices = new PointerDeviceInfo[count];
				if (GetPointerDevices(ref count, devices))
				{
					foreach (var dev in devices)
					{
						if (dev.PointerDeviceType != PointerDeviceType.Touch) continue;
						// Only the display rectangle is used. The screen-space rectangle is ignored.
						if (!GetPointerDeviceRects(dev.Device, out _, out var dispRect)) continue;

						touchRects.Add(new Rectangle(
							dispRect.Left, dispRect.Top,
							dispRect.Right - dispRect.Left,
							dispRect.Bottom - dispRect.Top));
					}
				}
			}

			var screens = Screen.AllScreens;
			int totalMonitors = screens.Length;
			int touchscreenMonitors = 0;
			int nonTouchscreenMonitors = 0;

			List<object> monitorList = new(screens.Length);
			for (int i = 0; i < screens.Length; i++)
			{
				var s = screens[i];
				var bounds = s.Bounds;
				// A monitor counts as a touchscreen when any touch device overlaps its bounds.
				bool isTouch = touchRects.Any(tr => tr.IntersectsWith(bounds));
				if (isTouch) touchscreenMonitors++; else nonTouchscreenMonitors++;

				monitorList.Add(new
				{
					index = i,
					deviceName = s.DeviceName,
					primary = s.Primary,
					isTouchscreen = isTouch,
					bounds = new { bounds.X, bounds.Y, bounds.Width, bounds.Height }
				});
			}

			return new
			{
				total = totalMonitors,
				touchscreenCount = touchscreenMonitors,
				nonTouchscreenCount = nonTouchscreenMonitors,
				monitors = monitorList
			};
		}
		catch
		{
			return new { total = 0, touchscreenCount = 0, nonTouchscreenCount = 0 };
		}
	}

	/// <summary>Finds which monitor contains or is nearest to a given point.</summary>
	public static object? GetMonitorAtPoint(Point? point)
	{
		if (!point.HasValue) return null;

		try
		{
			var pt = point.Value;
			var screens = Screen.AllScreens;
			for (int i = 0; i < screens.Length; i++)
			{
				if (screens[i].Bounds.Contains(pt))
				{
					return new
					{
						index = i,
						deviceName = screens[i].DeviceName,
						primary = screens[i].Primary,
						bounds = new { screens[i].Bounds.X, screens[i].Bounds.Y, screens[i].Bounds.Width, screens[i].Bounds.Height }
					};
				}
			}

			// The point is outside every monitor, for example in a gap. Use the closest monitor.
			Screen nearest = Screen.FromPoint(pt);
			int idx = Array.FindIndex(screens, s => s.DeviceName == nearest.DeviceName);
			return new
			{
				index = idx >= 0 ? idx : 0,
				deviceName = nearest.DeviceName,
				primary = nearest.Primary,
				bounds = new { nearest.Bounds.X, nearest.Bounds.Y, nearest.Bounds.Width, nearest.Bounds.Height }
			};
		}
		catch
		{
			return null;
		}
	}

	/// <summary>Finds which monitor contains or is nearest to a given rectangle/window bounds.</summary>
	public static object? GetMonitorForRect(Rectangle? rect)
	{
		if (!rect.HasValue) return null;

		try
		{
			var r = rect.Value;
			// FromRectangle picks the monitor with the largest overlap, which suits windows that span monitors.
			Screen nearest = Screen.FromRectangle(r);
			var screens = Screen.AllScreens;
			int idx = Array.FindIndex(screens, s => s.DeviceName == nearest.DeviceName);
			return new
			{
				index = idx >= 0 ? idx : 0,
				deviceName = nearest.DeviceName,
				primary = nearest.Primary,
				bounds = new { nearest.Bounds.X, nearest.Bounds.Y, nearest.Bounds.Width, nearest.Bounds.Height }
			};
		}
		catch
		{
			return null;
		}
	}
}
