using System.ComponentModel;
using Microsoft.Extensions.AI;
using Plugin.Maui.AiChat.Theming;

namespace Plugin.Maui.AiChat.Chat;

/// <summary>
/// One row of the conversation as the view shows it. The MEAI history stays the source of truth; these items are
/// built from its contents and change only on the UI thread.
/// </summary>
internal abstract class ChatItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Raise(PropertyChangedEventArgs args) => PropertyChanged?.Invoke(this, args);
}

internal sealed class UserMessageItem(string text) : ChatItem
{
    public string Text { get; } = text;
}

/// <summary>Text the model streams, appended in place while it arrives.</summary>
internal abstract class StreamedTextItem(string text, object? messageKey, bool isStreaming) : ChatItem
{
    private static readonly PropertyChangedEventArgs TextChanged = new(nameof(Text));
    private static readonly PropertyChangedEventArgs IsStreamingChanged = new(nameof(IsStreaming));

    public string Text { get; private set; } = text;

    public bool IsStreaming
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                Raise(IsStreamingChanged);
                OnIsStreamingChanged();
            }
        }
    } = isStreaming;

    /// <summary>The message the text belongs to: the update's message id while streaming, the message when loaded.</summary>
    internal object? MessageKey { get; } = messageKey;

    internal void Append(string text)
    {
        Text += text;
        Raise(TextChanged);
    }

    protected abstract void OnIsStreamingChanged();
}

internal sealed class AssistantMessageItem(string text, object? messageKey, bool isStreaming)
    : StreamedTextItem(text, messageKey, isStreaming)
{
    private static readonly PropertyChangedEventArgs IsCompleteChanged = new(nameof(IsComplete));

    public bool IsComplete => !IsStreaming;

    protected override void OnIsStreamingChanged() => Raise(IsCompleteChanged);
}

internal sealed class ReasoningItem(string text, object? messageKey, bool isStreaming)
    : StreamedTextItem(text, messageKey, isStreaming)
{
    private static readonly PropertyChangedEventArgs IsExpandedChanged = new(nameof(IsExpanded));
    private static readonly PropertyChangedEventArgs HeaderChanged = new(nameof(Header));

    public bool IsExpanded
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                Raise(IsExpandedChanged);
                Raise(HeaderChanged);
            }
        }
    }

    public string Header => (IsExpanded ? "▾ " : "▸ ") + (IsStreaming ? ChatStrings.Thinking : ChatStrings.Thoughts);

    protected override void OnIsStreamingChanged() => Raise(HeaderChanged);
}

internal enum ToolCallStatus
{
    AwaitingApproval,
    Running,
    Completed,
    Failed,
    Rejected,
    Canceled,
}

internal sealed class ToolCallItem(ToolCallContent call) : ChatItem
{
    private static readonly PropertyChangedEventArgs StatusChanged = new(nameof(Status));
    private static readonly PropertyChangedEventArgs StatusTextChanged = new(nameof(StatusText));
    private static readonly PropertyChangedEventArgs IsAwaitingApprovalChanged = new(nameof(IsAwaitingApproval));
    private static readonly PropertyChangedEventArgs ArgumentLinesChanged = new(nameof(ArgumentLines));

    public string CallId { get; } = call.CallId;

    public string Name { get; } = ToolCallText.NameOf(call);

    public string Arguments { get; } = ToolCallText.ArgumentsOf(call);

    public bool HasArguments => Arguments.Length > 0;

    public ToolCallStatus Status
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                Raise(StatusChanged);
                Raise(StatusTextChanged);
                Raise(IsAwaitingApprovalChanged);
                Raise(ArgumentLinesChanged);
            }
        }
    } = ToolCallStatus.Running;

    public string StatusText => Status switch
    {
        ToolCallStatus.AwaitingApproval => ChatStrings.ToolAwaitingApproval,
        ToolCallStatus.Running => ChatStrings.ToolRunning,
        ToolCallStatus.Completed => ChatStrings.ToolCompleted,
        ToolCallStatus.Failed => ChatStrings.ToolFailed,
        ToolCallStatus.Rejected => ChatStrings.ToolRejected,
        _ => ChatStrings.ToolCanceled,
    };

    public bool IsAwaitingApproval => Status == ToolCallStatus.AwaitingApproval;

    /// <summary>All of the arguments while the user decides; two lines otherwise. -1 is "no limit" for a label.</summary>
    public int ArgumentLines => IsAwaitingApproval ? -1 : 2;

    internal ToolApprovalRequestContent? Request { get; set; }

    /// <summary>The user's answer, until it is sent to the model.</summary>
    internal ToolApprovalResponseContent? Response { get; set; }

    internal bool Answer(bool approved)
    {
        if (!IsAwaitingApproval)
        {
            return false;
        }

        Response = Request!.CreateResponse(approved);
        Status = approved ? ToolCallStatus.Running : ToolCallStatus.Rejected;
        return true;
    }
}

internal sealed class ErrorItem(string message, bool canRetry) : ChatItem
{
    private static readonly PropertyChangedEventArgs CanRetryChanged = new(nameof(CanRetry));

    public string Message { get; } = message;

    public bool CanRetry
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                Raise(CanRetryChanged);
            }
        }
    } = canRetry;
}
