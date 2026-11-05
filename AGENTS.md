# Working on Plugin.Maui.AiChat

This guide is for contributors and AI coding agents. The README is for people who use the package.

`Plugin.Maui.AiChat` is an AI chat for .NET MAUI on Android, iOS, Mac Catalyst and Windows. `AiChatSession` holds a
conversation with any `Microsoft.Extensions.AI` `IChatClient`; `AiChatView` shows it; `MarkdownView` renders Markdown
that streams in. The views are made of standard MAUI controls, with no handlers and no native binaries.

## Layout

| Path | What it is |
| --- | --- |
| `src/Plugin.Maui.AiChat` | The library. |
| `tests/Plugin.Maui.AiChat.Tests` | Unit tests (xUnit v3, `net10.0`). They cover all of the shared code. |
| `tests/Plugin.Maui.AiChat.UITests.HostApp` | The app the UI tests drive. Every page is a scenario on a scripted chat client. |
| `tests/Plugin.Maui.AiChat.UITests` | Appium UI tests (NUnit), screenshot baselines and Appium itself (`package.json`). |
| `samples/Plugin.Maui.AiChat.Sample` | The sample app: an offline demo model, OpenAI-compatible endpoints, a Markdown playground. |
| `docs/` | `architecture.md` (how the library works inside) and `manual-testing.md` (the checks on real devices). |
| `eng/` | Scripts for maintainers: `update-public-api.sh`, and `package-icon/make-icon.cs`, which draws the icon. |
| `.github/` | CI (`ci.yml`), UI tests (`ui-tests.yml`), CodeQL, the release workflow and Dependabot. |
| `README.md`, `llms.txt`, `llms-full.txt` | Documentation for people, and the reference for AI coding assistants that ships in the package. |
| `CHANGELOG.md`, `CONTRIBUTING.md`, `SECURITY.md` | Changes per version, how to contribute and release, how to report a vulnerability. |

Inside the library, code is grouped by feature: `Chat` (the session and the rows it builds), `Markdown` (the renderer),
`Controls` (the chat view and its rows), `Theming` and `Platform`. [docs/architecture.md](docs/architecture.md) explains
each part.

## Commands

```bash
dotnet build Plugin.Maui.AiChat.slnx                         # every TFM this OS can build
dotnet test --project tests/Plugin.Maui.AiChat.Tests         # unit tests (Microsoft.Testing.Platform, see global.json)
dotnet test --project tests/Plugin.Maui.AiChat.Tests --coverlet --coverlet-threshold 100 \
  --coverlet-threshold-type line,branch --coverlet-threshold-stat total   # fails below 100% line or branch coverage
cd tests/Plugin.Maui.AiChat.UITests && npm ci && ./run.sh ios           # UI tests (also: android, maccatalyst)
dotnet pack src/Plugin.Maui.AiChat -c Release -o artifacts              # NuGet package
```

The Windows target builds by default only on Windows. To compile it on macOS or Linux, run:

```bash
AICHAT_SINGLE_TARGET=net10.0-windows10.0.19041.0 dotnet build src/Plugin.Maui.AiChat -p:EnableWindowsTargeting=true
```

## Conventions

- **Platform code by file suffix.** `*.android.cs`, `*.macios.cs` (iOS and Mac Catalyst), `*.windows.cs` and `*.net.cs`
  (the plain `net10.0` target) compile only for their target. Every other `.cs` file is shared.
- **Decisions belong in shared code.** Platform files are thin adapters. The only ones are the monospace font name, the
  Android window's keyboard mode and the Android scroll to the end; the clipboard and the launcher go through
  `PlatformActions`.
- **Public API.**
  - Default to `internal`. Public API must have a demonstrated use.
  - Every public member has XML documentation; a missing doc is a build error.
  - Every public member is listed in `PublicAPI/<target>/PublicAPI.Unshipped.txt` (Microsoft.CodeAnalysis.PublicApiAnalyzers).
    Run `eng/update-public-api.sh` after an API change.
- **No reflection.** The library is marked `IsAotCompatible`. Bindings in code use the lambda overloads of `SetBinding`
  and `Binding.Create`, which MAUI's source generator compiles.
  - The generator writes back through any setter it can see, even a private one, so a property that views bind to and
    only the library may change is get-only (`AiChatSession.IsBusy`, `StreamedTextItem.Text`).
  - Its getter may only access properties: no `!`, no method calls.
- **Threading.** The session changes its rows only on the UI thread; `SendAsync` hops there.
- **Model output is untrusted.** Keep the link scheme check, never load images from Markdown, and never render HTML.
- **Comments explain why**, not what.

## Lessons from devices

These came from the UI tests; the unit tests cannot see them.

- **No horizontal `ScrollView` inside a list row.** On Android it takes the vertical drags meant for the list, and on
  both platforms it keeps the height it measured first while its content grows. Code wraps and table columns share the
  width instead.
- **Only a page honors the keyboard in its safe area** in .NET MAUI 10, and an Android window pans by default.
  `KeyboardSafeArea` handles both; check the keyboard on both platforms after layout changes.
- **The list measures a row after its content changes**, so following the end reacts to rows' `SizeChanged`, and a row
  that shrinks for a moment moves the offset up without the user. `FollowEnd` tells those apart.
- **The app's implicit styles reach the view's parts** (an explicit `Style` does not keep them out in MAUI 10), so the
  views set every property that matters for their look.

## Tests

- **Write the failing test first**, then the code.
- **Unit tests** run on `net10.0` without a device. The coverage gate is 100% line and branch for the shared code.
  `ScriptedChatClient` answers with scripted streams; tool approval is tested against the real
  `FunctionInvokingChatClient`.
- **UI tests** check what only a device shows: the keyboard, scrolling, following the end, taps in list rows, and
  rendering (screenshot baselines of restored conversations, light and dark). Record baselines with
  `--update-baselines` and review the images before committing them.
- On Android, `run.sh` uses the running emulator by its serial and never a connected phone.

## Documentation

- When the public API or its behavior changes, update `README.md`, `llms-full.txt` and `CHANGELOG.md` in the same
  change, and `docs/architecture.md` when the internals change.
- The C# samples of `README.md` and `llms-full.txt` are copied into `tests/Plugin.Maui.AiChat.Tests/Docs`, so they keep
  compiling. Change both together.
- The docs describe the package itself. They do not compare it with or judge other packages.

## Commits

- Use Conventional Commits: `feat:`, `fix:`, `perf:`, `test:`, `docs:`, `ci:`, `chore:`.
- No `Co-Authored-By` or other attribution trailers.
- Keep commits small and buildable.
- Releases are made by tagging a commit on `main`; see CONTRIBUTING.md. Never push tags or publish packages without the
  maintainer's explicit approval.

## Agent skills

`.claude/skills` holds pinned copies of the official MAUI and .NET skills from dotnet/maui-labs, dotnet/skills and
davidortinau/maui-skills; their sources and licenses are listed in `.claude/skills/README.md`.
