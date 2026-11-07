using System.Runtime.InteropServices;
using Windows.Globalization;
using Windows.Media.Ocr;

namespace ThreeFingerDrag;

internal static partial class OcrCaptureService
{
	/// <summary>Creates the OCR engine for the configured language, or the first usable profile language.</summary>
	private static OcrEngine? CreateEngine()
	{
		// Honor the configured language when installed, then fall back to Windows profile languages.
		// An empty tag means no preference, so the profile languages are used directly.
		var languageTag = PointerSettings.GetOcrLanguageTag();
		if (!string.IsNullOrEmpty(languageTag))
		{
			try
			{
				Language language = new(languageTag);
				// The language pack may be missing even when the tag parses, so check support first.
				if (OcrEngine.IsLanguageSupported(language))
				{
					OcrEngine? languageEngine = OcrEngine.TryCreateFromLanguage(language);
					if (languageEngine is not null)
					{
						CursorDiagnosticLogger.Record("ocr_engine", "language_engine_created", details: new { languageTag });
						return languageEngine;
					}
				}
				CursorDiagnosticLogger.Record("ocr_engine", "language_unavailable", details: new { languageTag });
			}
			catch (Exception ex) when (ex is ArgumentException or COMException)
			{
				// Falls back to the user's profile languages below.
				CursorDiagnosticLogger.Record("ocr_engine", "exception", details: new { exceptionType = ex.GetType().FullName, message = ex.Message });
			}
		}

		var profileEngine = OcrEngine.TryCreateFromUserProfileLanguages();
		CursorDiagnosticLogger.Record("ocr_engine", profileEngine is null ? "profile_engine_unavailable" : "profile_engine_created");
		return profileEngine;
	}
}
