using System.Diagnostics.CodeAnalysis;

namespace Plugin.Maui.AiChat.Platform;

/// <summary>
/// The two calls into platform services. They are settable because the unit tests run on the plain net10.0 target,
/// which has no clipboard or launcher.
/// </summary>
internal static class PlatformActions
{
    internal static Func<string, Task> CopyText { get; set; } = DefaultCopyText;

    internal static Func<Uri, Task> OpenUri { get; set; } = DefaultOpenUri;

    [ExcludeFromCodeCoverage(Justification = "Calls the platform clipboard; the UI tests cover it.")]
    private static Task DefaultCopyText(string text) => Clipboard.Default.SetTextAsync(text);

    [ExcludeFromCodeCoverage(Justification = "Calls the platform launcher; the UI tests cover it.")]
    private static Task DefaultOpenUri(Uri uri) => Launcher.Default.OpenAsync(uri);
}
