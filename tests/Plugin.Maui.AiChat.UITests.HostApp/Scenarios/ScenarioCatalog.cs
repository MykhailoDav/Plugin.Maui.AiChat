using Microsoft.Extensions.AI;

namespace Plugin.Maui.AiChat.UITests.HostApp.Scenarios;

/// <summary>A scenario of the host app: a name the UI tests open it by, and the page it shows.</summary>
public sealed record Scenario(string Name, Func<Page> Create);

/// <summary>Every scenario of the host app. README.md in the UI test project lists what each one has.</summary>
public static class ScenarioCatalog
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("Streaming", () => Page("Streaming", new ScriptedChatClient(_ => ScriptedChatClient.Text(Fixtures.LongAnswer)))),
        new("Short", () => Page("Short", new ScriptedChatClient(_ => ScriptedChatClient.Text(Fixtures.ShortAnswer)))),
        new("Approval", Approval),
        new("Failure", () => Page("Failure", new ScriptedChatClient(
            _ => [.. ScriptedChatClient.Text("Half an ans"), ScriptedChatClient.Failure()],
            _ => ScriptedChatClient.Text(Fixtures.ShortAnswer)))),
        new("Restored", () => Restored("Restored", AppTheme.Light)),
        new("RestoredDark", () => Restored("RestoredDark", AppTheme.Dark)),
    ];

    private static ScenarioPage Page(string name, ScriptedChatClient client)
        => new(name, new AiChatSession(client), () => client.Requests);

    // The model asks for delete_file, which needs approval; the answer after it says what the tool returned.
    private static ScenarioPage Approval()
    {
        var model = new ScriptedChatClient(
            _ => [new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent("c1", "delete_file", new Dictionary<string, object?> { ["path"] = "notes.txt" })])],
            messages => ScriptedChatClient.Text(messages[^1].Contents.OfType<FunctionResultContent>().First().Result?.ToString() == "Deleted"
                ? "The file is deleted."
                : "I left the file alone."));
        var deleteFile = new ApprovalRequiredAIFunction(AIFunctionFactory.Create((string path) => "Deleted", "delete_file"));
        var session = new AiChatSession(model.AsBuilder().UseFunctionInvocation().Build())
        {
            Options = new ChatOptions { Tools = [deleteFile] },
        };
        return new ScenarioPage("Approval", session, () => model.Requests);
    }

    private static ScenarioPage Restored(string name, AppTheme theme)
    {
        var client = new ScriptedChatClient(_ => ScriptedChatClient.Text(Fixtures.ShortAnswer));
        var session = new AiChatSession(client);
        session.Load(Fixtures.Conversation());
        return new ScenarioPage(name, session, () => client.Requests, theme);
    }
}
