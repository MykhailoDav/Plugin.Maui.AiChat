using System.ComponentModel;
using Microsoft.Extensions.AI;
using Plugin.Maui.AiChat.Chat;
using Plugin.Maui.AiChat.Tests.TestHelpers;
using Plugin.Maui.AiChat.Theming;

namespace Plugin.Maui.AiChat.Tests.Chat;

public class ChatItemBuilderTests
{
    private static ChatResponseUpdate Update(string? messageId, params AIContent[] contents)
        => new(ChatRole.Assistant, contents) { MessageId = messageId };

    [Fact]
    public void Load_shows_a_saved_conversation_as_it_looked_live()
    {
        var session = new AiChatSession(new ScriptedChatClient());
        var failing = new FunctionResultContent("c2", null) { Exception = new InvalidOperationException("Boom") };

        session.Load(
        [
            new ChatMessage(ChatRole.System, "You are helpful."),
            new ChatMessage(ChatRole.User, "Hi"),
            new ChatMessage(ChatRole.Assistant, [new TextReasoningContent("Let me think."), new TextContent("Hello"), new TextContent(" there")]),
            new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("c1", "lookup")]),
            new ChatMessage(ChatRole.Tool, [new FunctionResultContent("c1", "42")]),
            new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("c2", "broken")]),
            new ChatMessage(ChatRole.Tool, [failing]),
            new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("c3", "never_answered")]),
            new ChatMessage(ChatRole.Assistant, [new McpServerToolCallContent("m1", "search", "docs")]),
            new ChatMessage(ChatRole.Tool, [new McpServerToolResultContent("m1")]),
            new ChatMessage(ChatRole.Assistant, [new ErrorContent("Quota exceeded.")]),
            new ChatMessage(ChatRole.Assistant, "Second message"),
            new ChatMessage(ChatRole.Assistant, "Third message"),
            new ChatMessage(ChatRole.User, [new DataContent(new byte[] { 1 }, "image/png")]),
        ]);

        Assert.Equal(14, session.Messages.Count);
        var items = session.Items;
        Assert.Equal("Hi", Assert.IsType<UserMessageItem>(items[0]).Text);
        var reasoning = Assert.IsType<ReasoningItem>(items[1]);
        Assert.Equal("Let me think.", reasoning.Text);
        Assert.False(reasoning.IsStreaming);
        Assert.Equal("Hello there", Assert.IsType<AssistantMessageItem>(items[2]).Text);
        Assert.Equal(
            [ToolCallStatus.Completed, ToolCallStatus.Failed, ToolCallStatus.Canceled, ToolCallStatus.Completed],
            items.OfType<ToolCallItem>().Select(t => t.Status));
        var error = Assert.IsType<ErrorItem>(items[7]);
        Assert.Equal("Quota exceeded.", error.Message);
        Assert.False(error.CanRetry);
        Assert.Equal(["Second message", "Third message"], items.Skip(8).Cast<AssistantMessageItem>().Select(a => a.Text));
        Assert.Equal(10, items.Count);
    }

    [Fact]
    public void Load_accepts_the_sessions_own_messages()
    {
        var session = new AiChatSession(new ScriptedChatClient());
        session.Load([new ChatMessage(ChatRole.User, "Hi"), new ChatMessage(ChatRole.Assistant, "Hello")]);

        session.Load(session.Messages);

        Assert.Equal(2, session.Messages.Count);
        Assert.Equal(2, session.Items.Count);
    }

    [Fact]
    public void Merges_updates_of_one_message_and_splits_different_messages()
    {
        var builder = new ChatItemBuilder();
        var changes = 0;
        builder.Changed += (_, _) => changes++;

        builder.ApplyUpdate(Update("a", new TextContent("One")));
        builder.ApplyUpdate(Update("a", new TextContent(" more")));
        builder.ApplyUpdate(Update("b", new TextContent("Two")));
        builder.ApplyUpdate(Update(null, new TextContent("Three")));
        builder.ApplyUpdate(Update(null, new TextContent(" more"), new TextContent("")));

        Assert.Equal(["One more", "Two", "Three more"], builder.Items.Cast<AssistantMessageItem>().Select(i => i.Text));
        Assert.All(builder.Items.Cast<AssistantMessageItem>(), i => Assert.True(i.IsStreaming));
        Assert.Equal(5, changes);
    }

    [Fact]
    public void Ends_thinking_when_anything_else_arrives()
    {
        var builder = new ChatItemBuilder();
        builder.ApplyUpdate(Update("a", new TextReasoningContent("Hmm")));
        builder.ApplyUpdate(Update("a", new TextReasoningContent(", yes")));
        var reasoning = Assert.IsType<ReasoningItem>(Assert.Single(builder.Items));
        Assert.True(reasoning.IsStreaming);
        Assert.Equal("▸ " + ChatStrings.Thinking, reasoning.Header);

        builder.ApplyUpdate(Update("a", new TextContent("Answer")));

        Assert.Equal("Hmm, yes", reasoning.Text);
        Assert.False(reasoning.IsStreaming);
        Assert.Equal("▸ " + ChatStrings.Thoughts, reasoning.Header);
    }

    [Fact]
    public void Tracks_tool_calls_by_their_call_id()
    {
        var builder = new ChatItemBuilder();
        var call = new FunctionCallContent("c1", "lookup");

        builder.ApplyUpdate(Update("a", call));
        builder.ApplyUpdate(Update("a", call));
        builder.ApplyUpdate(Update("a", new ToolApprovalRequestContent("r1", call)));
        var tool = Assert.IsType<ToolCallItem>(Assert.Single(builder.Items));
        Assert.True(tool.IsAwaitingApproval);
        Assert.True(builder.HasPendingApprovals);

        builder.ApplyUpdate(Update("a", new ToolApprovalResponseContent("r1", false, call)));
        Assert.Equal(ToolCallStatus.Rejected, tool.Status);
        builder.ApplyUpdate(Update("a", new FunctionResultContent("c1", "rejected")));
        Assert.Equal(ToolCallStatus.Rejected, tool.Status);

        builder.ApplyUpdate(Update("a", new ToolApprovalResponseContent("r9", true, new FunctionCallContent("unknown", "x"))));
        builder.ApplyUpdate(Update("a", new FunctionResultContent("unknown", "x")));
        Assert.Single(builder.Items);
        Assert.False(builder.HasPendingApprovals);
    }

    [Fact]
    public void An_approved_call_runs_until_its_result()
    {
        var builder = new ChatItemBuilder();
        var call = new FunctionCallContent("c1", "lookup");
        builder.ApplyUpdate(Update("a", new ToolApprovalRequestContent("r1", call)));
        var tool = (ToolCallItem)builder.Items[0];

        builder.ApplyUpdate(Update("a", new ToolApprovalResponseContent("r1", true, call)));
        Assert.Equal(ToolCallStatus.Running, tool.Status);

        builder.ApplyUpdate(Update("a", new FunctionResultContent("c1", "ok")));
        Assert.Equal(ToolCallStatus.Completed, tool.Status);
    }

    [Fact]
    public void Ending_a_completed_response_keeps_its_approval_requests()
    {
        var builder = new ChatItemBuilder();
        builder.ApplyUpdate(Update("a", new ToolApprovalRequestContent("r1", new FunctionCallContent("c1", "lookup"))));

        builder.End(AiChatResponseStatus.Completed, null);

        Assert.True(((ToolCallItem)builder.Items[0]).IsAwaitingApproval);
    }

    [Fact]
    public void Shows_errors_that_arrive_in_the_stream()
    {
        var builder = new ChatItemBuilder();

        builder.ApplyUpdate(Update("a", new ErrorContent("Filtered."), new UsageContent()));

        var error = Assert.IsType<ErrorItem>(Assert.Single(builder.Items));
        Assert.Equal("Filtered.", error.Message);
        Assert.False(error.CanRetry);
    }

    [Fact]
    public void Items_raise_changes_only_when_values_change()
    {
        var reasoning = new ReasoningItem("x", null, isStreaming: false);
        var tool = new ToolCallItem(new FunctionCallContent("c", "f"));
        var error = new ErrorItem("e", canRetry: true);
        var answer = new AssistantMessageItem("a", null, isStreaming: true);
        var raised = new List<string?>();
        foreach (var item in new INotifyPropertyChanged[] { reasoning, tool, error, answer })
        {
            item.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        }

        reasoning.IsExpanded = false;
        tool.Status = ToolCallStatus.Running;
        error.CanRetry = true;
        answer.IsStreaming = true;
        Assert.Empty(raised);

        reasoning.IsExpanded = true;
        Assert.Equal("▾ " + ChatStrings.Thoughts, reasoning.Header);
        answer.IsStreaming = false;
        Assert.Equal(["IsExpanded", "Header", "IsStreaming", "IsComplete"], raised);
    }

    [Fact]
    public void Names_each_tool_status()
    {
        var expected = new Dictionary<ToolCallStatus, string>
        {
            [ToolCallStatus.AwaitingApproval] = ChatStrings.ToolAwaitingApproval,
            [ToolCallStatus.Running] = ChatStrings.ToolRunning,
            [ToolCallStatus.Completed] = ChatStrings.ToolCompleted,
            [ToolCallStatus.Failed] = ChatStrings.ToolFailed,
            [ToolCallStatus.Rejected] = ChatStrings.ToolRejected,
            [ToolCallStatus.Canceled] = ChatStrings.ToolCanceled,
        };

        foreach (var (status, text) in expected)
        {
            var tool = new ToolCallItem(new FunctionCallContent("c", "f")) { Status = status };
            Assert.Equal(text, tool.StatusText);
            Assert.False(tool.HasArguments);
        }
    }

    [Fact]
    public void Raises_ItemsChanged_through_the_session()
    {
        var session = new AiChatSession(new ScriptedChatClient(), dispatcher: null);
        var changes = 0;
        EventHandler handler = (_, _) => changes++;

        session.ItemsChanged += handler;
        session.Load([new ChatMessage(ChatRole.User, "Hi")]);
        session.ItemsChanged -= handler;
        session.Load([new ChatMessage(ChatRole.User, "Hi")]);

        Assert.Equal(1, changes);
    }

    [Fact]
    public void Stop_does_nothing_without_a_response()
    {
        var session = new AiChatSession(new ScriptedChatClient());

        session.Stop();

        Assert.False(session.IsBusy);
    }

    [Fact]
    public async Task A_session_without_a_dispatcher_runs_where_it_is_called()
    {
        var session = new AiChatSession(new ScriptedChatClient().RespondText("Hello"), dispatcher: null);

        await session.SendAsync("Hi");

        Assert.Equal(2, session.Items.Count);
    }
}
