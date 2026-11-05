namespace Plugin.Maui.AiChat.Theming;

/// <summary>
/// The colors of the chat and Markdown views, as light and dark pairs that follow the app theme. Neutrals are zinc,
/// so they sit on any brand color.
/// </summary>
internal static class ChatTheme
{
    /// <summary>The purple of the .NET MAUI app template, so the view matches a new app.</summary>
    internal static readonly Color DefaultAccent = Color.FromArgb("#512BD4");

    internal static readonly Color OnAccent = Colors.White;

    internal static readonly ThemeColor Text = new("#18181B", "#F4F4F5");
    internal static readonly ThemeColor Muted = new("#71717A", "#A1A1AA");
    internal static readonly ThemeColor Surface = new("#F4F4F5", "#27272A");
    internal static readonly ThemeColor Line = new("#E4E4E7", "#3F3F46");
    internal static readonly ThemeColor InlineCode = new("#EDEDF0", "#34343A");
    // The accent is too dark to read on a dark page, so links get a lighter violet there.
    internal static readonly ThemeColor Link = new("#512BD4", "#B9A5FF");
    internal static readonly ThemeColor ErrorSurface = new("#FEF2F2", "#3B1F1F");
    internal static readonly ThemeColor ErrorText = new("#B91C1C", "#FCA5A5");

    internal static void Apply(BindableObject target, BindableProperty property, ThemeColor color)
        => target.SetAppThemeColor(property, color.Light, color.Dark);

    internal static void ApplyBrush(BindableObject target, BindableProperty property, ThemeColor color)
        => target.SetAppTheme<Brush>(property, new SolidColorBrush(color.Light), new SolidColorBrush(color.Dark));
}

internal sealed class ThemeColor(string light, string dark)
{
    internal Color Light { get; } = Color.FromArgb(light);

    internal Color Dark { get; } = Color.FromArgb(dark);
}
