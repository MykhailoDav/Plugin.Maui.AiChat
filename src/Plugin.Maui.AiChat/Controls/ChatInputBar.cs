using Microsoft.Maui.Controls.Shapes;
using Plugin.Maui.AiChat.Theming;

namespace Plugin.Maui.AiChat.Controls;

/// <summary>The message field and one button: Send while idle, Stop while a response streams.</summary>
internal sealed partial class ChatInputBar : ContentView
{
    internal const string SendGlyph = "↑";
    internal const string StopGlyph = "■";

    private readonly AiChatView _owner;

    internal ChatInputBar(AiChatView owner)
    {
        _owner = owner;

        Editor = new Editor
        {
            AutomationId = "AiChatMessageField",
            AutoSize = EditorAutoSizeOption.TextChanges,
            MaximumHeightRequest = 140,
            FontSize = 16,
            BackgroundColor = Colors.Transparent,
            VerticalOptions = LayoutOptions.Center,
        };
        ChatTheme.Apply(Editor, Editor.TextColorProperty, ChatTheme.Text);
        ChatTheme.Apply(Editor, Editor.PlaceholderColorProperty, ChatTheme.Muted);
        Editor.SetBinding(Editor.PlaceholderProperty, static (AiChatView view) => view.Placeholder, source: owner);
        Editor.TextChanged += (_, _) => Refresh();
        StylePlatformEditor();

        Button = new Button
        {
            AutomationId = "AiChatSendButton",
            WidthRequest = 36,
            MinimumWidthRequest = 36,
            MinimumHeightRequest = 36,
            HeightRequest = 36,
            CornerRadius = 18,
            Padding = 0,
            FontSize = 18,
            TextColor = ChatTheme.OnAccent,
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 0, 0, 2),
        };
        Button.SetBinding(BackgroundColorProperty, static (AiChatView view) => view.AccentColor, source: owner);
        Button.Clicked += async (_, _) => await PressAsync().ConfigureAwait(true);

        var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 6 };
        row.Add(Editor);
        row.Add(Button, 1);

        var field = new Border
        {
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 22 },
            Padding = new Thickness(14, 2, 4, 2),
            Content = row,
        };
        ChatTheme.Apply(field, BackgroundColorProperty, ChatTheme.Surface);
        ChatTheme.ApplyBrush(field, Border.StrokeProperty, ChatTheme.Line);

        Padding = new Thickness(12, 6, 12, 10);
        Content = field;
        Refresh();
    }

    internal Editor Editor { get; }

    /// <summary>Takes off what the platform draws around its text field, which the rounded field already frames.</summary>
    partial void StylePlatformEditor();

    internal Button Button { get; }

    internal void Refresh()
    {
        var session = _owner.Session;
        var busy = session?.IsBusy == true;
        Button.Text = busy ? StopGlyph : SendGlyph;
        SemanticProperties.SetDescription(Button, busy ? ChatStrings.Stop : ChatStrings.Send);
        Button.IsEnabled = session is not null && (busy || !string.IsNullOrWhiteSpace(Editor.Text));
        Button.Opacity = Button.IsEnabled ? 1 : 0.4;
    }

    // The button is disabled when there is nothing to do, but a click can still be on its way when that changes.
    internal Task PressAsync()
    {
        if (_owner.Session is not { } session)
        {
            return Task.CompletedTask;
        }

        if (session.IsBusy)
        {
            session.Stop();
            return Task.CompletedTask;
        }

        var text = Editor.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return Task.CompletedTask;
        }

        Editor.Text = "";
        return _owner.SendAsync(text);
    }
}
