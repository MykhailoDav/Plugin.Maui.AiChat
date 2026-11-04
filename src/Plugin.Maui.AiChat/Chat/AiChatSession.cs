using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.Extensions.AI;
using Plugin.Maui.AiChat.Chat;

namespace Plugin.Maui.AiChat;

/// <summary>
/// A conversation with an <see cref="IChatClient"/>, without UI: it keeps the history, streams the answers, stops,
/// retries and carries tool approvals. Keep it in a view model and show it with <see cref="AiChatView"/>; it survives
/// navigation, and its <see cref="Messages"/> can be saved and loaded.
/// </summary>
/// <remarks>
/// Create the session on the UI thread. It changes what the view shows only there: <see cref="SendAsync"/> called from
/// another thread continues on the UI thread.
/// </remarks>
public sealed class AiChatSession : INotifyPropertyChanged
{
    private static readonly PropertyChangedEventArgs IsBusyChanged = new(nameof(IsBusy));

    private readonly List<ChatMessage> _history = [];
    private readonly ChatItemBuilder _items = new();
    private readonly IDispatcher? _dispatcher;
    private CancellationTokenSource? _run;
    // Changes when Clear or Load replaces the conversation, so a response that was cut off leaves the new one alone.
    private int _generation;
    private string? _conversationId;
    // With a conversation id, the service stores the history up to here and gets only the messages after it.
    private int _storedByService;
    private int _retryHistoryCount = -1;
    private int _retryItemCount;

    /// <summary>Creates a session for a chat client.</summary>
    /// <param name="chatClient">
    /// The client. For tools, give it a pipeline with function invocation, such as
    /// <c>client.AsBuilder().UseFunctionInvocation().Build()</c>; tools that require approval
    /// (<see cref="ApprovalRequiredAIFunction"/>) then show Approve and Reject in the chat.
    /// </param>
    public AiChatSession(IChatClient chatClient)
        : this(chatClient, Dispatcher.GetForCurrentThread())
    {
    }

