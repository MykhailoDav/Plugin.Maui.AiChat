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

        view.Follow.OnScrolled(verticalDelta: -40, lastVisibleIndex: 0, count: 2);
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
    public void Learns_from_the_users_scrolling()
    {
        var view = new AiChatView();
        view.List.SendScrolled(new ItemsViewScrolledEventArgs { VerticalDelta = -30, LastVisibleItemIndex = 0 });
        Assert.False(view.Follow.IsFollowing);

        view.List.SendScrolled(new ItemsViewScrolledEventArgs { VerticalDelta = 30, LastVisibleItemIndex = -1 });
        Assert.True(view.Follow.IsFollowing);

        view.Session = new AiChatSession(new ScriptedChatClient());
        view.Session.Load([new ChatMessage(ChatRole.User, "a"), new ChatMessage(ChatRole.Assistant, "b")]);
        view.List.SendScrolled(new ItemsViewScrolledEventArgs { VerticalDelta = -30, LastVisibleItemIndex = 0 });
        view.List.SendScrolled(new ItemsViewScrolledEventArgs { VerticalDelta = 30, LastVisibleItemIndex = 0 });
        Assert.False(view.Follow.IsFollowing);
        view.List.SendScrolled(new ItemsViewScrolledEventArgs { VerticalDelta = 30, LastVisibleItemIndex = 1 });
        Assert.True(view.Follow.IsFollowing);
    }

    [Theory]
    [InlineData(-2, 0, 3, true)]
    [InlineData(-10, 2, 3, false)]
    [InlineData(5, 1, 3, true)]
    public void Decides_when_to_follow(double delta, int lastVisible, int count, bool following)
    {
        var follow = new FollowEnd();
        follow.OnScrolled(-10, 0, 3);
        if (following && delta > -4 && lastVisible < count - 1)
        {
            follow.Follow();
        }

        follow.OnScrolled(delta, lastVisible, count);

        Assert.Equal(following, follow.IsFollowing);
    }
}
