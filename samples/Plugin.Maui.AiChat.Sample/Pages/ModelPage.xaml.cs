using Plugin.Maui.AiChat.Sample.ViewModels;

namespace Plugin.Maui.AiChat.Sample.Pages;

public partial class ModelPage : ContentPage
{
    private readonly ModelViewModel _viewModel;

    public ModelPage(ModelViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
