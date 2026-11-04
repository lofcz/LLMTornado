# Provider sync audit — 2026-10-21

Status: **in progress**. This is a requirement checklist, not a claim of completion.
Review window: August 21–October 21, 2026, with older gaps considered when needed to support the current APIs.

## Anthropic follow-up — November 4, 2026

Reviewed the [Claude release notes](https://platform.claude.com/docs/en/release-notes/overview)
since the October 21 sync (`98121a90`), including the October 20 profile/webhook additions absent
from that implementation. This follow-up does not close unrelated items in the original audit below.

| Release | Implementation or disposition |
| --- | --- |
| October 20–21: user profiles and enforcement | `AnthropicUserProfiles` exposes create/get/list/update/delete and current enforcement actions. Chat vendor options, `TokenizeRequest`, and batch vendor options send profile attribution as a header. Batch attribution applies to all items. Empty filtered pages retain `next_page`. |
| October 20: organization webhooks | `AnthropicOrganizationWebhooks` exposes endpoint CRUD and secret regeneration with the required beta. Typed enforcement events and `AnthropicWebhookSignatureVerifier` verify raw-body HMAC signatures and timestamp tolerance. |
| October 20: inference hooks and organization web restrictions | Server configuration and enforcement changes; existing Managed Agents web/network options still apply. No new client request field is required. |
| October 21: API revision | `AnthropicEndpointProvider.ApiRevision = AnthropicApiVersions.October2026` opts in. Messages and batch items omit removed sampling fields; chat, batches and token counting reject forbidden content. Rate-limit responses support canonical `group` and legacy `group_type`. The legacy revision remains the default to preserve existing sampling behavior. |
| October 22: Anthropic-managed Google Cloud | `AnthropicEndpointProvider.GoogleCloud` selects workspace or organization URLs. Its `AccessTokenProvider` supplies a fresh Google bearer token per request. No API-key or workspace header is sent. Credential acquisition and refresh belong to the caller; no Google authentication package is added. |
| October 23: Claude Tag | `AnthropicClaudeTag` supports channel setup/list/get/update/delete, workspace list/get/update, bundle list/get, and bundle-target list/get/attach/detach. Native settings/provenance and Slack/resource IDs remain distinct. Assigning null to a settings update restores inheritance; unassigned fields are omitted. |
| October 28: 128 MB requests | The transport imposes no 32 MB request cap. No client limit needed changing. |
| October 29: usage/cost analytics | `AnthropicAnalytics.Usage` and `.Cost` use the dedicated beta, repeated bracket filters, opaque cursors, typed dimensions/actors and 64-bit token counters. Cost amounts remain exact decimal strings in cents. Legacy reporting APIs remain supported by Anthropic; there was no legacy reporting surface here to migrate. |
| October 30: Opus 4.8 fast mode | Documented November 30 removal and Opus 5.5 replacement. Requests remain possible before retirement; the Opus 4.8 model itself is not marked retired. |
| November 2: message threads | `Chat.CreateAnthropicThread` owns full native history, streams turns, replays once on explicit expiry/conflict errors, rolls back refused input, restarts after fallback, and supports independent forks/export/restore. Optional unsupported-request fallback switches to full unthreaded requests. Low-level `AnthropicMessageThread.Create/Continue` remains available. Threads are rejected in batches; token counting requires full history. |
| November 3: watermark | Provider-side output marking; no request or response schema change. |

### Using the new protocol and threads

```csharp
var provider = new AnthropicEndpointProvider
{
    Auth = new(LLmProviders.Anthropic, apiKey),
    ApiRevision = AnthropicApiVersions.October2026
};
var api = new TornadoApi(provider);
var request = new ChatRequest
{
    Model = ChatModel.Anthropic.Claude5.Opus55,
    MaxTokens = 4096,
    Messages = [new(ChatMessageRoles.User, "Summarize this task.")],
    VendorExtensions = new(new ChatRequestVendorAnthropicExtensions
    {
        UserProfileId = profileId
    })
};
var thread = api.Chat.CreateAnthropicThread(request, continueUnthreaded: true);
var first = await thread.Send();
var second = await thread.Send([new(ChatMessageRoles.User, "What should happen next?")]);

await foreach (var chunk in thread.Stream([new(ChatMessageRoles.User, "Explain the next step.")]))
{
    // Consume chunks as with StreamChatEnumerable.
}
var completed = thread.LastMessage;
var snapshot = thread.Export(); // Native blocks and fixed parameters; persist in application storage.
var fork = thread.Fork();       // Independent local copy; first send creates a new server thread.
var restored = api.Chat.CreateAnthropicThread(snapshot);
```

The session keeps native response blocks, including signed thinking and server-tool results, so replay
preserves information that a conversion through shared chat messages could lose. It snapshots the
prototype and feature headers. `Update` accepts native parameter fields; after a response it rejects
changes to fixed fields, while allowing request limits, MCP authorization tokens, and container IDs to
change. Fork before changing fixed fields. Tool calls are returned for the application to execute;
the session does not run CLR tool delegates or a tool loop.

Only `thread_not_found` (404), `thread_already_continued` (400), and `thread_continue_in_flight` (400)
trigger a single replay. Unrelated 404s and fingerprint errors propagate. The optional
`continueUnthreaded` policy retries an unsupported request without `thread`, and disables threads if
the first continuation immediately after replay is missing again. Earlier message cache breakpoints
are removed from replay; new message, system, tool, and top-level breakpoints are retained.

A refusal rolls back messages supplied to that send, preserves previously staged input, and starts a
new thread next time. Fallback responses remain in history but also require a new create. Call `Send()`
without messages to resume `pause_turn`. Stream continuation advances only on `message_stop`.
Disposing or cancelling a partial stream retains completed blocks and received text, drops unfinished
tool inputs/thinking, and resets continuation. Unexpected EOF raises `IOException`. Nothing is retried
after streamed data arrives; `Send()` with no new messages retries the interrupted turn without
including its partial assistant response. A new user/tool-result message instead keeps the partial
response in history. A session rejects overlapping operations until its stream has been disposed.

For applications that already own history and recovery, `AnthropicMessageThread.Create/Continue`
exposes the wire protocol on ordinary chat requests. That low-level API never automatically replays,
rolls back input, or advances a caller-supplied ID. Structured errors are available through
`AnthropicErrorResponse.Parse(result.Response)`.

Implementation sources: [API migration](https://platform.claude.com/docs/en/api/migration-2026-10-21),
[message threads](https://platform.claude.com/docs/en/build-with-claude/message-threads),
[user profiles](https://platform.claude.com/docs/en/manage-claude/user-profiles),
[organization webhooks](https://platform.claude.com/docs/en/manage-claude/organization-webhooks),
[analytics](https://platform.claude.com/docs/en/manage-claude/usage-cost-analytics-api),
[Claude Tag](https://platform.claude.com/docs/en/manage-claude/claude-tag-api), and
[Google Cloud](https://platform.claude.com/docs/en/build-with-claude/claude-models-on-google-cloud/anthropic-managed).

Validation: **365 offline tests passed**, including **66 new contract/lifecycle tests**; **20 credentialed tests
were skipped**. Local HTTP tests cover native routes, revision/beta/profile headers, repeated filters,
exact cost strings, null updates, provider errors, and complete multi-request thread lifecycles. Thread
checks exercise expiry/conflict recovery, refusal rollback, fallback, pause/resume, forks, independent
snapshots, cache replay, cancellation, early disposal, truncated streams, and signed/tool block replay. The broader
initial selection also picked up 54 demo tests without local API configuration; these are excluded
from the offline result. Its remaining task-budget test exposed an existing unset-remainder
serialization issue, fixed by omitting `remaining` when null. No credentialed provider calls were used.
The final SDK build passed on net10.0, net8.0, net462, and netstandard2.0 with zero errors;
existing repository warnings remain. `git diff --check` passed.

## Baseline evidence

The starting worktree was clean at `3c5651a8`. There is no single uniform last-sync date:

- `df1d57d3` (September 20, 2026, `sync`) last changed the Google and Anthropic model catalogs and added broad provider capabilities (262 files).
- `e1555804` (October 3, 2026, `Update OpenAI models and Realtime support`) last changed the OpenAI catalog.
- Earlier broad API work: `838c243b` / `2e3dbf36` (June 1) and `9001542c` (June 7, Anthropic).
- Later fixes, including October 21 finish-reason changes, are not evidence of a complete provider sync.

Canonical release ledgers:
[OpenAI](https://developers.openai.com/api/docs/changelog),
[Gemini](https://ai.google.dev/gemini-api/docs/changelog),
[Anthropic](https://platform.claude.com/docs/en/release-notes/overview).

## Build report

The pasted report names CS0029 on `net462` and `netstandard2.0` in the finish-reason map.
`System.Collections.Immutable` 8.0.0 is already a dependency for these targets and supplies
`System.Collections.Frozen`. Remove the duplicate `LegacyFrozen.cs` implementation;
use the dependency's comparer-aware implementation across all targets.

Validation: all four SDK targets built with zero errors after removal. Existing repository warnings remain.
Case-insensitive lookup is covered by `FinishReasonMapTests` and `VendorFinishReasonMapTests`.

## Implemented in the working tree; focused regressions passed

- Claude Opus/Sonnet/Haiku 5.5 registration and capability membership.
- Claude 5.5 adaptive thinking and sampling policies; Opus always-on thinking;
  Sonnet `between_tools`; forced tool choice normalization for Opus/Sonnet;
  Haiku disabled-thinking effort limit.
- Gemini 4 Argon, Nano Banana 2.1, Flash 3.8 TTS and Flash-Lite 3.8 TTS catalog entries.
- Gemini reasoning, image, speech, grounding, and computer-use capability membership.
- Nano Banana 2.1 image catalog and video-to-image eligibility.
- Responses request cache options and content breakpoints with 30-minute/12-hour TTLs,
  serialization, deserialization, and request-copy preservation.
- Anthropic Models API `line`, disabled/between-tools thinking, and server-tool capabilities.
- Native OpenAI Decisions under `Decision.OpenAi`, including ordered predicate/choice/score questions,
  typed boolean/string choices, probabilities, per-question refusals, usage, multimodal input,
  cache partitioning and safety identifiers. Its dedicated cache TTL is restricted to 30 minutes;
  the Responses-only 12-hour TTL is not exposed on Decisions.
- GPT-Live Decisions delegation, transcript triggers, shared decision items, parsed lifecycle events,
  callback dispatch, session snapshot deserialization, and WebRTC data-channel event permissions.
- OpenAI prompt-to-voice creation under `Audio.OpenAiVoices`, saved-voice ID objects,
  and the complete current built-in Live voice list. Existing enum numeric values are preserved.
- Correct absolute provider URLs for Live fork/accept/reject/refer/hangup and recording download.
- Anthropic signed on-demand compaction: request parameter, dedicated beta selection, nonstreaming
  parsing, whole-block streaming, signature-preserving history replay, and mutual exclusion with threshold compaction.
  Conversation streaming preserves leading whitespace in signed summaries even with TrimResponseStart enabled.
- Anthropic GA header policy for cache diagnostics and Opus 5.5 fast mode; explicit first-turn diagnostics opt-in.
- Native Gemini image text generation/editing through generateContent, with inline reference images,
  resolution/aspect ratio/thinking settings and video-to-image compatibility.
- Argon continuation-hop aggregation and queued status, medium default thinking,
  and implicit-only cache validation; October Flash/image replacement metadata.
- Gemini 3.8 TTS through `Audio.CreateSpeech` and `Audio.StreamSpeech`, with decoded audio,
  output metadata, styled verbatim turns, speaker mappings, saved voices and stateless voice keys.
- Google voice design, replication, retrieval, deletion and filtered/paginated discovery through `Audio.GoogleVoices`.
- Interactions audio annotations/configuration, audio delta callbacks, cancellable SSE reads,
  JSON lifecycle event discrimination, stream disposal, and absolute retrieval/action URLs.
- Responses prompt cache diagnostics, comparison IDs and request copies, including completed streaming snapshots.
- Responses async function/custom tool definitions, call metadata, and output-to-input replay. Async delegate
  calls are left for the application to schedule instead of blocking response delivery.
- GPT-6 `configuration_update` input items for reasoning effort, with dedicated effort-only configuration.
- Preserve function-tool `defer_loading` during serialization; the previous converter read the field but omitted it on writes.
- Native Gemini transcription through `Audio.CreateTranscription` and asynchronous request serialization,
  with file URIs, inline bytes/streams/data URLs, language hints, custom vocabulary, smart/verbatim modes,
  diarization, word timings and original duration strings. Invalid mode/vocabulary combinations are rejected
  before reading caller audio; caller streams remain open.
- Anthropic inline tool additions/removals by reference or full function/server-tool/MCP-toolset definition,
  ordered among system-message text blocks, with automatic September beta headers.
- MCP fetched tool listings in JSON and streamed responses, stable history replay and explicit toolset pinning,
  including an empty pinned list. Inline MCP toolsets are not exposed before their addition message.
- Thinking input transformation reports, diagnostic-only opt-in, and final serving-model entries after streaming fallback.
- Managed Agents `multiagent_20261001` with independent inline/predefined subagent and workflow controls,
  advisor configuration, and backward-compatible coordinator rosters. Versioned-reference converters no
  longer recursively invoke themselves during serialization/deserialization.
- Managed Agents toolset default/per-tool permission policies and server evaluation metadata, web search/fetch
  restrictions, approximate search location, limited sandbox networking and preinstalled package lists.
- Session budgets represented as exact whole-cent strings; omitted updates preserve the cap, while
  `ClearBudget` sends an explicit null. Typed usage and workflow lifecycle/phase/result/error metadata.
- Managed Agents thread list/get/archive/event history/stream methods, native page/next_page cursors,
  repeated event/status filters, typed accepted-event/deletion responses, and compatibility with existing list APIs.
- Managed Agents session/thread preview streams, separate preview/workflow/tool callbacks, data-only SSE
  parsing, cancellation of pending reads and transport disposal. Streams keep yielding after idle events.
  Resource methods now resolve absolute provider URLs, and the existing boolean Delete method maps the
  native session_deleted confirmation instead of trying to deserialize it directly as a bool.
- Ordered Anthropic text/client-tool assistant turns, preserving normal/redacted/omitted thinking,
  signatures and MCP listing positions in JSON and streamed history. Client tool positions reference
  the public tool-call list, so edits to its arguments are respected without duplicating calls.
- Stream thinking-start dispatch, fragmented signatures, initial whole-block payloads, and thinking/text
  deltas after a tool block. Rich conversation tool notifications carry the complete turn before tool
  callbacks run, and plain streaming keeps one authoritative history message without rendering it twice.
- Reasoning data and provider identity survive serialization of message parts. Binding behavior now uses
  the canonical error/drop_block values while preserving the existing Reject/Drop enum names and numeric
  values; reading old saved enum names remains supported. Invalid disabled/between-tools combinations
  are rejected, and always-on adaptive models retain explicitly requested binding controls.
- Responses WebSocket steering with the exact three-field client envelope, text/image/file user input,
  typed accepted/pending/failed events, all required tool/approval input kinds and extensible error/reason fields.
- CreateResponseAsync follows automatic successors after either a steered incomplete response or normal
  completion. It returns control for required client input, or continues reading when a callback sends an
  explicit tool-result request. Multiple queued steers for one parent do not cause automatic duplicate requests.
- Low-level SendResponseAsync/ReadEventsAsync for explicit lifecycle control and multiplexed stream_id lanes;
  per-lane continuation state, active IDs, raw envelopes for unknown events and deterministic single-reader
  ownership. Steering itself never includes stream_id: the target response selects its lane.
- Steering journal tracks unacknowledged, accepted, waiting, committed, failed and unknown submissions.
  A successor's created event commits accepted input; disconnects leave uncommitted outcomes unknown.
- WebSocket incomplete/failed response state, nested protocol errors and headers, fragmented messages,
  cancellation/disposal and transport-safe callbacks. Client-side processing follows the request bound to
  each response, including explicit continuations with different tools or formatting settings.


Implementation specifications:
[Opus 5.5](https://platform.claude.com/docs/en/models/opus-5-5/whats-new-opus-5-5),
[Sonnet 5.5](https://platform.claude.com/docs/en/models/sonnet-5-5/whats-new-sonnet-5-5),
[Haiku 5.5](https://platform.claude.com/docs/en/models/haiku-5-5/whats-new-haiku-5-5),
[Argon](https://ai.google.dev/gemini-api/docs/latest-model),
[Nano Banana](https://ai.google.dev/gemini-api/docs/models/gemini-nano-banana-2.1),
[Flash TTS](https://ai.google.dev/gemini-api/docs/models/gemini-3.8-flash-tts),
[Flash-Lite TTS](https://ai.google.dev/gemini-api/docs/models/gemini-3.8-flash-lite-tts),
[OpenAI cache controls](https://developers.openai.com/api/docs/guides/prompt-caching).
Additional specifications: [native Decisions](https://developers.openai.com/api/docs/guides/decisions),
[Live Decisions](https://developers.openai.com/api/docs/guides/decisions-voice),
[Live API reference](https://developers.openai.com/api/reference/resources/live/methods/create),
[prompt-to-voice](https://developers.openai.com/api/docs/guides/custom-voices),
[on-demand compaction](https://platform.claude.com/docs/en/build-with-claude/compaction-on-demand),
[cache diagnostics GA](https://platform.claude.com/docs/en/build-with-claude/cache-diagnostics),
[fast mode](https://platform.claude.com/docs/en/build-with-claude/fast-mode),
[native Gemini images](https://ai.google.dev/gemini-api/docs/image-generation),
[Gemini speech](https://ai.google.dev/gemini-api/docs/speech-generation),
[voice design](https://ai.google.dev/gemini-api/docs/voice-design),
[voice replication](https://ai.google.dev/gemini-api/docs/voice-replication),
[Voices API](https://ai.google.dev/api/voices),
[Interactions OpenAPI contract](https://ai.google.dev/static/api/interactions.openapi.json).
Responses specifications: [cache diagnostics](https://developers.openai.com/api/docs/guides/prompt-caching/diagnostics),
[async tools](https://developers.openai.com/api/docs/guides/async-tool-calling),
[configuration updates](https://developers.openai.com/api/docs/guides/reasoning#change-reasoning-mid-conversation).
Further specifications: [Gemini transcription](https://ai.google.dev/gemini-api/docs/transcribe),
[inline tools](https://platform.claude.com/docs/en/build-with-claude/mid-conversation-system-messages),
[MCP listing and pinning](https://platform.claude.com/docs/en/agents-and-tools/mcp-connector#pin-an-mcp-servers-tool-list-beta),
[thinking transformations](https://platform.claude.com/docs/en/build-with-claude/preserved-thinking),
[Anthropic stream metadata](https://platform.claude.com/docs/en/build-with-claude/streaming).
Managed Agents specifications:
[multiagent configuration](https://platform.claude.com/docs/en/managed-agents/multiagent-orchestration),
[workflow runs](https://platform.claude.com/docs/en/managed-agents/workflow-runs),
[permission policies](https://platform.claude.com/docs/en/managed-agents/permission-policies),
[web restrictions](https://platform.claude.com/docs/en/managed-agents/tools-web-restrictions),
[environment configuration](https://platform.claude.com/docs/en/managed-agents/environments),
[session budgets](https://platform.claude.com/docs/en/managed-agents/budgets),
[session threads](https://platform.claude.com/docs/en/managed-agents/session-threads),
[event previews](https://platform.claude.com/docs/en/managed-agents/event-deltas),
[native deletion response](https://platform.claude.com/docs/en/api/beta/sessions/delete).
Thinking replay specifications:
[preserved thinking and prefix binding](https://platform.claude.com/docs/en/build-with-claude/preserved-thinking),
[thinking, omitted output and redacted blocks](https://platform.claude.com/docs/en/build-with-claude/thinking).
OpenAI WebSocket specifications:
[mid-turn steering](https://developers.openai.com/api/docs/guides/steering),
[WebSocket client/server events](https://developers.openai.com/api/reference/resources/responses/websocket-events).


## Remaining implementation and coverage audit

Each row must be checked against actual request/response/event code and meaningful tests.
An existing model ID or generic JSON escape hatch alone does not prove feature coverage.

| Provider | Area | Required next check |
| --- | --- | --- |
| OpenAI | Catalog | Verify all August–October model IDs, snapshots, endpoint capabilities, and deprecations; GPT-6 family and image/audio updates already exist at baseline. |
| OpenAI | Responses | Async tools, configuration updates, cache diagnostics/write usage, WebSocket steering and lane send/read support implemented. Native multi-agent and safety alerts remain. |
| OpenAI | Agents API | Compare existing managed-agent endpoint with September API; add missing session, sandbox prewarming, computer-use and service-tier controls. |
| OpenAI | Live | Core October additions implemented and locally tested. Final contract audit for remaining startup fields and hosted Responses tool subset still required. |
| OpenAI | Decisions | Native contract and optional cache/safety fields checked against the create reference; local tests cover text/image inputs, questions, answers, refusals, usage and routing. |
| OpenAI | Transport | Regional processing, custom certificate/identity options, new errors and Retry-After preservation. |
| Google | Argon | Unified context and long-decode output aggregation; background execution; sampling, thinking defaults/budgets, implicit-only cache restrictions; signatures and combined tools. |
| Google | TTS/Voices | Native Interactions and public audio paths implemented and tested; local contracts cover formats, streaming/cancellation, design/replication and voice-library CRUD/filter pagination. |
| Google | Images | Native generation/editing routing implemented and tested; finish auditing multi-turn thought signatures, grounding, and public HTTP path coverage. |
| Google | Transcription | Native unary Interactions routing, configuration and word/speaker annotations implemented and locally tested. Live transcription still needs its own contract audit. |
| Google | Existing September additions | Audit Live 3.8 async tools/client updates, Lyria 3.5, agentic video, Omni extension/interpolation/resolution and Antigravity September tools. |
| Google | Lifecycle | October Flash/image/deep-research deprecations and current replacement IDs. |
| Anthropic | Model migrations | Text/client-tool thinking replay/binding implemented and locally tested. Audit full native server-tool/advisor replay, toolset selection, prefill handling and token counting for new models. |
| Anthropic | Messages | On-demand compaction, GA headers, inline tool definitions/references, MCP listing replay/pinning and input transformations implemented and tested. |
| Anthropic | Managed Agents | Dynamic configuration/events, auto permission evaluations, web/network configuration, budgets, native pagination, thread access and previews implemented and tested. Scheduled deployments and remaining platform resource coverage still need an audit. |
| Anthropic | Files/Skills | GA shapes and beta compatibility; verify September sync already handles them. |
| All | Administrative/server changes | Account for changelog entries involving console, billing, compliance, governance, telemetry and external SDKs; distinguish hosted changes from library work with evidence. |
| All | Validation/docs | Complete regression tests, all-target final build, FeatureMatrix and CHANGELOG updates; do not mark sync complete before this checklist is resolved. |

## Test environment

SDK: .NET 10.0.400. Restored SDK and test dependencies because the initial package cache was incomplete.
The first test compilation exposed an `IModel`/`ChatModel` test cast issue, corrected in the working tree.
The next run compiled but could not launch because the machine only had .NET 10 runtime.
Installed .NET 8.0.32 ASP.NET Core and .NET runtimes for the existing net8.0 test project.
No provider credentials are required for the added serialization/catalog tests.

Focused run: **83 passed**, including October2026ProviderSyncTests, finish-reason mapping,
AnthropicLatestCapabilityTests, AnthropicClaude5ModelTests, AnthropicModelsApiTests, and Gemini38ModelTests.
This is not full feature-audit coverage. Final build for this implementation pass: **zero errors** on net10.0, net8.0, net462, and netstandard2.0 (existing warnings remain).

The new tests also exposed reference-identity comparisons for Google model names supplied as strings,
and sampling policies that ran in conversation preparation but not direct request serialization; both
paths are corrected. Responses usage now reads the canonical plural `input_tokens_details` and
`output_tokens_details` fields, retains legacy singular-field reading, and exposes `cache_write_tokens`.


## Current validation pass

Added `OpenAiNativeDecisionAndVoiceTests`, `AnthropicOnDemandCompactionTests`, `GeminiOctober2026Tests`,
and `GeminiSpeechAndVoiceTests`.
Local HTTP tests verify native Decisions/voice paths, authorization, response/error handling,
and Live REST call-control paths. Stream tests verify complete signed compaction blocks without deltas.
The completed pre-speech regression run passed **159 tests**. All four SDK targets built with zero errors.
The Google speech/Voices pass passed **181 tests**, including local HTTP, audio streaming/cancellation,
and discovery tests. All four SDK targets again built with zero errors. These tests require no provider
credentials and make only loopback calls.

The Responses diagnostics/async/configuration pass passed **194 focused tests** overall, including
`ResponsesOctoberContractTests`. The final SDK build passed with **zero errors** on net10.0, net8.0,
net462 and netstandard2.0. `git diff --check` also passed. This is a focused regression suite, not a
claim that every provider feature or credentialed integration has been validated.

The Gemini unary transcription pass passed **213 focused tests**. The subsequent Anthropic conversation
tools pass passed **226 focused tests**, including `GeminiTranscriptionTests` and
`AnthropicConversationToolsTests`. All four targets built with zero errors for both passes. Local HTTP
tests cover native transcription success/errors and public conversation streaming with MCP listing replay.

Next major implementation areas: OpenAI Responses multi-agent/safety alerts and Agents API;
Google Live transcription and remaining media audit; Anthropic model migration and remaining Managed Agents
platform updates. These remain part of the original goal.

The Managed Agents pass passed **276 focused regression tests** overall, with credentialed Integration
tests excluded. `AnthropicManagedAgentsOctoberTests` covers configuration/persistence, strict budget
amounts and null updates, native workflow/permission metadata, resource routing, cursor encoding,
thread operations, preview callbacks, post-idle workflow events and pending-read cancellation. It also
uses the documented session_deleted response for the existing boolean deletion API. All four SDK targets
built with **zero errors** (existing warnings remain); `git diff --check` passed.

Managed Agents runtime behavior remains server-controlled: budgets pause between model requests and
can be exceeded by in-flight work; a workflow error or session idle event does not by itself end a run.
The client exposes permission requests and evaluations without automatically confirming tool calls.
Preview callbacks carry temporary fragments; final events remain the authoritative record. Start
enumerating a stream before sending events, and use history plus event IDs to reconcile reconnects.

The speech guide documents WAV/PCM/A-law/mu-law. The linked OpenAPI schema additionally specifies
MP3 and Ogg Opus output, bit rate and delivery controls; these are mapped without transcoding.

The thinking replay pass passed **318 focused regression tests**, including
`AnthropicThinkingReplayTests`, the existing parallel-tool and stream-thinking tests, and prior provider
sync regressions. Local HTTP follow-ups compare replayed content with the original native blocks in
both JSON and streaming conversations, with manual tool results and resolving callbacks. Coverage also
includes saved part/call data, signature fragments, omitted/redacted thinking, whole-block starts,
canonical binding values and unsupported mode validation. All four SDK targets built with **zero errors**
(existing warnings remain). No credentialed provider tests ran. This does not establish exact native
replay for every hosted server-tool/advisor result shape; those remain in the audit.

The OpenAI steering/WebSocket pass passed **350 focused regression tests** overall, including
`ResponsesSteeringTests`. A real loopback WebSocket peer covers automatic successors after completed
and steered-incomplete responses, multiple queued steers, pending tool results supplied after returning
or inside callbacks, lane isolation/forks, unknown events, fragmented Unicode payloads, native error
headers, cancellation/disposal and disconnect recovery. Client-processing regressions distinguish the
current response's tool definitions from explicit continuation and queued request definitions. Shared
terminal-event dispatch is covered without an OnEvent handler. All four SDK targets built with
**zero errors** (warnings remain), and `git diff --check` passed. No credentialed provider tests ran.

The convenience CreateResponseAsync API owns one reader and follows a response chain. Applications
that multiplex lanes use SendResponseAsync with one ReadEventsAsync enumeration and route its
envelopes by StreamId. Pending events identify saved tool results or explicit approval decisions;
send one continuation per parent even when several steering submissions report it. Accepted input
is not resent automatically, and uncertain submissions are not retried after a disconnect.

## Next contract checks

- [OpenAI steering](https://developers.openai.com/api/docs/guides/steering) now follows automatic successors,
  exposes pending tool/approval requirements, and retains submission outcomes across disconnect observation.
  Native Responses multi-agent configuration/events and safety alerts still require their own contract audit.
- [Claude Managed Agents](https://platform.claude.com/docs/en/managed-agents/multiagent-orchestration)
  dynamic orchestration, workflow events, evaluations, budgets and thread streams now have typed support
  and loopback regressions. Audit scheduled deployments, deployment runs and remaining platform resources next.
- Anthropic thinking-only, redacted and omitted outputs now preserve their signatures, and text/client-tool
  turns retain block order in JSON and streaming conversations. The ordered mapper currently handles
  text, thinking, redacted_thinking, tool_use, mcp_tool_listing and compaction. Native hosted server-tool
  and advisor result blocks still use the older mapping path: audit and preserve their full native content
  before claiming every preserved-thinking workflow is covered.
