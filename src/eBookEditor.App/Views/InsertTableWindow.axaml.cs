using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace eBookEditor.App.Views;

/// <summary>
/// A Markdown table builder inspired by tablesgenerator.com/markdown_tables: an editable grid
/// (first row is the header), per-column alignment dropdowns, a live Markdown preview, and
/// Discard/Insert buttons. Insert sets <see cref="Result"/> to the generated GFM table text;
/// the caller (MainWindow's editor context menu) inserts it at the caret and leaves it null on
/// Discard. The grid is built imperatively in code-behind (not data-bound XAML) since its row/
/// column count changes at runtime — simplest way to get a resizable spreadsheet-like editor
/// out of Avalonia's Grid control.
/// </summary>
public partial class InsertTableWindow : Window
{
    private enum ColumnAlignment
    {
        Default,
        Left,
        Center,
        Right
    }

    private readonly List<List<string>> _cells;
    private readonly List<ColumnAlignment> _alignments;

    public string? Result { get; private set; }

    public InsertTableWindow()
    {
        InitializeComponent();

        _alignments = [ColumnAlignment.Default, ColumnAlignment.Default, ColumnAlignment.Default];
        _cells =
        [
            ["", "", ""],
            ["", "", ""],
            ["", "", ""]
        ];

        RebuildGrid();
    }

    private void OnAddRowClick(object? sender, RoutedEventArgs e)
    {
        _cells.Add(Enumerable.Repeat(string.Empty, _alignments.Count).ToList());
        RebuildGrid();
    }

    private void OnRemoveRowClick(object? sender, RoutedEventArgs e)
    {
        if (_cells.Count > 2) // keep at least a header row and one data row
            _cells.RemoveAt(_cells.Count - 1);
        RebuildGrid();
    }

    private void OnAddColumnClick(object? sender, RoutedEventArgs e)
    {
        _alignments.Add(ColumnAlignment.Default);
        foreach (var row in _cells)
            row.Add(string.Empty);
        RebuildGrid();
    }

    private void OnRemoveColumnClick(object? sender, RoutedEventArgs e)
    {
        if (_alignments.Count <= 1)
            return;

        _alignments.RemoveAt(_alignments.Count - 1);
        foreach (var row in _cells)
            row.RemoveAt(row.Count - 1);
        RebuildGrid();
    }

    private void OnDiscardClick(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close();
    }

    private void OnInsertClick(object? sender, RoutedEventArgs e)
    {
        Result = BuildMarkdownTable();
        Close();
    }

    private void RebuildGrid()
    {
        TableGrid.RowDefinitions.Clear();
        TableGrid.ColumnDefinitions.Clear();
        TableGrid.Children.Clear();

        var columnCount = _alignments.Count;

        TableGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var r = 0; r < _cells.Count; r++)
            TableGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (var c = 0; c < columnCount; c++)
            TableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });

        for (var c = 0; c < columnCount; c++)
        {
            var combo = new ComboBox
            {
                ItemsSource = new[] { "Default", "Left", "Center", "Right" },
                SelectedIndex = (int)_alignments[c],
                Margin = new Avalonia.Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var col = c;
            combo.SelectionChanged += (_, _) =>
            {
                _alignments[col] = (ColumnAlignment)combo.SelectedIndex;
                UpdatePreview();
            };
            Grid.SetRow(combo, 0);
            Grid.SetColumn(combo, c);
            TableGrid.Children.Add(combo);
        }

        for (var r = 0; r < _cells.Count; r++)
        {
            for (var c = 0; c < columnCount; c++)
            {
                var isHeaderRow = r == 0;
                var textBox = new TextBox
                {
                    Text = _cells[r][c],
                    Margin = new Avalonia.Thickness(2),
                    FontWeight = isHeaderRow ? Avalonia.Media.FontWeight.Bold : Avalonia.Media.FontWeight.Normal,
                    Watermark = isHeaderRow ? $"Header {c + 1}" : "Cell"
                };
                var (row, col) = (r, c);
                textBox.TextChanged += (_, _) =>
                {
                    _cells[row][col] = textBox.Text ?? string.Empty;
                    UpdatePreview();
                };
                Grid.SetRow(textBox, r + 1);
                Grid.SetColumn(textBox, c);
                TableGrid.Children.Add(textBox);
            }
        }

        UpdatePreview();
    }

    private void UpdatePreview() => PreviewText.Text = BuildMarkdownTable();

    private string BuildMarkdownTable()
    {
        var columnCount = _alignments.Count;
        var escapedCells = _cells.Select(row => row.Select(EscapeCell).ToList()).ToList();

        var columnWidths = new int[columnCount];
        for (var col = 0; col < columnCount; col++)
            columnWidths[col] = Math.Max(escapedCells.Max(row => row[col].Length), 3);

        var sb = new StringBuilder();
        AppendRow(sb, escapedCells[0], columnWidths);
        AppendSeparatorRow(sb, columnWidths);
        for (var row = 1; row < escapedCells.Count; row++)
            AppendRow(sb, escapedCells[row], columnWidths);

        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, List<string> cells, int[] widths)
    {
        sb.Append('|');
        for (var col = 0; col < cells.Count; col++)
            sb.Append(' ').Append(cells[col].PadRight(widths[col])).Append(' ').Append('|');
        sb.Append('\n');
    }

    private void AppendSeparatorRow(StringBuilder sb, int[] widths)
    {
        sb.Append('|');
        for (var col = 0; col < widths.Length; col++)
        {
            var marker = _alignments[col] switch
            {
                ColumnAlignment.Left => ":" + new string('-', widths[col] - 1),
                ColumnAlignment.Right => new string('-', widths[col] - 1) + ":",
                ColumnAlignment.Center => ":" + new string('-', widths[col] - 2) + ":",
                _ => new string('-', widths[col])
            };
            sb.Append(' ').Append(marker).Append(' ').Append('|');
        }
        sb.Append('\n');
    }

    private static string EscapeCell(string text) =>
        text.Replace("|", "\\|").Replace("\r", "").Replace("\n", " ");
}
