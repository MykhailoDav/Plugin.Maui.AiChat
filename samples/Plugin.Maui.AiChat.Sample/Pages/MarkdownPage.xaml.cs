using Plugin.Maui.AiChat.Sample.ViewModels;

namespace Plugin.Maui.AiChat.Sample.Pages;

public partial class MarkdownPage : ContentPage
{
    public MarkdownPage(MarkdownViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
