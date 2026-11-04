using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Plugin.Maui.AiChat.Markdown;
using Plugin.Maui.AiChat.Tests.TestHelpers;
using Plugin.Maui.AiChat.Theming;

namespace Plugin.Maui.AiChat.Tests.Markdown;

public class MarkdownViewTests
{
    private static MarkdownView Render(string markdown, bool streaming = false)
        => new() { IsStreaming = streaming, Text = markdown };

    private static IList<Span> SpansOf(IView view) => ((Label)view).FormattedText.Spans;

    [Fact]
    public void Shows_nothing_without_text()
    {
        var view = new MarkdownView();
        Assert.Empty(view.Blocks);
        Assert.Null(view.Text);
        Assert.False(view.IsStreaming);

        view.Text = "";
        Assert.Empty(view.Blocks);
    }

    [Fact]
    public void Renders_a_paragraph_as_one_label_with_styled_spans()
    {
        var view = Render("Plain **bold** *italic* ~~gone~~ `code` ***both***");

        var paragraph = Assert.IsType<MarkdownParagraph>(Assert.Single(view.Blocks));
        Assert.Equal(0, paragraph.Level);
        Assert.Equal(16, paragraph.FontSize);
        Assert.Equal(FontAttributes.None, paragraph.FontAttributes);

        var spans = paragraph.FormattedText.Spans;
        Assert.Equal(["Plain ", "bold", " ", "italic", " ", "gone", " ", "code", " ", "both"], spans.Select(s => s.Text));
        Assert.Equal(FontAttributes.Bold, spans[1].FontAttributes);
        Assert.Equal(FontAttributes.Italic, spans[3].FontAttributes);
        Assert.Equal(TextDecorations.Strikethrough, spans[5].TextDecorations);
        Assert.Equal(MonospaceFont.Name, spans[7].FontFamily);
        Assert.Equal(ChatTheme.InlineCode.Light, spans[7].BackgroundColor);
        Assert.Equal(FontAttributes.Bold | FontAttributes.Italic, spans[9].FontAttributes);
        Assert.Equal(ChatTheme.Text.Light, paragraph.TextColor);
    }

    [Theory]
    [InlineData("# One", 1, 24)]
    [InlineData("## Two", 2, 20.8)]
    [InlineData("### Three", 3, 18.4)]
    [InlineData("#### Four", 4, 16)]
    [InlineData("Setext\n===", 1, 24)]
    public void Renders_headings_bold_and_larger(string markdown, int level, double size)
    {
        var heading = Assert.IsType<MarkdownParagraph>(Assert.Single(Render(markdown).Blocks));
        Assert.Equal(level, heading.Level);
        Assert.Equal(size, heading.FontSize, 3);
        Assert.Equal(FontAttributes.Bold, heading.FontAttributes);
    }

    [Fact]
    public void Renders_links_underlined_with_a_tap_that_opens_them()
    {
        var view = Render("See [the docs](https://example.com/docs), <https://a.dev> and <me@example.com>.");

        var spans = SpansOf(Assert.Single(view.Blocks));
        var link = spans[1];
        Assert.Equal("the docs", link.Text);
        Assert.Equal(TextDecorations.Underline, link.TextDecorations);
        Assert.Equal(ChatTheme.Link.Light, link.TextColor);
        var tap = Assert.IsType<TapGestureRecognizer>(Assert.Single(link.GestureRecognizers));
        Assert.Same(MarkdownLinks.OpenCommand, tap.Command);
        Assert.Equal("https://example.com/docs", tap.CommandParameter);

        Assert.Equal("https://a.dev", spans[3].Text);
        Assert.Equal("https://a.dev", ((TapGestureRecognizer)spans[3].GestureRecognizers[0]).CommandParameter);
        Assert.Equal("me@example.com", spans[5].Text);
        Assert.Equal("mailto:me@example.com", ((TapGestureRecognizer)spans[5].GestureRecognizers[0]).CommandParameter);
    }

