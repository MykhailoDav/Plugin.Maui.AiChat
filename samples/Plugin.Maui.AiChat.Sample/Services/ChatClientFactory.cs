using System.ClientModel;
using System.Globalization;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Plugin.Maui.AiChat.Sample.Services;

/// <summary>
/// Builds the chat client from the settings: the demo model or an OpenAI-compatible endpoint, both behind function
/// invocation, so tools run and the ones that need approval ask for it in the chat.
/// </summary>
public sealed class ChatClientFactory(ModelSettings settings)
{
    public async Task<IChatClient> CreateAsync()
    {
        IChatClient model = settings.UseDemo
            ? new DemoChatClient()
            : new OpenAIClient(new ApiKeyCredential(await ApiKeyOrPlaceholderAsync()), new OpenAIClientOptions { Endpoint = new Uri(settings.Endpoint) })
                .GetChatClient(settings.Model)
                .AsIChatClient();

        return model.AsBuilder().UseFunctionInvocation().Build();
    }

    public ChatOptions CreateOptions() => new()
    {
        Instructions = settings.UseDemo ? null : settings.Instructions,
        Tools =
        [
            // Reading the user's location is personal, so the chat asks before the model may do it.
            new ApprovalRequiredAIFunction(AIFunctionFactory.Create(GetWeather, "get_weather", "Gets the current weather in a city.")),
            AIFunctionFactory.Create(GetTime, "get_time", "Gets the local date and time of the device."),
        ],
    };

    // Ollama and other local servers take any key, but the client requires one.
    private async Task<string> ApiKeyOrPlaceholderAsync() => await settings.GetApiKeyAsync() is { Length: > 0 } key ? key : "none";

    // Made up, but the same city always gets the same weather.
    private static string GetWeather(string city)
    {
        var temperature = 8 + (city.ToUpperInvariant().Sum(c => c) % 15);
        return string.Create(CultureInfo.InvariantCulture, $"{temperature} °C and clear in {city}");
    }

    private static string GetTime() => DateTime.Now.ToString("f", CultureInfo.CurrentCulture);
}
