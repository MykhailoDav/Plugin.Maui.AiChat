using Plugin.Maui.AiChat.Sample.ViewModels;

namespace Plugin.Maui.AiChat.Sample.Pages;

public partial class ChatPage : ContentPage
{
    public ChatPage(ChatViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
