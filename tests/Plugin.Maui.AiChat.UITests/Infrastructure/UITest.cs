using NUnit.Framework;

namespace Plugin.Maui.AiChat.UITests.Infrastructure;

/// <summary>The base of every UI test.</summary>
public abstract class UITest
{
    protected static AiChatApp App => UITestSetup.App;

    protected static TestPlatform Platform => UITestSettings.Platform;

    [TearDown]
    public void SaveDiagnosticsOfFailure()
    {
        if (TestContext.CurrentContext.Result.Outcome.Status != NUnit.Framework.Interfaces.TestStatus.Failed)
            return;

        try
        {
            var name = string.Concat(TestContext.CurrentContext.Test.Name.Select(c => char.IsLetterOrDigit(c) ? c : '_'));
            foreach (var path in App.SaveDiagnostics(name))
                TestContext.AddTestAttachment(path);
        }
        catch (Exception exception) when (exception is OpenQA.Selenium.WebDriverException or InvalidOperationException or IOException)
        {
            // The app or the session is gone; the test's own failure says enough.
        }
    }

    /// <summary>Compares a screenshot of the chat view with the baseline named <paramref name="name"/>.</summary>
    protected static void ChatMatchesBaseline(string name)
    {
        if (UITestSettings.CompareScreenshots)
            ScreenshotAssert.MatchesBaseline(App.Screenshot("Chat"), name);
    }

    /// <summary>Skips a test on platforms that cannot do what it needs.</summary>
    protected static void SkipOn(TestPlatform platform, string reason)
    {
        if (Platform == platform)
            Assert.Ignore($"Not supported on {platform}: {reason}");
    }

    /// <summary>Runs a test only on <paramref name="platform"/>.</summary>
    protected static void OnlyOn(TestPlatform platform, string reason)
    {
        if (Platform != platform)
            Assert.Ignore($"Only on {platform}: {reason}");
    }
}
