using NUnit.Framework;
using Plugin.Maui.AiChat.UITests.Infrastructure;

namespace Plugin.Maui.AiChat.UITests.Tests;

/// <summary>A tool that requires approval waits for the user, through FunctionInvokingChatClient.</summary>
public class ToolApprovalTests : UITest
{
    [Test]
    public void Approve_RunsTheToolAndTheAnswerContinues()
    {
        App.OpenScenario("Approval");

        App.Send("Delete notes.txt");
        Assert.That(App.WaitForText("AiChatToolStatus", "Needs your approval"), Is.EqualTo("Needs your approval"));
        Assert.That(App.Text("LastStatus"), Is.EqualTo("Completed"), "The first response ends with the request.");

        App.Tap("AiChatApprove");

        Assert.That(App.WaitForText("Requests", "2"), Is.EqualTo("2"));
        Assert.That(App.WaitForText("AiChatToolStatus", "Done"), Is.EqualTo("Done"));
        Assert.That(App.Exists("AiChatApprove"), Is.False, "The buttons go away once answered.");
    }

    [Test]
    public void Reject_TellsTheModelWithoutRunningTheTool()
    {
        App.OpenScenario("Approval");

        App.Send("Delete notes.txt");
        App.Element("AiChatReject");
        App.Tap("AiChatReject");

        Assert.That(App.WaitForText("AiChatToolStatus", "Rejected"), Is.EqualTo("Rejected"));
        Assert.That(App.WaitForText("Requests", "2"), Is.EqualTo("2"));
    }
}
