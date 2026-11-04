using System.Globalization;

namespace Plugin.Maui.AiChat.UITests.HostApp;

/// <summary>
/// The page of a scenario: a <c>BackButton</c>, the session's state as texts the UI tests read (<c>IsBusy</c>,
/// <c>Messages</c>, <c>LastStatus</c>, <c>Requests</c>), and the chat (<c>Chat</c>) filling the rest.
/// </summary>
public sealed class ScenarioPage : ContentPage
{
    private readonly AiChatSession _session;
    private readonly Func<int> _requests;
    private readonly Label _isBusy = Status("IsBusy");
    private readonly Label _messages = Status("Messages");
    private readonly Label _lastStatus = Status("LastStatus");
    private readonly Label _requestCount = Status("Requests");

    /// <param name="name">The scenario's name, shown as the title.</param>
    /// <param name="session">The conversation the chat shows.</param>
    /// <param name="requests">How many requests the client has had.</param>
    /// <param name="theme">The theme while the scenario is open.</param>
    public ScenarioPage(string name, AiChatSession session, Func<int> requests, AppTheme theme = AppTheme.Light)
    {
        Title = name;
        _session = session;
        _requests = requests;
        Application.Current!.UserAppTheme = theme;
        this.SetAppThemeColor(BackgroundColorProperty, Colors.White, Color.FromArgb("#121212"));
        _lastStatus.Text = "None";

        session.PropertyChanged += (_, _) => UpdateStatus();
        session.ResponseCompleted += (_, e) =>
        {
            _lastStatus.Text = e.Status.ToString();
            UpdateStatus();
        };

        var back = new Button { AutomationId = "BackButton", Text = "Back", Padding = new Thickness(12, 4) };
        back.Clicked += (_, _) =>
        {
            session.Stop();
            Application.Current.UserAppTheme = AppTheme.Light;
            App.Show(new GalleryPage());
        };

        var title = new Label { Text = name, AutomationId = "ScenarioTitle", FontSize = 18, VerticalOptions = LayoutOptions.Center };
        title.SetAppThemeColor(Label.TextColorProperty, Colors.Black, Colors.White);
        var header = new HorizontalStackLayout { Spacing = 12, Padding = new Thickness(8, 4), Children = { back, title } };
        var status = new HorizontalStackLayout { Spacing = 12, Padding = new Thickness(8, 0), Children = { _isBusy, _messages, _lastStatus, _requestCount } };

        var chat = new AiChatView { AutomationId = "Chat", Session = session };
        var layout = new Grid { RowDefinitions = [new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Star)] };
        layout.Add(header);
        layout.Add(status, 0, 1);
        layout.Add(chat, 0, 2);
        Content = layout;
        UpdateStatus();
    }

    private static Label Status(string automationId)
    {
        var label = new Label { AutomationId = automationId, FontSize = 12 };
        label.SetAppThemeColor(Label.TextColorProperty, Colors.DimGray, Colors.LightGray);
        return label;
    }

    private void UpdateStatus()
    {
        _isBusy.Text = _session.IsBusy.ToString();
        _messages.Text = _session.Messages.Count.ToString(CultureInfo.InvariantCulture);
        _requestCount.Text = _requests().ToString(CultureInfo.InvariantCulture);
    }
}
