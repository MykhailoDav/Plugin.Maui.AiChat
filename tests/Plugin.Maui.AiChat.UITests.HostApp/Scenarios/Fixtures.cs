using Microsoft.Extensions.AI;

namespace Plugin.Maui.AiChat.UITests.HostApp.Scenarios;

/// <summary>The fixed texts and conversations of the scenarios.</summary>
public static class Fixtures
{
    /// <summary>An answer long enough to scroll, with every kind of block the view renders.</summary>
    public const string LongAnswer = """
        ## Streaming answer

        This answer arrives **four characters** at a time, with *emphasis*, `inline code` and a [link](https://example.com).

        1. First step
        2. Second step
           - a nested point
        3. Third step

        > A quote, to break up the text.

        ```csharp
        var session = new AiChatSession(client);
        await session.SendAsync("Hello");
        ```

        | Column | Value |
        |:--|--:|
        | One | 1 |
        | Two | 2 |

        Paragraph after the table, so the answer is long enough to need scrolling on a phone. It keeps going for a while, with a few more words in it, and then it ends here.

        The last line of the answer.
        """;

    public const string ShortAnswer = "Short answer.";

    /// <summary>A finished conversation with a reasoning section, a tool card, Markdown and a code block.</summary>
    public static IReadOnlyList<ChatMessage> Conversation() =>
    [
        new(ChatRole.User, "What's the weather in Kyiv, and how do I print it in C#?"),
        new(ChatRole.Assistant, [new TextReasoningContent("The user wants the weather and a code sample.")]),
        new(ChatRole.Assistant, [new FunctionCallContent("c1", "get_weather", new Dictionary<string, object?> { ["city"] = "Kyiv" })]),
        new(ChatRole.Tool, [new FunctionResultContent("c1", "12 °C, clear")]),
        new(ChatRole.Assistant, """
            It is **12 °C** and clear in Kyiv. To print it:

            ```csharp
            Console.WriteLine("12 °C");
            ```

            | City | °C |
            |:--|--:|
            | Kyiv | 12 |
            """),
        new(ChatRole.User, "Thanks!"),
        new(ChatRole.Assistant, "You're welcome. *Anything else?*"),
    ];
}
