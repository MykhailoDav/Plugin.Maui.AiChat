using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Plugin.Maui.AiChat.Sample.Services;

namespace Plugin.Maui.AiChat.Sample.ViewModels;

/// <summary>
/// Owns the chat session, so the conversation outlives the page. It saves the conversation after every response,
/// restores it at start, and moves it to a new session when the model changes.
/// </summary>
public sealed partial class ChatViewModel : ObservableObject
{
    private readonly ChatClientFactory _factory;
    private readonly ChatStore _store;
    private readonly ModelSettings _settings;

    public ChatViewModel(ChatClientFactory factory, ChatStore store, ModelSettings settings)
    {
        _factory = factory;
        _store = store;
        _settings = settings;
        _settings.Changed += async (_, _) => await ConnectAsync();
        _ = ConnectAsync();
    }

    [ObservableProperty]
    public partial AiChatSession? Session { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = "Demo model";

    private async Task ConnectAsync()
    {
        var history = Session?.Messages.ToList() ?? _store.Load();
        if (Session is { } previous)
        {
            previous.ResponseCompleted -= OnResponseCompleted;
            previous.Stop();
        }

        var session = new AiChatSession(await _factory.CreateAsync()) { Options = _factory.CreateOptions() };
        session.Load(history);
        session.ResponseCompleted += OnResponseCompleted;
        Session = session;
        Title = _settings.UseDemo ? "Demo model" : _settings.Model;
    }

    private void OnResponseCompleted(object? sender, AiChatResponseEventArgs e) => _store.Save(((AiChatSession)sender!).Messages);

    [RelayCommand]
    private async Task AskAsync(string question)
    {
        if (Session is { IsBusy: false } session)
        {
            await session.SendAsync(question);
        }
    }

    [RelayCommand]
    private void NewChat()
    {
        Session?.Clear();
        _store.Delete();
    }
}
