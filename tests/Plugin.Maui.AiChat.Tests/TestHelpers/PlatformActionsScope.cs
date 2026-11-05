using Plugin.Maui.AiChat.Platform;

namespace Plugin.Maui.AiChat.Tests.TestHelpers;

/// <summary>Records the copies and launches of the code under test, and restores the platform calls afterwards.</summary>
internal sealed class PlatformActionsScope : IDisposable
{
    private readonly Func<string, Task> _copyText = PlatformActions.CopyText;
    private readonly Func<Uri, Task> _openUri = PlatformActions.OpenUri;

    public PlatformActionsScope()
    {
        PlatformActions.CopyText = text =>
        {
            Copied.Add(text);
            return Task.CompletedTask;
        };
        PlatformActions.OpenUri = uri =>
        {
            Opened.Add(uri);
            return Task.CompletedTask;
        };
    }

    public List<string> Copied { get; } = [];

    public List<Uri> Opened { get; } = [];

    public void Dispose()
    {
        PlatformActions.CopyText = _copyText;
        PlatformActions.OpenUri = _openUri;
    }
}

/// <summary>Tests that replace the platform calls, which are static, run one at a time.</summary>
[CollectionDefinition(nameof(SerialPlatformActions), DisableParallelization = true)]
public sealed class SerialPlatformActions;
