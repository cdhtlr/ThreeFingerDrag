using static ThreeFingerDrag.NativeMethods;

namespace ThreeFingerDrag;

/// <summary>Reads and writes the Windows ClickLock accessibility setting.</summary>
internal static partial class ClickLock
{
	/// <summary>ClickLock is detected when Windows answers the query.</summary>
	public static bool IsSupported() => TryGetEnabled(out _);

	/// <summary>Reads whether ClickLock is turned on in Windows.</summary>
	public static bool TryGetEnabled(out bool enabled)
	{
		enabled = false;
		if (!SystemParametersInfo(SpiGetMouseClickLock, 0, out int value, 0))
		{
			CursorDiagnosticLogger.Record("click_lock_setting", "read_failed", details: new { setting = "enabled" });
			return false;
		}
		enabled = value != 0;
		CursorDiagnosticLogger.Record("click_lock_setting", "read_enabled", details: new { enabled });
		return true;
	}

	/// <summary>Reads the ClickLock hold time in milliseconds.</summary>
	public static bool TryGetTimeMs(out int timeMs)
	{
		timeMs = 0;
		if (!SystemParametersInfo(SpiGetMouseClickLockTime, 0, out uint value, 0))
		{
			CursorDiagnosticLogger.Record("click_lock_setting", "read_failed", details: new { setting = "time_ms" });
			return false;
		}
		timeMs = (int)value;
		CursorDiagnosticLogger.Record("click_lock_setting", "read_time", details: new { timeMs });
		return true;
	}

	/// <summary>Turns ClickLock on or off in Windows.</summary>
	public static bool SetEnabled(bool enabled)
	{
		var ok = SystemParametersInfo(SpiSetMouseClickLock, 0, enabled ? (nint)1 : 0, SpifUpdateIniFile | SpifSendChange);
		CursorDiagnosticLogger.Record("click_lock_setting", ok ? "write_enabled" : "write_failed", details: new { enabled });
		return ok;
	}

	/// <summary>Sets the ClickLock hold time. The value is clamped to the range Windows accepts.</summary>
	public static bool SetTimeMs(int timeMs)
	{
		var clamped = Math.Clamp(timeMs, MinTimeMs, MaxTimeMs);
		var ok = SystemParametersInfo(SpiSetMouseClickLockTime, 0, (nint)clamped, SpifUpdateIniFile | SpifSendChange);
		CursorDiagnosticLogger.Record("click_lock_setting", ok ? "write_time" : "write_failed", details: new { requestedMs = timeMs, appliedMs = clamped });
		return ok;
	}
}
