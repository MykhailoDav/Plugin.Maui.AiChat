using System.Drawing;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.iOS;
using OpenQA.Selenium.Appium.Mac;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;

namespace Plugin.Maui.AiChat.UITests.Infrastructure;

/// <summary>
/// The host app driven through Appium. Elements are found by their automation id: the chat view's own parts
/// (<c>AiChatMessageField</c>, <c>AiChatSendButton</c>, <c>AiChatApprove</c> and so on) and the state texts of
/// ScenarioPage in the host app.
/// </summary>
public sealed class AiChatApp : IDisposable
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    private readonly AppiumDriver _driver;

    private AiChatApp(AppiumDriver driver)
    {
        _driver = driver;
    }

    /// <summary>Whether the platform's pointer is a finger; otherwise it is a mouse.</summary>
    public static bool IsTouch => UITestSettings.Platform is TestPlatform.iOS or TestPlatform.Android;

    public static AiChatApp Launch(Uri server)
    {
        var options = new AppiumOptions { App = UITestSettings.AppPath };
        if (UITestSettings.PlatformVersion is { } platformVersion)
            options.PlatformVersion = platformVersion;

        options.AddAdditionalAppiumOption("newCommandTimeout", 600);

        // Building and starting the driver's agent on the device can take minutes the first time.
        var sessionTimeout = TimeSpan.FromMinutes(10);

        AppiumDriver driver;
        switch (UITestSettings.Platform)
        {
            case TestPlatform.iOS:
                options.PlatformName = "iOS";
                options.AutomationName = "XCUITest";
                options.DeviceName = UITestSettings.Device ?? "iPhone 17";
                options.AddAdditionalAppiumOption("wdaLaunchTimeout", 600_000);
                options.AddAdditionalAppiumOption("wdaConnectionTimeout", 600_000);
                options.AddAdditionalAppiumOption("simulatorStartupTimeout", 300_000);
                options.AddAdditionalAppiumOption("reduceMotion", true);
                options.AddAdditionalAppiumOption("shouldTerminateApp", true);
                driver = new IOSDriver(server, options, sessionTimeout);
                break;

            case TestPlatform.Android:
                options.PlatformName = "Android";
                options.AutomationName = "UiAutomator2";
                options.DeviceName = "Android Emulator";
                if (UITestSettings.Device is { } avd)
                {
                    options.AddAdditionalAppiumOption("avd", avd);
                    options.AddAdditionalAppiumOption("avdLaunchTimeout", 300_000);
                    options.AddAdditionalAppiumOption("avdReadyTimeout", 300_000);
                }

                options.AddAdditionalAppiumOption("disableWindowAnimation", true);

                // Every build has the same version code, so the app is installed again rather than kept.
                options.AddAdditionalAppiumOption("enforceAppInstall", true);
                options.AddAdditionalAppiumOption("uiautomator2ServerInstallTimeout", 120_000);
                options.AddAdditionalAppiumOption("adbExecTimeout", 120_000);
                driver = new AndroidDriver(server, options, sessionTimeout);
                break;

            case TestPlatform.MacCatalyst:
                options.App = null;
                options.PlatformName = "mac";
                options.AutomationName = "Mac2";
                options.AddAdditionalAppiumOption("bundleId", UITestSettings.AppId);
                options.AddAdditionalAppiumOption("appPath", UITestSettings.AppPath);
                options.AddAdditionalAppiumOption("showServerLogs", true);
                driver = new MacDriver(server, options, sessionTimeout);
                break;

            default:
                options.PlatformName = "Windows";
                options.AutomationName = "Windows";
                options.DeviceName = "WindowsPC";
                driver = new WindowsDriver(server, options, sessionTimeout);
                break;
        }

        driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
        return new AiChatApp(driver);
    }

    // ── Elements ─────────────────────────────────────────────────────────────

    /// <summary>The element with <paramref name="automationId"/>, waiting until it exists.</summary>
    public IWebElement Element(string automationId, TimeSpan? timeout = null) =>
        Wait(timeout).Until(driver => driver.FindElements(ByAutomationId(automationId)).FirstOrDefault())
            ?? throw new NoSuchElementException(automationId);

    public bool Exists(string automationId) => _driver.FindElements(ByAutomationId(automationId)).Count > 0;

    /// <summary>The size of the element with <paramref name="automationId"/>: in points on iOS, in pixels on Android.</summary>
    public Size SizeOf(string automationId) => Element(automationId).Size;

    /// <summary>Whether the element with <paramref name="automationId"/> exists within <paramref name="timeout"/>.</summary>
    public bool Appears(string automationId, TimeSpan timeout)
    {
        try
        {
            Element(automationId, timeout);
            return true;
        }
        catch (WebDriverTimeoutException)
        {
            return false;
        }
    }

    /// <summary>The text of the label with <paramref name="automationId"/>.</summary>
    public string Text(string automationId)
    {
        var element = Element(automationId);

        // A Mac Catalyst label exposes its text as its value.
        return UITestSettings.Platform == TestPlatform.MacCatalyst
            ? element.GetAttribute("value") ?? element.Text
            : element.Text;
    }

    /// <summary>Waits until the label with <paramref name="automationId"/> shows <paramref name="expected"/>, and returns what it shows.</summary>
    public string WaitForText(string automationId, string expected, TimeSpan? timeout = null)
    {
        var text = string.Empty;
        try
        {
            Wait(timeout ?? TimeSpan.FromSeconds(5)).Until(_ => (text = Text(automationId)) == expected);
        }
        catch (WebDriverTimeoutException)
        {
            // The caller asserts on the returned text, which gives the better message.
        }

        return text;
    }

    /// <summary>Waits until the label with <paramref name="automationId"/> shows a text that satisfies <paramref name="condition"/>.</summary>
    public string WaitForText(string automationId, Func<string, bool> condition, TimeSpan? timeout = null)
    {
        var text = string.Empty;
        try
        {
            Wait(timeout ?? TimeSpan.FromSeconds(5)).Until(_ => condition(text = Text(automationId)));
        }
        catch (WebDriverTimeoutException)
        {
            // The caller asserts on the returned text, which gives the better message.
        }

        return text;
    }

    public void Tap(string automationId) => Element(automationId).Click();

    /// <summary>Taps the element that shows <paramref name="text"/>, such as a tab, which has no automation id.</summary>
    public void TapText(string text)
    {
        var by = UITestSettings.Platform == TestPlatform.Android
            ? By.XPath($"//*[@text='{text}' or @content-desc='{text}']")
            : MobileBy.AccessibilityId(text);
        (Wait(null).Until(driver => driver.FindElements(by).FirstOrDefault()) ?? throw new NoSuchElementException(text)).Click();
    }

    // ── Scenarios ────────────────────────────────────────────────────────────

    /// <summary>Opens the scenario of the host app named <paramref name="name"/> (see ScenarioCatalog).</summary>
    public void OpenScenario(string name)
    {
        // Back returns to the scenario list. When it does not, an earlier test left the app stuck (an iOS simulator under
        // load can leave a synthesized touch down), and the app starts over rather than failing every test after it.
        if (!Exists("ScenarioEntry"))
        {
            if (Exists("BackButton"))
                Tap("BackButton");
            if (!Appears("ScenarioEntry", TimeSpan.FromSeconds(5)))
                Restart();
        }

        var entry = Element("ScenarioEntry");
        entry.Click();
        entry.Clear();
        entry.SendKeys(name);
        Tap("GoButton");

        var title = WaitForText("ScenarioTitle", name, DefaultTimeout);
        if (title != name)
            throw new InvalidOperationException($"The scenario '{name}' did not open (the title shows '{title}').");

        Element("AiChatMessageField");
    }

    private void Restart()
    {
        _driver.TerminateApp(UITestSettings.AppId);
        _driver.ActivateApp(UITestSettings.AppId);
    }

    // ── Gestures ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Drags a finger (a mouse on the desktop) across the element, from and to points given as fractions of its size,
    /// as a user scrolls a list.
    /// </summary>
    public void Drag(string automationId, PointF from, PointF to, TimeSpan duration)
    {
        var element = Element(automationId);
        var rect = new Rectangle(element.Location, element.Size);
        var kind = IsTouch ? PointerKind.Touch : PointerKind.Mouse;
        var button = IsTouch ? MouseButton.Touch : MouseButton.Left;

        // W3C input sources keep their kind for the whole session, so each kind needs a name of its own.
        var pointer = new PointerInputDevice(kind, kind.ToString().ToLowerInvariant());
        var drag = new ActionSequence(pointer, 0);
        var start = ToViewport(rect, from);
        var end = ToViewport(rect, to);
        drag.AddAction(pointer.CreatePointerMove(CoordinateOrigin.Viewport, start.X, start.Y, TimeSpan.Zero));
        drag.AddAction(pointer.CreatePointerDown(button));
        drag.AddAction(pointer.CreatePointerMove(CoordinateOrigin.Viewport, end.X, end.Y, duration));
        drag.AddAction(pointer.CreatePointerUp(button));
        _driver.PerformActions([drag]);
    }

    private static Point ToViewport(Rectangle rect, PointF point) =>
        new(rect.X + (int)Math.Round(point.X * rect.Width), rect.Y + (int)Math.Round(point.Y * rect.Height));

    // ── Chat ─────────────────────────────────────────────────────────────────

    /// <summary>Types <paramref name="text"/> into the message field and taps Send.</summary>
    public void Send(string text)
    {
        var field = Element("AiChatMessageField");
        field.Click();
        field.SendKeys(text);
        Tap("AiChatSendButton");
    }

    /// <summary>The elements with <paramref name="automationId"/> that exist now, such as one per message.</summary>
    public IReadOnlyList<IWebElement> Elements(string automationId) => _driver.FindElements(ByAutomationId(automationId));

    /// <summary>Whether the element lies inside the bounds of the element with <paramref name="containerId"/>.</summary>
    public bool IsWithin(IWebElement element, string containerId)
    {
        var container = Element(containerId);
        var outer = new Rectangle(container.Location, container.Size);
        var inner = new Rectangle(element.Location, element.Size);
        return inner.Height > 0 && outer.Contains(inner);
    }

    // ── Screenshots ──────────────────────────────────────────────────────────

    /// <summary>
    /// A PNG screenshot of the element with <paramref name="automationId"/>, taken once two screenshots in a row are the
    /// same, so layout passes that are still running never end up in it.
    /// </summary>
    public byte[] Screenshot(string automationId)
    {
        var element = Element(automationId);
        var previous = ((ITakesScreenshot)element).GetScreenshot().AsByteArray;

        for (var attempt = 0; attempt < 10; attempt++)
        {
            Thread.Sleep(300);
            var current = ((ITakesScreenshot)element).GetScreenshot().AsByteArray;
            if (current.AsSpan().SequenceEqual(previous))
                return current;

            previous = current;
        }

        return previous;
    }

    /// <summary>Saves the screen and the element tree, to see what the app showed when a test failed.</summary>
    public IReadOnlyList<string> SaveDiagnostics(string name)
    {
        Directory.CreateDirectory(UITestSettings.ResultsDirectory);
        var screen = Path.Combine(UITestSettings.ResultsDirectory, name + ".screen.png");
        var source = Path.Combine(UITestSettings.ResultsDirectory, name + ".source.xml");
        _driver.GetScreenshot().SaveAsFile(screen);
        File.WriteAllText(source, _driver.PageSource);
        return [screen, source];
    }

    private WebDriverWait Wait(TimeSpan? timeout) => new(_driver, timeout ?? DefaultTimeout)
    {
        PollingInterval = TimeSpan.FromMilliseconds(250),
    };

    // ── Elements (private) ───────────────────────────────────────────────────

    // MAUI makes the automation id the view's resource id on Android, and its accessibility identifier elsewhere.
    private static By ByAutomationId(string automationId) => UITestSettings.Platform == TestPlatform.Android
        ? By.Id(automationId)
        : MobileBy.AccessibilityId(automationId);

    public void Dispose()
    {
        _driver.Quit();
        _driver.Dispose();
    }
}
