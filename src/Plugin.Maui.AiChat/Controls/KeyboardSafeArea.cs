namespace Plugin.Maui.AiChat.Controls;

/// <summary>
/// Keeps the message field above the keyboard with the conversation still in view. In .NET MAUI 10 only a page honors
/// the keyboard (<see cref="SafeAreaRegions.SoftInput"/>) in its safe area, and an Android window pans for the keyboard
/// instead of resizing; either way the whole page slides up and the messages leave the screen while the user types.
/// </summary>
internal static partial class KeyboardSafeArea
{
    /// <summary>
    /// Adds the keyboard to the bottom safe area of the view's page, unless the app set the page's safe area itself, and
    /// lets the window resize for the keyboard while the view is loaded when the page avoids it.
    /// </summary>
    internal static void Apply(VisualElement view)
    {
        var parent = view.Parent;
        while (parent is not null and not ContentPage)
        {
            parent = parent.Parent;
        }

        if (parent is not ContentPage page)
        {
            return;
        }

        if (!page.IsSet(ContentPage.SafeAreaEdgesProperty))
        {
            var edges = page.SafeAreaEdges;
            page.SafeAreaEdges = new SafeAreaEdges(edges.Left, edges.Top, edges.Right, SafeAreaRegions.SoftInput);
        }

        if (AvoidsKeyboard(page.SafeAreaEdges.Bottom))
        {
            ResizeWindow(view);
        }
    }

    internal static bool AvoidsKeyboard(SafeAreaRegions regions)
        => regions == SafeAreaRegions.All || (regions != SafeAreaRegions.Default && regions.HasFlag(SafeAreaRegions.SoftInput));

    /// <summary>Makes the platform window resize for the keyboard while <paramref name="view"/> is loaded.</summary>
    static partial void ResizeWindow(VisualElement view);
}
