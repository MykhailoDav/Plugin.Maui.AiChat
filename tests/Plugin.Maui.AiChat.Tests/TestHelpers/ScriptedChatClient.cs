using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Plugin.Maui.AiChat.Tests.TestHelpers;

/// <summary>A chat client that answers each request with the next scripted stream and records what it was asked.</summary>
internal sealed class ScriptedChatClient : IChatClient
{
    private readonly Queue<Func<CancellationToken, IAsyncEnumerable<ChatResponseUpdate>>> _responses = new();

    public List<List<ChatMessage>> Requests { get; } = [];

    public List<ChatOptions?> RequestOptions { get; } = [];

    public ScriptedChatClient Respond(params ChatResponseUpdate[] updates)
    {
        _responses.Enqueue(token => Stream(updates, token));
        return this;
    }

    public ScriptedChatClient RespondText(params string[] chunks)
        => Respond([.. chunks.Select(chunk => new ChatResponseUpdate(ChatRole.Assistant, chunk) { MessageId = "m" })]);

    public ScriptedChatClient Respond(Func<CancellationToken, IAsyncEnumerable<ChatResponseUpdate>> stream)
    {
        _responses.Enqueue(stream);
        return this;
    }

    public ScriptedChatClient Fail(Exception exception)
    {
        _responses.Enqueue(_ => throw exception);
        return this;
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Requests.Add([.. messages]);
        RequestOptions.Add(options);
        return _responses.Dequeue()(cancellationToken);
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => GetStreamingResponseAsync(messages, options, cancellationToken).ToChatResponseAsync(cancellationToken);

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }

    public static async IAsyncEnumerable<ChatResponseUpdate> Stream(
        IEnumerable<ChatResponseUpdate> updates, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var update in updates)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return update;
        }
    }

    /// <summary>Yields the updates, then waits for the gate; with <paramref name="honorCancellation"/> off it ignores Stop.</summary>
    public static async IAsyncEnumerable<ChatResponseUpdate> StreamThenWait(
        ChatResponseUpdate[] first, Task gate, ChatResponseUpdate[] after, bool honorCancellation,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var update in first)
        {
            yield return update;
        }

        await (honorCancellation ? gate.WaitAsync(cancellationToken) : gate);
        foreach (var update in after)
        {
            yield return update;
        }
    }
}
