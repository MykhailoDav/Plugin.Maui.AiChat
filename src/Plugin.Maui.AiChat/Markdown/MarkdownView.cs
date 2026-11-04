using Markdig.Syntax;
using Plugin.Maui.AiChat.Markdown;

namespace Plugin.Maui.AiChat;

/// <summary>
/// Shows Markdown as native views: paragraphs, headings, emphasis, inline code, links, code blocks with a Copy button,
/// lists, task lists, quotes, tables and rules. Built for text that streams in: set <see cref="IsStreaming"/> while it
/// arrives and only the blocks that changed are rebuilt.
/// </summary>
/// <remarks>
/// Links open in the system browser or mail app, and only for <c>http</c>, <c>https</c> and <c>mailto</c> URLs.
/// Images are not loaded; their alt text is shown.
/// </remarks>
public class MarkdownView : ContentView
{
    /// <summary>Identifies the <see cref="Text"/> bindable property.</summary>
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(MarkdownView), null, propertyChanged: OnContentChanged);

    /// <summary>Identifies the <see cref="IsStreaming"/> bindable property.</summary>
    public static readonly BindableProperty IsStreamingProperty = BindableProperty.Create(
        nameof(IsStreaming), typeof(bool), typeof(MarkdownView), false, propertyChanged: OnContentChanged);

    /// <summary>Identifies the <see cref="TextColor"/> bindable property.</summary>
    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
        nameof(TextColor), typeof(Color), typeof(MarkdownView), null, propertyChanged: OnStyleChanged);

    /// <summary>Identifies the <see cref="LinkColor"/> bindable property.</summary>
    public static readonly BindableProperty LinkColorProperty = BindableProperty.Create(
        nameof(LinkColor), typeof(Color), typeof(MarkdownView), null, propertyChanged: OnStyleChanged);

    /// <summary>Identifies the <see cref="FontSize"/> bindable property.</summary>
    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(
        nameof(FontSize), typeof(double), typeof(MarkdownView), 16d, propertyChanged: OnStyleChanged,
        validateValue: static (_, value) => value is double size && double.IsFinite(size) && size > 0);

    private readonly VerticalStackLayout _blocks = new() { Spacing = MarkdownRenderer.BlockSpacing };
    private readonly List<(string Source, View View)> _rendered = [];
    private MarkdownRenderer? _renderer;
    private bool _renderScheduled;

    /// <summary>Creates an empty view.</summary>
    public MarkdownView() => Content = _blocks;

    /// <summary>Gets or sets the Markdown to show. The default is <see langword="null"/>, which shows nothing.</summary>
    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// Gets or sets whether <see cref="Text"/> is still arriving. While it is, unfinished emphasis, inline code and
    /// links render as if they were closed, so the text does not flicker between markup and formatting. The default is
    /// <see langword="false"/>.
    /// </summary>
    public bool IsStreaming
    {
        get => (bool)GetValue(IsStreamingProperty);
        set => SetValue(IsStreamingProperty, value);
    }

    /// <summary>
    /// Gets or sets the color of the text. The default is <see langword="null"/>: near-black in the light theme and
    /// near-white in the dark theme.
    /// </summary>
    public Color? TextColor
    {
        get => (Color?)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the color of links. The default is <see langword="null"/>: violet, lighter in the dark theme.
    /// </summary>
    public Color? LinkColor
    {
        get => (Color?)GetValue(LinkColorProperty);
        set => SetValue(LinkColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the size of body text in device-independent units; headings and code scale from it. The default is
    /// 16. Values that are not positive and finite are ignored.
    /// </summary>
    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    internal IList<IView> Blocks => _blocks.Children;

    private static void OnContentChanged(BindableObject bindable, object oldValue, object newValue)
        => ((MarkdownView)bindable).ScheduleRender();

    private static void OnStyleChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (MarkdownView)bindable;
        view._renderer = null;
        view._rendered.Clear();
        view._blocks.Clear();
        view.ScheduleRender();
    }

    // Tokens arrive faster than frames; one pending render takes in all the text that came before it runs.
    private void ScheduleRender()
    {
        if (_renderScheduled)
        {
            return;
        }

        _renderScheduled = true;
        Dispatcher.Dispatch(Render);
    }

    private void Render()
    {
        _renderScheduled = false;
        var text = Text ?? "";
        if (IsStreaming)
        {
            text = StreamingMarkdown.Heal(text);
        }

        var renderer = _renderer ??= new MarkdownRenderer(new MarkdownStyle(FontSize, TextColor, LinkColor));
        var document = Markdig.Markdown.Parse(text, MarkdownRenderer.Pipeline);
        var index = 0;
        for (var i = 0; i < document.Count; i++)
        {
            var block = document[i];
            if (block is LinkReferenceDefinitionGroup)
            {
                continue;
            }

            // A block owns the text up to the next block: the span of an open code fence stops before its last lines.
            var end = i + 1 < document.Count ? document[i + 1].Span.Start : text.Length;
            var source = text[block.Span.Start..end];
            if (index == _rendered.Count)
            {
                var view = renderer.Render(block);
                _rendered.Add((source, view));
                _blocks.Add(view);
            }
            else if (_rendered[index].Source != source)
            {
                var view = _rendered[index].View;
                if (!renderer.TryUpdate(view, block))
                {
                    view = renderer.Render(block);
                    _blocks[index] = view;
                }

                _rendered[index] = (source, view);
            }

            index++;
        }

        while (_rendered.Count > index)
        {
            _rendered.RemoveAt(_rendered.Count - 1);
            _blocks.RemoveAt(_blocks.Count - 1);
        }
    }
}
