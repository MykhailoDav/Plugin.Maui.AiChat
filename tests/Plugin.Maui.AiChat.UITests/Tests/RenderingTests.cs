using NUnit.Framework;
using Plugin.Maui.AiChat.UITests.Infrastructure;

namespace Plugin.Maui.AiChat.UITests.Tests;

/// <summary>A restored conversation looks the same as its baseline, in the light and the dark theme.</summary>
public class RenderingTests : UITest
{
    [Test]
    public void RestoredConversation_Light()
    {
        App.OpenScenario("Restored");
        App.Element("AiChatCopyCode");

        ChatMatchesBaseline("Restored");
    }

    [Test]
    public void RestoredConversation_Dark()
    {
        App.OpenScenario("RestoredDark");
        App.Element("AiChatCopyCode");

        ChatMatchesBaseline("RestoredDark");
    }
}
