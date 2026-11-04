using System.Collections.ObjectModel;
using Microsoft.Extensions.AI;

namespace Plugin.Maui.AiChat.Chat;

/// <summary>
/// Builds the rows of the view from the conversation's contents: whole messages when they are sent or loaded, and
/// updates while a response streams. Both paths go through <see cref="Apply"/>, so a restored conversation looks the
/// same as it did live.
/// </summary>
internal sealed class ChatItemBuilder
{
    internal ObservableCollection<ChatItem> Items { get; } = [];

    /// <summary>Raised when a row is added or its text grows, so the view can follow the end.</summary>
    internal event EventHandler? Changed;

    internal bool HasPendingApprovals => FindLast(static item => item is ToolCallItem { IsAwaitingApproval: true }) is not null;

    internal void AddMessage(ChatMessage message)
    {
        if (message.Role == ChatRole.System)
        {
            return;
        }

        var fromUser = message.Role == ChatRole.User;
        if (fromUser && message.Text.Length > 0)
        {
            Add(new UserMessageItem(message.Text));
        }

        foreach (var content in message.Contents)
        {
            if (!(fromUser && content is TextContent))
            {
                // The message itself is the key: two loaded messages never merge into one row.
                Apply(content, message, streaming: false);
            }
        }
    }

    internal void ApplyUpdate(ChatResponseUpdate update)
    {
        foreach (var content in update.Contents)
        {
            Apply(content, update.MessageId, streaming: true);
        }
    }

    /// <summary>Ends a response: text stops streaming, and tool calls that never finished are canceled.</summary>
    internal void End(AiChatResponseStatus status, Exception? error)
    {
        foreach (var item in Items)
        {
            switch (item)
            {
                case StreamedTextItem text:
                    text.IsStreaming = false;
                    break;
                case ToolCallItem { Status: ToolCallStatus.Running } tool:
                    tool.Status = ToolCallStatus.Canceled;
                    break;
                // A stopped or failed response is not in the history with its approval requests, so they cannot be answered.
                case ToolCallItem { Status: ToolCallStatus.AwaitingApproval } tool when status != AiChatResponseStatus.Completed:
                    tool.Status = ToolCallStatus.Canceled;
                    break;
            }
        }

        if (error is not null)
        {
            Add(new ErrorItem(error.Message, canRetry: true));
        }
    }

    /// <summary>Rejects every request still waiting, for when the user writes instead of answering.</summary>
    internal void RejectPendingApprovals()
    {
        foreach (var item in Items)
        {
            if (item is ToolCallItem tool)
            {
                tool.Answer(approved: false);
            }
        }
    }

    /// <summary>The answers given since the last request, which the next request carries to the model.</summary>
    internal List<AIContent> TakeAnswers()
    {
        var answers = new List<AIContent>();
        foreach (var item in Items)
        {
            if (item is ToolCallItem { Response: { } response } tool)
            {
                answers.Add(response);
                tool.Response = null;
            }
        }

        return answers;
    }

    internal void DisableRetry()
    {
        foreach (var item in Items)
        {
            if (item is ErrorItem error)
            {
                error.CanRetry = false;
            }
        }
    }

    internal void RemoveFrom(int index)
    {
        while (Items.Count > index)
        {
            Items.RemoveAt(Items.Count - 1);
        }
    }

    internal void Clear() => Items.Clear();

    private void Apply(AIContent content, object? messageKey, bool streaming)
    {
        if (content is not TextReasoningContent && Items.Count > 0 && Items[^1] is ReasoningItem reasoning)
        {
            reasoning.IsStreaming = false;
        }

        switch (content)
        {
            case TextContent { Text.Length: > 0 } text:
                AppendText(text.Text, messageKey, streaming, static (t, key, s) => new AssistantMessageItem(t, key, s));
                break;
            case TextReasoningContent { Text.Length: > 0 } thought:
                AppendText(thought.Text, messageKey, streaming, static (t, key, s) => new ReasoningItem(t, key, s));
                break;
            case ToolApprovalRequestContent request:
                var awaiting = FindTool(request.ToolCall.CallId) ?? AddTool(request.ToolCall);
                awaiting.Request = request;
                awaiting.Status = ToolCallStatus.AwaitingApproval;
                break;
            case ToolApprovalResponseContent response when FindTool(response.ToolCall.CallId) is { } answered:
                answered.Status = response.Approved ? ToolCallStatus.Running : ToolCallStatus.Rejected;
                break;
            case ToolCallContent call when FindTool(call.CallId) is null:
                AddTool(call);
                break;
            case ToolResultContent result when FindTool(result.CallId) is { Status: not ToolCallStatus.Rejected } tool:
                tool.Status = result is FunctionResultContent { Exception: not null } ? ToolCallStatus.Failed : ToolCallStatus.Completed;
                break;
            case ErrorContent error:
                Add(new ErrorItem(error.Message, canRetry: false));
                break;
        }
    }

    private void AppendText<TItem>(string text, object? messageKey, bool streaming, Func<string, object?, bool, TItem> create)
        where TItem : StreamedTextItem
    {
        if (Items.Count > 0 && Items[^1] is TItem last && last.IsStreaming == streaming && Equals(last.MessageKey, messageKey))
        {
            last.Append(text);
            Changed?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            Add(create(text, messageKey, streaming));
        }
    }

    private ToolCallItem AddTool(ToolCallContent call)
    {
        var item = new ToolCallItem(call);
        Add(item);
        return item;
    }

    private ToolCallItem? FindTool(string callId)
        => (ToolCallItem?)FindLast(item => item is ToolCallItem tool && tool.CallId == callId);

    private ChatItem? FindLast(Func<ChatItem, bool> match)
    {
        for (var i = Items.Count - 1; i >= 0; i--)
        {
            if (match(Items[i]))
            {
                return Items[i];
            }
        }

        return null;
    }

    private void Add(ChatItem item)
    {
        Items.Add(item);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
