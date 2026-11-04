using Microsoft.Extensions.AI;

namespace Plugin.Maui.AiChat;

/// <summary>How a response of the chat client ended.</summary>
public enum AiChatResponseStatus
{
    /// <summary>The client finished the response.</summary>
    Completed,

    /// <summary>The response was stopped with <see cref="AiChatSession.Stop"/> or the cancellation token passed to
    /// <see cref="AiChatSession.SendAsync"/>.</summary>
    Stopped,

    /// <summary>The client threw an exception; the chat shows its message with a Retry button.</summary>
    Failed,
}

/// <summary>Provides data for <see cref="AiChatSession.ResponseCompleted"/>.</summary>
public sealed class AiChatResponseEventArgs : EventArgs
{
    /// <summary>Creates the event data.</summary>
    /// <param name="response">The response, combined from the updates that arrived.</param>
    /// <param name="status">How the response ended.</param>
    /// <param name="exception">The exception of a failed response.</param>
    public AiChatResponseEventArgs(ChatResponse response, AiChatResponseStatus status, Exception? exception)
    {
        ArgumentNullException.ThrowIfNull(response);
        Response = response;
        Status = status;
        Exception = exception;
    }

    /// <summary>
    /// Gets the response, combined from the updates that arrived. After a stop or a failure it holds what arrived
    /// before; its <see cref="ChatResponse.Usage"/> is set when the client reported it.
    /// </summary>
    public ChatResponse Response { get; }

    /// <summary>Gets how the response ended.</summary>
    public AiChatResponseStatus Status { get; }

    /// <summary>Gets the exception of a failed response, or <see langword="null"/>.</summary>
    public Exception? Exception { get; }
}
