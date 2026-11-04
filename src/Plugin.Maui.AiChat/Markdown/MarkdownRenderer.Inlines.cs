using System.Text;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax.Inlines;
using Plugin.Maui.AiChat.Theming;

namespace Plugin.Maui.AiChat.Markdown;

internal sealed partial class MarkdownRenderer
{
    /// <summary>
    /// Renders inlines as spans. Neighbouring text with the same style goes into one span: Markdig splits text at
    /// every character that could start markup, and a span per piece would make long answers slow to lay out.
    /// </summary>
    internal FormattedString Inlines(ContainerInline? inline)
    {
        var spans = new SpanBuilder(this);
        if (inline is not null)
        {
            Append(spans, inline, default);
        }

        return spans.Build();
    }

    private static void Append(SpanBuilder spans, ContainerInline container, InlineStyle style)
    {
        for (var inline = container.FirstChild; inline is not null; inline = inline.NextSibling)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    spans.Add(literal.Content.ToString(), style);
                    break;
                case EmphasisInline emphasis:
                    Append(spans, emphasis, emphasis.DelimiterChar == '~'
                        ? style with { Strikethrough = true }
                        : style with { Attributes = style.Attributes | (emphasis.DelimiterCount >= 2 ? FontAttributes.Bold : FontAttributes.Italic) });
                    break;
                case CodeInline code:
                    spans.Add(code.Content, style with { Code = true });
                    break;
                case LinkInline { IsImage: true } image:
                    // Images are not loaded from model output; their alt text stands in for them.
                    Append(spans, image, style);
                    break;
                case LinkInline link when link.FirstChild is null:
                    spans.Add(link.Url ?? "", style with { Link = link.Url });
                    break;
                case LinkInline link:
                    Append(spans, link, style with { Link = link.Url });
                    break;
                case AutolinkInline autolink:
                    spans.Add(autolink.Url, style with { Link = autolink.IsEmail ? "mailto:" + autolink.Url : autolink.Url });
                    break;
                case LineBreakInline:
                    // Chat answers use single newlines as line breaks, as chat apps and GitHub comments render them.
                    spans.Add("\n", style);
                    break;
                case HtmlEntityInline entity:
                    spans.Add(entity.Transcoded.ToString(), style);
                    break;
                case HtmlInline html:
                    spans.Add(IsLineBreakTag(html.Tag) ? "\n" : html.Tag, style);
                    break;
                case TaskList:
                    // The list item shows it as its marker.
                    break;
                case ContainerInline nested:
                    Append(spans, nested, style);
                    break;
                default:
                    spans.Add(inline.ToString() ?? "", style);
                    break;
            }
        }
    }

    private static bool IsLineBreakTag(string tag)
        => tag.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant() is "<BR>" or "<BR/>";

    private Span CreateSpan(string text, InlineStyle style)
    {
        var span = new Span { Text = text, FontAttributes = style.Attributes };
        var decorations = TextDecorations.None;
        if (style.Strikethrough)
        {
            decorations |= TextDecorations.Strikethrough;
        }

        if (style.Code)
        {
            span.FontFamily = MonospaceFont.Name;
            ChatTheme.Apply(span, Span.BackgroundColorProperty, ChatTheme.InlineCode);
        }

        if (style.Link is { } url)
        {
            // Underlined as well as colored, so a link does not depend on color alone.
            decorations |= TextDecorations.Underline;
            if (Style.LinkColor is { } linkColor)
            {
                span.TextColor = linkColor;
            }
            else
            {
                ChatTheme.Apply(span, Span.TextColorProperty, ChatTheme.Link);
            }

            span.GestureRecognizers.Add(new TapGestureRecognizer { Command = MarkdownLinks.OpenCommand, CommandParameter = url });
        }

        span.TextDecorations = decorations;
        return span;
    }

    private readonly record struct InlineStyle(FontAttributes Attributes, bool Strikethrough, bool Code, string? Link);

    private sealed class SpanBuilder(MarkdownRenderer renderer)
    {
        private readonly FormattedString _result = new();
        private readonly StringBuilder _text = new();
        private InlineStyle _style;

        internal void Add(string text, InlineStyle style)
        {
            if (style != _style)
            {
                Flush();
                _style = style;
            }

            _text.Append(text);
        }

        internal FormattedString Build()
        {
            Flush();
            return _result;
        }

        private void Flush()
        {
            if (_text.Length > 0)
            {
                _result.Spans.Add(renderer.CreateSpan(_text.ToString(), _style));
                _text.Clear();
            }
        }
    }
}
