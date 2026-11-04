<!-- markdownlint-disable MD022 MD024 -->
# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Unreleased

- Add Anthropic Messages thread sessions with full native history, streaming, bounded expiry/conflict recovery, refusal rollback, fallback handling, forks, and export/restore. Retain typed low-level create/continue requests and batch/token-count safeguards.
- Support opt-in Anthropic API version `2026-10-21`, including sampling removal, message-content validation, and canonical rate-limit group metadata; preserve the legacy default.
- Add Anthropic user-profile CRUD and enforcement data, request attribution for chat/token counting/batches, organization webhooks, and signature verification.
- Add Anthropic usage/cost analytics and Claude Tag channel/workspace settings and Access bundle attachments, including native pagination and explicit-null updates.
- Support Anthropic-managed Claude on Google Cloud with separate workspace/organization routing and caller-managed bearer-token refresh; document Opus 4.8 fast-mode retirement.
- Omit an unset Anthropic task-budget remainder so the API can use the total budget as its default.
- Read Cohere's `TIMEOUT` finish reason as an error instead of failing to deserialize the response.
- Fix legacy-target builds by using the existing System.Collections.Immutable FrozenDictionary implementation instead of a conflicting local shim.
- Add Claude Opus, Sonnet, and Haiku 5.5, including adaptive and between-tools thinking policies.
- Add Gemini 4 Argon, Nano Banana 2.1, and Gemini 3.8 Flash/Flash-Lite TTS catalog entries and capability mappings.
- Add Responses prompt cache options and explicit breakpoints with 30-minute and 12-hour lifetimes; preserve cache-write usage.
- Expose Anthropic model lines, server-tool capabilities, and disabled/between-tools thinking support.
- Add native OpenAI Decisions, GPT-Live Decisions delegation and transcript triggers, and prompt-to-voice creation.
- Add nine GPT-Live voices, serialize saved voices as ID objects, and fix provider URL resolution for Live REST actions.
- Add Anthropic on-demand compaction with signed summary replay and streaming; update cache diagnostics and Opus 5.5 fast-mode GA headers.
- Route native Gemini image generation and editing through generateContent, preserve Argon long-decode continuations, and map October model replacements.
- Add native Gemini 3.8 speech synthesis and audio streaming with styled/multi-speaker turns and output formats, plus voice design, replication, and voice-library operations.
- Preserve Interactions audio metadata, dispatch audio callbacks, propagate stream cancellation, and fix resource action URLs and duplicate API revision headers.
- Add Responses prompt cache diagnostics, async function/custom tool flags and replay, and GPT-6 reasoning configuration updates.
- Route Gemini unary transcription through Interactions with native language/vocabulary controls, smart/verbatim modes, diarization and word timings.
- Add Anthropic inline tool definitions/references, MCP listing replay and pinned toolsets, and thinking transformation diagnostics for JSON and streaming responses.
- Add Claude Managed Agents dynamic subagent/workflow/advisor configuration, permission evaluations, web/network controls, session budgets, thread operations, and event previews.
- Fix Managed Agents resource URLs, native page cursors, versioned-reference converters, stream disposal/cancellation, and session deletion confirmation parsing while preserving the existing boolean API.
- Preserve ordered Anthropic thinking, redacted content, text and client tool calls in JSON/streamed history, including omitted thinking and saved message parts; avoid duplicate plain-stream history.
- Correct Anthropic thinking binding values to `error`/`drop_block`, retain existing enum names, and reject unsupported binding modes instead of silently dropping the setting.
- Add Responses WebSocket steering with typed acceptance/pending/failure events, automatic successors, required tool/approval input metadata, and a submission journal for disconnect recovery.
- Add explicit WebSocket send/read operations and lane routing; fix terminal response state, concurrent reader protection, cancellation/disposal, native protocol errors, and request-specific continuation processing.
- Google chat usage: report thoughts as completion tokens and tool-use prompt tokens as prompt tokens, and stop reporting the prompt size as the completion when `candidatesTokenCount` is absent.

## 3.8.76 - 2026-10-18

- Add text and image input to Codex app-server and direct OAuth threads, including local images and Base64 data URLs.
- Preserve image content across direct OAuth follow-up turns, function calls, and restored history.
- Read local images asynchronously with cancellation support in direct OAuth turns and `CodexOAuthHistoryItem.UserMessageAsync`.

## 3.8.75 - 2026-10-18

- Fix parallel Anthropic tool calls and duplicated thinking blocks in streams.

## 3.8.74 - 2026-10-12

- Fix Decision model initialization and Anthropic cache control on tool results.

## 3.8.73 - 2026-10-07

