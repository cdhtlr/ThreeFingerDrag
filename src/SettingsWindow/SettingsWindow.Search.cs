namespace ThreeFingerDrag;

internal sealed partial class SettingsWindow : Form
{
	/// <summary>Search box in the header. Filters the visible setting rows by their title and help text.</summary>
	private readonly TextBox searchBox = new() { Width = 190, Height = 28 };
	/// <summary>Label above the search box.</summary>
	private readonly Label searchLabel = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
	/// <summary>True while the search box has text. Tabbed view is replaced by the column view during a search.</summary>
	private bool searchWasActive;
	/// <summary>True when the search box had keyboard focus while its text changed. Focus is restored after the layout runs.</summary>
	private bool searchHadFocus;
	/// <summary>Rows and sections hidden by the search. Kept separately because Control.Visible is false for controls on unselected tabs.</summary>
	private readonly HashSet<Control> filteredOut = [];
	/// <summary>Id of the tab that was selected when the search started. Restored when the search is cleared.</summary>
	private string? savedTabId;
	/// <summary>Tab id to select the next time the tab view is built. Null when there is nothing to restore.</summary>
	private string? restoreTabId;

	/// <summary>Handles a change in the search text. Remembers and restores the selected tab around a search.</summary>
	private void OnSearchTextChanged()
	{
		searchHadFocus = searchBox.Focused;
		bool active = searchBox.Text.Trim().Length > 0;
		if (active && !searchWasActive) savedTabId = tabbedContent.SelectedTab?.Tag as string;
		if (!active && searchWasActive) restoreTabId = savedTabId;
		searchWasActive = active;
		RequestResponsiveLayout();
	}

	/// <summary>
	/// Shows only the rows that match the search text. A row matches when every search word appears in its text,
	/// or a word in its text is close to it (one or two letters off). If nothing matches exactly, rows that match
	/// any word are shown instead. A section whose title matches shows all its rows. With no search text, everything is shown.
	/// </summary>
	private void ApplySearchFilter()
	{
		string[] tokens = searchBox.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(token => token.ToLowerInvariant()).ToArray();
		bool strict = tokens.Length == 0 || sections.Any(section => SectionRows(section.Group).Any(row => TextMatches(RowText(row), tokens, strict: true)));
		foreach (var (_, title, group) in sections)
		{
			if (SectionStack(group) is not { } stack) continue;

			bool titleMatches = tokens.Length > 0 && TextMatches(WithAllLanguages(title), tokens, strict);
			int shown = 0;
			for (int i = 0; i < stack.RowCount; i++)
			{
				if (stack.GetControlFromPosition(0, i) is not TableLayoutPanel row)
				{
					stack.RowStyles[i].Height = tokens.Length == 0 ? SpacerRowHeight : 0;
					continue;
				}

				bool show = tokens.Length == 0 || titleMatches || TextMatches(RowText(row), tokens, strict);
				row.Visible = show;
				if (show) shown++;
				SetFilteredOut(row, !show);
			}
			bool groupShown = tokens.Length == 0 || shown > 0;
			group.Visible = groupShown;
			SetFilteredOut(group, !groupShown);
			FitSectionGroup(group);
		}
	}

	/// <summary>
	/// Records whether the search hid a control. Control.Visible also reads false while the control sits on an unselected tab,
	/// so the layout uses this record instead.
	/// </summary>
	private void SetFilteredOut(Control control, bool hidden)
	{
		if (hidden) filteredOut.Add(control); else filteredOut.Remove(control);
	}

	/// <summary>Returns the table that holds a section's rows, or null if the section has none.</summary>
	private static TableLayoutPanel? SectionStack(GroupBox group) => group.Controls.OfType<TableLayoutPanel>().FirstOrDefault();

	/// <summary>Returns the setting rows of a section, leaving out spacers.</summary>
	private static IEnumerable<TableLayoutPanel> SectionRows(GroupBox group) => SectionStack(group)?.Controls.OfType<TableLayoutPanel>() ?? [];

	/// <summary>Maps each translated text to the texts of the same key in every language. Built once, from all loaded language files.</summary>
	private Dictionary<string, HashSet<string>>? crossLanguageTexts;

	private Dictionary<string, HashSet<string>> CrossLanguageTexts => crossLanguageTexts ??= BuildCrossLanguageTexts();

	private Dictionary<string, HashSet<string>> BuildCrossLanguageTexts()
	{
		Dictionary<string, HashSet<string>> valuesByKey = new(StringComparer.Ordinal);
		foreach (var language in translations.Values)
			foreach (var pair in language)
			{
				if (!valuesByKey.TryGetValue(pair.Key, out var values)) valuesByKey[pair.Key] = values = new HashSet<string>(StringComparer.Ordinal);
				values.Add(pair.Value);
			}

		Dictionary<string, HashSet<string>> result = new(StringComparer.Ordinal);
		foreach (var values in valuesByKey.Values)
			foreach (var value in values)
			{
				if (!result.TryGetValue(value, out var related)) result[value] = related = new HashSet<string>(StringComparer.Ordinal);
				related.UnionWith(values);
			}
		return result;
	}

	/// <summary>Returns the text followed by its translations in every language, so a search works in any language.</summary>
	private string WithAllLanguages(string text)
	{
		if (!CrossLanguageTexts.TryGetValue(text, out var related)) return text;
		return text + " " + string.Join(' ', related);
	}

	/// <summary>Collects the visible text of a row (its labels and buttons), in every language.</summary>
	private string RowText(Control control)
	{
		System.Text.StringBuilder text = new();
		AppendControlText(control, text);
		return text.ToString();
	}

	private void AppendControlText(Control control, System.Text.StringBuilder text)
	{
		if (control is Label or ButtonBase) text.Append(' ').Append(WithAllLanguages(control.Text));
		foreach (Control child in control.Controls) AppendControlText(child, text);
	}

	/// <summary>Checks whether the text matches the search words, either all of them (strict) or any of them.</summary>
	private static bool TextMatches(string text, string[] tokens, bool strict)
	{
		string haystack = text.ToLowerInvariant();
		string[] words = System.Text.RegularExpressions.Regex.Split(haystack, @"\W+");
		bool TokenMatches(string token) => haystack.Contains(token, StringComparison.Ordinal)
			|| words.Any(word => WithinEditDistance(word, token, token.Length <= 4 ? 1 : 2));
		return strict ? tokens.All(TokenMatches) : tokens.Any(TokenMatches);
	}

	/// <summary>Returns true when the Levenshtein distance between two words is at most <paramref name="maxDistance"/>.</summary>
	private static bool WithinEditDistance(string a, string b, int maxDistance)
	{
		if (Math.Abs(a.Length - b.Length) > maxDistance) return false;

		int[] previous = Enumerable.Range(0, b.Length + 1).ToArray();
		for (int i = 1; i <= a.Length; i++)
		{
			int[] current = new int[b.Length + 1];
			current[0] = i;
			for (int j = 1; j <= b.Length; j++)
			{
				int cost = a[i - 1] == b[j - 1] ? 0 : 1;
				current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
			}
			previous = current;
		}
		return previous[b.Length] <= maxDistance;
	}
}
