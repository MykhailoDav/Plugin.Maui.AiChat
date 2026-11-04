using Plugin.Maui.AiChat.Sample.Pages;
using Plugin.Maui.AiChat.Sample.Services;
using Plugin.Maui.AiChat.Sample.ViewModels;

namespace Plugin.Maui.AiChat.Sample;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // The chat views need no registration: they are made of standard MAUI controls.
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddSingleton(Preferences.Default);
        builder.Services.AddSingleton(SecureStorage.Default);
        builder.Services.AddSingleton(FileSystem.Current);
        builder.Services.AddSingleton<ModelSettings>();
        builder.Services.AddSingleton<ChatClientFactory>();
        builder.Services.AddSingleton<ChatStore>();

        // The chat's view model is a singleton, so the conversation survives switching tabs.
        builder.Services.AddTransient<ChatPage>().AddSingleton<ChatViewModel>();
        builder.Services.AddTransient<MarkdownPage>().AddTransient<MarkdownViewModel>();
        builder.Services.AddTransient<ModelPage>().AddTransient<ModelViewModel>();

        return builder.Build();
    }
}
