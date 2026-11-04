using Microsoft.Maui.Controls.Shapes;
using Plugin.Maui.AiChat.Controls;
using Plugin.Maui.AiChat.Theming;

namespace Plugin.Maui.AiChat.Markdown;

/// <summary>A code block: its language, a Copy button, and the code unwrapped in a horizontal scroll view.</summary>
internal sealed class MarkdownCodeBlock : Border
{
    private readonly Label _code;

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

        CopyButton = new CopyButton(() => Code, ChatStrings.CopyCode);

        var header = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
        header.Add(languageLabel);
        header.Add(CopyButton, 1);

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

    internal CopyButton CopyButton { get; }
}
