using Microsoft.Maui.Controls.Shapes;
using Plugin.Maui.AiChat.Chat;
using Plugin.Maui.AiChat.Markdown;
using Plugin.Maui.AiChat.Theming;

namespace Plugin.Maui.AiChat.Controls;

/// <summary>Picks the row view for each kind of item. One template per kind, as recycling list views require.</summary>
internal sealed class ChatItemTemplateSelector : DataTemplateSelector
{
    private readonly DataTemplate _user;
    private readonly DataTemplate _assistant;
    private readonly DataTemplate _reasoning;
    private readonly DataTemplate _tool;
    private readonly DataTemplate _error;

    internal ChatItemTemplateSelector(AiChatView owner)
    {
        _user = new DataTemplate(() => owner.Track(new UserMessageView(owner)));
        _assistant = new DataTemplate(() => owner.Track(new AssistantMessageView()));
        _reasoning = new DataTemplate(() => owner.Track(new ReasoningView()));
        _tool = new DataTemplate(() => owner.Track(new ToolCallView(owner)));
        _error = new DataTemplate(() => owner.Track(new ErrorView(owner)));
    }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container) => item switch
    {
        UserMessageItem => _user,
        AssistantMessageItem => _assistant,
        ReasoningItem => _reasoning,
        ToolCallItem => _tool,
        _ => _error,
    };
}

/// <summary>The user's message: a bubble in the accent color, on the right.</summary>
internal sealed class UserMessageView : ContentView
{
    internal UserMessageView(AiChatView owner)
    {
        var text = new Label { AutomationId = "AiChatUserMessage", TextColor = ChatTheme.OnAccent, FontSize = 16, LineBreakMode = LineBreakMode.WordWrap };
        text.SetBinding(Label.TextProperty, static (UserMessageItem item) => item.Text);

        var bubble = new Border
        {
            Padding = new Thickness(14, 9),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            HorizontalOptions = LayoutOptions.End,
            Content = text,
        };
        bubble.SetBinding(BackgroundColorProperty, static (AiChatView view) => view.AccentColor, source: owner);

        Padding = new Thickness(48, 6, 12, 6);
        Content = bubble;
    }
}

/// <summary>The model's answer: Markdown across the width, and Copy once it is complete.</summary>
internal sealed class AssistantMessageView : ContentView
{
    internal AssistantMessageView()
    {
        Markdown = new MarkdownView { AutomationId = "AiChatAnswer" };
        Markdown.SetBinding(MarkdownView.TextProperty, static (AssistantMessageItem item) => item.Text);
        Markdown.SetBinding(MarkdownView.IsStreamingProperty, static (AssistantMessageItem item) => item.IsStreaming);

        Copy = new CopyButton(() => (BindingContext as AssistantMessageItem)?.Text, ChatStrings.CopyAnswer)
        {
            AutomationId = "AiChatCopyAnswer",
            HorizontalOptions = LayoutOptions.Start,
            Margin = new Thickness(-8, 0, 0, 0),
        };
        Copy.SetBinding(IsVisibleProperty, static (AssistantMessageItem item) => item.IsComplete);

        Content = new VerticalStackLayout { Padding = new Thickness(12, 6), Spacing = 2, Children = { Markdown, Copy } };
    }

    internal MarkdownView Markdown { get; }

    internal CopyButton Copy { get; }
}

/// <summary>The model's reasoning: a header that shows or hides it, in muted text.</summary>
internal sealed class ReasoningView : ContentView
{
    internal ReasoningView()
    {
        Header = new Label { AutomationId = "AiChatReasoning", FontSize = 14 };
        ChatTheme.Apply(Header, Label.TextColorProperty, ChatTheme.Muted);
        Header.SetBinding(Label.TextProperty, static (ReasoningItem item) => item.Header);
        Header.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(Toggle) });
        SemanticProperties.SetHint(Header, "Shows or hides the reasoning");

        Body = new MarkdownView { FontSize = 14 };
        ChatTheme.Apply(Body, MarkdownView.TextColorProperty, ChatTheme.Muted);
        Body.SetBinding(MarkdownView.TextProperty, static (ReasoningItem item) => item.Text);
        Body.SetBinding(MarkdownView.IsStreamingProperty, static (ReasoningItem item) => item.IsStreaming);
        Body.SetBinding(IsVisibleProperty, static (ReasoningItem item) => item.IsExpanded);

        Content = new VerticalStackLayout { Padding = new Thickness(12, 4), Spacing = 4, Children = { Header, Body } };
    }

    internal Label Header { get; }

    internal MarkdownView Body { get; }

    private void Toggle()
    {
        if (BindingContext is ReasoningItem item)
        {
            item.IsExpanded = !item.IsExpanded;
        }
    }
}

/// <summary>A tool call: its name, status and arguments, with Approve and Reject while it waits for the user.</summary>
internal sealed class ToolCallView : ContentView
{
    private readonly AiChatView _owner;

