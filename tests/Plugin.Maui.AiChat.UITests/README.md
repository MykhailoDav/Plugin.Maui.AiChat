# UI tests

These tests drive a real app with [Appium](https://appium.io). They check the chat the way a user meets it:
- they type a message and tap Send, Stop, Approve, Reject and Retry, with the keyboard up;
- they scroll the list while an answer streams;
- they read the session's state from the screen;
- they compare screenshots of restored conversations with baseline images.

| Folder | What it is |
| --- | --- |
| `tests/Plugin.Maui.AiChat.UITests.HostApp` | The app under test. Every page is a *scenario*: a chat on a scripted client. |
| `tests/Plugin.Maui.AiChat.UITests` | The NUnit tests, Appium (`package.json`) and the baselines (`Baselines/<platform>`). |

The unit tests in `tests/Plugin.Maui.AiChat.Tests` check all of the shared code without a device. The UI tests check what
only a device shows: the keyboard, scrolling and following the end, taps in list rows, and rendering.

## How the host app makes the tests repeatable

**Scripted client.** `ScriptedChatClient` answers the n-th request with the n-th script, four characters every 30 ms,
so every run sees the same chat. The approval scenario puts the real `FunctionInvokingChatClient` in front of it.

**Scenario page.** Every scenario shows a `BackButton`, the chat (`Chat`) and the session's state as texts the tests read:
`IsBusy`, `Messages`, `LastStatus` (`None`, `Completed`, `Stopped` or `Failed`), `Requests` and `Seconds` (how long the
last answer took). The chat's own parts have the automation ids listed in the README.

**Opening a scenario.** The tests type the scenario's name into `ScenarioEntry` on the first page and tap `GoButton`.

**Baselines** exist only for restored conversations, which do not depend on timing.

| Scenario | What it has |
| --- | --- |
| `Short` | A one-line answer. |
| `Streaming` | A long answer with every kind of block, which scrolls on a phone. |
| `Approval` | A `delete_file` tool that needs approval, then an answer that says what happened. |
| `Failure` | An answer that fails halfway, then a whole one for Retry. |
| `Restored` | A loaded conversation with reasoning, a tool card, a code block and a table, in the light theme. |
| `RestoredDark` | The same in the dark theme. |

## Setup

- **Every platform:**
  - the .NET 10 SDK with the MAUI workload;
  - Node.js 20 or later, then once `npm ci` in this folder. This installs Appium and its drivers here. The tests start
    this Appium themselves, listening on 127.0.0.1 only and with `APPIUM_HOME` set to this folder, so nothing is
    installed globally.
- **iOS:** Xcode and the simulator the baselines were recorded on (iPhone 17). The first run builds Appium's
  WebDriverAgent, which takes a few minutes.
- **Android:** the Android SDK, Java 17 or later, and a running emulator. `run.sh` uses the running emulator by its
  serial, so a phone that is connected at the same time, for example over wireless debugging, is left alone. If Gboard
  offers a stylus tutorial when the message field is focused (after stylus input on that emulator), turn stylus
  handwriting off: `adb -s emulator-5554 shell settings put secure stylus_handwriting_enabled 0`.
- **Mac Catalyst:**
  - Xcode and macOS *Automation Mode*. To turn off the password prompt once, run `sudo automationmodetool enable-automationmode-without-authentication`.
  - Allow the terminal to control the computer (System Settings › Privacy & Security › Accessibility).
- **Windows:** WinAppDriver (`appium-windows-driver` uses it) and Developer Mode.

## Running

On macOS, `run.sh` builds the host app for the platform and runs the tests:

```bash
cd tests/Plugin.Maui.AiChat.UITests
./run.sh ios
./run.sh android
./run.sh ios --filter "FullyQualifiedName~ToolApprovalTests"
./run.sh ios --update-baselines
```

Or by hand (also on Windows):

```bash
dotnet build tests/Plugin.Maui.AiChat.UITests.HostApp -f net10.0-android
UITEST_PLATFORM=android UITEST_UDID=emulator-5554 dotnet test --project tests/Plugin.Maui.AiChat.UITests
```

| Variable | Meaning |
| --- | --- |
| `UITEST_PLATFORM` | `ios`, `android`, `maccatalyst` or `windows` (default: `ios` on macOS, `windows` on Windows) |
| `UITEST_DEVICE` | The iOS simulator (default `iPhone 17`) or an Android AVD for Appium to start |
| `UITEST_UDID` | The device by its id, such as an emulator's serial (`run.sh` sets it to the running emulator) |
| `UITEST_PLATFORM_VERSION` | The iOS or Android version of that device |
| `UITEST_CONFIGURATION` | The configuration the host app was built with (default `Debug`) |
| `UITEST_APP` | An app to test instead of the host app's build output |
| `UITEST_APPIUM_URL` | An Appium server that is already running (otherwise the tests start one) |
| `UITEST_UPDATE_BASELINES` | `1` saves the screenshots as the new baselines instead of comparing them |
| `UITEST_SCREENSHOTS` | `off` skips the screenshot comparisons and checks the behavior only |

The Appium log of a run is in `TestResults/Screenshots/<platform>/appium.log`, together with the screen and element tree
of every failed test.

### Timing

A loaded emulator can stream the long answer ten times slower than a simulator; the tests wait up to a minute for it,
and they wait for conditions (the question leaving the screen, the field emptying after Send) rather than for fixed
times. UiAutomator2 is told not to wait for the app to be idle, which a streaming answer never is.
