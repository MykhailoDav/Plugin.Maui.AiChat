namespace Plugin.Maui.AiChat.Sample.Services;

/// <summary>Which model the chat talks to. The API key goes to secure storage, the rest to preferences.</summary>
public sealed class ModelSettings(IPreferences preferences, ISecureStorage secureStorage)
{
    public const string OpenAIEndpoint = "https://api.openai.com/v1";
    public const string OpenAIModel = "gpt-5-mini";
    public const string OllamaModel = "llama3.2";
    private const string ApiKeyName = "model.apiKey";

    /// <summary>Raised after <see cref="SaveAsync"/>, so the chat can switch to the new model.</summary>
    public event EventHandler? Changed;

    public bool UseDemo => preferences.Get("model.useDemo", true);

    public string Endpoint => preferences.Get("model.endpoint", OpenAIEndpoint);

    public string Model => preferences.Get("model.name", OpenAIModel);

    public string Instructions => preferences.Get("model.instructions", "You are a helpful assistant in a .NET MAUI app. Answer in Markdown.");

    /// <summary>Ollama on the development machine, as the emulator or simulator reaches it.</summary>
    public static string OllamaEndpoint => DeviceInfo.Platform == DevicePlatform.Android ? "http://10.0.2.2:11434/v1" : "http://localhost:11434/v1";

    public async Task<string> GetApiKeyAsync() => await secureStorage.GetAsync(ApiKeyName) ?? "";

    public async Task SaveAsync(bool useDemo, string endpoint, string model, string apiKey, string instructions)
    {
        preferences.Set("model.useDemo", useDemo);
        preferences.Set("model.endpoint", endpoint.Trim());
        preferences.Set("model.name", model.Trim());
        preferences.Set("model.instructions", instructions.Trim());
        if (apiKey.Length == 0)
        {
            secureStorage.Remove(ApiKeyName);
        }
        else
        {
            await secureStorage.SetAsync(ApiKeyName, apiKey.Trim());
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
