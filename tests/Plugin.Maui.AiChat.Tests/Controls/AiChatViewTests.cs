using Microsoft.Extensions.AI;
using Plugin.Maui.AiChat.Controls;
using Plugin.Maui.AiChat.Tests.TestHelpers;
using Plugin.Maui.AiChat.Theming;

namespace Plugin.Maui.AiChat.Tests.Controls;

public class AiChatViewTests
{
    private static void Click(Button button) => ((IButtonController)button).SendClicked();

    [Fact]
    public void Starts_without_a_session_and_with_its_defaults()
    {
        var view = new AiChatView();

        Assert.Null(view.Session);
        Assert.Equal("Message", view.Placeholder);
        Assert.Equal(Color.FromArgb("#512BD4"), view.AccentColor);
        Assert.Null(view.EmptyView);
        Assert.Equal("Message", view.Input.Editor.Placeholder);
        Assert.False(view.Input.Button.IsEnabled);
        Assert.Equal(0.4, view.Input.Button.Opacity);
        Assert.Equal(ChatInputBar.SendGlyph, view.Input.Button.Text);
    }

    [Fact]
    public void Passes_its_properties_to_the_parts()
    {
        var empty = new Label { Text = "Ask me anything" };
        var view = new AiChatView { Placeholder = "Ask", AccentColor = Colors.Teal, EmptyView = empty };

        Assert.Equal("Ask", view.Input.Editor.Placeholder);
        Assert.Equal(Colors.Teal, view.Input.Button.BackgroundColor);
        Assert.Same(empty, view.List.EmptyView);
    }

    [Fact]
    public void Shows_the_items_of_its_session()
    {
        var session = new AiChatSession(new ScriptedChatClient());
        var view = new AiChatView { Session = session };

        Assert.Same(session.Items, view.List.ItemsSource);

        view.Session = null;
        Assert.Null(view.List.ItemsSource);
    }

    [Fact]
    public void Enables_Send_for_text_and_shows_Stop_while_busy()
    {
        var gate = new TaskCompletionSource();
        var session = new AiChatSession(new ScriptedChatClient().Respond(token => ScriptedChatClient.StreamThenWait(
            [new ChatResponseUpdate(ChatRole.Assistant, "…")], gate.Task, [], honorCancellation: true, token)));
        var view = new AiChatView { Session = session };
        var button = view.Input.Button;

        Assert.False(button.IsEnabled);
        view.Input.Editor.Text = "  ";
        Assert.False(button.IsEnabled);
        view.Input.Editor.Text = " Hi ";
        Assert.True(button.IsEnabled);
        Assert.Equal(1, button.Opacity);

        Click(button);

        Assert.Equal("", view.Input.Editor.Text);
        Assert.Equal("Hi", session.Messages[0].Text);
        Assert.Equal(ChatInputBar.StopGlyph, button.Text);
        Assert.True(button.IsEnabled);
        Assert.Equal(ChatStrings.Stop, SemanticProperties.GetDescription(button));

        Click(button);

        Assert.False(session.IsBusy);
        Assert.Equal(ChatInputBar.SendGlyph, button.Text);
        Assert.Equal(ChatStrings.Send, SemanticProperties.GetDescription(button));
    }

    [Fact]
    public async Task The_button_does_nothing_without_a_session_or_text()
    {
        var client = new ScriptedChatClient();
        var view = new AiChatView();

        await view.Input.PressAsync();

        view.Session = new AiChatSession(client);
        view.Input.Editor.Text = null;
        await view.Input.PressAsync();
        view.Input.Editor.Text = "   ";
        await view.Input.PressAsync();

        Assert.Empty(client.Requests);
    }

    [Fact]
    public async Task Follows_the_end_of_the_conversation_until_the_user_scrolls_up()
    {
        var client = new ScriptedChatClient().RespondText("One").RespondText("Two").RespondText("Three");
        var session = new AiChatSession(client);
        var view = new AiChatView();
        var scrolls = new List<int>();
        view.ScrollToItem = scrolls.Add;

        view.Session = session;
        Assert.Empty(scrolls);

        await session.SendAsync("First");
        Assert.Equal(1, scrolls[^1]);

        view.Follow.OnScrolled(delta: -40, offset: 0, lastVisibleIndex: 0, count: 2, viewport: 100);
        scrolls.Clear();
        await session.SendAsync("Second");
        Assert.Empty(scrolls);

        view.Input.Editor.Text = "Third";
        Click(view.Input.Button);
        await Task.Yield();
        Assert.NotEmpty(scrolls);
    }

    [Fact]
    public void Scrolls_once_for_all_the_changes_before_the_dispatcher_runs()
    {
        using var dispatcher = QueuedDispatcher.Use();
        var session = new AiChatSession(new ScriptedChatClient(), dispatcher: null);
        var view = new AiChatView();
        var scrolls = 0;
        view.ScrollToItem = _ => scrolls++;
        view.Session = session;

        session.Load([new ChatMessage(ChatRole.User, "a"), new ChatMessage(ChatRole.Assistant, "b")]);
        dispatcher.RunAll();

        Assert.Equal(1, scrolls);
    }

    [Fact]
    public void Stops_listening_to_a_session_it_no_longer_shows()
    {
        var old = new AiChatSession(new ScriptedChatClient());
        var view = new AiChatView { Session = old };
        view.Session = new AiChatSession(new ScriptedChatClient());
        var scrolls = 0;
        view.ScrollToItem = _ => scrolls++;

        old.Load([new ChatMessage(ChatRole.User, "a")]);

        Assert.Equal(0, scrolls);
    }

