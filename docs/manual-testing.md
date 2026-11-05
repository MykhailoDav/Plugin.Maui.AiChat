# Manual testing

The unit tests cover the shared code and the UI tests drive simulators and emulators with a scripted model. What they
cannot reach is checked by hand before a release, with the sample app, on the devices below.

## Devices

| Platform | Device |
| --- | --- |
| iOS | An iPhone, and an iPad with a hardware keyboard |
| Android | A phone with gesture navigation, and one with three-button navigation |
| Mac Catalyst | A Mac with a trackpad |
| Windows | A PC with a mouse; a touch screen if there is one |

## Checks

| Area | What to check |
| --- | --- |
| Keyboard | The message field rises with the keyboard and the last message stays visible; the list scrolls while the keyboard is up; a hardware keyboard and dictation work in the field. |
| Real models | With OpenAI and a local Ollama on the Model tab: streaming, a tool call, approval of `get_weather`, Stop in the middle of an answer, and a second question after a stopped answer. |
| Reasoning | A model that sends reasoning (for example a reasoning model through Ollama) shows *Thinking…* while it thinks. |
| Long answers | A few screens of Markdown with code and tables streams smoothly and follows the end; scrolling up stops following, and scrolling back down starts it again. |
| Links | A link opens the browser; a `mailto:` link opens the mail app. |
| Copy | Copy on an answer and on a code block puts the text on the clipboard. |
| Persistence | Quit the sample during and after an answer, start it again: the conversation is back, and a waiting approval can still be answered. |
| Themes | Switch the system between light and dark while the chat is open. |
| Screen readers | VoiceOver and TalkBack read the messages, the tool cards and the buttons (Send, Stop, Copy, Approve, Reject, Retry) by name. |
| Text size | The largest system text size keeps everything readable and the buttons reachable. |
| Windows | Everything above; Windows has not been checked on a device yet. |
