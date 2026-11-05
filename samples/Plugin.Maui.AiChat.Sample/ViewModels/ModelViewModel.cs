using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Plugin.Maui.AiChat.Sample.Services;

namespace Plugin.Maui.AiChat.Sample.ViewModels;

/// <summary>Chooses the model: the offline demo, or any OpenAI-compatible endpoint such as OpenAI or Ollama.</summary>
public sealed partial class ModelViewModel(ModelSettings settings) : ObservableObject
{
    [ObservableProperty]
    public partial bool UseDemo { get; set; } = true;

    [ObservableProperty]
    public partial string Endpoint { get; set; } = ModelSettings.OpenAIEndpoint;

    [ObservableProperty]
    public partial string Model { get; set; } = ModelSettings.OpenAIModel;

    [ObservableProperty]
    public partial string ApiKey { get; set; } = "";

    [ObservableProperty]
    public partial string Instructions { get; set; } = "";

    [RelayCommand]
    private async Task LoadAsync()
    {
        UseDemo = settings.UseDemo;
        Endpoint = settings.Endpoint;
        Model = settings.Model;
        Instructions = settings.Instructions;
        ApiKey = await settings.GetApiKeyAsync();
    }

    [RelayCommand]
    private void UseOpenAI()
    {
        UseDemo = false;
        Endpoint = ModelSettings.OpenAIEndpoint;
        Model = ModelSettings.OpenAIModel;
    }

    [RelayCommand]
    private void UseOllama()
    {
        UseDemo = false;
        Endpoint = ModelSettings.OllamaEndpoint;
        Model = ModelSettings.OllamaModel;
        ApiKey = "";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!UseDemo && !Uri.TryCreate(Endpoint, UriKind.Absolute, out _))
        {
            await Shell.Current.DisplayAlertAsync("Endpoint", "Enter the endpoint's full URL, such as https://api.openai.com/v1.", "OK");
            return;
        }

        await settings.SaveAsync(UseDemo, Endpoint, Model, ApiKey, Instructions);
        await Shell.Current.GoToAsync("//chat");
    }
}
