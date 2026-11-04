using System.Drawing;
using NUnit.Framework;
using Plugin.Maui.AiChat.UITests.Infrastructure;

namespace Plugin.Maui.AiChat.UITests.Tests;

/// <summary>Sending, streaming, stopping and following the end of the answer, with a scripted client.</summary>
public class ConversationTests : UITest
{
    private static readonly TimeSpan LongAnswer = TimeSpan.FromSeconds(30);

    [Test]
    public void Send_ShowsTheMessageAndStreamsTheAnswer()
    {
        App.OpenScenario("Short");

        App.Send("Hello");

        Assert.That(App.WaitForText("LastStatus", "Completed"), Is.EqualTo("Completed"));
        Assert.That(App.Text("Messages"), Is.EqualTo("2"));
        Assert.That(App.Text("AiChatUserMessage"), Is.EqualTo("Hello"));
        Assert.That(App.Appears("AiChatCopyAnswer", TimeSpan.FromSeconds(5)), Is.True, "Copy shows once the answer is complete.");
    }

    [Test]
    public void SendButton_StopsTheAnswerWhileItStreams()
    {
        App.OpenScenario("Streaming");

        App.Send("Tell me everything");
        Assert.That(App.WaitForText("IsBusy", "True"), Is.EqualTo("True"));
        App.Tap("AiChatSendButton");

        Assert.That(App.WaitForText("LastStatus", "Stopped"), Is.EqualTo("Stopped"));
        Assert.That(App.Text("IsBusy"), Is.EqualTo("False"));
        Assert.That(App.Text("Messages"), Is.EqualTo("2"), "What arrived stays in the conversation.");
    }

    [Test]
    public void List_FollowsTheEndOfALongAnswer()
    {
        App.OpenScenario("Streaming");

        App.Send("Tell me everything");
        Assert.That(App.WaitForText("LastStatus", "Completed", LongAnswer), Is.EqualTo("Completed"));

        var copy = App.Element("AiChatCopyAnswer");
        Assert.That(App.IsWithin(copy, "AiChatList"), Is.True, "The end of the answer is in view.");
    }

    [Test]
    public void List_StopsFollowingWhenTheUserScrollsUp()
    {
        App.OpenScenario("Streaming");

        App.Send("Tell me everything");
        Thread.Sleep(TimeSpan.FromSeconds(3));
        App.Drag("AiChatList", new PointF(0.5f, 0.2f), new PointF(0.5f, 0.9f), TimeSpan.FromMilliseconds(300));
        Assert.That(App.WaitForText("LastStatus", "Completed", LongAnswer), Is.EqualTo("Completed"));

        var question = App.Element("AiChatUserMessage");
        Assert.That(App.IsWithin(question, "AiChatList"), Is.True, "The list stays where the user scrolled to.");
    }

    [Test]
    public void Reasoning_OpensOnATap()
    {
        App.OpenScenario("Restored");

        Assert.That(App.Text("AiChatReasoning"), Is.EqualTo("▸ Thoughts"));
        App.Tap("AiChatReasoning");

        Assert.That(App.WaitForText("AiChatReasoning", "▾ Thoughts"), Is.EqualTo("▾ Thoughts"));
    }
}
