using Plugin.Maui.AiChat.Platform;
using Plugin.Maui.AiChat.Theming;

namespace Plugin.Maui.AiChat.Controls;

/// <summary>A small text button that copies text and says "Copied" for a moment.</summary>
internal sealed class CopyButton : Button
{
    internal static readonly TimeSpan FeedbackDuration = TimeSpan.FromSeconds(2);

    private readonly Func<string?> _text;

    internal CopyButton(Func<string?> text, string description)
    {
        _text = text;
        Text = ChatStrings.Copy;
        FontSize = 12;
        FontAttributes = FontAttributes.None;
        Padding = new Thickness(8, 0);
        MinimumHeightRequest = 32;
        BackgroundColor = Colors.Transparent;
        ChatTheme.Apply(this, TextColorProperty, ChatTheme.Muted);
        SemanticProperties.SetDescription(this, description);
        Clicked += OnClicked;
    }

    private async void OnClicked(object? sender, EventArgs e)
    {
        if (_text() is not { } text)
        {
            return;
        }

        await PlatformActions.CopyText(text).ConfigureAwait(true);
        Text = ChatStrings.Copied;
        Dispatcher.DispatchDelayed(FeedbackDuration, () => Text = ChatStrings.Copy);
    }
}
