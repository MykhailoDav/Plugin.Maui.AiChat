using Microsoft.Extensions.AI;
using Plugin.Maui.AiChat.Chat;
using Plugin.Maui.AiChat.Controls;
using Plugin.Maui.AiChat.Tests.TestHelpers;
using Plugin.Maui.AiChat.Theming;

namespace Plugin.Maui.AiChat.Tests.Controls;

[Collection(nameof(SerialPlatformActions))]
public class ChatRowViewTests
{
    private static void Click(Button button) => ((IButtonController)button).SendClicked();

    private static T Row<T>(AiChatView owner, ChatItem item)
        where T : View
    {
        var selector = (DataTemplateSelector)owner.List.ItemTemplate;
        var view = Assert.IsType<T>(selector.SelectTemplate(item, owner.List).CreateContent());
        view.BindingContext = item;
        return view;
    }

    [Fact]
    public void Picks_one_template_per_kind_of_row()
    {
        var owner = new AiChatView();
        var selector = (DataTemplateSelector)owner.List.ItemTemplate;
        var call = new FunctionCallContent("c", "f");

        Assert.Same(
            selector.SelectTemplate(new UserMessageItem("a"), owner.List),
            selector.SelectTemplate(new UserMessageItem("b"), owner.List));
        Row<UserMessageView>(owner, new UserMessageItem("a"));
        Row<AssistantMessageView>(owner, new AssistantMessageItem("a", null, false));
        Row<ReasoningView>(owner, new ReasoningItem("a", null, false));
        Row<ToolCallView>(owner, new ToolCallItem(call));
        Row<ErrorView>(owner, new ErrorItem("a", false));
    }

    [Fact]
    public void A_user_message_is_a_bubble_in_the_accent_color()
    {
        var owner = new AiChatView();
        var row = Row<UserMessageView>(owner, new UserMessageItem("Hello"));
        var bubble = (Border)row.Content;

        Assert.Equal("Hello", ((Label)bubble.Content!).Text);
        Assert.Equal(owner.AccentColor, bubble.BackgroundColor);

        owner.AccentColor = Colors.Teal;
        Assert.Equal(Colors.Teal, bubble.BackgroundColor);
    }

    [Fact]
    public void An_answer_streams_as_markdown_and_offers_Copy_when_complete()
    {
        using var platform = new PlatformActionsScope();
        var item = new AssistantMessageItem("**Hi**", null, isStreaming: true);
        var row = Row<AssistantMessageView>(new AiChatView(), item);

        Assert.Equal("**Hi**", row.Markdown.Text);
        Assert.True(row.Markdown.IsStreaming);
        Assert.False(row.Copy.IsVisible);

        item.Append(" there");
        item.IsStreaming = false;

        Assert.Equal("**Hi** there", row.Markdown.Text);
        Assert.True(row.Copy.IsVisible);
        Click(row.Copy);
        Assert.Equal("**Hi** there", Assert.Single(platform.Copied));
    }

    [Fact]
    public void Copy_without_an_answer_copies_nothing()
    {
        using var platform = new PlatformActionsScope();
        var row = new AssistantMessageView();

        Click(row.Copy);

        Assert.Empty(platform.Copied);
    }

    [Fact]
    public void Reasoning_opens_and_closes_on_a_tap()
    {
        var item = new ReasoningItem("Because…", null, isStreaming: true);
        var row = Row<ReasoningView>(new AiChatView(), item);
        var tap = (TapGestureRecognizer)row.Header.GestureRecognizers[0];

        Assert.Equal("▸ " + ChatStrings.Thinking, row.Header.Text);
        Assert.False(row.Body.IsVisible);
        Assert.Equal("Because…", row.Body.Text);
        Assert.True(row.Body.IsStreaming);

        tap.Command!.Execute(null);
        Assert.True(row.Body.IsVisible);
        Assert.Equal("▾ " + ChatStrings.Thinking, row.Header.Text);

        tap.Command.Execute(null);
        Assert.False(row.Body.IsVisible);

        row.BindingContext = null;
        tap.Command.Execute(null);
        Assert.False(item.IsExpanded);
    }

    [Fact]
    public async Task A_tool_card_shows_Approve_and_Reject_only_while_waiting()
    {
        var call = new FunctionCallContent("c1", "get_weather", new Dictionary<string, object?> { ["city"] = "Kyiv" });
        var model = new ScriptedChatClient()
            .Respond(new ChatResponseUpdate(ChatRole.Assistant, [new ToolApprovalRequestContent("r1", call)]))
            .RespondText("Done.");
        var session = new AiChatSession(model);
        var owner = new AiChatView { Session = session };
        await session.SendAsync("Weather?");
        var item = (ToolCallItem)session.Items[1];
        var row = Row<ToolCallView>(owner, item);
        var buttons = (HorizontalStackLayout)row.Approve.Parent;

        Assert.True(buttons.IsVisible);
        Assert.Equal(owner.AccentColor, row.Approve.BackgroundColor);

        Click(row.Approve);
        await Task.Yield();

        Assert.False(buttons.IsVisible);
        Assert.Equal(2, model.Requests.Count);
        Assert.Contains(model.Requests[1][^1].Contents, c => c is ToolApprovalResponseContent { Approved: true });
    }

    [Fact]
    public async Task Reject_answers_no()
    {
        var call = new FunctionCallContent("c1", "delete_everything");
        var model = new ScriptedChatClient()
            .Respond(new ChatResponseUpdate(ChatRole.Assistant, [new ToolApprovalRequestContent("r1", call)]))
            .RespondText("Okay, I won't.");
        var session = new AiChatSession(model);
        var owner = new AiChatView { Session = session };
        await session.SendAsync("Clean up");
        var item = (ToolCallItem)session.Items[1];

        Click(Row<ToolCallView>(owner, item).Reject);
        await Task.Yield();

        Assert.Equal(ToolCallStatus.Rejected, item.Status);
        Assert.Contains(model.Requests[1][^1].Contents, c => c is ToolApprovalResponseContent { Approved: false });
    }

    [Fact]
    public void Tool_buttons_do_nothing_without_a_session_or_an_item()
    {
        var item = new ToolCallItem(new FunctionCallContent("c", "f"))
        {
            Status = ToolCallStatus.AwaitingApproval,
            Request = new ToolApprovalRequestContent("r", new FunctionCallContent("c", "f")),
        };
        var row = Row<ToolCallView>(new AiChatView(), item);

        Click(row.Approve);
        row.BindingContext = null;
        Click(row.Reject);

        Assert.True(item.IsAwaitingApproval);
    }

    [Fact]
    public async Task Retry_runs_the_failed_response_again()
    {
        var model = new ScriptedChatClient().Fail(new HttpRequestException("Offline.")).RespondText("Back online.");
        var session = new AiChatSession(model);
        var owner = new AiChatView { Session = session };
        await session.SendAsync("Hi");
        var row = Row<ErrorView>(owner, session.Items[^1]);

        Assert.True(row.Retry.IsVisible);
        Click(row.Retry);
        await Task.Yield();

        Assert.Equal("Back online.", ((AssistantMessageItem)session.Items[^1]).Text);
    }

    [Fact]
    public void Retry_without_a_session_does_nothing()
    {
        var row = Row<ErrorView>(new AiChatView(), new ErrorItem("Offline.", canRetry: true));

        Click(row.Retry);

        Assert.True(row.Retry.IsVisible);
    }
}
