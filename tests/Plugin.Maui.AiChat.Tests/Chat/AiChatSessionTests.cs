using System.ComponentModel;
using Microsoft.Extensions.AI;
using Plugin.Maui.AiChat.Chat;
using Plugin.Maui.AiChat.Tests.TestHelpers;

namespace Plugin.Maui.AiChat.Tests.Chat;

public class AiChatSessionTests
{
    private static ChatResponseUpdate Text(string text, string? messageId = "m") => new(ChatRole.Assistant, text) { MessageId = messageId };

    [Fact]
    public async Task Streams_the_answer_into_the_history_and_the_rows()
    {
        var client = new ScriptedChatClient().RespondText("Hel", "lo");
        var session = new AiChatSession(client);
        var busy = new List<bool>();
        session.PropertyChanged += (_, e) => busy.Add(session.IsBusy);
        AiChatResponseEventArgs? completed = null;
        session.ResponseCompleted += (_, e) => completed = e;

        await session.SendAsync("Hi");

        Assert.Equal([true, false], busy);
        Assert.Equal(["Hi", "Hello"], session.Messages.Select(m => m.Text));
        Assert.Equal([ChatRole.User, ChatRole.Assistant], session.Messages.Select(m => m.Role));
        Assert.Equal("Hi", Assert.IsType<UserMessageItem>(session.Items[0]).Text);
        var answer = Assert.IsType<AssistantMessageItem>(session.Items[1]);
        Assert.Equal("Hello", answer.Text);
        Assert.False(answer.IsStreaming);
        Assert.True(answer.IsComplete);
        Assert.NotNull(completed);
        Assert.Equal(AiChatResponseStatus.Completed, completed.Status);
        Assert.Equal("Hello", completed.Response.Text);
        Assert.Null(completed.Exception);
        Assert.Same(client, session.ChatClient);
    }

    [Fact]
    public async Task Shows_the_answer_while_it_streams()
    {
        var gate = new TaskCompletionSource();
        var client = new ScriptedChatClient().Respond(token =>
            ScriptedChatClient.StreamThenWait([Text("Hel")], gate.Task, [Text("lo")], honorCancellation: true, token));
        var session = new AiChatSession(client);

        var sending = session.SendAsync("Hi");

        Assert.True(session.IsBusy);
        var answer = Assert.IsType<AssistantMessageItem>(session.Items[1]);
        Assert.Equal("Hel", answer.Text);
        Assert.True(answer.IsStreaming);
        Assert.Single(session.Messages);

        gate.SetResult();
        await sending;

        Assert.Equal("Hello", answer.Text);
        Assert.False(answer.IsStreaming);
    }

    [Fact]
    public async Task Sends_the_options_and_the_whole_history()
    {
        var client = new ScriptedChatClient().RespondText("One").RespondText("Two");
        var options = new ChatOptions { Instructions = "Be brief." };
        var session = new AiChatSession(client) { Options = options };

        await session.SendAsync("First");
        await session.SendAsync("Second");

        Assert.Same(options, session.Options);
        Assert.All(client.RequestOptions, o => Assert.Same(options, o));
        Assert.Equal(["First", "One", "Second"], client.Requests[1].Select(m => m.Text));
    }

    [Fact]
    public async Task Rejects_empty_text_and_a_second_message_while_busy()
    {
        var gate = new TaskCompletionSource();
        var client = new ScriptedChatClient().Respond(token =>
            ScriptedChatClient.StreamThenWait([Text("…")], gate.Task, [], honorCancellation: true, token));
        var session = new AiChatSession(client);

        await Assert.ThrowsAsync<ArgumentException>(() => session.SendAsync(" "));
        await Assert.ThrowsAsync<ArgumentNullException>(() => session.SendAsync(null!));
        var sending = session.SendAsync("Hi");
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.SendAsync("Again"));

