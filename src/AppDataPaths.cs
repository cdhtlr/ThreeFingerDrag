namespace ThreeFingerDrag;

internal static class AppDataPaths
{
	private const string ApplicationDirectoryName = "ThreeFingerDrag";

	public static string DataDirectory { get; } = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		ApplicationDirectoryName);

	public static string ConfigurationFilePath => Path.Combine(DataDirectory, "ThreeFingerDrag.config.json");
	public static string LanguageDirectory => Path.Combine(DataDirectory, "Languages");
	public static string LogDirectory => Path.Combine(DataDirectory, "logs");
}
