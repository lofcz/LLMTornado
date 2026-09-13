using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using LlmTornado.Chat;
using LlmTornado.Chat.Vendors.Cohere;
using LlmTornado.Chat.Vendors.Perplexity;
using LlmTornado.Audio;
using LlmTornado.Audio.Vendors.MiniMax;
using LlmTornado.Chat.Vendors.XAi;
using LlmTornado.Images;
using LlmTornado.Code.Models;
using LlmTornado.Code.Sse;
using LlmTornado.Infra;
using LlmTornado.Threads;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ToolCall = LlmTornado.ChatFunctions.ToolCall;

namespace LlmTornado.Code.Vendor;

/// <summary>
/// Built-in OpenAI provider.
/// </summary>
public class OpenAiEndpointProvider : BaseEndpointProvider, IEndpointProvider, IEndpointProviderExtended
{
    private const string DataString = "data:";
    private const string DoneString = "[DONE]";
    private static readonly HashSet<string> toolFinishReasons = [ "function_call", "tool_calls" ];

    public static Version OutboundVersion { get; set; } = OutboundDefaultVersion;

    public Func<CapabilityEndpoints, string?, RequestUrlContext, string>? UrlResolver { get; set; }
    public Action<HttpRequestMessage, object?, bool>? RequestResolver { get; set; }
    public Action<JObject, RequestSerializerContext>? RequestSerializer { get; set; }
    
    public OpenAiEndpointProvider()
    {
        Provider = LLmProviders.OpenAi;
        StoreApiAuth();
    }
    
    public OpenAiEndpointProvider(LLmProviders provider)
    {
        Provider = provider;
        StoreApiAuth();
    }
    
    public override JsonSchemaCapabilities GetJsonSchemaCapabilities()
    {
        return new JsonSchemaCapabilities
        {
            Const = true
        };
    }

    enum ChatStreamParsingStates
    {
        Text,
        Tools
    }

