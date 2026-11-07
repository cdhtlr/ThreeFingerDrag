using System.Text;

namespace ThreeFingerDrag;

/// <summary>
/// Derives a log section id from the source file that wrote the entry. The id is the file name without
/// ".cs" in kebab-case, so every source file has its own id. For example ClipboardWriter.Paste.cs becomes
/// "clipboard-writer-paste" and Program.InputRouting.cs becomes "program-input-routing".
/// </summary>
internal static class DiagnosticLogSection
{
	/// <summary>Section id used when the source file is unknown.</summary>
	public const string Unknown = "unknown";

	/// <summary>Returns the section id for a source file path.</summary>
	public static string FromSourceFile(string sourceFile)
	{
		var fileName = Path.GetFileName(sourceFile);
		if (string.IsNullOrEmpty(fileName)) return Unknown;

		var baseName = fileName.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ? fileName[..^3] : fileName;
		return ToKebabCase(baseName);
	}

	private static string ToKebabCase(string value)
	{
		StringBuilder builder = new(value.Length + 8);
		for (int i = 0; i < value.Length; i++)
		{
			var c = value[i];
			if (c == '.') { _ = builder.Append('-'); continue; }
			if (char.IsUpper(c) && i > 0 && value[i - 1] != '.') _ = builder.Append('-');
			_ = builder.Append(char.ToLowerInvariant(c));
		}
		return builder.ToString();
	}
}
