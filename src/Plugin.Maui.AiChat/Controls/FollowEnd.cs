namespace Plugin.Maui.AiChat.Controls;

/// <summary>
/// Decides whether the list keeps the end of a streaming answer in view. It stops when the user scrolls up to read
/// and starts again when they scroll back to the last row or send a message.
/// </summary>
internal sealed class FollowEnd
{
    // Small upward moves come from the bounce at the end of a list on iOS, not from the user.
    private const double UpwardThreshold = 4;

    internal bool IsFollowing { get; private set; } = true;

    internal void OnScrolled(double verticalDelta, int lastVisibleIndex, int count)
    {
        if (verticalDelta < -UpwardThreshold)
        {
            IsFollowing = false;
        }
        else if (lastVisibleIndex >= count - 1)
        {
            IsFollowing = true;
        }
    }

    internal void Follow() => IsFollowing = true;
}
