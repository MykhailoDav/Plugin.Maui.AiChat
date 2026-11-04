using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Plugin.Maui.AiChat.Sample.Services;

namespace Plugin.Maui.AiChat.Sample.ViewModels;

/// <summary>
/// Edits Markdown and replays it a few characters at a time, with and without IsStreaming, to show what streaming-safe
/// rendering changes.
/// </summary>
public sealed partial class MarkdownViewModel : ObservableObject
{
    private bool _replaying;

    [ObservableProperty]
    public partial string Source { get; set; } = DemoAnswers.Markdown;

    [ObservableProperty]
    public partial string Preview { get; set; } = DemoAnswers.Markdown;

    [ObservableProperty]
    public partial bool IsStreaming { get; set; }

    [ObservableProperty]
    public partial bool StreamingSafe { get; set; } = true;

    partial void OnSourceChanged(string value)
    {
        if (!_replaying)
        {
            Preview = value;
        }
    }

    [RelayCommand]
    private async Task ReplayAsync(CancellationToken cancellationToken)
    {
        _replaying = true;
        IsStreaming = StreamingSafe;
        try
        {
            for (var length = 0; length < Source.Length; length += 3)
            {
                Preview = Source[..length];
                await Task.Delay(16, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _replaying = false;
            IsStreaming = false;
            Preview = Source;
        }
    }
}
