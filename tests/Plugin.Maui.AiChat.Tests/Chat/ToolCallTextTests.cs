using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Plugin.Maui.AiChat.Chat;
using Plugin.Maui.AiChat.Theming;

namespace Plugin.Maui.AiChat.Tests.Chat;

public class ToolCallTextTests
{
    [Fact]
    public void Names_functions_mcp_tools_and_hosted_tools()
    {
        Assert.Equal("get_weather", ToolCallText.NameOf(new FunctionCallContent("c", "get_weather")));
        Assert.Equal("search", ToolCallText.NameOf(new McpServerToolCallContent("c", "search", "docs")));
        Assert.Equal("WebSearch", ToolCallText.NameOf(new WebSearchToolCallContent("c")));
        Assert.Equal(ChatStrings.Tool, ToolCallText.NameOf(new ToolCallContent("c")));
    }

    [Fact]
    public void Lists_the_arguments_in_invariant_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("uk-UA");
        try
        {
            using var json = JsonDocument.Parse("""{"days":[1,2],"unit":"C"}""");
            var call = new FunctionCallContent("c", "f", new Dictionary<string, object?>
            {
                ["city"] = "Kyiv",
                ["count"] = 3,
                ["ratio"] = 1.5,
                ["exact"] = true,
                ["loose"] = false,
                ["nothing"] = null,
                ["options"] = json.RootElement.Clone(),
                ["custom"] = new Custom("shown"),
                ["blank"] = new Custom(null),
            });

            Assert.Equal(
                """city: "Kyiv", count: 3, ratio: 1.5, exact: true, loose: false, nothing: null, options: {"days":[1,2],"unit":"C"}, custom: shown, blank: """,
                ToolCallText.ArgumentsOf(call));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Lists_mcp_arguments_and_nothing_for_calls_without_any()
    {
        var mcp = new McpServerToolCallContent("c", "search", "docs") { Arguments = new Dictionary<string, object?> { ["q"] = "maui" } };

        Assert.Equal("q: \"maui\"", ToolCallText.ArgumentsOf(mcp));
        Assert.Equal("", ToolCallText.ArgumentsOf(new McpServerToolCallContent("c", "search", "docs")));
        Assert.Equal("", ToolCallText.ArgumentsOf(new FunctionCallContent("c", "f")));
        Assert.Equal("", ToolCallText.ArgumentsOf(new WebSearchToolCallContent("c")));
    }

    private sealed class Custom(string? text)
    {
        public override string? ToString() => text;
    }
}