    internal ToolCallView(AiChatView owner)
    {
        _owner = owner;

        var name = new Label { FontSize = 14, FontAttributes = FontAttributes.Bold, FontFamily = MonospaceFont.Name };
        ChatTheme.Apply(name, Label.TextColorProperty, ChatTheme.Text);
        name.SetBinding(Label.TextProperty, static (ToolCallItem item) => item.Name);

        var status = new Label { AutomationId = "AiChatToolStatus", FontSize = 13, VerticalTextAlignment = TextAlignment.Center };
        ChatTheme.Apply(status, Label.TextColorProperty, ChatTheme.Muted);
        status.SetBinding(Label.TextProperty, static (ToolCallItem item) => item.StatusText);

        var arguments = new Label { FontSize = 13, FontFamily = MonospaceFont.Name, LineBreakMode = LineBreakMode.WordWrap };
        ChatTheme.Apply(arguments, Label.TextColorProperty, ChatTheme.Muted);
        arguments.SetBinding(Label.TextProperty, static (ToolCallItem item) => item.Arguments);
        arguments.SetBinding(IsVisibleProperty, static (ToolCallItem item) => item.HasArguments);
        arguments.SetBinding(Label.MaxLinesProperty, static (ToolCallItem item) => item.ArgumentLines);

        Approve = new Button { AutomationId = "AiChatApprove", Text = ChatStrings.Approve, FontAttributes = FontAttributes.Bold, TextColor = ChatTheme.OnAccent, FontSize = 14, CornerRadius = 16, Padding = new Thickness(16, 0), MinimumHeightRequest = 36 };
        Approve.SetBinding(BackgroundColorProperty, static (AiChatView view) => view.AccentColor, source: owner);
        Approve.Clicked += (_, _) => Respond(approved: true);

        Reject = new Button { AutomationId = "AiChatReject", Text = ChatStrings.Reject, FontAttributes = FontAttributes.None, BackgroundColor = Colors.Transparent, BorderWidth = 1, FontSize = 14, CornerRadius = 16, Padding = new Thickness(16, 0), MinimumHeightRequest = 36 };
        ChatTheme.Apply(Reject, Button.TextColorProperty, ChatTheme.Text);
        ChatTheme.Apply(Reject, Button.BorderColorProperty, ChatTheme.Line);
        Reject.Clicked += (_, _) => Respond(approved: false);

        var buttons = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(0, 4, 0, 0), Children = { Approve, Reject } };
        buttons.SetBinding(IsVisibleProperty, static (ToolCallItem item) => item.IsAwaitingApproval);

        var header = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 8 };
        header.Add(name);
        header.Add(status, 1);

        var card = new Border
        {
            Padding = new Thickness(12, 10),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Content = new VerticalStackLayout { Spacing = 4, Children = { header, arguments, buttons } },
        };
        ChatTheme.Apply(card, BackgroundColorProperty, ChatTheme.Surface);
        ChatTheme.ApplyBrush(card, Border.StrokeProperty, ChatTheme.Line);

        Padding = new Thickness(12, 4);
        Content = card;
    }

    internal Button Approve { get; }

    internal Button Reject { get; }

    private async void Respond(bool approved)
    {
        if (BindingContext is ToolCallItem item && _owner.Session is { } session)
        {
            await session.RespondAsync(item, approved).ConfigureAwait(true);
        }
    }
}

/// <summary>A failure: its message, and Retry for the response that failed.</summary>
internal sealed class ErrorView : ContentView
{
    private readonly AiChatView _owner;

    internal ErrorView(AiChatView owner)
    {
        _owner = owner;

        var message = new Label { AutomationId = "AiChatError", FontSize = 14, LineBreakMode = LineBreakMode.WordWrap };
        ChatTheme.Apply(message, Label.TextColorProperty, ChatTheme.ErrorText);
        message.SetBinding(Label.TextProperty, static (ErrorItem item) => item.Message);

        Retry = new Button { AutomationId = "AiChatRetry", Text = ChatStrings.Retry, FontAttributes = FontAttributes.Bold, BackgroundColor = Colors.Transparent, FontSize = 14, Padding = new Thickness(0), HorizontalOptions = LayoutOptions.Start, MinimumHeightRequest = 32 };
        ChatTheme.Apply(Retry, Button.TextColorProperty, ChatTheme.ErrorText);
        Retry.SetBinding(IsVisibleProperty, static (ErrorItem item) => item.CanRetry);
        Retry.Clicked += OnRetryClicked;

        var card = new Border
        {
            Padding = new Thickness(12, 10),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Content = new VerticalStackLayout { Spacing = 2, Children = { message, Retry } },
        };
        ChatTheme.Apply(card, BackgroundColorProperty, ChatTheme.ErrorSurface);

        Padding = new Thickness(12, 4);
        Content = card;
    }

    internal Button Retry { get; }

    private async void OnRetryClicked(object? sender, EventArgs e)
    {
        if (_owner.Session is { } session)
        {
            await session.RetryAsync().ConfigureAwait(true);
        }
    }
}
