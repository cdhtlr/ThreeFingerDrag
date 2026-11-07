using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ThreeFingerDrag.Launcher;

internal static class Program
{
	[STAThread]
	private static int Main(string[] args)
	{
		try
		{
			if (args.Length == 2 && args[0] == "--stop-installation")
			{
				StopInstallation(args[1]);
				return 0;
			}
			if (args.Length != 0) throw new ArgumentException("Invalid launcher arguments.");

			using Process process = new()
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = Path.Combine(Environment.SystemDirectory, "schtasks.exe"),
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
				}
			};
			process.StartInfo.ArgumentList.Add("/Run");
			process.StartInfo.ArgumentList.Add("/TN");
			process.StartInfo.ArgumentList.Add("ThreeFingerDragRunAsAdmin");
			if (!process.Start()) throw new InvalidOperationException("Unable to start Task Scheduler.");
			var output = process.StandardOutput.ReadToEndAsync();
			var error = process.StandardError.ReadToEndAsync();
			process.WaitForExit();
			if (process.ExitCode != 0)
			{
				var details = error.GetAwaiter().GetResult().Trim();
				if (details.Length == 0) details = output.GetAwaiter().GetResult().Trim();
				throw new InvalidOperationException($"Task Scheduler error {process.ExitCode}: {details}");
			}
			return 0;
		}
		catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
		{
			if (args.Length > 0 && args[0] == "--stop-installation")
			{
				Console.Error.WriteLine(ex.Message);
				return 1;
			}
			var isIndonesian = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "id";
			var message = isIndonesian
				? "ThreeFingerDrag tidak dapat dimulai. Pastikan Anda masuk dengan akun administrator yang memasang program, lalu coba perbaiki instalasi."
				: "ThreeFingerDrag could not start. Sign in with the administrator account that installed it, then try repairing the installation.";
			_ = MessageBox.Show($"{message}\n\n{ex.Message}", "ThreeFingerDrag", MessageBoxButtons.OK, MessageBoxIcon.Error);
			return 1;
		}
	}

	private static void StopInstallation(string installationDirectory)
	{
		var expectedPath = Path.GetFullPath(Path.Combine(installationDirectory, "ThreeFingerDrag.exe"));
		foreach (var process in Process.GetProcessesByName("ThreeFingerDrag"))
		{
			using (process)
			{
				try
				{
					if (process.HasExited) continue;
					var path = GetProcessPath(process.Id);
					if (!string.Equals(Path.GetFullPath(path), expectedPath, StringComparison.OrdinalIgnoreCase)) continue;
					if (process.CloseMainWindow() && process.WaitForExit(3000)) continue;
					process.Kill();
					if (!process.WaitForExit(10000))
						throw new InvalidOperationException($"Process {process.Id} did not stop.");
				}
				catch (InvalidOperationException) when (process.HasExited)
				{
				}
			}
		}
	}

	private static string GetProcessPath(int processId)
	{
		using var handle = OpenProcess(0x1000, false, processId);
		if (handle.IsInvalid) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
		StringBuilder path = new(32768);
		var length = path.Capacity;
		if (!QueryFullProcessImageName(handle, 0, path, ref length))
			throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
		return path.ToString();
	}

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern SafeProcessHandle OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, StringBuilder path, ref int size);
}
