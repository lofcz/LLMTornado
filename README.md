<div align="center">

<img width="600" alt="LLM Tornado" src="https://github.com/user-attachments/assets/d98344f2-e6d9-4ec9-bd0a-5ef1be30109f" />

# LLM Tornado

**Build AI agents and workflows in minutes with one toolkit and built-in connectors to 30+ API Providers & Vector Databases.**    

**[Official Website](https://llmtornado.ai)**

[![LlmTornado](https://shields.io/nuget/v/LlmTornado?v=302&icon=nuget&label=LlmTornado)](https://www.nuget.org/packages/LlmTornado)
[![LlmTornado.Agents](https://shields.io/nuget/v/LlmTornado.Agents?v=303&icon=nuget&label=LlmTornado.Agents)](https://www.nuget.org/packages/LlmTornado.Agents)
[![LlmTornado.Mcp](https://shields.io/nuget/v/LlmTornado.Mcp?v=302&icon=nuget&label=LlmTornado.MCP)](https://www.nuget.org/packages/LlmTornado.Mcp)
[![LlmTornado.A2A](https://shields.io/nuget/v/LlmTornado.A2A?v=302&icon=nuget&label=LlmTornado.A2A)](https://www.nuget.org/packages/LlmTornado.A2A)
[![License:MIT](https://img.shields.io/badge/License-MIT-34D058.svg)](https://opensource.org/license/mit)

LLM Tornado is a .NET provider-agnostic SDK that empowers developers to build, orchestrate, and deploy AI agents and workflows. Whether you're building a simple chatbot or an autonomous coding agent, LLM Tornado provides the tools you need with unparalleled integration into the AI ecosystem.

<a href="https://www.youtube.com/watch?v=h7yTai0cRtE">
 <img alt="LLM Tornado in .NET AI Community Standup by Microsoft" width="768" src="https://github.com/user-attachments/assets/bbb8f8fa-4675-4728-b1ff-ae3b25cab7d2" />
</a>
 
</div>

## ✨ Features
-  **Use Any Provider**: Built-in connectors to: [Alibaba](https://modelstudio.console.alibabacloud.com), [Anthropic](https://docs.anthropic.com/en/docs/intro), [Azure](https://azure.microsoft.com/en-us/products/ai-services/openai-service), [Blablador](https://sdlaml.pages.jsc.fz-juelich.de/ai/guides/blablador_api_access), [Cohere](https://docs.cohere.com/changelog), [DeepInfra](https://deepinfra.com/docs/), [DeepSeek](https://api-docs.deepseek.com/), [Google](https://ai.google.dev/gemini-api/docs), [Groq](https://console.groq.com/docs/overview), [LiteLLM](https://docs.litellm.ai/docs/simple_proxy), [MiniMax](https://platform.minimax.io/docs/guides/models-intro), [Mistral](https://docs.mistral.ai/getting-started), [MoonshotAI](https://platform.moonshot.ai/docs/overview), [OpenAI](https://platform.openai.com/docs), [OpenRouter](https://openrouter.ai/docs/quickstart), [Perplexity](https://docs.perplexity.ai/home), [Requesty](https://www.requesty.ai), [TypeSafe](https://docs.typesafe.ai), [Upstage](https://www.upstage.ai), [Voyage](https://www.voyageai.com/), [xAI](https://docs.x.ai/docs), [Z.ai](https://docs.z.ai/guides/overview/quick-start), and more. Connectors expose all niche/unique features via strongly typed code and are up-to-date with the latest AI development. No dependencies on first-party SDKs. [Feature Matrix](https://github.com/lofcz/LlmTornado/blob/master/FeatureMatrix.md) tracks detailed endpoint support.
- **First-class Local Deployments**: Run with [vLLM](https://docs.vllm.ai/en/latest), [Ollama](https://ollama.com/), or [LocalAI](https://localai.io/) with integrated support for request transformations.
- **Agents Orchestration**: [Coordinate specialist agents](https://llmtornado.ai/agents/getting-started) that can autonomously perform complex tasks with three core concepts: `Orchestrator` (graph), `Runner` (node), and `Advancer` (edge). Comes with [handoffs](https://llmtornado.ai/agents/agent-orchestration/basics), [parallel execution](https://llmtornado.ai/agents/chat-runtime), [Mermaid](https://mermaid.js.org) export, and [builder pattern](https://llmtornado.ai/agents/agent-orchestration/orchestration#using-the-builder) to keep it simple.
- **Rapid Development**: Write pipelines once, execute with any Provider by changing the model's name. Connect your editor to [Context7](https://context7.com/lofcz/llmtornado) or [FSKB](https://github.com/lofcz/LlmTornado/tree/master/src/LlmTornado.FsKb/LlmTornado.FsKb) to accelerate coding with instant access to vectorized documentation.
- **Fully Multimodal**: Text, images, videos, documents, URLs, and audio inputs/outputs are supported.
- **Cutting Edge Protocols:**
  - [**MCP**](https://llmtornado.ai/mcp/mcp): Connect agents to data sources, tools, and workflows via Model Context Protocol with `LlmTornado.Mcp`.
  - [**A2A**](https://llmtornado.ai/a2a/getting-started): Enable seamless collaboration between AI agents across different platforms with `LlmTornado.A2A`.
  - [**Skills**](https://llmtornado.ai/llmtornado/anthropic-specific/skills): Dynamically load folders of instructions, scripts, and resources to improve performance on specialized tasks.
- **Vector Databases**: Built-in connectors to [Chroma](https://www.trychroma.com), [PgVector](https://github.com/pgvector/pgvector), [Pinecone](https://www.pinecone.io), [Faiss](https://github.com/facebookresearch/faiss), and [QDrant](https://qdrant.tech).
- **Integrated**: Interoperability with [Microsoft.Extensions.AI](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai) enables plugging Tornado in [Semantic Kernel](https://github.com/lofcz/LlmTornado/blob/master/src/LlmTornado.Demo/MicrosoftExtensionsAiDemo.cs) applications with `LlmTornado.Microsoft.Extensions.AI`.
- **Enterprise Ready**: [Guardrails](https://llmtornado.ai/agents/tornado-agent/guardrails) framework, [preview and transform any request](https://github.com/lofcz/LlmTornado/blob/2fd75e9cdf551724d59d91aebbcb74caea8ae7b2/src/LlmTornado.Demo/ChatDemo2.cs#L233-L256) before committing to it. [Open Telemetry](https://opentelemetry.io) support. Stable APIs.

<div align="center">
 <h4>➡️ Get started in minutes – <a href="https://llmtornado.ai/getting-started">Quickstart</a> ⬅️</h4>
</div>

## 🔥 News 2026
- 26/02 - [MiniMax](https://www.minimax.io) connector is implemented. Compaction endpoint is added, and can be used along the compaction built-in to LLM Tornado. Started work on ACP.
- 26/01 - [Ivy Framework](https://ivy.app) uses `LlmTornado.Agents` to build their [AI Components](https://github.com/Ivy-Interactive/Ivy-Examples/pull/362). `/ocr` endpoint is implemented for Mistral. `/files` endpoint support is extended to all supported providers.

## News 2025
- 25/12 - [Upstage](https://www.upstage.ai) connector is implemented. `/batch` endpoint is implemented for OpenAI, Anthropic, and Google. `/videos` endpoint now supports OpenAI/Sora.
- 25/11 - [Flowbite Blazor](https://github.com/themesberg/flowbite-blazor) uses LLM Tornado to build their [AI Chat](https://flowbite-blazor.org/docs/ai/chat) WASM component. [Requesty](https://www.requesty.ai) connector is implemented. `/tokenize` endpoint is implemented for all Providers supporting the feature.
- 25/10 - LLM Tornado is featured in [dotInsights](https://blog.jetbrains.com/dotnet/2025/10/06/dotinsights-october-2025) by [JetBrains](https://www.jetbrains.com). [Microsoft](https://www.microsoft.com) uses LLM Tornado in [Generative AI for Beginners .NET](https://github.com/microsoft/Generative-AI-for-beginners-dotnet). Interoperability with [Microsoft.Extensions.AI](https://github.com/dotnet/extensions) is launched. [Skills](https://llmtornado.ai/llmtornado/anthropic-specific/skills) protocol is implemented.
- 25/09 - Maintainers [Matěj Štágl](https://github.com/lofcz) and [John Lomba](https://github.com/Johnny2x2) talk about LLM Tornado in [.NET AI Community Standup](https://www.youtube.com/watch?v=h7yTai0cRtE). [PgVector](https://llmtornado.ai/vectordatabases/pgvector) and [ChromaDb](https://llmtornado.ai/vectordatabases/chromadb) connectors are implemented.
- 25/08 - [ProseFlow](https://lsxprime.github.io/proseflow-web) is built with LLM Tornado. [Sciobot](https://sciobot.org) – an AI platform for Educators built with LLM Tornado is accepted into [Cohere Labs Catalyst Grant Program](https://cohere.com/research/grants). [A2A](https://llmtornado.ai/a2a/getting-started) protocol is implemented.
- 25/07 - Contributor [Shaltiel Shmidman](https://github.com/shaltielshmid) talks about LLM Tornado in [Asp.net Community Standup](https://www.youtube.com/watch?v=RaZc-2tfh9k). New connectors to [Z.ai](https://docs.z.ai/guides/overview/quick-start), [Alibaba](https://modelstudio.console.alibabacloud.com), and [Blablador](https://sdlaml.pages.jsc.fz-juelich.de/ai/guides/blablador_api_access) are implemented.
- 25/06 - [C# delegates as Agent tools](https://llmtornado.ai/agents/tornado-agent/tools/function-tools) system is added, freeing applications from authoring JSON schema manually. [MCP](https://llmtornado.ai/mcp/mcp) protocol is implemented.
- 25/05 - [Chat to Responses](https://llmtornado.ai/agents/tornado-agent/tools/response-tools) system is added, allowing usage of `/responses` exclusive models from `/chat` endpoint.
- 25/04 - New connectors to [DeepInfra](https://deepinfra.com/docs/), [DeepSeek](https://api-docs.deepseek.com/), and [Perplexity](https://docs.perplexity.ai/home) are added.
- 25/03 - [Assistants](https://llmtornado.ai/llmtornado/assistants/basics) are implemented.
- 25/02 - New connectors to [OpenRouter](https://openrouter.ai/docs/quickstart), [Voyage](https://www.voyageai.com/), and [xAI](https://docs.x.ai/docs) are added.
- 25/01 - Strict JSON mode is implemented, [Groq](https://console.groq.com/docs/overview) and [Mistral](https://docs.mistral.ai/getting-started) connectors are added.

## ⭐ Samples
- [Chat with your documents](https://github.com/lofcz/LlmTornado/blob/61d2a4732c88c45d4a8c053204ecdef807c34652/LlmTornado.Demo/ChatDemo.cs#L722-L757)
- [Make multiple-speaker podcasts](https://github.com/lofcz/LlmTornado/blob/d1042281082ea5ff1de9dcb438a847d4cd9c416b/LlmTornado.Demo/ChatDemo2.cs#L332-L374)
- [Voice call with AI using your microphone](https://github.com/lofcz/LlmTornado/blob/61d2a4732c88c45d4a8c053204ecdef807c34652/LlmTornado.Demo/ChatDemo.cs#L905-L968)
- [Orchestrate Assistants](https://github.com/lofcz/LlmTornado/blob/61d2a4732c88c45d4a8c053204ecdef807c34652/LlmTornado.Demo/ThreadsDemo.cs#L331-L429)
- [Generate images](https://github.com/lofcz/LlmTornado/blob/61d2a4732c88c45d4a8c053204ecdef807c34652/LlmTornado.Demo/ImagesDemo.cs#L10-L13)
- [Summarize a video (local file / YouTube)](https://github.com/lofcz/LlmTornado/blob/cfd47f915584728d9a2365fc9d38d158673da68a/LlmTornado.Demo/ChatDemo2.cs#L119)
- [Turn text & images into high quality embeddings](https://github.com/lofcz/LlmTornado/blob/61d2a4732c88c45d4a8c053204ecdef807c34652/LlmTornado.Demo/EmbeddingDemo.cs#L50-L75)
- [Transcribe audio in real time](https://github.com/lofcz/LlmTornado/blob/e592a2fc0a37dbd0e754dac7b1655703367369df/LlmTornado.Demo/AudioDemo.cs#L29)

## ⚡Getting Started

Install LLM Tornado via NuGet:

```bash
dotnet add package LlmTornado
```

Optional addons:

```bash
dotnet add package LlmTornado.Agents # Agentic framework, higher-level abstractions
dotnet add package LlmTornado.Mcp # Model Context Protocol (MCP) integration
dotnet add package LlmTornado.A2A # Agent2Agent (A2A) integration
dotnet add package LlmTornado.Microsoft.Extensions.AI # Semantic Kernel interoperability
dotnet add package LlmTornado.Contrib # productivity, quality of life enhancements
```

## 🪄 Quick Inference

Inferencing across multiple providers is as easy as changing the `ChatModel` argument. Tornado instance can be constructed with multiple API keys, the correct key is then used based on the model automatically:

```csharp
TornadoApi api = new TornadoApi([
    // note: delete lines with providers you won't be using
    new (LLmProviders.OpenAi, "OPEN_AI_KEY"),
    new (LLmProviders.Anthropic, "ANTHROPIC_KEY"),
    new (LLmProviders.Cohere, "COHERE_KEY"),
    new (LLmProviders.Google, "GOOGLE_KEY"),
    new (LLmProviders.Groq, "GROQ_KEY"),
    new (LLmProviders.DeepSeek, "DEEP_SEEK_KEY"),
    new (LLmProviders.Mistral, "MISTRAL_KEY"),
    new (LLmProviders.XAi, "XAI_KEY"),
    new (LLmProviders.Perplexity, "PERPLEXITY_KEY"),
    new (LLmProviders.Voyage, "VOYAGE_KEY"),
    new (LLmProviders.DeepInfra, "DEEP_INFRA_KEY"),
    new (LLmProviders.OpenRouter, "OPEN_ROUTER_KEY")
]);

// this sample iterates a bunch of models, gives each the same task, and prints results.
List<ChatModel> models = [
    ChatModel.OpenAi.O3.Mini, ChatModel.Anthropic.Claude37.Sonnet,
    ChatModel.Cohere.Command.RPlus, ChatModel.Google.Gemini.Gemini2Flash001,
    ChatModel.Groq.Meta.Llama370B, ChatModel.DeepSeek.Models.Chat,
    ChatModel.Mistral.Premier.MistralLarge, ChatModel.XAi.Grok.Grok2241212,
    ChatModel.Perplexity.Sonar.Default
];

foreach (ChatModel model in models)
{
    string? response = await api.Chat.CreateConversation(model)
        .AppendSystemMessage("You are a fortune teller.")
        .AppendUserInput("What will my future bring?")
        .GetResponse();

    Console.WriteLine(response);
}
```

💡 Instead of passing in a strongly typed model, you can pass a string instead: `await api.Chat.CreateConversation("gpt-5-mini")`, Tornado will automatically resolve the provider.

### OpenAI API keys and Codex subscriptions

OpenAI API keys continue to support normal text and image models, while ChatGPT subscription access for Codex is explained in the [Codex guide](src/LlmTornado/Codex/README.md).

## ❄️ Vendor Extensions

Tornado has a powerful concept of `VendorExtensions` which can be applied to various endpoints and are strongly typed. Many Providers offer unique/niche APIs, often enabling use cases otherwise unavailable. For example, let's set a reasoning budget for Anthropic's Claude 3.7:

```cs
public static async Task AnthropicSonnet37Thinking()
{
    Conversation chat = Program.Connect(LLmProviders.Anthropic).Chat.CreateConversation(new ChatRequest
    {
        Model = ChatModel.Anthropic.Claude37.Sonnet,
        VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorAnthropicExtensions
        {
            Thinking = new AnthropicThinkingSettings
            {
                BudgetTokens = 2_000,
                Enabled = true
            }
        })
    });
    
    chat.AppendUserInput("Explain how to solve differential equations.");

    ChatRichResponse blocks = await chat.GetResponseRich();

    if (blocks.Blocks is not null)
    {
        foreach (ChatRichResponseBlock reasoning in blocks.Blocks.Where(x => x.Type is ChatRichResponseBlockTypes.Reasoning))
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(reasoning.Reasoning?.Content);
            Console.ResetColor();
        }

        foreach (ChatRichResponseBlock reasoning in blocks.Blocks.Where(x => x.Type is ChatRichResponseBlockTypes.Message))
        {
            Console.WriteLine(reasoning.Message);
        }
    }
}
```

## 🔮 Self-Hosted/Custom Providers

Instead of consuming commercial APIs, one can easily roll their inference servers with [a plethora](https://github.com/janhq/awesome-local-ai) of available tools. Here is a simple demo for streaming response with Ollama, but the same approach can be used for any custom provider:

```cs
public static async Task OllamaStreaming()
{
    TornadoApi api = new TornadoApi(new Uri("http://localhost:11434")); // default Ollama port, API key can be passed in the second argument if needed
    
    await api.Chat.CreateConversation(new ChatModel("falcon3:1b")) // <-- replace with your model
        .AppendUserInput("Why is the sky blue?")
        .StreamResponse(Console.Write);
}
```

If you need more control over requests, for example, custom headers, you can create an instance of a built-in Provider. This is useful for custom deployments like Amazon Bedrock, Vertex AI, etc.

```cs
TornadoApi tornadoApi = new TornadoApi(new AnthropicEndpointProvider
{
    Auth = new ProviderAuthentication("ANTHROPIC_API_KEY"),
    // {0} = endpoint, {1} = action, {2} = model's name
    UrlResolver = (endpoint, url, ctx) => "https://api.anthropic.com/v1/{0}{1}",
    RequestResolver = (request, data, streaming) =>
    {
        // by default, providing a custom request resolver omits beta headers
        // request is HttpRequestMessage, data contains the payload
    },
    RequestSerializer = (data, ctx) =>
    {
       // data is JObject, which can be modified before
       // being serialized into a string.
    }
});
```

https://github.com/user-attachments/assets/de62f0fe-93e0-448c-81d0-8ab7447ad780

## 🔎 Advanced Inference

### Streaming

Tornado offers three levels of abstraction, trading more details for more complexity. The simple use cases where only plaintext is needed can be represented in a terse format:

```cs
await api.Chat.CreateConversation(ChatModel.Anthropic.Claude3.Sonnet)
    .AppendSystemMessage("You are a fortune teller.")
    .AppendUserInput("What will my future bring?")
    .StreamResponse(Console.Write);
```  
  
The levels of abstraction are:
- `Response` (`string` for chat, `float[]` for embeddings, etc.)
- `ResponseRich` (tools, modalities, metadata such as usage)
- `ResponseRichSafe` (same as level 2, guaranteed not to throw on network level, for example, if the provider returns an internal error or doesn't respond at all)

### Streaming with Rich content (tools, images, audio..)

When plaintext is insufficient, switch to `StreamResponseRich` or `GetResponseRich()` APIs. Tools requested by the model can be resolved later and never returned to the model. This is useful in scenarios where we use the tools without intending to continue the conversation:

```cs
//Ask the model to generate two images, and stream the result:
public static async Task GoogleStreamImages()
{
    Conversation chat = api.Chat.CreateConversation(new ChatRequest
    {
        Model = ChatModel.Google.GeminiExperimental.Gemini2FlashImageGeneration,
        Modalities = [ ChatModelModalities.Text, ChatModelModalities.Image ]
    });
    
    chat.AppendUserInput([
        new ChatMessagePart("Generate two images: a lion and a squirrel")
    ]);
    
    await chat.StreamResponseRich(new ChatStreamEventHandler
    {
        MessagePartHandler = async (part) =>
        {
            if (part.Text is not null)
            {
                Console.Write(part.Text);
                return;
            }

            if (part.Image is not null)
            {
                // In our tests this executes Chafa to turn the raw base64 data into Sixels
                await DisplayImage(part.Image.Url);
            }
        },
        BlockFinishedHandler = (block) =>
        {
            Console.WriteLine();
            return ValueTask.CompletedTask;
        },
        OnUsageReceived = (usage) =>
        {
            Console.WriteLine();
            Console.WriteLine(usage);
            return ValueTask.CompletedTask;
        }
    });
}
```

### Tools with immediate resolve

Tools requested by the model can be resolved and the results returned immediately. This has the benefit of automatically continuing the conversation:

```cs
Conversation chat = api.Chat.CreateConversation(new ChatRequest
{
    Model = ChatModel.OpenAi.Gpt4.O,
    Tools =
    [
        new Tool(new ToolFunction("get_weather", "gets the current weather", new
        {
            type = "object",
            properties = new
            {
                location = new
                {
                    type = "string",
                    description = "The location for which the weather information is required."
                }
            },
            required = new List<string> { "location" }
        }))
    ]
})
.AppendSystemMessage("You are a helpful assistant")
.AppendUserInput("What is the weather like today in Prague?");

ChatStreamEventHandler handler = new ChatStreamEventHandler
{
  MessageTokenHandler = (x) =>
  {
      Console.Write(x);
      return Task.CompletedTask;
  },
  FunctionCallHandler = (calls) =>
  {
      calls.ForEach(x => x.Result = new FunctionResult(x, "A mild rain is expected around noon.", null));
      return Task.CompletedTask;
  },
  AfterFunctionCallsResolvedHandler = async (results, handler) => { await chat.StreamResponseRich(handler); }
};

await chat.StreamResponseRich(handler);
```

### Tools with deferred resolve

Instead of resolving the tool call, we can postpone/quit the conversation. This is useful for extractive tasks, where we care only for the tool call:

```cs
Conversation chat = api.Chat.CreateConversation(new ChatRequest
{
    Model = ChatModel.OpenAi.Gpt4.Turbo,
    Tools = new List<Tool>
    {
        new Tool
        {
            Function = new ToolFunction("get_weather", "gets the current weather")
        }
    },
    ToolChoice = new OutboundToolChoice(OutboundToolChoiceModes.Required)
});

chat.AppendUserInput("Who are you?"); // user asks something unrelated, but we force the model to use the tool
ChatRichResponse response = await chat.GetResponseRich(); // the response contains one block of type Function
```

_`GetResponseRichSafe()` API is also available, which is guaranteed not to throw on the network level. The response is wrapped in a network-level wrapper, containing additional information. For production use cases, either use `try {} catch {}` on all the HTTP request-producing Tornado APIs, or use the safe APIs._

## 🌐 MCP
To use the Model Context Protocol, install the `LlmTornado.Mcp` adapter. After that, new interop methods will become available on the `ModelContextProtocol` types. The following example uses the `GetForecast` tool defined on an [example MCP server](https://modelcontextprotocol.io/quickstart/server#c%23):
```cs
[McpServerToolType]
public sealed class WeatherTools
{
    [McpServerTool, Description("Get weather forecast for a location.")]
    public static async Task<string> GetForecast(
        HttpClient client,
        [Description("Latitude of the location.")] double latitude,
        [Description("Longitude of the location.")] double longitude)
    {
        var pointUrl = string.Create(CultureInfo.InvariantCulture, $"/points/{latitude},{longitude}");
        using var jsonDocument = await client.ReadJsonDocumentAsync(pointUrl);
        var forecastUrl = jsonDocument.RootElement.GetProperty("properties").GetProperty("forecast").GetString()
            ?? throw new Exception($"No forecast URL provided by {client.BaseAddress}points/{latitude},{longitude}");

        using var forecastDocument = await client.ReadJsonDocumentAsync(forecastUrl);
        var periods = forecastDocument.RootElement.GetProperty("properties").GetProperty("periods").EnumerateArray();

        return string.Join("\n---\n", periods.Select(period => $"""
                {period.GetProperty("name").GetString()}
                Temperature: {period.GetProperty("temperature").GetInt32()}°F
                Wind: {period.GetProperty("windSpeed").GetString()} {period.GetProperty("windDirection").GetString()}
                Forecast: {period.GetProperty("detailedForecast").GetString()}
                """));
    }
}
```

The following is done by the client:
```cs
// your clientTransport, for example StdioClientTransport
await using McpClient mcpClient = await McpClient.CreateAsync(clientTransport);

// 1. fetch tools
List<Tool> tools = await mcpClient.ListTornadoToolsAsync();

// 2. create a conversation, pass available tools
TornadoApi api = new TornadoApi(LLmProviders.OpenAi, apiKeys.OpenAi);
Conversation conversation = api.Chat.CreateConversation(new ChatRequest
{
    Model = ChatModel.OpenAi.Gpt41.V41,
    Tools = tools,
    // force any of the available tools to be used (use new OutboundToolChoice("toolName") to specify which if needed)
    ToolChoice = OutboundToolChoice.Required
});

// 3. let the model call the tool and infer arguments
await conversation
    .AddSystemMessage("You are a helpful assistant")
    .AddUserMessage("What is the weather like in Dallas?")
    .GetResponseRich(async calls =>
    {
        foreach (FunctionCall call in calls)
        {
            // retrieve arguments inferred by the model
            double latitude = call.GetOrDefault<double>("latitude");
            double longitude = call.GetOrDefault<double>("longitude");
            
            // call the tool on the MCP server, pass args
            await call.ResolveRemote(new
            {
                latitude = latitude,
                longitude = longitude
            });

            // extract the tool result and pass it back to the model
            if (call.Result?.RemoteContent is McpContent mcpContent)
            {
                foreach (IMcpContentBlock block in mcpContent.McpContentBlocks)
                {
                    if (block is McpContentBlockText textBlock)
                    {
                        call.Result.Content = textBlock.Text;
                    }
                }
            }
        }
    });

// stop forcing the client to call the tool
conversation.RequestParameters.ToolChoice = null;

// 4. stream final response
await conversation.StreamResponse(Console.Write);
```

A complete example is available here: [client](https://github.com/lofcz/LlmTornado/blob/master/src/LlmTornado.Mcp.Sample.Server/WeatherTools.cs), [server](https://github.com/lofcz/LlmTornado/blob/master/src/LlmTornado.Mcp.Sample/Program.cs).

## 🧰 Toolkit

Tornado includes powerful abstractions in the `LlmTornado.Toolkit` package, allowing rapid development of applications, while avoiding many design pitfalls. Scalability and tuning-friendly code design are at the core of these abstractions.

### ToolkitChat

`ToolkitChat` is a primitive for graph-based workflows, where edges move data and nodes execute functions. ToolkitChat supports streaming, rich responses, and chaining tool calls. Tool calls are provided via `ChatFunction` or `ChatPlugin` (an envelope with multiple tools). Many overloads accept a primary and a secondary model acting as a backup, this zig-zag strategy overcomes temporary downtime in APIs better than simple retrying of the same model. All tool calls are strongly typed and `strict` by default. For providers, where a strict JSON schema is not supported (Anthropic, for example), prefill with `{` is used as a fallback. Call can be marked as non-strict by simply changing a parameter.

```cs
class DemoAggregatedItem
{
    public string Name { get; set; }
    public string KnownName { get; set; }
    public int Quantity { get; set; }
}

string sysPrompt = "aggregate items by type";
string userPrompt = "three apples, one cherry, two apples, one orange, one orange";

await ToolkitChat.GetSingleResponse(Program.Connect(), ChatModel.Google.Gemini.Gemini25Flash, ChatModel.OpenAi.Gpt41.V41Mini, sysPrompt, new ChatFunction([
    new ToolParam("items", new ToolParamList("aggregated items", [
        new ToolParam("name", "name of the item", ToolParamAtomicTypes.String),
        new ToolParam("quantity", "aggregated quantity", ToolParamAtomicTypes.Int),
        new ToolParam("known_name", new ToolParamEnum("known name of the item", [ "apple", "cherry", "orange", "other" ]))
    ]))
], async (args, ctx) =>
{
    if (!args.ParamTryGet("items", out List<DemoAggregatedItem>? items) || items is null)
    {
        return new ChatFunctionCallResult(ChatFunctionCallResultParameterErrors.MissingRequiredParameter, "items");
    }
    
    Console.WriteLine("Aggregated items:");

    foreach (DemoAggregatedItem item in items)
    {
        Console.WriteLine($"{item.Name}: {item.Quantity}");
    }
    
    return new ChatFunctionCallResult();
}), userPrompt); // temp defaults to 0, output length to 8k

/*
Aggregated items:
apple: 5
cherry: 1
orange: 2
*/
```

## 👉 Why Tornado?

- 100,000+ installs on [NuGet](https://www.nuget.org/packages/LlmTornado).
- Used in [award-winning](https://www-aiawards-cz.translate.goog/?_x_tr_sl=cs&_x_tr_tl=en&_x_tr_hl=cs) commercial projects, processing > 100B tokens monthly.
- Covered by 500+ tests.
- Great performance.
- The license will never change.

## 📢 Built With Tornado
- [ScioBot](https://sciobot.org/) - AI For Educators, 100k+ users.
- [ProseFlow](https://github.com/LSXPrime/ProseFlow) - Your universal AI text processor, powered by local and cloud LLMs. Edit, refactor, and transform text in any application on Windows, macOS, and Linux.
- [NotT3Chat](https://github.com/shaltielshmid/NotT3Chat) - The C# Answer to the T3 Stack.
- [ClaudeCodeProxy](https://github.com/salty-flower/ClaudeCodeProxy) - Provider multiplexing proxy.
- [Semantic Search](https://github.com/primaryobjects/semantic-search) - AI semantic search where a query is matched by context and meaning.

_Have you built something with Tornado? Let us know about it in the issues to get a spotlight!_

## 🤝 Partners

### Sponsored by

<a href="https://www.scio.cz/prace-u-nas" target="_blank">
    <figure>
        <img alt="Scio" width="300" alt="image" src="https://github.com/user-attachments/assets/6a5aa9b3-af8b-4194-8dbe-c3add79763e7" />
    </figure>
</a>

### Powered by
[![JetBrains logo.](https://resources.jetbrains.com/storage/products/company/brand/logos/jetbrains.svg)](https://jb.gg/OpenSource)

## 📚 Contributing

PRs are welcome! We are accepting new Provider implementations, contributions towards a 100 % green [Feature Matrix](https://github.com/lofcz/LlmTornado/blob/master/FeatureMatrix.md), and, after public discussion, new abstractions.

## Star History

[![Star History Chart](https://star-history.dera.page/svg?repos=lofcz/llmtornado&type=date&legend=top-left)](https://star-history.dera.page/#lofcz/llmtornado&type=date&legend=top-left)

## License

This library is licensed under the [MIT](https://github.com/lofcz/LlmTornado/blob/master/LICENSE) license. 💜
