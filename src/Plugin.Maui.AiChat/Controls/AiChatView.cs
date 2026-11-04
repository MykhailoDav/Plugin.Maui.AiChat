using System.Diagnostics.CodeAnalysis;
using Plugin.Maui.AiChat.Controls;
using Plugin.Maui.AiChat.Theming;

namespace Plugin.Maui.AiChat;

/// <summary>
/// A chat with an AI model: the conversation of an <see cref="AiChatSession"/>, streamed as Markdown, with tool
/// calls, approvals and reasoning, and a message field with Send and Stop. Light and dark themes are built in.
/// </summary>
/// <example>
/// <code language="xaml"><![CDATA[
/// <chat:AiChatView Session="{Binding Chat}" />
/// ]]></code>
/// </example>
public class AiChatView : ContentView
{
    /// <summary>Identifies the <see cref="Session"/> bindable property.</summary>
    public static readonly BindableProperty SessionProperty = BindableProperty.Create(
        nameof(Session), typeof(AiChatSession), typeof(AiChatView), null, propertyChanged: OnSessionChanged);

    /// <summary>Identifies the <see cref="Placeholder"/> bindable property.</summary>
    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(
        nameof(Placeholder), typeof(string), typeof(AiChatView), ChatStrings.Placeholder);

    /// <summary>Identifies the <see cref="AccentColor"/> bindable property.</summary>
    public static readonly BindableProperty AccentColorProperty = BindableProperty.Create(
        nameof(AccentColor), typeof(Color), typeof(AiChatView), ChatTheme.DefaultAccent);

    /// <summary>Identifies the <see cref="EmptyView"/> bindable property.</summary>
    public static readonly BindableProperty EmptyViewProperty = BindableProperty.Create(
        nameof(EmptyView), typeof(View), typeof(AiChatView), null,
        propertyChanged: static (bindable, _, value) => ((AiChatView)bindable).List.EmptyView = value);

    // Follows the session's IsBusy through a binding, which MAUI holds weakly, so the session does not keep the view alive.
    private static readonly BindableProperty IsSessionBusyProperty = BindableProperty.Create(
        "IsSessionBusy", typeof(bool?), typeof(AiChatView), null,
        propertyChanged: static (bindable, _, _) => ((AiChatView)bindable).Input.Refresh());

    private readonly FollowEnd _follow = new();
    private bool _scrollScheduled;

    /// <summary>Creates a view with no session; set <see cref="Session"/> to show one.</summary>
    public AiChatView()
    {
        List = new CollectionView
        {
            AutomationId = "AiChatList",
            SelectionMode = SelectionMode.None,
            ItemTemplate = new ChatItemTemplateSelector(this),
            Footer = new BoxView { HeightRequest = 8, Color = Colors.Transparent },
        };
        List.Scrolled += (_, e) => _follow.OnScrolled(e.VerticalDelta, e.VerticalOffset, e.LastVisibleItemIndex, Session?.Items.Count ?? 0, List.Height);
        Input = new ChatInputBar(this);

        var layout = new Grid { RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) } };
        layout.Add(List);
        layout.Add(Input, 0, 1);
        Content = layout;

        // Clear of the status bar, notch and home indicator on an edge-to-edge page.
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container);
        Loaded += OnLoaded;

        SetBinding(IsSessionBusyProperty, Binding.Create(static (AiChatView view) => view.Session?.IsBusy, source: this));
        ScrollToItem = index => List.ScrollTo(index, position: ScrollToPosition.End, animate: false);
    }

    /// <summary>
    /// Gets or sets the conversation to show. Keep the session in a view model so it outlives the page. The default is
    /// <see langword="null"/>, which shows an empty chat with the message field disabled.
    /// </summary>
    public AiChatSession? Session
    {
        get => (AiChatSession?)GetValue(SessionProperty);
        set => SetValue(SessionProperty, value);
    }

    /// <summary>Gets or sets the hint in the empty message field. The default is "Message".</summary>
    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>
    /// Gets or sets the color of the user's messages, the Send button and Approve. White text goes on it, so pick a
    /// color dark enough for that. The default is #512BD4, the purple of the .NET MAUI app template.
    /// </summary>
    public Color AccentColor
    {
        get => (Color)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the view shown while the conversation is empty, such as a greeting or suggested questions. The
    /// default is <see langword="null"/>.
    /// </summary>
    public View? EmptyView
    {
        get => (View?)GetValue(EmptyViewProperty);
        set => SetValue(EmptyViewProperty, value);
    }

    internal CollectionView List { get; }

    internal ChatInputBar Input { get; }

    internal FollowEnd Follow => _follow;

    /// <summary>Scrolls the list to the end of an item; replaceable because a list without a handler does not scroll.</summary>
    internal Action<int> ScrollToItem { get; set; }

    /// <summary>
    /// Follows the end when a row changes size: the platform measures a row after its content changes, so a scroll
    /// made at the change itself stops short of the new end.
    /// </summary>
    internal View Track(View row)
    {
        var height = row.Height;
        row.SizeChanged += (_, _) =>
        {
            if (row.Height < height)
            {
                _follow.OnRowShrank(height - row.Height);
            }

            height = row.Height;
            if (_follow.IsFollowing)
            {
                ScheduleScrollToEnd();
            }
        };
        return row;
    }

    internal Task SendAsync(string text)
    {
        _follow.Follow();
        return Session!.SendAsync(text);
    }

    [ExcludeFromCodeCoverage(Justification = "Loaded needs a window; the UI tests cover it.")]
    private void OnLoaded(object? sender, EventArgs e) => KeyboardSafeArea.Apply(this);

    private static void OnSessionChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        var view = (AiChatView)bindable;
        if (oldValue is AiChatSession old)
        {
            old.ItemsChanged -= view.OnItemsChanged;
        }

        var session = (AiChatSession?)newValue;
        view.List.ItemsSource = session?.Items;
        if (session is not null)
        {
            session.ItemsChanged += view.OnItemsChanged;
        }

        view.Input.Refresh();
        view._follow.Follow();
        view.ScheduleScrollToEnd();
    }

    private void OnItemsChanged(object? sender, EventArgs e)
    {
        if (_follow.IsFollowing)
        {
            ScheduleScrollToEnd();
        }
    }

    // Many tokens arrive between frames; one pending scroll covers them all.
    private void ScheduleScrollToEnd()
    {
        if (_scrollScheduled)
        {
            return;
        }

        _scrollScheduled = true;
        Dispatcher.Dispatch(ScrollToEnd);
    }

    private void ScrollToEnd()
    {
        _scrollScheduled = false;
        if (Session is { Items.Count: > 0 } session)
        {
            ScrollToItem(session.Items.Count - 1);
        }
    }
}