    /// <summary>
    /// Gets endpoint url for a given capability.
    /// </summary>
    public static string GetEndpointUrlFragment(CapabilityEndpoints endpoint, LLmProviders provider = LLmProviders.OpenAi)
    {
        return endpoint switch
        {
            // variant providers overrides
            CapabilityEndpoints.Chat when provider is LLmProviders.Cohere => "chat",
            CapabilityEndpoints.Tokenize when provider is LLmProviders.MoonshotAi => "tokenizers/estimate-token-count",
            CapabilityEndpoints.Videos when provider is LLmProviders.MiniMax => "video_generation",
            CapabilityEndpoints.ImageGeneration when provider is LLmProviders.MiniMax => "image_generation",
            CapabilityEndpoints.Music when provider is LLmProviders.MiniMax => "music_generation",
            CapabilityEndpoints.Lyrics when provider is LLmProviders.MiniMax => "lyrics_generation",
            // default endpoints
            CapabilityEndpoints.Audio => "audio",
            CapabilityEndpoints.Chat => "chat/completions",
            CapabilityEndpoints.Completions => "completions",
            CapabilityEndpoints.Embeddings => "embeddings",
            CapabilityEndpoints.FineTuning => "fine_tuning",
            CapabilityEndpoints.Files => "files",
            CapabilityEndpoints.Uploads => "uploads",
            CapabilityEndpoints.ImageGeneration => "images/generations",
            CapabilityEndpoints.ImageEdit => "images/edits",
            CapabilityEndpoints.Models => "models",
            CapabilityEndpoints.Moderation => "moderations",
            CapabilityEndpoints.Assistants => "assistants",
            CapabilityEndpoints.Threads => "threads",
            CapabilityEndpoints.VectorStores => "vector_stores",
            CapabilityEndpoints.Responses => "responses",
            CapabilityEndpoints.ResponsesConversation => "conversations",
            CapabilityEndpoints.Skills => "skills",
            CapabilityEndpoints.Batch => "batches",
            CapabilityEndpoints.Videos => "videos",
            CapabilityEndpoints.RealtimeTranslations => "realtime/translations",
            // conditionally supported
            CapabilityEndpoints.ContextualEmbeddings when provider is LLmProviders.Voyage => "contextualizedembeddings",
            CapabilityEndpoints.MultimodalEmbeddings when provider is LLmProviders.Voyage => "multimodalembeddings",
            CapabilityEndpoints.Rerank when provider is LLmProviders.Voyage => "rerank",
            CapabilityEndpoints.Ocr when provider is LLmProviders.Mistral => "ocr",
            CapabilityEndpoints.Realtime => "realtime",
            _ => throw new Exception($"{provider} doesn't support endpoint {endpoint}")
        };
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="endpoint"></param>
    /// <param name="url"></param>
    /// <param name="model"></param>
    /// <returns></returns>
    public override string ApiUrl(CapabilityEndpoints endpoint, string? url, IModel? model = null)
    {
        string eStr = GetEndpointUrlFragment(endpoint, Provider);
        return UrlResolver is not null ? string.Format(UrlResolver.Invoke(endpoint, url, new RequestUrlContext(eStr, url, model)), eStr, url, model?.Name) : $"{string.Format(Api?.ApiUrlFormat ?? "https://api.openai.com/{0}/{1}", Api?.ResolveApiVersion(), GetEndpointUrlFragment(endpoint), model?.Name)}{url}";
    }

    private static readonly HashSet<LLmProviders> noApiKeyProviders = [LLmProviders.OpenRouter, LLmProviders.Requesty];
    
    public override HttpRequestMessage OutboundMessage(string url, HttpMethod verb, object? data, bool streaming, object? sourceObject)
    {
        HttpRequestMessage req = new HttpRequestMessage(verb, url)
        {
            Version = OutboundVersion
        };
        req.Headers.Add("User-Agent", EndpointBase.ResolveUserAgent(Api));

        ProviderAuthentication? auth = Api?.GetProvider(Provider).Auth;
        
        if (auth is not null)
        {
            if (auth.ApiKey is not null)
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.ApiKey.Trim());

                if (!noApiKeyProviders.Contains(Provider))
                {
                    req.Headers.Add("api-key", auth.ApiKey.Trim());
                }
            }

            if (auth.Organization is not null)
            {
                req.Headers.Add("OpenAI-Organization", auth.Organization.Trim());
            }
        }

        if (RequestResolver is not null)
        {
            RequestResolver.Invoke(req, data, streaming);   
        }
        else
        {
            //req.Headers.Add("OpenAI-Beta", "");
        }
        