    [Fact]
    public void Follows_the_end_when_a_row_grows()
    {
        var session = new AiChatSession(new ScriptedChatClient());
        session.Load([new ChatMessage(ChatRole.User, "a"), new ChatMessage(ChatRole.Assistant, "b")]);
        var view = new AiChatView { Session = session };
        var scrolls = new List<int>();
        view.ScrollToItem = scrolls.Add;
        var selector = (DataTemplateSelector)view.List.ItemTemplate;
        var row = (View)selector.SelectTemplate(session.Items[1], view.List).CreateContent();

        ((IView)row).Arrange(new Rect(0, 0, 300, 100));
        Assert.Equal([1], scrolls);

        view.Follow.OnScrolled(-40, 0, 0, 2, 100);
        ((IView)row).Arrange(new Rect(0, 0, 300, 200));
        Assert.Equal([1], scrolls);
    }

    [Fact]
    public void Learns_from_the_users_scrolling()
    {
        var view = new AiChatView();
        view.List.SendScrolled(new ItemsViewScrolledEventArgs { VerticalDelta = 400, VerticalOffset = 400, LastVisibleItemIndex = -1 });
        view.List.SendScrolled(new ItemsViewScrolledEventArgs { VerticalDelta = -10, VerticalOffset = 390, LastVisibleItemIndex = -1 });
        Assert.False(view.Follow.IsFollowing);

        view.Session = new AiChatSession(new ScriptedChatClient());
        view.Session.Load([new ChatMessage(ChatRole.User, "a"), new ChatMessage(ChatRole.Assistant, "b")]);
        view.List.SendScrolled(new ItemsViewScrolledEventArgs { VerticalDelta = 400, VerticalOffset = 400, LastVisibleItemIndex = 1 });
        view.List.SendScrolled(new ItemsViewScrolledEventArgs { VerticalDelta = -300, VerticalOffset = 100, LastVisibleItemIndex = 0 });
        Assert.False(view.Follow.IsFollowing);
        view.List.SendScrolled(new ItemsViewScrolledEventArgs { VerticalDelta = 290, VerticalOffset = 390, LastVisibleItemIndex = 1 });
        Assert.True(view.Follow.IsFollowing);
    }

    [Fact]
    public void A_shrinking_row_does_not_stop_following()
    {
        var session = new AiChatSession(new ScriptedChatClient());
        session.Load([new ChatMessage(ChatRole.User, "a"), new ChatMessage(ChatRole.Assistant, "b")]);
        var view = new AiChatView { Session = session };
        view.ScrollToItem = _ => { };
        var row = (View)((DataTemplateSelector)view.List.ItemTemplate).SelectTemplate(session.Items[1], view.List).CreateContent();
        ((IView)row).Arrange(new Rect(0, 0, 300, 200));
        view.List.SendScrolled(new ItemsViewScrolledEventArgs { VerticalDelta = 100, VerticalOffset = 100, LastVisibleItemIndex = 1 });

        ((IView)row).Arrange(new Rect(0, 0, 300, 195));
        view.List.SendScrolled(new ItemsViewScrolledEventArgs { VerticalDelta = -5, VerticalOffset = 95, LastVisibleItemIndex = 1 });
        Assert.True(view.Follow.IsFollowing);

        view.List.SendScrolled(new ItemsViewScrolledEventArgs { VerticalDelta = -15, VerticalOffset = 80, LastVisibleItemIndex = 1 });
        Assert.False(view.Follow.IsFollowing);
    }

    [Fact]
    public void Layout_explains_only_as_much_as_rows_shrank()
    {
        var follow = new FollowEnd();
        follow.OnRowShrank(5);
        follow.OnScrolled(-3, 497, 3, 4, viewport: 400);
        follow.OnScrolled(-2.5, 494.5, 3, 4, viewport: 400);
        Assert.True(follow.IsFollowing);

        follow.OnScrolled(-1.5, 493, 3, 4, viewport: 400);
        Assert.False(follow.IsFollowing);
    }

    [Fact]
    public void A_scroll_down_forgets_old_shrinks()
    {
        var follow = new FollowEnd();
        follow.OnRowShrank(50);
        follow.OnScrolled(10, 510, 3, 4, viewport: 400);
        follow.OnScrolled(-20, 490, 3, 4, viewport: 400);

        Assert.False(follow.IsFollowing);
    }

    [Fact]
    public void Follows_again_back_at_the_end()
    {
        var follow = new FollowEnd();
        follow.OnScrolled(500, 500, 3, 4, viewport: 100);
        follow.OnScrolled(-200, 300, 2, 4, viewport: 100);
        Assert.False(follow.IsFollowing);

        follow.OnScrolled(160, 460, 2, 4, viewport: 100);
        Assert.False(follow.IsFollowing);
        follow.OnScrolled(-20, 440, 3, 4, viewport: 100);
        follow.OnScrolled(1, 441, 3, 4, viewport: 100);
        Assert.False(follow.IsFollowing);
        follow.OnScrolled(19, 460, 3, 4, viewport: 100);
        Assert.True(follow.IsFollowing);

        follow.OnScrolled(-160, 300, 3, 4, viewport: 100);
        Assert.False(follow.IsFollowing);
        follow.Follow();
        Assert.True(follow.IsFollowing);
        follow.OnScrolled(10, 10, 0, 1, viewport: 1000);
        Assert.True(follow.IsFollowing);
    }
}
