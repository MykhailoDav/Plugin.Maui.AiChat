using Plugin.Maui.AiChat.Markdown;
using Plugin.Maui.AiChat.Tests.TestHelpers;
using Plugin.Maui.AiChat.Theming;

namespace Plugin.Maui.AiChat.Tests.Markdown;

[Collection(nameof(SerialPlatformActions))]
public class MarkdownActionsTests
{
    [Theory]
    [InlineData("https://example.com/a?b=c")]
    [InlineData("http://example.com")]
    [InlineData("mailto:me@example.com")]
    public async Task Opens_web_and_mail_links(string url)
    {
        using var platform = new PlatformActionsScope();

        await MarkdownLinks.OpenAsync(url);

        Assert.Equal(new Uri(url), Assert.Single(platform.Opened));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/relative/path")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("tel:+380000000000")]
    [InlineData("myapp://do/something")]
    public async Task Ignores_every_other_link(string? url)
    {
        using var platform = new PlatformActionsScope();

        await MarkdownLinks.OpenAsync(url);

        Assert.Empty(platform.Opened);
    }

    [Fact]
    public void Tapping_a_link_opens_it()
    {
        using var platform = new PlatformActionsScope();
        var view = new MarkdownView { Text = "[docs](https://example.com)" };
        var span = ((Label)view.Blocks[0]).FormattedText.Spans[0];
        var tap = (TapGestureRecognizer)span.GestureRecognizers[0];

        tap.Command!.Execute(tap.CommandParameter);

        Assert.Equal(new Uri("https://example.com"), Assert.Single(platform.Opened));
    }

    [Fact]
    public void Copy_puts_the_code_on_the_clipboard_and_says_so()
    {
        using var platform = new PlatformActionsScope();
        var view = new MarkdownView { Text = "```\nline 1\nline 2\n```" };
        var code = (MarkdownCodeBlock)view.Blocks[0];
        var labels = new List<string>();
        code.CopyButton.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Button.Text))
            {
                labels.Add(code.CopyButton.Text);
            }
        };

        ((IButtonController)code.CopyButton).SendClicked();

        Assert.Equal("line 1\nline 2", Assert.Single(platform.Copied));
        // The test dispatcher runs the delayed reset at once.
        Assert.Equal([ChatStrings.Copied, ChatStrings.Copy], labels);
    }
}
