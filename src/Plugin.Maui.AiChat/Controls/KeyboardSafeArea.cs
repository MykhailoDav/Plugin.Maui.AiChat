namespace Plugin.Maui.AiChat.Controls;

/// <summary>
/// Keeps the message field above the keyboard with the conversation still in view. In .NET MAUI 10 only a page honors
/// the keyboard (<see cref="SafeAreaRegions.SoftInput"/>) in its safe area; without it, iOS slides the whole page up
/// and the messages leave the screen while the user types.
/// </summary>
internal static class KeyboardSafeArea
{
    /// <summary>
    /// Adds the keyboard to the bottom safe area of the view's page, unless the app set the page's safe area itself.
    /// </summary>
    internal static void Apply(Element view)
    {
        var parent = view.Parent;
        while (parent is not null and not ContentPage)
        {
            parent = parent.Parent;
        }

        if (parent is ContentPage page && !page.IsSet(ContentPage.SafeAreaEdgesProperty))
        {
            var edges = page.SafeAreaEdges;
            page.SafeAreaEdges = new SafeAreaEdges(edges.Left, edges.Top, edges.Right, SafeAreaRegions.SoftInput);
        }
    }
}
