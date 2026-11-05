using Android.Content.Res;
using Android.Widget;

namespace Plugin.Maui.AiChat.Controls;

internal sealed partial class ChatInputBar
{
    // Android underlines an EditText; inside the rounded field the line would be a second border. Only this editor
    // changes, not the app's.
    partial void StylePlatformEditor() => Editor.HandlerChanged += (_, _) =>
    {
        if (Editor.Handler?.PlatformView is EditText text)
        {
            text.BackgroundTintList = ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
        }
    };
}
