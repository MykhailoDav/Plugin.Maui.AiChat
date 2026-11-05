using Microsoft.Extensions.AI;
using Plugin.Maui.AiChat.Chat;
using Plugin.Maui.AiChat.Tests.TestHelpers;

namespace Plugin.Maui.AiChat.Tests.Chat;

/// <summary>Tool approval against the real <see cref="FunctionInvokingChatClient"/> protocol.</summary>
public class ToolApprovalTests
{
    private int _weatherCalls;

    private (AiChatSession Session, ScriptedChatClient Model) Create(params FunctionCallContent[] calls)
    {
        // A restored conversation (no calls) starts at the answer that follows the approval.
        var model = new ScriptedChatClient();
        if (calls.Length > 0)
        {
            model.Respond(new ChatResponseUpdate(ChatRole.Assistant, [.. calls]) { MessageId = "calls" });
        }

        model.RespondText("It is sunny.").RespondText("Another answer.");
        var weather = new ApprovalRequiredAIFunction(AIFunctionFactory.Create(
            (string city) =>
            {
                _weatherCalls++;
                return "Sunny in " + city;
            },
            "get_weather"));
        var session = new AiChatSession(model.AsBuilder().UseFunctionInvocation().Build())
        {
            Options = new ChatOptions { Tools = [weather] },
        };
        return (session, model);
    }

    private static FunctionCallContent Call(string id, string city)
        => new(id, "get_weather", new Dictionary<string, object?> { ["city"] = city });

    [Fact]
    public async Task Asks_before_running_a_tool_that_requires_approval()
    {
        var (session, model) = Create(Call("c1", "Kyiv"));

        await session.SendAsync("Weather in Kyiv?");

        var tool = Assert.IsType<ToolCallItem>(session.Items[1]);
        Assert.Equal("get_weather", tool.Name);
        Assert.Equal("city: \"Kyiv\"", tool.Arguments);
        Assert.True(tool.HasArguments);
        Assert.True(tool.IsAwaitingApproval);
        Assert.Equal(-1, tool.ArgumentLines);
        Assert.Equal(0, _weatherCalls);
        Assert.Single(model.Requests);
        Assert.Contains(session.Messages[^1].Contents, c => c is ToolApprovalRequestContent);
    }

    [Fact]
    public async Task Approving_runs_the_tool_and_continues_the_answer()
    {
        var (session, model) = Create(Call("c1", "Kyiv"));
        await session.SendAsync("Weather in Kyiv?");
        var tool = (ToolCallItem)session.Items[1];

        await session.RespondAsync(tool, approved: true);

        Assert.Equal(1, _weatherCalls);
        Assert.Equal(ToolCallStatus.Completed, tool.Status);
        Assert.Equal(2, tool.ArgumentLines);
        Assert.Equal("It is sunny.", Assert.IsType<AssistantMessageItem>(session.Items[^1]).Text);
        Assert.Contains(model.Requests[1].SelectMany(m => m.Contents),
            c => c is FunctionResultContent { CallId: "c1" } result && result.Result?.ToString() == "Sunny in Kyiv");
        Assert.Contains(session.Messages.SelectMany(m => m.Contents), c => c is ToolApprovalResponseContent { Approved: true });
    }

    [Fact]
    public async Task Rejecting_tells_the_model_without_running_the_tool()
    {
        var (session, model) = Create(Call("c1", "Kyiv"));
        await session.SendAsync("Weather in Kyiv?");
        var tool = (ToolCallItem)session.Items[1];

        await session.RespondAsync(tool, approved: false);

        Assert.Equal(0, _weatherCalls);
        Assert.Equal(ToolCallStatus.Rejected, tool.Status);
        Assert.Equal(2, model.Requests.Count);
        Assert.Equal("It is sunny.", ((AssistantMessageItem)session.Items[^1]).Text);
    }

    [Fact]
    public async Task Waits_for_an_answer_to_every_request_of_the_response()
    {
        var (session, model) = Create(Call("c1", "Kyiv"), Call("c2", "Lviv"));
        await session.SendAsync("Weather?");
        var tools = session.Items.OfType<ToolCallItem>().ToList();

        await session.RespondAsync(tools[0], approved: true);
        Assert.Single(model.Requests);
        Assert.Equal(ToolCallStatus.Running, tools[0].Status);

        await session.RespondAsync(tools[1], approved: true);
        Assert.Equal(2, model.Requests.Count);
        Assert.Equal(2, _weatherCalls);
    }

