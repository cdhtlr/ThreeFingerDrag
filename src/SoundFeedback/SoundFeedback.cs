using static ThreeFingerDrag.NativeMethods;

namespace ThreeFingerDrag;

/// <summary>
/// Tiny wrapper around the Win32 <c>MessageBeep</c> system sound, used by
/// <see cref="ForcedDragModeService"/> to give a non-visual confirmation when a configured shortcut
/// toggles a forced drag mode on or off. A raw API call is used instead of any audio library/console
/// beep because this project is a <c>WinExe</c> with no console and intentionally has no other audio
/// dependency.
/// </summary>
internal static partial class SoundFeedback
{
	/// <summary>
	/// Plays a short, distinct system sound: a higher "asterisk" tone when a forced mode just turned
	/// on, a lower "exclamation" tone when it just turned off.
	/// </summary>
	public static void PlayForcedModeToggle(bool isNowActive)
	{
		try
		{
			// A failed system sound must not interfere with changing the drag mode.
			var played = MessageBeep(isNowActive ? MbIconAsterisk : MbIconExclamation);
			CursorDiagnosticLogger.Record("sound_forced_mode_toggle", "played", details: new { isNowActive, played });
		}
		catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
		{
			// Sound is optional feedback. Any other failure is ignored on purpose.
			CursorDiagnosticLogger.Record("sound_forced_mode_toggle", "exception", details: new { exceptionType = ex.GetType().FullName, message = ex.Message });
		}
	}

	/// <summary>
	/// Plays a subtle confirmation sound when a drag is released via the flick-to-drop gesture.
	/// </summary>
	public static void PlayDragRelease()
	{
		try
		{
			// The default OK sound confirms a flick-to-drop release.
			var played = MessageBeep(MbOk);
			CursorDiagnosticLogger.Record("sound_drag_release", "played", details: new { played });
		}
		catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
		{
			// Sound is optional feedback. Any other failure is ignored on purpose.
			CursorDiagnosticLogger.Record("sound_drag_release", "exception", details: new { exceptionType = ex.GetType().FullName, message = ex.Message });
		}
	}
}
