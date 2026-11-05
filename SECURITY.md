# Security policy

## Supported versions

Security fixes go into the latest release only.

## Reporting a vulnerability

Please do not open a public issue for a vulnerability. Report it privately through GitHub instead: on the
repository's **Security** tab, choose **Report a vulnerability**
([direct link](https://github.com/MykhailoDav/Plugin.Maui.AiChat/security/advisories/new)).

Include what an attacker can do, the version you tested, and the smallest code or input that shows the problem.
You should get an answer within a week. Once a fix is released, the advisory is published with credit to you, unless
you prefer to stay anonymous.

## Scope

A model's output is untrusted: a prompt injection can make it write anything. The parts of the package that handle it:

- **Markdown rendering** (`MarkdownView`, and the answers in `AiChatView`).
  - Links open only for `http`, `https` and `mailto` URLs, in the system browser or mail app, and only when the user
    taps them. A link that opens anything else, or opens without a tap, is a vulnerability.
  - Images are never loaded, and HTML is shown as text.
  - Markdown that makes the view hang, crash the app or use far more memory than its size suggests is a vulnerability.
- **Tool approval.** A call of an `ApprovalRequiredAIFunction` must not run before the user taps Approve; a call that
  does is a vulnerability. The card shows the call's arguments as the model sent them, so the user sees what they
  approve.

Out of scope: what the model says, what your tools do once approved, and API keys you ship inside an app (see the
README's note on keys).