    internal AiChatSession(IChatClient chatClient, IDispatcher? dispatcher)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ChatClient = chatClient;
        _dispatcher = dispatcher;
        Messages = _history.AsReadOnly();
    }

    /// <summary>Occurs when <see cref="IsBusy"/> changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Occurs when a response ends: completed, stopped or failed. <see cref="Messages"/> already holds it, so this is
    /// the moment to save the conversation. It does not occur for a response cut off by <see cref="Clear"/> or
    /// <see cref="Load"/>.
    /// </summary>
    public event EventHandler<AiChatResponseEventArgs>? ResponseCompleted;

    /// <summary>Gets the chat client.</summary>
    public IChatClient ChatClient { get; }

    /// <summary>
    /// Gets or sets the options of every request, such as the model, <see cref="ChatOptions.Instructions"/> (the system
    /// prompt) and <see cref="ChatOptions.Tools"/>. The default is <see langword="null"/>.
    /// </summary>
    public ChatOptions? Options { get; set; }

    /// <summary>
    /// Gets the history: the user's messages and the model's responses, with tool calls, results and approvals.
    /// Serialize it with <c>AIJsonUtilities.DefaultOptions</c> and restore it with <see cref="Load"/>.
    /// </summary>
    public IReadOnlyList<ChatMessage> Messages { get; }

    /// <summary>Gets whether a response is in progress.</summary>
    public bool IsBusy
    {
        get;
        private set
        {
            if (field != value)
            {
                field = value;
                PropertyChanged?.Invoke(this, IsBusyChanged);
            }
        }
    }

    internal ObservableCollection<ChatItem> Items => _items.Items;

    /// <summary>Raised when a row is added or its text grows.</summary>
    internal event EventHandler? ItemsChanged
    {
        add => _items.Changed += value;
        remove => _items.Changed -= value;
    }

    /// <summary>
    /// Sends a message and streams the answer into the conversation. Approval requests that are still waiting are
    /// rejected first.
    /// </summary>
    /// <param name="text">The user's message.</param>
    /// <param name="cancellationToken">Stops the response, like <see cref="Stop"/>.</param>
    /// <returns>
    /// A task that completes when the response ends. It does not fail when the client does: the chat shows the error
    /// with a Retry button, and <see cref="ResponseCompleted"/> reports it.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="text"/> is empty or white space.</exception>
    /// <exception cref="InvalidOperationException">A response is in progress (<see cref="IsBusy"/>).</exception>
    public Task SendAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (_dispatcher is { IsDispatchRequired: true } dispatcher)
        {
            return dispatcher.DispatchAsync(() => SendAsync(text, cancellationToken));
        }

        if (IsBusy)
        {
            throw new InvalidOperationException("A response is in progress. Wait for it to end, or call Stop first.");
        }

        _items.RejectPendingApprovals();
        var contents = _items.TakeAnswers();
        contents.Add(new TextContent(text));
        return RunAsync(new ChatMessage(ChatRole.User, contents), cancellationToken);
    }

    /// <summary>Stops the response in progress. What arrived stays in the conversation.</summary>
    public void Stop() => _run?.Cancel();

    /// <summary>Stops the response in progress and starts a new conversation.</summary>
    public void Clear() => Reset();

    /// <summary>
    /// Replaces the conversation with saved messages, for example <see cref="Messages"/> deserialized with
    /// <c>AIJsonUtilities.DefaultOptions</c>. Approval requests that were waiting can be answered again.
    /// </summary>
    /// <param name="messages">The messages, oldest first.</param>
    /// <exception cref="ArgumentNullException"><paramref name="messages"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="messages"/> contains null.</exception>
    public void Load(IEnumerable<ChatMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        // Copied first: the caller may pass Messages itself, which Reset empties.
        var loaded = messages.ToList();
        if (loaded.Contains(null!))
        {
            throw new ArgumentException("The messages contain null.", nameof(messages));
        }

        Reset();
        foreach (var message in loaded)
        {
            _history.Add(message);
            _items.AddMessage(message);
        }

        _items.End(AiChatResponseStatus.Completed, null);
    }

    internal Task RespondAsync(ToolCallItem item, bool approved)
    {
        if (IsBusy || !item.Answer(approved) || _items.HasPendingApprovals)
        {
            return Task.CompletedTask;
        }

        return RunAsync(new ChatMessage(ChatRole.User, _items.TakeAnswers()), CancellationToken.None);
    }

    internal Task RetryAsync()
    {
        if (IsBusy || _retryHistoryCount < 0)
        {
            return Task.CompletedTask;
        }

        _history.RemoveRange(_retryHistoryCount, _history.Count - _retryHistoryCount);
        _items.RemoveFrom(_retryItemCount);
        return RunCoreAsync(CancellationToken.None);
    }

    private void Reset()
    {
        // Detached before the cancel: a canceled stream can resume inside Cancel(), and must see that it was cut off.
        var run = _run;
        _run = null;
        _generation++;
        run?.Cancel();
        _history.Clear();
        _items.Clear();
        _conversationId = null;
        _storedByService = 0;
        _retryHistoryCount = -1;
        IsBusy = false;
    }

    private Task RunAsync(ChatMessage request, CancellationToken cancellationToken)
    {
        _history.Add(request);
        _items.AddMessage(request);
        return RunCoreAsync(cancellationToken);
    }

    private async Task RunCoreAsync(CancellationToken cancellationToken)
    {
        var generation = _generation;
        _retryHistoryCount = _history.Count;
        _retryItemCount = _items.Items.Count;
        _items.DisableRetry();
        using var run = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _run = run;
        IsBusy = true;

        var (messages, options) = CreateRequest();
        var updates = new List<ChatResponseUpdate>();
        var status = AiChatResponseStatus.Completed;
        Exception? error = null;
        try
        {
            await foreach (var update in ChatClient.GetStreamingResponseAsync(messages, options, run.Token).ConfigureAwait(true))
            {
                // A client that ignores cancellation keeps streaming into a conversation that is gone.
                if (generation != _generation)
                {
                    break;
                }

                updates.Add(update);
                _items.ApplyUpdate(update);
            }
        }
        catch (OperationCanceledException) when (run.IsCancellationRequested)
        {
            status = AiChatResponseStatus.Stopped;
        }
#pragma warning disable CA1031 // Any failure of the client is shown in the chat; an app crash would lose the conversation.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            status = AiChatResponseStatus.Failed;
            error = exception;
        }

        if (generation != _generation)
        {
            return;
        }

        _run = null;
        AddToHistory(updates, status);
        _items.End(status, error);
        IsBusy = false;
        ResponseCompleted?.Invoke(this, new AiChatResponseEventArgs(updates.ToChatResponse(), status, error));
    }

    private (ChatMessage[] Messages, ChatOptions? Options) CreateRequest()
    {
        if (_conversationId is null)
        {
            return (_history.ToArray(), Options);
        }

        var options = Options?.Clone() ?? new ChatOptions();
        options.ConversationId = _conversationId;
        return (_history.GetRange(_storedByService, _history.Count - _storedByService).ToArray(), options);
    }

    private void AddToHistory(List<ChatResponseUpdate> updates, AiChatResponseStatus status)
    {
        var start = _history.Count;
        _history.AddMessages(updates);
        if (status != AiChatResponseStatus.Completed)
        {
            RemoveUnansweredToolCalls(start);
        }

        for (var i = updates.Count - 1; i >= 0; i--)
        {
            if (updates[i].ConversationId is { } conversationId)
            {
                _conversationId = conversationId;
                _storedByService = _history.Count;
                break;
            }
        }
    }

    // A tool call without its result makes the next request invalid for most providers, so a response that was cut
    // off keeps its text and the calls that finished.
    private void RemoveUnansweredToolCalls(int start)
    {
        var answered = new HashSet<string>(StringComparer.Ordinal);
        for (var i = start; i < _history.Count; i++)
        {
            foreach (var content in _history[i].Contents)
            {
                if (content is ToolResultContent result)
                {
                    answered.Add(result.CallId);
                }
            }
        }

        for (var i = _history.Count - 1; i >= start; i--)
        {
            var contents = _history[i].Contents;
            for (var j = contents.Count - 1; j >= 0; j--)
            {
                if (contents[j] is ToolApprovalRequestContent || (contents[j] is ToolCallContent call && !answered.Contains(call.CallId)))
                {
                    contents.RemoveAt(j);
                }
            }

            if (contents.Count == 0)
            {
                _history.RemoveAt(i);
            }
        }
    }
}