        return req;
    }

    public override void ParseInboundHeaders(object? res, HttpResponseMessage response)
    {

    }

    public override void ParseInboundHeaders<T>(T res, HttpResponseMessage response)
    {
        res.Provider = this;

        if (response.Headers.TryGetValues("Openai-Organization", out IEnumerable<string>? orgH))
        {
            res.Organization = orgH.FirstOrDefault();
        }

        if (response.Headers.TryGetValues("X-RequestSettings-ID", out IEnumerable<string>? reqId))
        {
            res.RequestId = reqId.FirstOrDefault();
        }

        if (response.Headers.TryGetValues("Openai-Processing-Ms", out IEnumerable<string>? pms))
        {
            string? processing = pms.FirstOrDefault();
            
            if (processing is not null && int.TryParse(processing, out int n))
            {
                res.ProcessingTime = TimeSpan.FromMilliseconds(n);
            }
        }

        if (response.Headers.TryGetValues("Openai-Version", out IEnumerable<string>? oav))
        {
            res.RequestId = oav.FirstOrDefault();
        }
    }
    
    public override T? InboundMessage<T>(string jsonData, string? postData, object? requestObject) where T : default
    {
        return Provider switch
        {
            LLmProviders.OpenAi when typeof(T) == typeof(Tokenize.TokenizeResult) => (T?)(object?)Tokenize.TokenizeResult.Deserialize(LLmProviders.OpenAi, jsonData, postData),
            LLmProviders.OpenAi => JsonConvert.DeserializeObject<T>(jsonData),
            LLmProviders.XAi => InboundMessageVariantProviderXAi<T>(jsonData, postData),
            LLmProviders.Cohere => InboundMessageVariantProviderCohere<T>(jsonData, postData),
            LLmProviders.Mistral => InboundMessageVariantProviderMistral<T>(jsonData, postData),
            LLmProviders.Perplexity => InboundMessageVariantProviderPerplexity<T>(jsonData, postData),
            LLmProviders.Zai => InboundMessageVariantProviderZai<T>(jsonData, postData),
            LLmProviders.MoonshotAi when typeof(T) == typeof(Tokenize.TokenizeResult) => (T?)(object?)Tokenize.TokenizeResult.Deserialize(LLmProviders.MoonshotAi, jsonData, postData),
            LLmProviders.MiniMax when typeof(T) == typeof(ImageGenerationResult) => (T?)(object?)ImageGenerationResult.Deserialize(LLmProviders.MiniMax, jsonData, postData),
            LLmProviders.MiniMax when typeof(T) == typeof(MusicGenerationResult) => (T?)(object?)JsonConvert.DeserializeObject<VendorMiniMaxMusicResponse>(jsonData)?.ToResult(),
            LLmProviders.MiniMax when typeof(T) == typeof(LyricsGenerationResult) => (T?)(object?)JsonConvert.DeserializeObject<VendorMiniMaxLyricsResponse>(jsonData)?.ToResult(),
            _ => JsonConvert.DeserializeObject<T>(jsonData)
        };
    }
    
    static T? InboundMessageVariantProviderCohere<T>(string jsonData, string? postData)
    {
        if (typeof(T) == typeof(ChatResult))
        {
            return (T?)(object?)VendorCohereChatResult.Deserialize(jsonData);
        }
        
        return JsonConvert.DeserializeObject<T>(jsonData);
    }

    static T? InboundMessageVariantProviderXAi<T>(string jsonData, string? postData)
    {
        if (typeof(T) == typeof(ChatResult))
        {
            return (T?)(object?)ChatResultVendorXAi.Deserialize(jsonData);
        }
        
        return JsonConvert.DeserializeObject<T>(jsonData);
    }
    
    static T? InboundMessageVariantProviderPerplexity<T>(string jsonData, string? postData)
    {
        if (typeof(T) == typeof(ChatResult))
        {
            return (T?)(object?)ChatResultVendorPerplexity.Deserialize(jsonData);
        }
        
        return JsonConvert.DeserializeObject<T>(jsonData);
    }
    
    static T? InboundMessageVariantProviderZai<T>(string jsonData, string? postData)
    {
        if (typeof(T) == typeof(ChatResult))
        {
            return (T?)(object?)Chat.Vendors.Zai.ChatResultVendorZai.Deserialize(jsonData);
        }
        
        return JsonConvert.DeserializeObject<T>(jsonData);
    }
    
    static T? InboundMessageVariantProviderMistral<T>(string jsonData, string? postData)
    {
        JsonSerializerSettings settings = new JsonSerializerSettings
        {
            Converters = { new Chat.Vendors.Mistral.MistralChatMessageConverter() }
        };

        if (typeof(T) == typeof(ChatResult))
        {
            return (T?)(object?)JsonConvert.DeserializeObject<ChatResult>(jsonData, settings);
        }
        
        return JsonConvert.DeserializeObject<T>(jsonData, settings);
    }
    
    public override object? InboundMessage(Type type, string jsonData, string? postData, object? requestObject)
    {
        return JsonConvert.DeserializeObject(jsonData, type);
    }

    public override async IAsyncEnumerable<ChatResult?> InboundStream(StreamReader reader, ChatRequest request, ChatStreamEventHandler? eventHandler)
    {
        ChatStreamParsingStates state = ChatStreamParsingStates.Text;
        bool parseTools = request.Tools?.Count > 0;
        ChatResult? toolsAccumulator = null;
        ChatMessage? toolsMessage = null;
        StringBuilder? plaintextBuilder = null;
        StringBuilder? reasoningBuilder = null;
        string? encryptedContent = null;
        ChatUsage? usage = null;
        ChatMessageFinishReasons finishReason = ChatMessageFinishReasons.Unknown;
        string? stopReason = null;
        ChatResponseVendorExtensions? vendorExtensions = null;
        
        #if DEBUG
        List<string> data = [];
        #endif
        
        await foreach (SseItem<string> item in SseParser.Create(reader.BaseStream).EnumerateAsync(request.CancellationToken))
        {
            #if DEBUG
            data.Add(item.Data);
            #endif
            
            if (string.Equals(item.Data, DoneString, StringComparison.InvariantCulture))
            {
                goto afterStreamEnds;
            }

            ChatResult? res = Provider switch
            {
                LLmProviders.OpenAi => JsonConvert.DeserializeObject<ChatResult>(item.Data),
                LLmProviders.XAi => InboundMessageVariantProviderXAi<ChatResult>(item.Data, null),
                LLmProviders.Cohere => InboundMessageVariantProviderCohere<ChatResult>(item.Data, null),
                LLmProviders.Mistral => InboundMessageVariantProviderMistral<ChatResult>(item.Data, null),
                LLmProviders.Zai => InboundMessageVariantProviderZai<ChatResult>(item.Data, null),
                _ => JsonConvert.DeserializeObject<ChatResult>(item.Data)
            };
     
            if (res is null)
            {
                continue;
            }

            // process extensions for variant providers
            switch (Provider)
            {
                case LLmProviders.OpenAi:
                {
                    break;
                }
                case LLmProviders.XAi:
                {
                    if (res is ChatResultVendorXAi { Citations.Count: > 0 } xAiRes)
                    {
                        vendorExtensions = new ChatResponseVendorExtensions
                        {
                            XAi = new ChatResponseVendorXAiExtensions
                            {
                                Citations = xAiRes.Citations
                            }
                        };
                    }

                    break;
                }
                case LLmProviders.Zai:
                {
                    if (res is Chat.Vendors.Zai.ChatResultVendorZai { WebSearch.Count: > 0 } zaiRes)
                    {
                        vendorExtensions = new ChatResponseVendorExtensions
                        {
                            Zai = new Chat.Vendors.Zai.ChatResponseVendorZaiExtensions
                            {
                                WebSearchResults = zaiRes.WebSearch
                            }
                        };
                    }

                    break;
                }
                default:
                {
                    break;
                }
            }
            
            if (res.Choices?.Count > 0)
            {
                ChatChoice choice = res.Choices[0];

                if (choice.FinishReason is not (null or ChatMessageFinishReasons.Unknown))
                {
                    finishReason = choice.FinishReason ?? ChatMessageFinishReasons.Unknown;
                    stopReason ??= choice.StopReason;
                }

                if (choice.FinishReason is not (null or ChatMessageFinishReasons.Unknown))
                {
                    if (res.Usage?.TotalTokens > 0)
                    {
                        usage ??= res.Usage;   
                    }
                }
            }
            
            if (request.StreamOptions?.IncludeUsage ?? false)
            {
                if (usage is null && res.Choices is null || res.Choices?.Count is 0)
                {
                    usage = res.Usage;
                    continue;
                }
            }

            switch (state)
            {
                case ChatStreamParsingStates.Text when res is { Choices.Count: > 0 } && res.Choices[0].Delta?.ToolCalls?.Count > 0:
                {
                    toolsAccumulator = res;
                    toolsMessage = res.Choices[0].Delta;
                    state = ChatStreamParsingStates.Tools;

                    if (toolsMessage is not null)
                    {
                        toolsMessage.ToolCallsDict = [];

                        if (toolsMessage.ToolCalls is not null)
                        {
                            foreach (ToolCall toolCall in toolsMessage.ToolCalls)
                            {
                                string? argumentsDelta = toolCall.FunctionCall is not null ? toolCall.FunctionCall.Arguments : toolCall.CustomCall?.Input;
                                toolsMessage.ToolCallsDict.TryAdd(toolCall.Index?.ToString() ?? toolCall.Id ?? string.Empty, new ToolCallInboundAccumulator
                                {
                                    ArgumentsBuilder = new StringBuilder(argumentsDelta),
                                    ToolCall = toolCall
                                });

                                if (!string.IsNullOrEmpty(argumentsDelta) && eventHandler?.FunctionCallDeltaHandler is not null)
                                {
                                    await eventHandler.FunctionCallDeltaHandler.Invoke(new FunctionCallStreamUpdate
                                    {
                                        Name = toolCall.FunctionCall?.Name ?? toolCall.CustomCall?.Name,
                                        ArgumentsDelta = argumentsDelta,
                                        ArgumentsSnapshot = argumentsDelta,
                                        CallId = toolCall.Id,
                                        Index = toolCall.Index
                                    });
                                }
                            }   
                        }
                    }
                    
                    continue;
                }
                case ChatStreamParsingStates.Text:
                {
                    if (res.Choices is null || res.Choices.Count is 0)
                    {
                        continue;
                    }

                    ChatChoice choice = res.Choices[0];

                    if (choice.Delta is null)
                    {
                        continue;
                    }
                    
                    choice.Delta.Role = ChatMessageRoles.Assistant;

                    if (choice.Delta.ReasoningTokens is not null)
                    {
                        reasoningBuilder ??= new StringBuilder();
                        reasoningBuilder.Append(choice.Delta.ReasoningTokens);
                    }
                    
                    if (choice.Delta.EncryptedContent is not null)
                    {
                        encryptedContent = choice.Delta.EncryptedContent;
                    }

                    if (choice.Delta.Content is not null)
                    {
                        plaintextBuilder ??= new StringBuilder();
                        plaintextBuilder.Append(choice.Delta!.Content);
                    }
                    
                    yield return res;
                    continue;
                }
                case ChatStreamParsingStates.Tools:
                {
                    if (toolsMessage?.ToolCalls is not null && toolsMessage.ToolCallsDict is not null && res.Choices?.Count > 0)
                    {
                        ChatChoice choice = res.Choices[0];

                        if (choice.Delta?.ToolCalls?.Count > 0)
                        {
                            foreach (ToolCall toolCall in choice.Delta.ToolCalls)
                            {
                                string key = toolCall.Index?.ToString() ?? toolCall.Id ?? string.Empty;
                                string? argumentsDelta = toolCall.FunctionCall is not null ? toolCall.FunctionCall.Arguments : toolCall.CustomCall?.Input;
                                
                                // we can either encounter a new function or we get a new arguments token
                                if (toolsMessage.ToolCallsDict.TryGetValue(key, out ToolCallInboundAccumulator? accu))
                                {
                                    accu.ArgumentsBuilder.Append(argumentsDelta);

                                    if (!string.IsNullOrEmpty(argumentsDelta) && eventHandler?.FunctionCallDeltaHandler is not null)
                                    {
                                        await eventHandler.FunctionCallDeltaHandler.Invoke(new FunctionCallStreamUpdate
                                        {
                                            Name = accu.ToolCall.FunctionCall?.Name ?? accu.ToolCall.CustomCall?.Name,
                                            ArgumentsDelta = argumentsDelta,
                                            ArgumentsSnapshot = accu.ArgumentsBuilder.ToString(),
                                            CallId = accu.ToolCall.Id,
                                            Index = accu.ToolCall.Index
                                        });
                                    }
                                }
                                else
                                {
                                    toolsMessage.ToolCalls.Add(toolCall);
                                    toolsMessage.ToolCallsDict.Add(key, new ToolCallInboundAccumulator
                                    {
                                        ArgumentsBuilder = new StringBuilder(argumentsDelta),
                                        ToolCall = toolCall
                                    });

                                    if (!string.IsNullOrEmpty(argumentsDelta) && eventHandler?.FunctionCallDeltaHandler is not null)
                                    {
                                        await eventHandler.FunctionCallDeltaHandler.Invoke(new FunctionCallStreamUpdate
                                        {
                                            Name = toolCall.FunctionCall?.Name ?? toolCall.CustomCall?.Name,
                                            ArgumentsDelta = argumentsDelta,
                                            ArgumentsSnapshot = argumentsDelta,
                                            CallId = toolCall.Id,
                                            Index = toolCall.Index
                                        });
                                    }
                                }
                            }
                        }
                    }
                    break;
                }
            }
        }
        
        afterStreamEnds:
        
        if (parseTools && toolsAccumulator is not null && toolsMessage?.ToolCalls is not null && toolsMessage.ToolCallsDict is not null)
        {
            foreach (KeyValuePair<string, ToolCallInboundAccumulator> tool in toolsMessage.ToolCallsDict)
            {
                if (tool.Value.ToolCall.FunctionCall is not null)
                {
                    tool.Value.ToolCall.FunctionCall.Arguments = tool.Value.ArgumentsBuilder.ToString();   
                }
                else if (tool.Value.ToolCall.CustomCall is not null)
                {
                    tool.Value.ToolCall.CustomCall.Input = tool.Value.ArgumentsBuilder.ToString();   
                }
            }

            if (toolsAccumulator.Choices is not null)
            {
                toolsAccumulator.Choices[0].FinishReason = ChatMessageFinishReasons.ToolCalls;
            }

            toolsAccumulator.Usage = usage;
            toolsMessage.Role = ChatMessageRoles.Assistant;

            // Preserve the assistant text streamed before the tool calls. In this
            // branch the plaintext accumulator is otherwise dropped (the code below
            // that emits AppendAssistantMessage is skipped by the yield break), and
            // Conversation.StreamResponseRich then overwrites the appended message's
            // Content with ApiResultBase.Object (the API "object" type, e.g.
            // "chat.completion.chunk"). The model would never see its own reply and
            // would re-answer the prompt on every tool round.
            if (plaintextBuilder is { Length: > 0 } && string.IsNullOrEmpty(toolsMessage.Content))
            {
                toolsMessage.Content = plaintextBuilder.ToString();
            }

            yield return toolsAccumulator;
            yield break;
        }

        string? accuPlaintext = plaintextBuilder?.ToString();
        string? reasoningPlaintext = reasoningBuilder?.ToString();
        
        if (accuPlaintext is not null)
        {
            ChatMessage delta = new ChatMessage
            {
                Content = accuPlaintext,
                ReasoningContent = reasoningPlaintext,
                EncryptedContent = encryptedContent
            };
            
            // For xAI, create Parts with reasoning part to harmonize with other providers
            if (Provider is LLmProviders.XAi && reasoningPlaintext is not null)
            {
                delta.Parts = [];
                
                if (accuPlaintext.Length > 0)
                {
                    delta.Parts.Add(new ChatMessagePart(ChatMessageTypes.Text)
                    {
                        Text = accuPlaintext
                    });
                }
                
                delta.Parts.Add(new ChatMessagePart(new ChatMessageReasoningData
                {
                    Content = reasoningPlaintext,
                    Signature = encryptedContent,
                    Provider = Provider
                }));
            }
            
            yield return new ChatResult
            {
                Usage = usage,
                Choices =
                [
                    new ChatChoice
                    {
                        Delta = delta
                    }
                ],
                StreamInternalKind = ChatResultStreamInternalKinds.AppendAssistantMessage,
            };
        }

        if (vendorExtensions is not null)
        {
            yield return new ChatResult
            {
                VendorExtensions = vendorExtensions
            };
        }

        yield return new ChatResult
        {
            Usage = usage,
            Choices = [
                new ChatChoice
                {
                    FinishReason = finishReason,
                    StopReason = stopReason
                }
            ],
            StreamInternalKind = ChatResultStreamInternalKinds.FinishData
        };
    }
    
    public override async IAsyncEnumerable<object?> InboundStream(Type type, StreamReader reader)
    {
        await foreach (SseItem<string> item in SseParser.Create(reader.BaseStream).EnumerateAsync())
        {
            yield return JsonConvert.DeserializeObject(item.Data, type);
        }
    }

    public override async IAsyncEnumerable<T?> InboundStream<T>(StreamReader reader) where T : class
    {
        await foreach (SseItem<string> item in SseParser.Create(reader.BaseStream).EnumerateAsync())
        {
            if (item.Data is "[DONE]")
            {
                continue;
            }
            
            yield return JsonConvert.DeserializeObject<T>(item.Data);
        }
    }
}