    [Fact]
    public async Task Writing_instead_of_answering_rejects_the_requests()
    {
        var (session, model) = Create(Call("c1", "Kyiv"));
        await session.SendAsync("Weather?");
        var tool = (ToolCallItem)session.Items[1];

        await session.SendAsync("Never mind, tell me a joke.");

        Assert.Equal(ToolCallStatus.Rejected, tool.Status);
        Assert.Equal(0, _weatherCalls);
        var request = model.Requests[1];
        Assert.Contains(request.SelectMany(m => m.Contents), c => c is FunctionResultContent { CallId: "c1" });
        Assert.Equal("Never mind, tell me a joke.", request[^1].Text);
        Assert.IsType<UserMessageItem>(session.Items[2]);
    }

    [Fact]
    public async Task Ignores_answers_that_cannot_be_given()
    {
        var gate = new TaskCompletionSource();
        var model = new ScriptedChatClient().Respond(token => ScriptedChatClient.StreamThenWait(
            [new ChatResponseUpdate(ChatRole.Assistant, [new ToolApprovalRequestContent("r1", Call("c1", "Kyiv"))])],
            gate.Task, [], honorCancellation: true, token));
        var session = new AiChatSession(model);

        var sending = session.SendAsync("Weather?");
        var tool = (ToolCallItem)session.Items[1];
        await session.RespondAsync(tool, approved: true);
        Assert.True(tool.IsAwaitingApproval);

        gate.SetResult();
        await sending;
        await session.RespondAsync(tool, approved: false);
        await session.RespondAsync(tool, approved: true);

        Assert.Equal(ToolCallStatus.Rejected, tool.Status);
    }

    [Fact]
    public async Task A_request_cut_off_by_Stop_can_no_longer_be_answered()
    {
        var model = new ScriptedChatClient().Respond(token => ScriptedChatClient.StreamThenWait(
            [new ChatResponseUpdate(ChatRole.Assistant, [new ToolApprovalRequestContent("r1", Call("c1", "Kyiv")), new TextContent("Checking")])],
            new TaskCompletionSource().Task, [], honorCancellation: true, token));
        var session = new AiChatSession(model);

        var sending = session.SendAsync("Weather?");
        session.Stop();
        await sending;

        var tool = (ToolCallItem)session.Items[1];
        Assert.Equal(ToolCallStatus.Canceled, tool.Status);
        Assert.DoesNotContain(session.Messages.SelectMany(m => m.Contents), c => c is ToolApprovalRequestContent);
    }

    [Fact]
    public async Task A_restored_request_can_be_answered()
    {
        var (session, model) = Create(Call("c1", "Kyiv"));
        await session.SendAsync("Weather in Kyiv?");
        var saved = session.Messages.ToList();

        var (restored, _) = Create();
        restored.Load(saved);
        var tool = Assert.IsType<ToolCallItem>(restored.Items[1]);
        Assert.True(tool.IsAwaitingApproval);

        await restored.RespondAsync(tool, approved: true);

        Assert.Equal(ToolCallStatus.Completed, tool.Status);
        Assert.Equal(1, _weatherCalls);
        Assert.Single(model.Requests);
    }

    [Fact]
    public async Task A_tool_that_throws_shows_as_failed_and_the_model_hears_of_it()
    {
        var model = new ScriptedChatClient()
            .Respond(new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent("c1", "broken")]))
            .RespondText("The tool failed, sorry.");
        var broken = AIFunctionFactory.Create(string () => throw new InvalidOperationException("Disk full."), "broken");
        var session = new AiChatSession(model.AsBuilder().UseFunctionInvocation().Build())
        {
            Options = new ChatOptions { Tools = [broken] },
        };

        await session.SendAsync("Run it");

        Assert.Equal(ToolCallStatus.Failed, Assert.IsType<ToolCallItem>(session.Items[1]).Status);
        Assert.Contains(model.Requests[1].SelectMany(m => m.Contents), c => c is FunctionResultContent { CallId: "c1" });
        Assert.Equal("The tool failed, sorry.", ((AssistantMessageItem)session.Items[^1]).Text);
    }
}
