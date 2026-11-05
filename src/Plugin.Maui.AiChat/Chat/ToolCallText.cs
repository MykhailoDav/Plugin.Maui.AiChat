using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Plugin.Maui.AiChat.Theming;

namespace Plugin.Maui.AiChat.Chat;

/// <summary>The name and the arguments of a tool call, as the card shows them.</summary>
internal static class ToolCallText
{
    internal static string NameOf(ToolCallContent call) => call switch
    {
        FunctionCallContent function => function.Name,
        McpServerToolCallContent mcp => mcp.Name,
        _ => HostedToolName(call),
    };

    // Hosted tools (web search, code interpreter, image generation) are named by their content type.
    private static string HostedToolName(ToolCallContent call)
    {
        var name = call.GetType().Name.Replace("ToolCallContent", "", StringComparison.Ordinal);
        return name.Length > 0 ? name : ChatStrings.Tool;
    }

    internal static string ArgumentsOf(ToolCallContent call) => call switch
    {
        FunctionCallContent { Arguments: { } arguments } => Format(arguments),
        McpServerToolCallContent { Arguments: { } arguments } => Format(arguments),
        _ => "",
    };

    private static string Format(IEnumerable<KeyValuePair<string, object?>> arguments)
    {
        var text = new StringBuilder();
        foreach (var (name, value) in arguments)
        {
            if (text.Length > 0)
            {
                text.Append(", ");
            }

            text.Append(name).Append(": ").Append(FormatValue(value));
        }

        return text.ToString();
    }

    // No JsonSerializer: it needs reflection for arbitrary objects, and the library is trimmed.
    private static string FormatValue(object? value) => value switch
    {
        null => "null",
        string text => "\"" + text + "\"",
        JsonElement json => json.GetRawText(),
        bool flag => flag ? "true" : "false",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };
}