    [Fact]
    public void Shows_the_url_of_a_link_without_text_and_the_alt_text_of_an_image()
    {
        var spans = SpansOf(Assert.Single(Render("[](https://x.dev) ![a cat](cat.png)").Blocks));

        Assert.Equal("https://x.dev", spans[0].Text);
        Assert.Equal(" a cat", string.Concat(spans.Skip(1).Select(s => s.Text)));
        Assert.Empty(spans[1].GestureRecognizers);
    }

    [Fact]
    public void Renders_line_breaks_entities_and_inline_html_as_text()
    {
        var spans = SpansOf(Assert.Single(Render("one\ntwo  \nthree&amp;four<br/>five <b>six</b> <BR >seven").Blocks));

        Assert.Equal("one\ntwo\nthree&four\nfive <b>six</b> \nseven", string.Concat(spans.Select(s => s.Text)));
    }

    [Fact]
    public void Renders_fenced_and_indented_code_blocks()
    {
        var view = Render("```csharp\nvar x = 1;\nvar y = 2;\n```\n\n    indented");

        var fenced = Assert.IsType<MarkdownCodeBlock>(view.Blocks[0]);
        Assert.Equal("csharp", fenced.Language);
        Assert.Equal("var x = 1;\nvar y = 2;", fenced.Code);
        var indented = Assert.IsType<MarkdownCodeBlock>(view.Blocks[1]);
        Assert.Null(indented.Language);
        Assert.Equal("indented", indented.Code);
    }

    [Fact]
    public void Renders_bullet_ordered_task_and_nested_lists()
    {
        var view = Render("- a\n- b\n  - nested\n\n3) three\n4) four\n\n- [x] done\n- [ ] todo\n\n+\n");

        var bullets = Assert.IsType<VerticalStackLayout>(view.Blocks[0]);
        Assert.Equal(2, bullets.Count);
        Assert.Equal("•", MarkerOf(bullets[0]));
        var nestedItem = Assert.IsType<VerticalStackLayout>(((Grid)bullets[1]).Children[1]);
        Assert.IsType<VerticalStackLayout>(nestedItem[1]);

        var ordered = Assert.IsType<VerticalStackLayout>(view.Blocks[1]);
        Assert.Equal(["3)", "4)"], ordered.Select(MarkerOf));

        var tasks = Assert.IsType<VerticalStackLayout>(view.Blocks[2]);
        Assert.Equal(["☑", "☐"], tasks.Select(MarkerOf));
        Assert.Equal("done", string.Concat(SpansOf(((Grid)tasks[0]).Children[1]).Select(s => s.Text)).Trim());

        var empty = Assert.IsType<VerticalStackLayout>(view.Blocks[3]);

        Assert.Empty(Assert.IsType<VerticalStackLayout>(((Grid)empty[0]).Children[1]));
    }

    [Fact]
    public void Renders_a_list_item_that_starts_with_a_code_block()
    {
        var list = Assert.IsType<VerticalStackLayout>(Assert.Single(Render("- ```\n  code\n  ```").Blocks));

        Assert.Equal("•", MarkerOf(list[0]));
        Assert.IsType<MarkdownCodeBlock>(((Grid)list[0]).Children[1]);
    }

    [Fact]
    public void Renders_a_list_item_whose_paragraph_has_no_inlines()
    {
        var renderer = new MarkdownRenderer(new MarkdownStyle(16, null, null));
        var item = new ListItemBlock(null!) { new ParagraphBlock() };
        var list = new ListBlock(null!) { BulletType = '-' };
        list.Add(item);

        var rows = Assert.IsType<VerticalStackLayout>(renderer.Render(list));

        Assert.Equal("•", MarkerOf(rows[0]));
    }

    private static string MarkerOf(IView row) => ((Label)((Grid)row).Children[0]).Text;

    [Fact]
    public void Renders_a_quote_with_a_bar()
    {
        var quote = Assert.IsType<Grid>(Assert.Single(Render("> quoted\n>\n> more").Blocks));

        Assert.IsType<BoxView>(quote.Children[0]);
        var content = Assert.IsType<VerticalStackLayout>(quote.Children[1]);
        Assert.Equal(2, content.Count);
    }