        gate.SetResult();
        await sending;
    }

    [Fact]
    public async Task Stop_keeps_what_arrived()
    {
        var client = new ScriptedChatClient().Respond(token =>
            ScriptedChatClient.StreamThenWait([Text("Partial")], new TaskCompletionSource().Task, [], honorCancellation: true, token));
        var session = new AiChatSession(client);
        AiChatResponseEventArgs? completed = null;
        session.ResponseCompleted += (_, e) => completed = e;

        var sending = session.SendAsync("Hi");
        session.Stop();
        await sending;

        Assert.False(session.IsBusy);
        Assert.Equal(AiChatResponseStatus.Stopped, completed!.Status);
        Assert.Equal("Partial", completed.Response.Text);
        Assert.Equal(["Hi", "Partial"], session.Messages.Select(m => m.Text));
        Assert.False(((AssistantMessageItem)session.Items[1]).IsStreaming);
    }

    [Fact]
    public async Task The_callers_token_stops_like_Stop()
    {
        using var cancel = new CancellationTokenSource();
        var client = new ScriptedChatClient().Respond(token =>
            ScriptedChatClient.StreamThenWait([Text("Partial")], new TaskCompletionSource().Task, [], honorCancellation: true, token));
        var session = new AiChatSession(client);
        AiChatResponseEventArgs? completed = null;
        session.ResponseCompleted += (_, e) => completed = e;

        var sending = session.SendAsync("Hi", cancel.Token);
        await cancel.CancelAsync();
        await sending;

        Assert.Equal(AiChatResponseStatus.Stopped, completed!.Status);
    }

    [Fact]
    public async Task A_failure_shows_in_the_chat_and_Retry_runs_the_request_again()
    {
        var failure = new HttpRequestException("The service is down.");
        var client = new ScriptedChatClient()
            .Respond(token => FailAfter(Text("Half an ans"), failure, token))
            .RespondText("A whole answer");
        var session = new AiChatSession(client);
        AiChatResponseEventArgs? completed = null;
        session.ResponseCompleted += (_, e) => completed = e;

        await session.SendAsync("Hi");

        Assert.Equal(AiChatResponseStatus.Failed, completed!.Status);
        Assert.Same(failure, completed.Exception);
        var error = Assert.IsType<ErrorItem>(session.Items[^1]);
        Assert.Equal("The service is down.", error.Message);
        Assert.True(error.CanRetry);

        await session.RetryAsync();

        Assert.Equal(AiChatResponseStatus.Completed, completed.Status);
        Assert.Equal(["Hi", "A whole answer"], session.Messages.Select(m => m.Text));
        Assert.Equal(2, session.Items.Count);
        Assert.Equal(client.Requests[0].Select(m => m.Text), client.Requests[1].Select(m => m.Text));
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> FailAfter(
        ChatResponseUpdate update, Exception failure, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token = default)
    {
        yield return update;
        await Task.Yield();
        token.ThrowIfCancellationRequested();
        throw failure;
    }

    [Fact]
    public async Task A_client_that_throws_at_once_also_fails_softly()
    {
        var client = new ScriptedChatClient().Fail(new InvalidOperationException("No model."));
        var session = new AiChatSession(client);

        await session.SendAsync("Hi");

        Assert.Equal("No model.", Assert.IsType<ErrorItem>(session.Items[^1]).Message);
        Assert.Single(session.Messages);
    }

    [Fact]
    public async Task Retry_does_nothing_before_a_response_or_while_busy_and_a_new_message_retires_old_errors()
    {
        var gate = new TaskCompletionSource();
        var client = new ScriptedChatClient()
            .Fail(new InvalidOperationException("First failure."))
            .Respond(token => ScriptedChatClient.StreamThenWait([Text("…")], gate.Task, [], honorCancellation: true, token));
        var session = new AiChatSession(client);

        await session.RetryAsync();
        Assert.Empty(client.Requests);

        await session.SendAsync("One");
        var error = (ErrorItem)session.Items[^1];
        var sending = session.SendAsync("Two");
        Assert.False(error.CanRetry);

        await session.RetryAsync();
        Assert.Equal(2, client.Requests.Count);

        gate.SetResult();
        await sending;
    }

    [Fact]
    public async Task Clear_cuts_off_the_response_and_starts_over()
    {
        var gate = new TaskCompletionSource();
        var client = new ScriptedChatClient()
            .Respond(token => ScriptedChatClient.StreamThenWait([Text("Old")], gate.Task, [], honorCancellation: true, token))
            .RespondText("New");
        var session = new AiChatSession(client);
        var events = 0;
        session.ResponseCompleted += (_, _) => events++;

        var sending = session.SendAsync("Hi");
        session.Clear();
        await sending;

        Assert.False(session.IsBusy);
        Assert.Empty(session.Messages);
        Assert.Empty(session.Items);
        Assert.Equal(0, events);

        await session.SendAsync("Again");
        Assert.Equal(["Again", "New"], session.Messages.Select(m => m.Text));
    }

    [Fact]
    public async Task A_client_that_ignores_cancellation_cannot_write_into_a_cleared_conversation()
    {
        var gate = new TaskCompletionSource();
        var client = new ScriptedChatClient().Respond(token =>
            ScriptedChatClient.StreamThenWait([Text("Old")], gate.Task, [Text(" and more")], honorCancellation: false, token));
        var session = new AiChatSession(client);

        var sending = session.SendAsync("Hi");
        session.Clear();
        gate.SetResult();
        await sending;

        Assert.Empty(session.Items);
        Assert.Empty(session.Messages);
    }

    [Fact]
    public async Task A_stateful_service_gets_its_conversation_id_and_only_the_new_messages()
    {
        var client = new ScriptedChatClient()
            .Respond(new ChatResponseUpdate(ChatRole.Assistant, "One") { ConversationId = "resp-1" }, new ChatResponseUpdate(ChatRole.Assistant, ""))
            .Respond(new ChatResponseUpdate(ChatRole.Assistant, "Two") { ConversationId = "resp-2" })
            .RespondText("Three");
        var options = new ChatOptions { Instructions = "x" };
        var session = new AiChatSession(client) { Options = options };

        await session.SendAsync("First");
        await session.SendAsync("Second");

        Assert.Null(client.RequestOptions[0]!.ConversationId);
        Assert.Equal("resp-1", client.RequestOptions[1]!.ConversationId);
        Assert.Equal("x", client.RequestOptions[1]!.Instructions);
        Assert.Null(options.ConversationId);
        Assert.Equal(["Second"], client.Requests[1].Select(m => m.Text));

        session.Options = null;
        await session.SendAsync("Third");
        Assert.Equal("resp-2", client.RequestOptions[2]!.ConversationId);
        Assert.Equal(["Third"], client.Requests[2].Select(m => m.Text));
        Assert.Equal(6, session.Messages.Count);
    }

    [Fact]
    public async Task A_response_without_an_id_is_sent_again_with_the_next_message()
    {
        var client = new ScriptedChatClient()
            .Respond(new ChatResponseUpdate(ChatRole.Assistant, "One") { ConversationId = "resp-1" })
            .Fail(new HttpRequestException("Dropped."))
            .RespondText("Three");
        var session = new AiChatSession(client);

        await session.SendAsync("First");
        await session.SendAsync("Second");
        await session.SendAsync("Third");

        Assert.Equal(["Second", "Third"], client.Requests[2].Select(m => m.Text));
    }

    [Fact]
    public async Task Clear_forgets_the_conversation_id()
    {
        var client = new ScriptedChatClient()
            .Respond(new ChatResponseUpdate(ChatRole.Assistant, "One") { ConversationId = "resp-1" })
            .RespondText("Two");
        var session = new AiChatSession(client);

        await session.SendAsync("First");
        session.Clear();
        await session.SendAsync("Again");

        Assert.Null(client.RequestOptions[1]);
        Assert.Equal(["Again"], client.Requests[1].Select(m => m.Text));
    }

    [Fact]
    public async Task A_cut_off_response_keeps_finished_tool_calls_and_drops_the_others()
    {
        var finished = new FunctionCallContent("done", "lookup");
        var unfinished = new FunctionCallContent("open", "lookup");
        var client = new ScriptedChatClient().Respond(token => ScriptedChatClient.StreamThenWait(
            [
                new ChatResponseUpdate(ChatRole.Assistant, [finished]) { MessageId = "a" },
                new ChatResponseUpdate(ChatRole.Tool, [new FunctionResultContent("done", "42")]) { MessageId = "t" },
                new ChatResponseUpdate(ChatRole.Assistant, [unfinished, new TextContent("Let me")]) { MessageId = "b" },
            ],
            new TaskCompletionSource().Task, [], honorCancellation: true, token));
        var session = new AiChatSession(client);

        var sending = session.SendAsync("Hi");
        session.Stop();
        await sending;

        var contents = session.Messages.SelectMany(m => m.Contents).ToList();
        Assert.Contains(finished, contents);
        Assert.DoesNotContain(unfinished, contents);
        Assert.Equal("Let me", session.Messages[^1].Text);
        Assert.Equal([ToolCallStatus.Completed, ToolCallStatus.Canceled], session.Items.OfType<ToolCallItem>().Select(t => t.Status));
    }

    [Fact]
    public async Task A_cut_off_response_drops_a_message_left_with_only_an_unfinished_call()
    {
        var client = new ScriptedChatClient().Respond(token => ScriptedChatClient.StreamThenWait(
            [new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent("open", "lookup")])],
            new TaskCompletionSource().Task, [], honorCancellation: true, token));
        var session = new AiChatSession(client);

        var sending = session.SendAsync("Hi");
        session.Stop();
        await sending;

        Assert.Equal(["Hi"], session.Messages.Select(m => m.Text));
    }

    [Fact]
    public async Task Continues_on_the_dispatcher_when_called_from_another_thread()
    {
        var dispatcher = new QueuedDispatcher { IsDispatchRequired = true };
        var session = new AiChatSession(new ScriptedChatClient().RespondText("Hello"), dispatcher);

        var sending = session.SendAsync("Hi");
        Assert.Empty(session.Items);

        dispatcher.IsDispatchRequired = false;
        dispatcher.RunAll();
        await sending;

        Assert.Equal(2, session.Items.Count);
    }

    [Fact]
    public void Validates_its_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => new AiChatSession(null!));
        var session = new AiChatSession(new ScriptedChatClient());
        Assert.Throws<ArgumentNullException>(() => session.Load(null!));
        Assert.Throws<ArgumentException>(() => session.Load([new ChatMessage(ChatRole.User, "a"), null!]));
        Assert.Throws<ArgumentNullException>(() => new AiChatResponseEventArgs(null!, AiChatResponseStatus.Completed, null));
    }

    [Fact]
    public void Raises_IsBusy_changes_only_when_it_changes()
    {
        var session = new AiChatSession(new ScriptedChatClient());
        var changes = new List<string?>();
        ((INotifyPropertyChanged)session).PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        session.Clear();

        Assert.Empty(changes);
    }
}
