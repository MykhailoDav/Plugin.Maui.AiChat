using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Plugin.Maui.AiChat.Sample.Services;

/// <summary>
/// A chat client that needs no model and no key. It streams canned answers that show what the chat view renders:
/// Markdown, tool calls (one needs approval), reasoning, and a failure with Retry. It goes through the same
/// <see cref="FunctionInvokingChatClient"/> pipeline as a real model.
/// </summary>
public sealed class DemoChatClient : IChatClient
{
    private static readonly TimeSpan Pace = TimeSpan.FromMilliseconds(18);
    private readonly HashSet<string> _failedOnce = [];

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var last = messages.Last();
        var messageId = Guid.NewGuid().ToString("N");

        if (last.Contents.OfType<FunctionResultContent>().FirstOrDefault() is { } result)
        {
            await foreach (var update in Stream(AfterTool(result), messageId, cancellationToken))
            {
                yield return update;
            }

            yield break;
        }

        var question = last.Text;
        var lower = question.ToLowerInvariant();
        if (lower.Contains("weather", StringComparison.Ordinal))
        {
            await Task.Delay(300, cancellationToken);
            yield return Call("get_weather", new Dictionary<string, object?> { ["city"] = CityIn(question) }, messageId);
        }
        else if (lower.Contains("time", StringComparison.Ordinal))
        {
            await Task.Delay(300, cancellationToken);
            yield return Call("get_time", null, messageId);
        }
        else if (lower.Contains("think", StringComparison.Ordinal) || lower.Contains("prime", StringComparison.Ordinal))
        {
            foreach (var chunk in Chunks(DemoAnswers.Reasoning))
            {
                await Task.Delay(Pace, cancellationToken);
                yield return new ChatResponseUpdate(ChatRole.Assistant, [new TextReasoningContent(chunk)]) { MessageId = messageId };
            }

            await foreach (var update in Stream(DemoAnswers.Prime, messageId, cancellationToken))
            {
                yield return update;
            }
        }
        else if (lower.Contains("fail", StringComparison.Ordinal) && _failedOnce.Add(question))
        {
            await foreach (var update in Stream(DemoAnswers.BeforeFailure, messageId, cancellationToken))
            {
                yield return update;
            }

            throw new HttpRequestException("The demo model dropped the connection, as real ones sometimes do. Tap Retry.");
        }
        else
        {
            var answer = lower.Contains("fail", StringComparison.Ordinal) ? DemoAnswers.AfterRetry
                : lower.Contains("markdown", StringComparison.Ordinal) ? DemoAnswers.Markdown
                : DemoAnswers.Welcome;
            await foreach (var update in Stream(answer, messageId, cancellationToken))
            {
                yield return update;
            }
        }
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => await GetStreamingResponseAsync(messages, options, cancellationToken).ToChatResponseAsync(cancellationToken);

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }

    private static ChatResponseUpdate Call(string name, IDictionary<string, object?>? arguments, string messageId)
        => new(ChatRole.Assistant, [new FunctionCallContent("call_" + Guid.NewGuid().ToString("N")[..8], name, arguments)]) { MessageId = messageId };

    private static string AfterTool(FunctionResultContent result)
    {
        var value = result.Result?.ToString() ?? "";
        return value.Contains("reject", StringComparison.OrdinalIgnoreCase) || value.Contains("denied", StringComparison.OrdinalIgnoreCase)
            ? DemoAnswers.Rejected
            : DemoAnswers.AfterTool.Replace("{0}", value, StringComparison.Ordinal);
    }

    private static string CityIn(string question)
    {
        var at = question.LastIndexOf(" in ", StringComparison.OrdinalIgnoreCase);
        return at < 0 ? "Kyiv" : question[(at + 4)..].Trim(' ', '?', '.', '!');
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> Stream(
        string text, string messageId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var chunk in Chunks(text))
        {
            await Task.Delay(Pace, cancellationToken);
            yield return new ChatResponseUpdate(ChatRole.Assistant, chunk) { MessageId = messageId };
        }
    }

    // Pieces of 2 to 6 characters, as tokens arrive from a model; the cut does not care about Markdown, on purpose.
    private static IEnumerable<string> Chunks(string text)
    {
        var sizes = new[] { 3, 5, 2, 6, 4, 3, 2, 5 };
        for (int i = 0, n = 0; i < text.Length; n++)
        {
            var size = Math.Min(sizes[n % sizes.Length], text.Length - i);
            yield return text.Substring(i, size);
            i += size;
        }
    }
}
