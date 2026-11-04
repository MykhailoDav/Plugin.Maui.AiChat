using NUnit.Framework;
using Plugin.Maui.AiChat.UITests.Infrastructure;

namespace Plugin.Maui.AiChat.UITests.Tests;

/// <summary>A failed response shows its error, and Retry runs it again.</summary>
public class FailureTests : UITest
{
    [Test]
    public void Retry_RunsTheFailedResponseAgain()
    {
        App.OpenScenario("Failure");

        App.Send("Hello");
        Assert.That(App.WaitForText("LastStatus", "Failed"), Is.EqualTo("Failed"));
        Assert.That(App.Text("AiChatError"), Is.EqualTo("The connection dropped."));

        App.Tap("AiChatRetry");

        Assert.That(App.WaitForText("LastStatus", "Completed"), Is.EqualTo("Completed"));
        Assert.That(App.Exists("AiChatError"), Is.False);
        Assert.That(App.Text("Messages"), Is.EqualTo("2"));
    }
}
