namespace ThreeFingerDrag;

internal static partial class Program
{
	/// <summary>Shows a startup-failure balloon with the exception message.</summary>
	private static void ShowStartupFailedNotification(Exception ex)
	{
		CursorDiagnosticLogger.Record("app_notification", "startup_failed", details: new { exceptionType = ex.GetType().FullName, message = ex.Message });
		ShowStartupNotification(
			$"Program gagal dijalankan: {ex.Message}",
			$"The program failed to start: {ex.Message}",
			ToolTipIcon.Error);
	}

	/// <summary>Shows a tray balloon in Indonesian or English, based on the current UI culture.</summary>
	private static void ShowStartupNotification(string indonesianText, string englishText, ToolTipIcon icon)
	{
		// Use Indonesian only when the UI language is Indonesian. English is the fallback for all other cultures.
		var useIndonesian = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "id";
		var text = useIndonesian ? indonesianText : englishText;
		CursorDiagnosticLogger.Record("app_notification", "balloon_show", details: new { language = useIndonesian ? "id" : "en", icon, text });

		// A short-lived message loop keeps the balloon alive after this process would otherwise exit.
		using NotifyIcon notifyIcon = new()
		{
			Icon = SystemIcons.Application,
			Visible = true,
			BalloonTipTitle = "ThreeFingerDrag",
			BalloonTipText = text,
			BalloonTipIcon = icon,
		};
		// Safety timeout: end the loop after the configured delay even if the balloon never reports that it closed.
		using System.Windows.Forms.Timer closeTimer = new() { Interval = PointerSettings.GetNotificationSafetyTimeoutMs() };
		closeTimer.Tick += (_, _) =>
		{
			CursorDiagnosticLogger.Record("app_notification", "safety_timeout_exit_loop");
			Application.ExitThread();
		};
		notifyIcon.BalloonTipClosed += (_, _) =>
		{
			CursorDiagnosticLogger.Record("app_notification", "balloon_closed");
			Application.ExitThread();
		};
		closeTimer.Start();
		notifyIcon.ShowBalloonTip(PointerSettings.GetNotificationBalloonTimeoutMs());
		Application.Run();
		CursorDiagnosticLogger.Record("app_notification", "message_loop_exited");
	}
}
