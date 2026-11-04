namespace Plugin.Maui.AiChat.Sample.Services;

/// <summary>
/// The texts of the demo model. A paragraph is one line: like chat apps, the view shows a single newline as a line
/// break.
/// </summary>
internal static class DemoAnswers
{
    internal const string Welcome = """
        Hi! I'm the **demo model** of this sample. I run offline and stream canned answers, so you can see how the chat renders a real one. Try one of these:

        - *Show me some Markdown*: headings, lists, code, a table.
        - *What's the weather in Lviv?*: a tool call that asks for your **approval** first.
        - *What time is it?*: a tool call that runs right away.
        - *Think it through: is 1001 prime?*: reasoning you can open.
        - *Make it fail*: an error with **Retry**.

        To talk to a real model, open the **Model** tab and connect any OpenAI-compatible endpoint, such as OpenAI or a local [Ollama](https://ollama.com).
        """;

    internal const string Markdown = """
        ## Markdown, streamed

        Text arrives a few characters at a time, yet **bold**, *italic*, `inline code` and [links](https://github.com/MykhailoDav) never flash as raw markup.

        ### A list
        1. Paragraphs, headings and quotes
        2. Bullet, numbered and task lists
           - nested, too
        3. Tables and code with a **Copy** button

        - [x] Streams without flicker
        - [ ] Writes your app for you

        > Unchanged blocks keep their views; only the last one grows.

        ```csharp
        var session = new AiChatSession(chatClient);
        await session.SendAsync("Hello!");
        ```

        | Platform | Status |
        |:--|:-:|
        | Android | ✅ |
        | iOS | ✅ |
        | Mac Catalyst | ✅ |
        | Windows | ✅ |

        ---
        That's it. ~~Nothing~~ Everything above came in as tokens.
        """;

    internal const string Reasoning = """
        1001 is odd, so not 2. Its digit sum is 2, so not 3. It ends in 1, so not 5. Try 7: 7 × 143 = 1001, so it is composite. And 143 = 11 × 13.
        """;

    internal const string Prime = """
        **No**, 1001 is not prime: it is **7 × 11 × 13**.

        Tap *Thoughts* above to see how I got there.
        """;

    internal const string AfterTool = """
        Here's what the tool returned: **{0}**.

        Tool calls appear as cards with their status, and the ones that need your approval wait for it.
        """;

    internal const string Rejected = """
        Okay, I won't run that tool. Ask me something else, or try again and tap **Approve**.
        """;

    internal const string BeforeFailure = """
        Sure, here comes an answer that will not make it to the end, because the
        """;

    internal const string AfterRetry = """
        This time it worked. **Retry** sent the same request again, and the half answer from before is gone from the history, so the model never sees it.
        """;
}