- Add OpenRouter alpha Decisions support for Luna Decisions, Jev 1.13, and the other registered native Decision models.
- Add provider routing, ZDR restrictions, trace metadata, generation identifiers, and request costs for OpenRouter Decisions.
- Include all output modalities in OpenRouter model queries, including models that support both text and decisions.
- Add explicit OpenRouter live tests for Luna Decisions and Jev 1.13, plus local regression tests.
- Change `DecisionScoreAnswer.Legend` from `Dictionary<string, string>` to `Dictionary<string, object>` to preserve structured descriptions.
  Consumers that require string values must handle object and array values.

## 3.8.71 - 2026-10-03

- Add OpenRouter speech models and update OpenAI models.
- **Breaking:** `GetModels` now throws request errors. Use `GetModelsSafe` for error results.

## 3.8.69 - 2026-10-01

- Add TypeSafe SystemOne Jev support and correct MCP documentation links.

## 3.8.68 - 2026-09-19

- Add LiteLLM, Codex OAuth function tools, and restorable Codex history.

## 3.8.67 - 2026-08-16

- Fix Base64 documents in OpenAI chat requests.

## 3.8.66 - 2026-08-03

- Add agent telemetry, media storage, and Ollama configuration. Correct Codex routing.

## 3.8.65 - 2026-07-27

- Add Codex app-server and direct OAuth connections with ChatGPT subscription authentication, service tiers, and protocol configuration.

## 3.8.64 - 2026-07-21

- Keep Google reasoning separate from normal response text.

## 3.8.63 - 2026-07-10

- Add GPT-5.6 and Grok 4.5 and Build models.

## 3.8.62 - 2026-07-03

- Add Anthropic Fable 5 and Sonnet 5.

## 3.8.61 - 2026-07-03

- Add `RerankResult.Results` as an alias for `Data`.

## 3.8.60 - 2026-06-09

- Preserve URL subpaths for self-hosted Chroma databases.

## 3.8.59 - 2026-06-07

- Extend Anthropic geography configuration, advisor tools, managed agents, webhooks, and stop details.

## 3.8.58 - 2026-06-01

- Add Realtime, Responses WebSocket connections, Google Interactions and Live, and managed agents. Extend media APIs, tools, caching, and compaction.

## 3.8.57 - 2026-05-19

- Add Claude Opus 4.7.

## 3.8.56 - 2026-05-01

- Add system message placement configuration and improve agent runner error handling.

## 3.8.55 - 2026-04-24

- Fix Mistral tools, Gemini batch uploads, and the InMemory cache enum value.

## 3.8.54 - 2026-03-07

- Add GPT-5.4 and Qwen 3.5 Plus. Update GPT-5.3-Codex support.

## 3.8.53 - 2026-02-27

- Add Gemini image models and complete request tokenization. Correct GPT image request formats.

## 3.8.52 - 2026-02-26

- Extend Responses request configuration and add token counting.

## 3.8.51 - 2026-02-25

- Update MCP to 1.0 and A2A to 0.3.3. Relax vector database dependency constraints.

## 3.8.50 - 2026-02-25

- Add reasoning content to Microsoft.Extensions.AI and add GPT-5.3-Codex.

## 3.8.49 - 2026-02-20

- Add Gemini 3.1 Pro Preview, improve reasoning routing, and add automatic Anthropic caching.

## 3.8.48 - 2026-02-18

- Add Claude Sonnet 4.6 and Anthropic tools. Fix audio request handling.

## 3.8.47 - 2026-02-14

- Add MiniMax, Anthropic speed mode, OpenAI batch images, and Responses compaction. Fix deferred streaming tool calls.

## 3.8.46 - 2026-02-06

- Add Claude Opus 4.6, adaptive thinking, compaction, and geography configuration. Extend Z.ai and xAI reasoning support.

## 3.8.45 - 2026-02-02

- Update Anthropic structured outputs. Extend xAI video and image editing, Z.ai media, and Groq audio and file APIs.

## 3.8.44 - 2026-01-31

- Update Gemini defaults, tool utilities, and MCP support.

## 3.8.43 - 2026-01-18

- Fix optional arguments in the agent tool runner.

## 3.8.42 - 2026-01-18

- Add Mistral OCR, GPT-5.2-Codex, Voyage 4 models, and resolved tool metadata. Improve agent handoffs.

## 3.8.41 - 2026-01-10

- Improve agent tool permission requests.

## 3.8.40 - 2026-01-05

- Support a custom User-Agent for each API instance.

## 3.8.39 - 2026-01-03

- Add Google batch operations and OpenAI video generation.

