using Plugin.Maui.AiChat.Markdown;

namespace Plugin.Maui.AiChat.Tests.Markdown;

public class StreamingMarkdownTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("plain text", "plain text")]
    [InlineData("**bold**", "**bold**")]
    [InlineData("a ** b", "a ** b")]
    [InlineData("2 * 3 = 6", "2 * 3 = 6")]
    [InlineData("snake_case_word", "snake_case_word")]
    [InlineData("word**", "word**")]
    [InlineData("**a\n\nb", "**a\n\nb")]
    [InlineData("- item **one\n- item two", "- item **one\n- item two")]
    [InlineData("~single", "~single")]
    [InlineData("[not a link", "[not a link")]
    [InlineData("[done](https://example.com) next", "[done](https://example.com) next")]
    public void Leaves_complete_or_literal_markdown_alone(string text, string expected)
        => Assert.Equal(expected, StreamingMarkdown.Heal(text));

    [Theory]
    [InlineData("**bold", "**bold**")]
    [InlineData("Hello **wor", "Hello **wor**")]
    [InlineData("**bold** and *it", "**bold** and *it*")]
    [InlineData("_it", "_it_")]
    [InlineData("__strong", "__strong__")]
    [InlineData("~~strike", "~~strike~~")]
    [InlineData("***both", "***both***")]
    [InlineData("**bold *it", "**bold *it***")]
    [InlineData("**bold\nnext line", "**bold\nnext line**")]
    [InlineData("**a **b** c", "**a **b** c**")]
    [InlineData("**bold ", "**bold** ")]
    [InlineData("*it\n", "*it*\n")]
    public void Closes_open_emphasis(string text, string expected)
        => Assert.Equal(expected, StreamingMarkdown.Heal(text));

    [Theory]
    [InlineData("`code", "`code`")]
    [InlineData("``a ` b", "``a ` b``")]
    [InlineData("`a **b", "`a **b`")]
    [InlineData("**a `b", "**a `b`**")]
    [InlineData("`done` and **x", "`done` and **x**")]
    public void Closes_open_code_spans(string text, string expected)
        => Assert.Equal(expected, StreamingMarkdown.Heal(text));

    [Theory]
    [InlineData("[link](https://exa", "[link](https://exa)")]
    [InlineData("[link](https://ex_a/b*c", "[link](https://ex_a/b*c)")]
    [InlineData("**see [docs](http://x", "**see [docs](http://x)**")]
    public void Closes_a_link_whose_url_is_still_arriving(string text, string expected)
        => Assert.Equal(expected, StreamingMarkdown.Heal(text));

    [Theory]
    [InlineData("Hello **", "Hello ")]
    [InlineData("Hello *", "Hello ")]
    [InlineData("Hello `", "Hello ")]
    [InlineData("Hello ~~ \n", "Hello  \n")]
    [InlineData("**", "")]
    [InlineData("text\n**", "text\n")]
    public void Removes_an_opener_with_no_text_after_it(string text, string expected)
        => Assert.Equal(expected, StreamingMarkdown.Heal(text));

    [Theory]
    [InlineData("```\ncode **", "```\ncode **")]
    [InlineData("  ~~~~py\nx = `1", "  ~~~~py\nx = `1")]
    [InlineData("```\n````\n**x", "```\n````\n**x**")]
    [InlineData("```\ncode\n```\n**b", "```\ncode\n```\n**b**")]
    [InlineData("~~~\n```\n**x", "~~~\n```\n**x")]
    public void Leaves_an_open_code_fence_to_the_parser(string text, string expected)
        => Assert.Equal(expected, StreamingMarkdown.Heal(text));

    [Theory]
    [InlineData("\\**not", "\\**not*")]
    [InlineData("a\\`b `c", "a\\`b `c`")]
    [InlineData("end\\", "end\\")]
    public void Skips_escaped_characters(string text, string expected)
        => Assert.Equal(expected, StreamingMarkdown.Heal(text));

    [Theory]
    [InlineData("# Title **x", "# Title **x**")]
    [InlineData("> quote *x", "> quote *x*")]
    [InlineData("| a | **b", "| a | **b**")]
    [InlineData("1. step _x", "1. step _x_")]
    [InlineData("**a\n- b", "**a\n- b")]
    [InlineData("**a\n12) b", "**a\n12) b")]
    [InlineData("**a\n12 b", "**a\n12 b**")]
    [InlineData("**a\n1.", "**a\n1.")]
    [InlineData("**a\n123", "**a\n123**")]
    [InlineData("**a\n12345678901. b", "**a\n12345678901. b**")]
    [InlineData("**a\n-5 degrees", "**a\n-5 degrees**")]
    [InlineData("**a\n-", "**a\n-")]
    public void Starts_over_at_each_block_line(string text, string expected)
        => Assert.Equal(expected, StreamingMarkdown.Heal(text));

    [Theory]
    [InlineData("a_b_ c", "a_b_ c")]
    [InlineData("(_x", "(_x_")]
    [InlineData("_x_y", "_x_y_")]
    [InlineData("x_(", "x_(")]
    [InlineData("**x*", "**x**")]
    [InlineData("_a **b_", "_a **b_")]
    public void Follows_the_flanking_rules(string text, string expected)
        => Assert.Equal(expected, StreamingMarkdown.Heal(text));
}
