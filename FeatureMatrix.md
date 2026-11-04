# Feature Matrix

✅ - Supported  
⚠️ - Partially supported/under construction  
❌ - Not supported  
_Empty field means the feature is unsupported by the provider._

## Inference

| Provider | Chat | Models | Embeddings | Files | Images | Audio | Uploads | Moderation | Responses | Videos | Tokenize | FIM | Fine-tuning | Batch |
|:---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **OpenAI** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | | | ✅ | ✅ |
| **Anthropic** | ✅ | ✅ | | ✅ | | | | | | | ✅ | | | ✅ |
| **Google** | ✅ | ✅ | ✅ | ✅ | ✅ | | | | | ✅ | ✅ | | ❌ | ✅ |
| **Cohere** | ✅ | ✅ | ✅ | | | ✅ | | | | | ✅ | | | |
| **DeepSeek** | ✅ | ✅ | | | | | | | | | | ❌ | | |
| **Groq** | ✅ | ✅ | | ✅ | | ✅ | | | | | | | | ❌ |
| **Mistral** | ✅ | ✅ | ✅ | ✅ | | ✅ | | | | | | ❌ | ❌ | ❌ |
| **xAI** | ✅ | ✅ | | | ✅ | | | | ❌ | ✅ | | | | |
| **MoonshotAI**| ✅ | ✅ | | ✅ | | | | | ✅ | | ✅ | | | ✅ |
| **Perplexity**| ✅ | | | | | | | | | | | | | |
| **Voyage** | | | ✅ | | | | | | | | | | | |
| **DeepInfra** | ✅ | | | | | | | | | | | | | |
| **OpenRouter**| ✅ | ✅ | ✅ | | | | | | | | | | | |
| **Requesty**| ✅ | ✅ | ✅ | | | | | | | | | | | |
| **Upstage** | ✅ | | ✅ | | | | | | | | | | | |
| **Z.ai** | ✅ | | | ✅ | | ✅ | | | | ✅ | | | | |
| **Alibaba** | ✅ | ✅ | ✅ | | | | | | | | | | | ❌ |
| **Blablador** | ✅ | ✅ | ✅ | | | | | | | | | | | |
| **MiniMax** | ✅ | ✅ | | ✅ | ✅ | ✅ | | | | ✅ | | | | |
| **TypeSafe** | | ✅ | | | | | | | | | | | | |
| **Custom*** | ✅ | ✅ | | | | | | | | | | | | |

_*Custom means any OpenAI compatible provider, such as Azure OpenAI, Ollama, KoboldCpp, etc._  

## OpenAI Specific

Conversations | Assistants | Threads | Messages | Runs | Run steps | Vector stores | Vector store files | Vector store file batches | Realtime | Evals | Graders |
|----------- |-----------|------------|---------|----------|------| ---------------|-------------------|-------------------------|-----------|-----------|-----------|
| ❌ | ✅ | ✅️ | ✅️ | ✅️ | ✅️ | ✅ | ✅ | ✅  | ❌ | ❌ | ❌ |

## Google Specific

 Caching | Semantic Retrieval | Live | Speech
|-----------|------------ | ------------ | ------------ |
| ✅ | ❌ | ❌ | ✅ |

## Anthropic Specific

| Skills | Messages threads | User profiles | Organization webhooks | Usage/cost analytics | Claude Tag |
| --- | --- | --- | --- | --- | --- |
| ✅ | ✅ (sessions and explicit create/continue) | ✅ (CRUD and enforcement) | ✅ | ✅ | ✅ |

Anthropic API version `2026-10-21` is opt-in through `AnthropicEndpointProvider.ApiRevision`;
the default remains `2023-06-01`. Anthropic-managed Google Cloud routing supports application-supplied,
refreshed bearer tokens. Messages thread sessions own full local history and recovery; the explicit create/continue API also supports application-owned history.

## Voyage Specific

 Multimodal Embedding | Contextualized Embedding | Rerankers
|-----------|------------ | ------------ |
| ✅ | ✅ | ✅ |

## Cohere Specific

| Rerank | Parse |
|-----------|-----------|
| ✅ | ✅ |

## Decisions (System One)

| Provider | Decision |
|-----------|-----------|
| TypeSafe | ✅ |
| OpenRouter (alpha) | ✅ |
| OpenAI (native predicate, choice, score) | ✅ |

## MiniMax Specific

| Music Generation | Lyrics Generation |
|-----------|-----------|
| ✅ | ✅ |

## Mistral Specific

Agents | Classifiers | OCR | Guardrails | Reasoning effort |
| ------------ | ------------ | ------------ | ------------ | ------------ |
| ❌ |  ❌ | ✅ | ✅ | ✅ |

## October 2026 API additions

| Provider | Feature | Support |
| --- | --- | --- |
| OpenAI | Native Decisions via `Decision.OpenAi` | ✅ |
| OpenAI | GPT-Live Decisions delegation and transcript triggers | ✅ |
| OpenAI | Prompt-to-voice via `Audio.OpenAiVoices` | ✅ |
| OpenAI | Responses explicit cache breakpoints and 12-hour TTL | ✅ |
| OpenAI | Responses cache diagnostics and reasoning configuration updates | ✅ |
| OpenAI | Responses async function/custom tool flags and call replay | ✅ |
| OpenAI | Responses WebSocket steering, automatic successors and pending tool/approval input | ✅ |
| OpenAI | Responses WebSocket lane routing and low-level send/read operations | ✅ |
| Anthropic | Signed on-demand compaction, including streams | ✅ |
| Anthropic | Claude 5.5 thinking modes and model capability discovery | ✅ |
| Anthropic | Inline tool additions/removals and MCP tool-list replay/pinning | ✅ |
| Anthropic | Thinking input transformations, including streaming fallback | ✅ |
| Anthropic | Ordered text/client-tool thinking replay, redaction and omitted signatures in JSON/streams | ✅ |
| Anthropic | Managed Agents dynamic workflow/subagent/advisor configuration and workflow events | ✅ |
| Anthropic | Managed Agents auto permission evaluation, web/network controls and session budgets | ✅ |
| Anthropic | Managed Agents thread operations, native pagination and event preview streams | ✅ |
| Google | Native Gemini image generation/editing and video-to-image | ✅ |
| Google | Argon long-decode text aggregation | ✅ |
| Google | Gemini 3.8 TTS via `Audio.CreateSpeech` and `Audio.StreamSpeech` | ✅ |
| Google | Voice design/replication and voice-library CRUD/filter pagination | ✅ |
| Google | Gemini unary transcription with smart/verbatim modes and word/speaker annotations | ✅ |

The full provider sync is still being audited; see [the dated audit](docs/provider-sync-2026-10-21.md) for outstanding areas.

## Misc

Provider-specific administration is exposed through dedicated endpoints where implemented, including
Anthropic rate limits, usage/cost analytics, organization webhooks, and Claude Tag. This is not full
coverage of every provider's Admin API.
