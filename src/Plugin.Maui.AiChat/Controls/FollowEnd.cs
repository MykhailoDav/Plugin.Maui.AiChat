namespace Plugin.Maui.AiChat.Controls;

/// <summary>
/// Decides whether the list keeps the end of a streaming answer in view. It stops as soon as the user scrolls up and
/// starts again when they scroll back down to the end or send a message.
/// </summary>
/// <remarks>
/// Scrolled events do not say who moved the list. When a row shrinks for a moment as its Markdown is rebuilt, the
/// platform moves the offset up by as much, so an upward move that a shrink explains is layout; any other upward move
/// is the user. Stopping at the user's first move matters: while an answer streams, every follow-up scroll would pull
/// the list back from under their finger.
/// </remarks>
internal sealed class FollowEnd
{
    // How far above the furthest point the user can be and still count as back at the end.
    private const double MinimumSlack = 48;
    // Platforms round offsets and sizes differently.
    private const double Tolerance = 1;

    private double _furthest;
    private double _shrunk;

    internal bool IsFollowing { get; private set; } = true;

    /// <summary>A row got shorter by <paramref name="amount"/>, which may move the offset up by as much.</summary>
    internal void OnRowShrank(double amount) => _shrunk += amount;

    /// <param name="delta">The vertical change of the offset.</param>
    /// <param name="offset">The vertical scroll offset.</param>
    /// <param name="lastVisibleIndex">The index of the last row in view.</param>
    /// <param name="count">The number of rows.</param>
    /// <param name="viewport">The height of the list.</param>
    internal void OnScrolled(double delta, double offset, int lastVisibleIndex, int count, double viewport)
    {
        if (delta < 0)
        {
            var byLayout = Math.Min(-delta, _shrunk);
            _shrunk -= byLayout;
            if (-delta - byLayout > Tolerance)
            {
                IsFollowing = false;
            }

            return;
        }

        _shrunk = 0;
        if (IsFollowing)
        {
            _furthest = Math.Max(_furthest, offset);
        }
        else if (lastVisibleIndex >= count - 1 && offset >= _furthest - Math.Max(MinimumSlack, viewport / 4))
        {
            Follow();
            _furthest = offset;
        }
    }

    /// <summary>Follows from where the list is now, for a new message or a new conversation.</summary>
    internal void Follow()
    {
        IsFollowing = true;
        _furthest = 0;
        _shrunk = 0;
    }
}
