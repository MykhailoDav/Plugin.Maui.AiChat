using AndroidX.RecyclerView.Widget;

namespace Plugin.Maui.AiChat;

public partial class AiChatView
{
    // On Android, a scroll to the end of a row taller than the list shows the row's top. The rest of the way is a plain
    // scroll, which the list stops at its end; it runs after the layout pass the first scroll started.
    partial void ScrollPastEnd()
    {
        if (List.Handler?.PlatformView is RecyclerView list)
        {
            list.Post(() => list.ScrollBy(0, int.MaxValue / 2));
        }
    }
}
