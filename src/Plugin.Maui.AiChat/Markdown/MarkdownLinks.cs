using System.Diagnostics.CodeAnalysis;
using Plugin.Maui.AiChat.Platform;

namespace Plugin.Maui.AiChat.Markdown;

/// <summary>
/// Opens links tapped in Markdown. Model output is untrusted, so only web and mail links open; anything else
/// (custom schemes, files, relative paths) is ignored.
/// </summary>
internal static class MarkdownLinks
{
    internal static readonly Command<string> OpenCommand = new(url => _ = OpenAsync(url));

    internal static bool TryGetSafeUri(string? url, [NotNullWhen(true)] out Uri? uri)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeMailto))
        {
            return true;
        }

        uri = null;
        return false;
    }

    internal static Task OpenAsync(string? url)
        => TryGetSafeUri(url, out var uri) ? PlatformActions.OpenUri(uri) : Task.CompletedTask;
}
