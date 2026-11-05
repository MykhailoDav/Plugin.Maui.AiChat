# Architecture

How Plugin.Maui.AiChat works inside. The README describes how to use it.

```
AiChatView (ContentView)                              AiChatSession (no UI; lives in a view model)
 ├─ CollectionView ── ItemsSource ──────────────────►  Items: ObservableCollection<ChatItem>
 │    ChatItemTemplateSelector: one template per kind     built by ChatItemBuilder from the MEAI contents
 │      UserMessageView, AssistantMessageView,          Messages: List<ChatMessage>, the MEAI history
 │      ReasoningView, ToolCallView, ErrorView          SendAsync / Stop / Clear / Load, internal Respond and Retry
 └─ ChatInputBar: Editor + Send/Stop ──────────────►
MarkdownView (ContentView)
  Text ─► StreamingMarkdown.Heal (while IsStreaming) ─► Markdig ─► diff of top-level blocks ─► MarkdownRenderer
```

## The session

`AiChatSession` keeps two things in step:

- **`Messages`**, the history in `Microsoft.Extensions.AI` form. A request sends it (or, when the service returned a
  `ConversationId`, only the messages after the last response, with the id in a copy of `Options`). A response is added
  with `AddMessages(updates)`, which merges the streamed updates into messages the way MEAI does everywhere.
- **`Items`**, the rows the view shows, built by `ChatItemBuilder` from the same contents. Sent and loaded messages go
  through `AddMessage`, streamed updates through `ApplyUpdate`; both end in one `Apply` per content, so a restored
  conversation looks as it did live.

| Content | Row |
| --- | --- |
| user `TextContent` | `UserMessageItem`, all of the message's text |
| assistant `TextContent` | `AssistantMessageItem`; text of the same message appends to it |
| `TextReasoningContent` | `ReasoningItem`, collapsed; it stops streaming when anything else arrives |
| `FunctionCallContent`, other `ToolCallContent` | `ToolCallItem`, Running, found again by `CallId` |
| `ToolApprovalRequestContent` | `ToolCallItem`, AwaitingApproval, with the request |
| `ToolApprovalResponseContent` | Running or Rejected on its row |
| `FunctionResultContent`, other `ToolResultContent` | Completed, or Failed when it carries an exception; a rejected row stays Rejected |
| `ErrorContent` | `ErrorItem` without Retry |

"The same message" is the update's `MessageId` while streaming and the `ChatMessage` itself when loading, so two saved
messages never merge into one row.

**A response** (`RunCoreAsync`) streams on the UI thread: `SendAsync` called elsewhere hops to the dispatcher the session
was created on. When the stream ends:

- a stop or a failure keeps what arrived, but drops tool calls without a result and approval requests from the history,
  because most providers reject a request with an unanswered call;
- rows stop streaming, calls still running become Canceled, and a failure adds an `ErrorItem` with Retry;
- `ResponseCompleted` reports the combined `ChatResponse`.

Retry removes what the failed response added, to the history and the rows, and runs the same request. `Clear` and `Load`
change a generation counter, so a response they cut off leaves the new conversation alone even if its client ignores
cancellation; the run is detached before it is canceled, because a canceled stream can resume inside `Cancel()`.

**Approval** follows the MEAI protocol. `FunctionInvokingChatClient` turns a call of an `ApprovalRequiredAIFunction` into
a `ToolApprovalRequestContent` and ends the response. Approve or Reject answers the row (`ToolApprovalRequestContent.CreateResponse`);
when no request is left waiting, the answers go to the model in one user message, and the function-invoking client runs
or rejects the calls. Sending text instead rejects the waiting requests and sends the answers with the text.

## Markdown

`MarkdownView` renders on the dispatcher, one pending render at a time; while `IsStreaming`, the render waits 80 ms, so
the tokens of a burst share it.

1. **Healing** (`StreamingMarkdown.Heal`, only while streaming). One pass over the lines tracks code fences, the inline
   state of the current paragraph (code spans and a stack of emphasis delimiters, with CommonMark's flanking rules) and
   a link URL that has not closed. If a fence is open, the text stays as it is, because Markdig renders an open fence
   to the end. Otherwise the open constructs are closed in reverse order, before trailing whitespace, and an opener with
   nothing after it is removed instead.
2. **Parsing** with Markdig: CommonMark plus pipe tables, strikethrough, task lists and autolinks.
3. **Diffing.** Each top-level block owns the source up to the next block (the span of an open fence stops early). A block
   whose source did not change keeps its view. A paragraph, a heading of the same level and a code block of the same
   language are updated in place; anything else gets a new view.
4. **Rendering** (`MarkdownRenderer`). Paragraphs and headings are labels with formatted spans; text of the same style
   merges into one span. Code blocks are a bordered stack with the language and Copy; tables a grid whose columns share
   the width; lists a grid per item; quotes a bar and the children. Spans set font attributes only when they have some,
   so they take the bold of a heading or a table header.

## The view

- **Rows.** The template selector makes one template per kind, as recycling requires; each row binds to its item with
  compiled bindings, and to the view (`AccentColor`) with the view as the source.
- **Following the end.** The view scrolls to the last row when rows are added, when text grows and when a row's size
  changes (the platform measures a row after its content changes), at most once per render interval. `FollowEnd`
  decides whether to: an upward move of the offset that a row's shrink explains is layout; any other upward move is the
  user, and following stops at once, so the next scroll does not pull the list back from under their finger. Scrolling
  back to the end, or sending, starts it again. On Android, a scroll to the end of a row taller than the list stops at
  the row's top, so a plain scroll finishes it.
- **The keyboard.** On `Loaded`, `KeyboardSafeArea` adds `SoftInput` to the bottom safe area of the view's page (only a
  page honors it in MAUI 10) unless the app set the page's safe area, and on Android switches the window from panning to
  resizing until the view unloads.
- **Weak references.** The view follows `IsBusy` through a binding and `ItemsChanged` through a `WeakEventManager`, so
  a session kept by a view model does not keep a closed page alive.