    [Fact]
    public void Renders_a_table_with_a_bold_header_and_aligned_columns()
    {
        var scroll = Assert.IsType<ScrollView>(Assert.Single(Render("| L | C | R | D |\n|:--|:-:|--:|---|\n| a | **b** | c | |").Blocks));

        Assert.Equal(ScrollOrientation.Horizontal, scroll.Orientation);
        var grid = Assert.IsType<Grid>(((Border)scroll.Content).Content);
        Assert.Equal(4, grid.ColumnDefinitions.Count);
        Assert.Equal(2, grid.RowDefinitions.Count);

        var header = (ContentView)grid.Children[0];
        Assert.Equal(ChatTheme.Surface.Light, header.BackgroundColor);
        var headerLabel = (Label)header.Content;
        Assert.Equal(FontAttributes.Bold, headerLabel.FontAttributes);
        Assert.False(headerLabel.FormattedText.Spans[0].IsSet(Span.FontAttributesProperty), "The span takes the label's bold.");

        var body = grid.Children.Skip(4).Cast<ContentView>().ToList();
        Assert.Equal(
            [TextAlignment.Start, TextAlignment.Center, TextAlignment.End],
            body.Take(3).Select(c => ((Label)c.Content).HorizontalTextAlignment));
        Assert.Null(body[0].BackgroundColor);
        Assert.Empty(((Label)body[3].Content).FormattedText.Spans);
    }

    [Fact]
    public void Renders_cells_beyond_the_header_columns()
    {
        var scroll = Assert.IsType<ScrollView>(Assert.Single(Render("| a | b |\n|---|---|\n| 1 | 2 | 3 |").Blocks));
        var grid = Assert.IsType<Grid>(Assert.IsType<Border>(scroll.Content).Content);

        Assert.Equal(3, grid.ColumnDefinitions.Count);
        var extra = Assert.IsAssignableFrom<Label>(Assert.IsType<ContentView>(grid.Children[^1]).Content);
        Assert.Equal("3", extra.FormattedText.Spans[0].Text);
        Assert.Equal(TextAlignment.Start, extra.HorizontalTextAlignment);
    }

    [Fact]
    public void Renders_rules_and_html_blocks()
    {
        var view = Render("---\n\n<div>raw</div>");

        Assert.IsType<BoxView>(view.Blocks[0]);
        Assert.Equal("<div>raw</div>", ((Label)view.Blocks[1]).Text);
    }

    [Fact]
    public void Skips_link_reference_definitions()
    {
        var view = Render("[docs]: https://example.com\n\nSee [docs].");

        var spans = SpansOf(Assert.Single(view.Blocks));
        Assert.Equal("https://example.com", ((TapGestureRecognizer)spans[1].GestureRecognizers[0]).CommandParameter);
    }

    [Fact]
    public void Heals_unfinished_markup_while_streaming()
    {
        var view = Render("Hello **wor", streaming: true);
        Assert.Equal(FontAttributes.Bold, SpansOf(view.Blocks[0])[1].FontAttributes);

        view.IsStreaming = false;
        Assert.Equal("Hello **wor", string.Concat(SpansOf(view.Blocks[0]).Select(s => s.Text)));
    }

    [Fact]
    public void Keeps_unchanged_blocks_and_updates_the_last_one_in_place()
    {
        var view = Render("# Title\n\nFirst para", streaming: true);
        var title = view.Blocks[0];
        var paragraph = view.Blocks[1];

        view.Text = "# Title\n\nFirst paragraph grows";

        Assert.Same(title, view.Blocks[0]);
        Assert.Same(paragraph, view.Blocks[1]);
        Assert.Equal("First paragraph grows", string.Concat(SpansOf(paragraph).Select(s => s.Text)));
    }

    [Fact]
    public void Updates_a_growing_heading_in_place()
    {
        var view = Render("## Tit", streaming: true);
        var heading = view.Blocks[0];

        view.Text = "## Title";

        Assert.Same(heading, view.Blocks[0]);
        Assert.Equal("Title", SpansOf(heading)[0].Text);
    }

