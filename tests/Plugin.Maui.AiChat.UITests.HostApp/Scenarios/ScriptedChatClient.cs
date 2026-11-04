using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Plugin.Maui.AiChat.UITests.HostApp.Scenarios;

/// <summary>
/// A chat client that answers the n-th request with the n-th script (the last one repeats), streaming each text in
/// fixed pieces at a fixed pace, so every run of a test sees the same chat.
/// </summary>
public sealed class ScriptedChatClient(params Func<IList<ChatMessage>, IReadOnlyList<ChatResponseUpdate>>[] scripts) : IChatClient
{
    public static readonly TimeSpan Pace = TimeSpan.FromMilliseconds(30);

    public int Requests { get; private set; }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var script = scripts[Math.Min(Requests, scripts.Length - 1)];
        Requests++;
        foreach (var update in script([.. messages]))
        {
            await Task.Delay(Pace, cancellationToken);
            if (update.AdditionalProperties?.ContainsKey("fail") == true)
                throw new HttpRequestException("The connection dropped.");
            yield return update;
        }
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => await GetStreamingResponseAsync(messages, options, cancellationToken).ToChatResponseAsync(cancellationToken);

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }

    /// <summary>The text as a model streams it: pieces of four characters.</summary>
    public static IReadOnlyList<ChatResponseUpdate> Text(string text)
    {
        var updates = new List<ChatResponseUpdate>();
        for (var i = 0; i < text.Length; i += 4)
            updates.Add(new ChatResponseUpdate(ChatRole.Assistant, text.Substring(i, Math.Min(4, text.Length - i))) { MessageId = "answer" });
        return updates;
    }

    /// <summary>An update that makes the stream throw when it is reached.</summary>
    public static ChatResponseUpdate Failure() => new() { AdditionalProperties = new() { ["fail"] = true } };
}
