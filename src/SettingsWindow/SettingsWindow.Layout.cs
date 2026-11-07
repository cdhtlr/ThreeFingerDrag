namespace ThreeFingerDrag;

internal sealed partial class SettingsWindow : Form
{
	/// <summary>
	/// Creates a two-column row. The left column holds the title and help text (42%), and the right column
	/// holds the editor (58%). <paramref name="forceStacked"/> is saved in the row Tag for the layout pass.
	/// </summary>
	private TableLayoutPanel Row(string label, string help, bool forceStacked = false)
	{
		TableLayoutPanel row = new()
		{
			Dock = DockStyle.Fill,
			Height = 84,
			ColumnCount = 2,
			RowCount = 1,
			Margin = new Padding(2, 4, 2, 4),
			Padding = Padding.Empty,
			Tag = forceStacked
		};
		_ = row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
		_ = row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
		Panel labels = new() { Dock = DockStyle.Fill, Margin = Padding.Empty };
		Label title = new() { Text = label, AutoSize = false, Dock = DockStyle.Top, Height = 25, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
		Label description = new() { Text = help, AutoSize = false, Dock = DockStyle.Fill, ForeColor = Color.DimGray, Font = new Font("Segoe UI", 8) };
		labels.Controls.Add(description);
		labels.Controls.Add(title);
		Panel editor = new() { Dock = DockStyle.Fill, Margin = new Padding(12, 0, 4, 0), Padding = Padding.Empty };
		row.Controls.Add(labels, 0, 0);
		row.Controls.Add(editor, 1, 0);
		return row;
	}
	/// <summary>Keeps a control at the top of its host and repositions it when the host is resized.</summary>
	private static void CenterVertically(Panel host, Control control)
	{
		void Position() => control.Top = 1;
		host.Resize += (_, _) => Position();
		Position();
	}
	/// <summary>
	/// Places the value display, the Record button, and the Clear button in one strip. The value takes
	/// the space left after the two buttons, and the buttons keep their preferred widths.
	/// </summary>
	private Panel LayoutCaptureControls(Panel host, Control value, Button record, Button clear, int? maxValueWidth = null)
	{
		Panel row = new() { Height = 34, Margin = Padding.Empty, Padding = Padding.Empty, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
		if (value is TextBox) value.AutoSize = false;

		host.Controls.Add(row);
		row.Controls.Add(value);
		row.Controls.Add(record);
		row.Controls.Add(clear);
		// Multi-line and label values are measured with word wrap, so the strip grows to fit them.
		void Arrange()
		{
			int recordWidth = Math.Max(64, record.GetPreferredSize(Size.Empty).Width);
			int clearWidth = Math.Max(56, clear.GetPreferredSize(Size.Empty).Width);
			int gap = 4;
			int availableWidth = host.ClientSize.Width;
			int valueWidth = Math.Max(40, availableWidth - recordWidth - clearWidth - gap * 2);
			if (maxValueWidth is { } maxWidth) valueWidth = Math.Min(valueWidth, maxWidth);

			int valueHeight = value switch
			{
				Label label => Math.Max(24, TextRenderer.MeasureText(label.Text, label.Font, new Size(Math.Max(40, valueWidth - 10), 10000), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding).Height + 8),
				TextBox textBox when textBox.Multiline => Math.Max(24, TextRenderer.MeasureText(textBox.Text, textBox.Font, new Size(Math.Max(40, valueWidth - 10), 10000), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding).Height + 8),
				_ => value.Height,
			};
			int rowHeight = Math.Max(valueHeight, Math.Max(record.Height, clear.Height));
			row.Height = rowHeight;
			if (row.Width != availableWidth || row.Left != 0 || row.Top != 1) row.SetBounds(0, 1, availableWidth, rowHeight);

			value.Bounds = new Rectangle(0, 0, valueWidth, valueHeight);
			record.Bounds = new Rectangle(valueWidth + gap, 0, recordWidth, record.Height);
			clear.Bounds = new Rectangle(valueWidth + gap + recordWidth + gap, 0, clearWidth, clear.Height);
		}
		host.Resize += (_, _) => Arrange();
		row.Resize += (_, _) => Arrange();
		// A taller value, such as a longer key list, can change the row height, so the section is refit.
		value.TextChanged += (_, _) =>
		{
			Arrange();
			if (host.Parent?.Parent?.Parent is GroupBox group) FitSectionGroup(group);
		};
		record.TextChanged += (_, _) => Arrange();
		clear.TextChanged += (_, _) => Arrange();
		Arrange();
		return row;
	}
	/// <summary>Returns the right-hand editor panel of a row.</summary>
	private static Panel EditorHost(TableLayoutPanel row) => (Panel)row.GetControlFromPosition(1, 0)!;
}
