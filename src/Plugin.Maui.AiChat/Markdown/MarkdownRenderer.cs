using System.Globalization;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Plugin.Maui.AiChat.Theming;

namespace Plugin.Maui.AiChat.Markdown;

/// <summary>The look of rendered Markdown. A null color means the theme's.</summary>
internal sealed record MarkdownStyle(double FontSize, Color? TextColor, Color? LinkColor);

/// <summary>Turns Markdig blocks into MAUI views: labels with formatted spans, borders, grids and scroll views.</summary>
internal sealed partial class MarkdownRenderer(MarkdownStyle style)
{
    /// <summary>CommonMark plus the GitHub extensions that models use: pipe tables, strikethrough, task lists, autolinks.</summary>
    internal static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseEmphasisExtras(Markdig.Extensions.EmphasisExtras.EmphasisExtraOptions.Strikethrough)
        .UseTaskLists()
        .UseAutoLinks()
        .Build();

    internal const double BlockSpacing = 8;
    private const double ListSpacing = 4;

    internal MarkdownStyle Style { get; } = style;

    internal View Render(Block block) => block switch
    {
        HeadingBlock heading => Paragraph(heading.Inline, heading.Level),
        ParagraphBlock paragraph => Paragraph(paragraph.Inline, 0),
        CodeBlock code => new MarkdownCodeBlock(Style, code.Lines.ToString(), LanguageOf(code)),
        ThematicBreakBlock => Rule(),
        ListBlock list => List(list),
        QuoteBlock quote => Quote(quote),
        Table table => TableView(table),
        ContainerBlock container => Children(container),
        // HTML and any other leaf without inlines: its source, as text.
        _ => Text(((LeafBlock)block).Lines.ToString()),
    };

    /// <summary>Updates a view rendered earlier when the block is still the same kind, which spares a new view per token.</summary>
    internal bool TryUpdate(View view, Block block)
    {
        switch (view, block)
        {
            case (MarkdownParagraph paragraph, HeadingBlock heading) when paragraph.Level == heading.Level:
                paragraph.FormattedText = Inlines(heading.Inline);
                return true;
            case (MarkdownParagraph paragraph, ParagraphBlock text) when paragraph.Level == 0:
                paragraph.FormattedText = Inlines(text.Inline);
                return true;
            case (MarkdownCodeBlock codeView, CodeBlock code) when codeView.Language == LanguageOf(code):
                codeView.Code = code.Lines.ToString();
                return true;
            default:
                return false;
        }
    }

    private static string? LanguageOf(CodeBlock code)
        => code is FencedCodeBlock fenced && !string.IsNullOrEmpty(fenced.Info) ? fenced.Info : null;

    private MarkdownParagraph Paragraph(ContainerInline? inline, int level, bool bold = false)
    {
        var label = new MarkdownParagraph(level)
        {
            FontSize = level switch
            {
                1 => Style.FontSize * 1.5,
                2 => Style.FontSize * 1.3,
                3 => Style.FontSize * 1.15,
                _ => Style.FontSize,
            },
            FontAttributes = level > 0 || bold ? FontAttributes.Bold : FontAttributes.None,
            LineBreakMode = LineBreakMode.WordWrap,
            FormattedText = Inlines(inline),
        };
        ApplyTextColor(label);
        return label;
    }

    private Label Text(string text)
    {
        var label = new Label { Text = text, FontSize = Style.FontSize, LineBreakMode = LineBreakMode.WordWrap };
        ApplyTextColor(label);
        return label;
    }

    private void ApplyTextColor(Label label)
    {
        if (Style.TextColor is { } color)
        {
            label.TextColor = color;
        }
        else
        {
            ChatTheme.Apply(label, Label.TextColorProperty, ChatTheme.Text);
        }
    }

    private static BoxView Rule()
    {
        // BackgroundColor overrides the app's implicit BoxView style (the MAUI template's is dark).
        var rule = new BoxView { HeightRequest = 1, Margin = new Thickness(0, 4), BackgroundColor = Colors.Transparent };
        ChatTheme.Apply(rule, BoxView.ColorProperty, ChatTheme.Line);
        return rule;
    }