    [Fact]
    public void Renders_once_for_all_the_text_that_arrived_before_the_dispatcher_ran()
    {
        using var dispatcher = QueuedDispatcher.Use();
        var view = new MarkdownView { Text = "one" };
        view.Text = "one two";
        view.IsStreaming = true;

        Assert.Equal(1, dispatcher.Pending);
        Assert.Empty(view.Blocks);

        dispatcher.RunAll();

        Assert.Equal("one two", SpansOf(Assert.Single(view.Blocks))[0].Text);
    }

    [Fact]
    public void Updates_a_growing_code_block_in_place()
    {
        var view = Render("```py\nprint(1)", streaming: true);
        var code = view.Blocks[0];

        view.Text = "```py\nprint(1)\nprint(2)";

        Assert.Same(code, view.Blocks[0]);
        Assert.Equal("print(1)\nprint(2)", ((MarkdownCodeBlock)code).Code);
    }

    [Theory]
    [InlineData("Para", "Para\n===")]
    [InlineData("# One", "## One")]
    [InlineData("## Two", "Two words")]
    [InlineData("```py\nx", "```js\nx")]
    [InlineData("```py\nx", "Just text")]
    [InlineData("- a", "- a\n- b")]
    public void Replaces_a_block_whose_kind_changed(string before, string after)
    {
        var view = Render(before);
        var old = view.Blocks[0];

        view.Text = after;

        Assert.NotSame(old, view.Blocks[0]);
        Assert.Single(view.Blocks);
    }

    [Fact]
    public void Removes_blocks_that_are_gone()
    {
        var view = Render("one\n\ntwo\n\nthree");
        view.Text = "one";

        Assert.Single(view.Blocks);
    }

    [Fact]
    public void Renders_again_with_new_colors_and_size()
    {
        var view = Render("Text [link](https://x.dev)\n\n```\ncode\n```");
        var before = view.Blocks[0];

        view.TextColor = Colors.Red;
        view.LinkColor = Colors.Blue;
        view.FontSize = 20;

        Assert.NotSame(before, view.Blocks[0]);
        var paragraph = (Label)view.Blocks[0];
        Assert.Equal(Colors.Red, paragraph.TextColor);
        Assert.Equal(20, paragraph.FontSize);
        Assert.Equal(Colors.Blue, paragraph.FormattedText.Spans[1].TextColor);
        Assert.Equal(Colors.Red, view.TextColor);
        Assert.Equal(Colors.Blue, view.LinkColor);
        Assert.Equal(20, view.FontSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Ignores_an_invalid_font_size(double size)
    {
        var view = new MarkdownView { FontSize = size };
        Assert.Equal(16, view.FontSize);
    }

    [Fact]
    public void Renders_any_other_container_block_as_its_children()
    {
        var renderer = new MarkdownRenderer(new MarkdownStyle(16, null, null));
        var container = new TestContainer();
        var paragraph = new ParagraphBlock { Inline = new ContainerInline().AppendChild(new LiteralInline("inside")) };
        container.Add(paragraph);

        Assert.Equal("inside", SpansOf(renderer.Render(container))[0].Text);
        Assert.Empty(Assert.IsType<VerticalStackLayout>(renderer.Render(new TestContainer())));
    }

    [Fact]
    public void Renders_unknown_inlines_as_their_text()
    {
        var renderer = new MarkdownRenderer(new MarkdownStyle(16, null, null));
        var inlines = new ContainerInline()
            .AppendChild(new LinkInline())
            .AppendChild(new ContainerInline().AppendChild(new LiteralInline("nested ")))
            .AppendChild(new TestInline("custom"))
            .AppendChild(new TestInline(null));

        Assert.Equal("nested custom", string.Concat(renderer.Inlines(inlines).Spans.Select(s => s.Text)));
        Assert.Empty(renderer.Inlines(null).Spans);
    }

    private sealed class TestContainer() : ContainerBlock(null);

    private sealed class TestInline(string? text) : LeafInline
    {
        public override string? ToString() => text;
    }
}
