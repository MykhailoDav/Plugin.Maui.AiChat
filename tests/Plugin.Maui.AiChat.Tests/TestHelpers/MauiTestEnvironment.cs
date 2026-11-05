using System.Runtime.CompilerServices;

namespace Plugin.Maui.AiChat.Tests.TestHelpers;

/// <summary>
/// Lets MAUI controls be created without a running app: a <c>BindableObject</c> needs a dispatcher for the current
/// thread, and the test host has none, so a synchronous one is registered before any test runs.
/// </summary>
internal static class MauiTestEnvironment
{
    [ModuleInitializer]
    internal static void Initialize() => DispatcherProvider.SetCurrent(new SyncDispatcherProvider());

    private sealed class SyncDispatcherProvider : IDispatcherProvider
    {
        public IDispatcher GetForCurrentThread() => QueuedDispatcher.Current ?? (IDispatcher)SyncDispatcher.Instance;
    }

    private sealed class SyncDispatcher : IDispatcher
    {
        public static readonly SyncDispatcher Instance = new();

        public bool IsDispatchRequired => false;

        public bool Dispatch(Action action)
        {
            action();
            return true;
        }

        public bool DispatchDelayed(TimeSpan delay, Action action) => Dispatch(action);

        public IDispatcherTimer CreateTimer() => throw new NotSupportedException("The tests use no timers.");
    }
}

/// <summary>
/// A dispatcher that holds work until the test runs it, like a UI thread that is busy. Objects created inside
/// <see cref="Use"/> and before its disposal get it as their dispatcher.
/// </summary>
internal sealed class QueuedDispatcher : IDispatcher, IDisposable
{
    private static readonly AsyncLocal<QueuedDispatcher?> CurrentSlot = new();
    private readonly Queue<Action> _queue = new();

    public static QueuedDispatcher? Current => CurrentSlot.Value;

    public bool IsDispatchRequired { get; set; }

    public int Pending => _queue.Count;

    public static QueuedDispatcher Use()
    {
        var dispatcher = new QueuedDispatcher();
        CurrentSlot.Value = dispatcher;
        return dispatcher;
    }

    public bool Dispatch(Action action)
    {
        _queue.Enqueue(action);
        return true;
    }

    public bool DispatchDelayed(TimeSpan delay, Action action) => Dispatch(action);

    public IDispatcherTimer CreateTimer() => throw new NotSupportedException("The tests use no timers.");

    public void Dispose() => CurrentSlot.Value = null;

    public void RunAll()
    {
        while (_queue.TryDequeue(out var action))
        {
            action();
        }
    }
}
