using Android.App;
using Android.Views;

namespace Plugin.Maui.AiChat.Controls;

internal static partial class KeyboardSafeArea
{
    // .NET MAUI pans an Android window for the keyboard. A panned window gets no keyboard insets, so the page's
    // SoftInput safe area would never apply. The app's own mode comes back when the chat leaves the screen.
    static partial void ResizeWindow(VisualElement view)
    {
        if (view.Window?.Handler?.PlatformView is not Activity { Window: { } window })
        {
            return;
        }

        var mode = window.Attributes?.SoftInputMode ?? SoftInput.StateUnspecified;
        var adjust = mode & SoftInput.MaskAdjust;
        if (adjust == SoftInput.AdjustResize)
        {
            return;
        }

        window.SetSoftInputMode((mode & ~SoftInput.MaskAdjust) | SoftInput.AdjustResize);

        void Restore(object? sender, EventArgs e)
        {
            view.Unloaded -= Restore;
            var current = window.Attributes?.SoftInputMode ?? SoftInput.StateUnspecified;
            window.SetSoftInputMode((current & ~SoftInput.MaskAdjust) | adjust);
        }

        view.Unloaded += Restore;
    }
}
