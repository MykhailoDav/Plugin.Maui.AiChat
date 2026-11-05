# Changelog

All notable changes to this package are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-11-05

The first release.

### Added

- `AiChatSession`, a conversation with any `Microsoft.Extensions.AI` `IChatClient`: streamed answers, `Stop`, `Clear`,
  `Load`, Retry after a failure, the history as `Messages`, `IsBusy` and `ResponseCompleted`. Services that keep the
  conversation (`ConversationId`) get only the new messages.
- Tool calls as cards with their arguments and status, and Approve and Reject for `ApprovalRequiredAIFunction` through
  `FunctionInvokingChatClient`. Sending a message instead of answering rejects the waiting requests.
- Reasoning (`TextReasoningContent`) as a collapsed *Thinking* section.
- `AiChatView`, the chat for a session: Markdown answers, Copy, Send and Stop, an empty view, an accent color, light and
  dark themes, and automation ids for UI tests. It keeps the message field above the keyboard, follows the end of a
  streaming answer and stops following when the user scrolls up.
- `MarkdownView`: CommonMark with pipe tables, strikethrough, task lists and autolinks, as native views. While
  `IsStreaming` is set, unfinished markup renders as if closed and only the blocks that changed are rebuilt. Links open
  only for `http`, `https` and `mailto`.