    private View Children(ContainerBlock container)
    {
        if (container.Count == 1)
        {
            return Render(container[0]);
        }

        var stack = new VerticalStackLayout { Spacing = ListSpacing };
        foreach (var child in container)
        {
            stack.Add(Render(child));
        }

        return stack;
    }

    private VerticalStackLayout List(ListBlock list)
    {
        var stack = new VerticalStackLayout { Spacing = ListSpacing };
        // Markdig sets the start of every ordered list; an unordered one has none and never uses the number.
        _ = int.TryParse(list.OrderedStart, CultureInfo.InvariantCulture, out var number);
        foreach (var block in list)
        {
            var item = (ListItemBlock)block;
            var marker = list.IsOrdered
                ? string.Create(CultureInfo.InvariantCulture, $"{number++}{list.OrderedDelimiter}")
                : TaskMarker(item) ?? "•";

            var row = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
                ColumnSpacing = 8,
            };
            row.Add(Text(marker));
            row.Add(Children(item), 1);
            stack.Add(row);
        }

        return stack;
    }

    private static string? TaskMarker(ListItemBlock item)
        => item.Count > 0 && item[0] is ParagraphBlock { Inline.FirstChild: TaskList task } ? (task.Checked ? "☑" : "☐") : null;

    private Grid Quote(QuoteBlock quote)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(3), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 10,
        };
        var bar = new BoxView { CornerRadius = 1.5, BackgroundColor = Colors.Transparent };
        ChatTheme.Apply(bar, BoxView.ColorProperty, ChatTheme.Line);
        grid.Add(bar);
        grid.Add(Children(quote), 1);
        return grid;
    }

    // The columns share the width and their cells wrap; a horizontal scroll view in a list row would take the list's
    // vertical drags on Android.
    private Border TableView(Table table)
    {
        // A pipe table's cells carry no column index (it is -1); their position in the row is the column.
        var grid = new Grid();
        for (var r = 0; r < table.Count; r++)
        {
            var row = (TableRow)table[r];
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            while (grid.ColumnDefinitions.Count < row.Count)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            }
        }

        for (var r = 0; r < table.Count; r++)
        {
            var row = (TableRow)table[r];
            for (var c = 0; c < row.Count; c++)
            {
                grid.Add(Cell((TableCell)row[c], row.IsHeader, AlignmentOf(table, c)), c, r);
            }
        }

        var frame = new Border
        {
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
            Padding = 0,
            Content = grid,
        };
        ChatTheme.ApplyBrush(frame, Border.StrokeProperty, ChatTheme.Line);
        return frame;
    }

    private static TextAlignment AlignmentOf(Table table, int column)
        => column < table.ColumnDefinitions.Count
            ? table.ColumnDefinitions[column].Alignment switch
            {
                TableColumnAlign.Center => TextAlignment.Center,
                TableColumnAlign.Right => TextAlignment.End,
                _ => TextAlignment.Start,
            }
            : TextAlignment.Start;

    private ContentView Cell(TableCell cell, bool header, TextAlignment alignment)
    {
        // A pipe table cell holds one paragraph, or nothing when the row is padded to the widest row.
        var content = Paragraph(cell.Count == 0 ? null : ((ParagraphBlock)cell[0]).Inline, 0, header);
        content.HorizontalTextAlignment = alignment;

        var view = new ContentView { Padding = new Thickness(10, 6), Content = content };
        if (header)
        {
            ChatTheme.Apply(view, VisualElement.BackgroundColorProperty, ChatTheme.Surface);
        }

        return view;
    }
}

/// <summary>A rendered paragraph (level 0) or heading (levels 1 to 6), so a later render can update it in place.</summary>
internal sealed class MarkdownParagraph(int level) : Label
{
    internal int Level { get; } = level;
}
