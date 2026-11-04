using Microsoft.Maui.Controls.Shapes;
using Plugin.Maui.AiChat.Platform;
using Plugin.Maui.AiChat.Theming;

namespace Plugin.Maui.AiChat.Markdown;

/// <summary>A code block: its language, a Copy button, and the code unwrapped in a horizontal scroll view.</summary>
internal sealed class MarkdownCodeBlock : Border
{
    internal static readonly TimeSpan CopiedFeedback = TimeSpan.FromSeconds(2);

    private readonly Label _code;
    private readonly Button _copy;

    internal MarkdownCodeBlock(MarkdownStyle style, string code, string? language)
    {
        Language = language;
        StrokeThickness = 1;
        StrokeShape = new RoundRectangle { CornerRadius = 8 };
        Padding = 0;
        ChatTheme.Apply(this, BackgroundColorProperty, ChatTheme.Surface);
        ChatTheme.ApplyBrush(this, StrokeProperty, ChatTheme.Line);

        var languageLabel = new Label
        {
            Text = language ?? "",
            FontSize = 12,
            VerticalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
        };
        ChatTheme.Apply(languageLabel, Label.TextColorProperty, ChatTheme.Muted);

        _copy = new Button
        {
            Text = ChatStrings.Copy,
            FontSize = 12,
            Padding = new Thickness(10, 0),
            MinimumHeightRequest = 32,
            BackgroundColor = Colors.Transparent,
        };
        ChatTheme.Apply(_copy, Button.TextColorProperty, ChatTheme.Muted);
        SemanticProperties.SetDescription(_copy, ChatStrings.CopyCode);
        _copy.Clicked += OnCopyClicked;

        var header = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
        header.Add(languageLabel);
        header.Add(_copy, 1);

        _code = new Label
        {
            Text = code,
            FontFamily = MonospaceFont.Name,
            FontSize = Math.Round(style.FontSize * 0.875),
            LineBreakMode = LineBreakMode.NoWrap,
            Padding = new Thickness(12, 0, 12, 12),
        };
        if (style.TextColor is { } color)
        {
            _code.TextColor = color;
        }
        else
        {
            ChatTheme.Apply(_code, Label.TextColorProperty, ChatTheme.Text);
        }

        Content = new VerticalStackLayout
        {
            header,
            new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = _code },
        };
    }

    internal string? Language { get; }

    internal string Code
    {
        get => _code.Text;
        set => _code.Text = value;
    }

    internal Button CopyButton => _copy;

    private async void OnCopyClicked(object? sender, EventArgs e)
    {
        await PlatformActions.CopyText(Code).ConfigureAwait(true);
        _copy.Text = ChatStrings.Copied;
        Dispatcher.DispatchDelayed(CopiedFeedback, () => _copy.Text = ChatStrings.Copy);
    }
}