## 3.8.38 - 2026-01-02

- Add Upstage and batch operations for OpenAI and Anthropic.

## 3.8.37 - 2026-01-02

- Fix duplicated final chunks in Microsoft.Extensions.AI streams.

## 3.8.36 - 2025-12-23

- Improve refusals and model aliases. Fix Anthropic inline tool calls and add GLM-4.7.

## 3.8.35 - 2025-12-19

- Preserve reasoning token usage for Ollama models.

## 3.8.34 - 2025-12-19

- Make the tool factory thread-safe and add Gemini 3 Flash Preview.

## 3.8.33 - 2025-12-16

- Update OpenAI audio and transcription models, including diarization.

## 3.8.32 - 2025-12-13

- Add `ISpeechToTextClient`, Haiku structured outputs, and new models. Update MCP support.

## 3.8.31 - 2025-12-09

- Support file links in OpenAI chat messages.

## 3.8.30 - 2025-12-06

- Add OpenRouter image generation and new Mistral and Grok models.

## 3.8.29 - 2025-12-04

- Correct Anthropic thinking configuration with tools and improve OpenAI request handling.

## 3.8.28 - 2025-12-01

- Add the fantasy game sample, improve JSON parsing, and configure reruns on orchestration runnables.

## 3.8.27 - 2025-11-29

- Correct streamed reasoning event parameters.

## 3.8.26 - 2025-11-25

- Correct finish reason mappings.
- **Breaking:** Replace `ChatMessageFinishReasons.Abort` with `ChatMessageFinishReasons.Cancel`.

## 3.8.25 - 2025-11-25

- Add the `Abort` finish reason.

## 3.8.24 - 2025-11-25

- Expose `StopReason` and add Claude Opus 4.5 and Gemini 3 Pro Image Preview.

## 3.8.23 - 2025-11-19

- Add Gemini 3 Pro Preview.

## 3.8.22 - 2025-11-17

- Add GPT-5.1, patch and shell tools, Anthropic structured outputs, and strict tools.

## 3.8.21 - 2025-11-12

- Add the .NET 10 target framework.

## 3.8.20 - 2025-11-12

- Add compiled orchestration graphs, validation, and checkpoints. Improve core request and tool processing performance.

## 3.8.19 - 2025-11-11

- Add explicit terminal output control through `SetOutputRunnable(..., withDeadEnd)`.

## 3.8.18 - 2025-11-11

- Allow terminal orchestration outputs and add MCP sample helpers.

## 3.8.17 - 2025-11-11

- Expose conversation errors through `Conversation.Error`.

## 3.8.16 - 2025-11-11

- Add a `TornadoApi` constructor for a single `ProviderAuthentication`.

## 3.8.15 - 2025-11-09

- Add the Tokenize API and Kimi K2 Thinking models.

## 3.8.14 - 2025-11-08

- Add Requesty and model catalog generation.

## 3.8.13 - 2025-11-08

- Include the required header in Anthropic model list requests.

## 3.8.12 - 2025-11-05

- Add agent message compression and remove the redundant OpenAI `api-key` header.

## 3.8.11 - 2025-10-28

- Add Pinecone, fix image editing with multiple inputs, and migrate to `.slnx`.

## 3.8.10 - 2025-10-24

- Add Alibaba, Google Maps grounding, and Veo 3.1. Decouple MCP from Agents.

## 3.8.9 - 2025-10-19

- Fix Anthropic headers.
- **Breaking:** Replace MCP `DisableTools` with `AllowedTools` and invert the filter.

## 3.8.8 - 2025-10-19

- Add Anthropic Skills, asynchronous HttpClient initialization, Blablador, and Claude Haiku 4.5.

## 3.8.7 - 2025-10-17

- **Breaking:** Call `MCPServer.InitializeAsync()` explicitly before use. Fix A2A cancellation.

## 3.8.6 - 2025-10-15

- Extend MCP stdio configuration and add A2A client connectors.

## 3.8.5 - 2025-10-14

- Improve remote MCP server connections.

## 3.8.4 - 2025-10-13

- Add PgVector, an A2A server template, and documentation. Fix orchestration conversions and remove the A2A Hosting and WebUI projects.

## 3.8.3 - 2025-10-13

- Apply orchestration conversions before input type validation.

## 3.8.2 - 2025-10-12

- Update MCP support to 0.4.0-preview.2.

## 3.8.1 - 2025-10-12

- Add `IImageGenerator` and Gemini Computer Use Preview.

## 3.8.0 - 2025-10-08

- Add Microsoft.Extensions.AI integration for `IChatClient` and Semantic Kernel applications.
