using Plugin.Maui.AiChat.Controls;

namespace Plugin.Maui.AiChat.Tests.Controls;

public class KeyboardSafeAreaTests
{
    [Fact]
    public void Adds_the_keyboard_to_the_bottom_of_the_page()
    {
        var chat = new AiChatView();
        var page = new ContentPage { Content = new Grid { new VerticalStackLayout { chat } } };

        KeyboardSafeArea.Apply(chat);

        Assert.Equal(SafeAreaRegions.SoftInput, page.SafeAreaEdges.Bottom);
        Assert.Equal(new SafeAreaEdges().Top, page.SafeAreaEdges.Top);
        Assert.Equal(SafeAreaRegions.Container, chat.SafeAreaEdges.Bottom);
    }

    [Fact]
    public void Leaves_a_page_whose_safe_area_the_app_set()
    {
        var chat = new AiChatView();
        var page = new ContentPage { SafeAreaEdges = SafeAreaEdges.None, Content = chat };

        KeyboardSafeArea.Apply(chat);

        Assert.Equal(SafeAreaRegions.None, page.SafeAreaEdges.Bottom);
    }

    [Fact]
    public void Does_nothing_outside_a_page()
    {
        var chat = new AiChatView();
        _ = new Grid { chat };

        KeyboardSafeArea.Apply(chat);
        KeyboardSafeArea.Apply(new AiChatView());

        Assert.Equal(SafeAreaRegions.Container, chat.SafeAreaEdges.Bottom);
    }
}
