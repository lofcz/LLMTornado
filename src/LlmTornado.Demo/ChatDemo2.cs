using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http.Headers;
using LibVLCSharp.Shared;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Chat.Vendors.Anthropic;
using LlmTornado.Chat.Vendors.Google;
using LlmTornado.Chat.Vendors.Mistral;
using LlmTornado.Chat.Vendors.Perplexity;
using LlmTornado.Chat.Vendors.XAi;
using LlmTornado.ChatFunctions;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using LlmTornado.Common;
using LlmTornado.Contrib;
using LlmTornado.Files;
using LlmTornado.Models;
using LlmTornado.Responses;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Demo;

public partial class ChatDemo : DemoBase
{
    [TornadoTest]
    public static async Task CohereTranslate()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Cohere.Command.ATranslate2508
        });
        
        chat.AppendUserInput("Translate everything that follows into English: Singulární rozklad (zkratkou SVD podle anglického názvu Singular Value Decomposition) matice je rozklad komplexní nebo reálné matice \nM\n{\\displaystyle \\mathbf {M} } na maticový součin \nU\nΣ\nV\n∗\n{\\displaystyle \\mathbf {U\\Sigma V^{*}} }. Přitom \nU\n{\\displaystyle \\mathbf {U} } je reálná nebo komplexní unitární matice o rozměrech \nm\n×\nm\n{\\displaystyle m\\times m}, \nV\n{\\displaystyle \\mathbf {V} } je reálná nebo komplexní unitární matice \nn\n×\nn\n{\\displaystyle n\\times n} a \nΣ\n{\\displaystyle \\mathbf {\\Sigma } } je matice \nm\n×\nn\n{\\displaystyle m\\times n} nulová až na případná nezáporná čísla na hlavní diagonále; čísla na její hlavní diagonále se označují jako singulární hodnoty matice \nM\n{\\displaystyle \\mathbf {M} }. Hvězdička označuje konjugovanou matici, tedy transponovanou matici komplexně sdružených prvků. Požadujeme-li, jak je obvyklé, aby singulární hodnoty byly seřazeny sestupně, je matice \nΣ\n{\\displaystyle \\mathbf {\\Sigma } } určena jednoznačně, naopak matice \nU\n{\\displaystyle \\mathbf {U} } a \nV\n{\\displaystyle \\mathbf {V} } jednoznačné být nemusejí. Singulární rozklad vždy existuje a používá se k řadě teoretických i praktických účelů.");
        string? str = await chat.GetResponse();

        Console.WriteLine("Cohere:");
        Console.WriteLine(str);
    }
    
    [TornadoTest]
    public static async Task ProviderCustomServerApiKey()
    {
        TornadoApi tornadoApi = new TornadoApi(new Uri("https://api.openai.com"), Program.ApiKeys.OpenAi);
        
        Conversation chat = tornadoApi.Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.OpenAi.Gpt41.V41
        });
        
        chat.AppendUserInput("Who are you?");
        string? str = await chat.GetResponse();

        Console.WriteLine("OpenAI:");
        Console.WriteLine(str);
    }

    [TornadoTest]
    public static async Task CustomToolsGrammar()
    {
        string grammar = """
                         start: expr
                         
                         expr: add_expr
                         add_expr: mul_expr (SP* (ADD | SUB) SP* mul_expr)*
                         mul_expr: unary_expr (SP* (MUL | DIV) SP* unary_expr)*
                         unary_expr: (ADD | SUB) SP* unary_expr
                                   | factor
                         factor: INT
                               | LPAR SP* expr SP* RPAR
                         
                         SP: " "
                         ADD: "+"
                         SUB: "-"
                         MUL: "*"
                         DIV: "/"
                         LPAR: "("
                         RPAR: ")"
                         INT: /[0-9]+/
                         """;

        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = "gpt-5",
            Tools = [
                new Tool(new ToolCustom(ToolCustomFormat.Lark(grammar), "math_exp", "Creates valid mathematical expressions"))
            ],
            ToolChoice = OutboundToolChoice.Auto
        });

        chat.AppendUserInput("Use the math_exp tool to transform the following query: negative one multiplied by the result of forty-five divided by three minus eight divided by two, all divided by two");

        TornadoRequestContent serialized = chat.Serialize(new ChatRequestSerializeOptions
        {
            Pretty = true
        });

        Console.WriteLine(serialized);
        ChatRichResponse response = await chat.GetResponseRich(ToolCallsHandler.ContinueConversation);
        Console.WriteLine(response);
    }
    
    [TornadoTest]
    public static async Task CustomToolsGrammarResponses()
    {
        string grammar = """
                         start: expr

                         expr: add_expr
                         add_expr: mul_expr (SP* (ADD | SUB) SP* mul_expr)*
                         mul_expr: unary_expr (SP* (MUL | DIV) SP* unary_expr)*
                         unary_expr: (ADD | SUB) SP* unary_expr
                                   | factor
                         factor: INT
                               | LPAR SP* expr SP* RPAR

                         SP: " "
                         ADD: "+"
                         SUB: "-"
                         MUL: "*"
                         DIV: "/"
                         LPAR: "("
                         RPAR: ")"
                         INT: /[0-9]+/
                         """;

        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = "gpt-5",
            Tools = [
                new Tool(new ToolCustom(ToolCustomFormat.Lark(grammar), "math_exp", "Creates valid mathematical expressions"))
            ],
            ToolChoice = OutboundToolChoice.Auto,
            ResponseRequestParameters = new ResponseRequest()
        });

        chat.AppendUserInput("Use the math_exp tool to transform the following query: negative one multiplied by the result of forty-five divided by three minus eight divided by two, all divided by two");

        TornadoRequestContent serialized = chat.Serialize(new ChatRequestSerializeOptions
        {
            Pretty = true
        });

        Console.WriteLine(serialized);
        ChatRichResponse response = await chat.GetResponseRich(ToolCallsHandler.ContinueConversation);
        Console.WriteLine(response);
    }
    
    /// <summary>
    /// Dedicated to EricGT who was a great help figuring this out!
    /// </summary>
    [TornadoTest]
    public static async Task CustomToolsGrammarStreaming()
    {
        string grammar = """
                         start: "<think>" "Ok, let's think about this:" "\n"think_content "</think>" address
                         
                         think_content: think_char{0,500}
                         think_char: SAFE_CHAR | SAFE_LT_SEQUENCE
                         SAFE_LT_SEQUENCE: "<" ( NOT_SLASH_OR_T | "/" (NOT_T | "t" (NOT_H | "h" (NOT_I | "i" (NOT_N | "n" (NOT_K | "k" NOT_GT))))) | "t" (NOT_H | "h" (NOT_I | "i" (NOT_N | "n" (NOT_K | "k" NOT_GT)))) )
                         
                         SAFE_CHAR:      /[^<]/
                         NOT_SLASH_OR_T: /[^\/t]/
                         NOT_T:          /[^t]/
                         NOT_H:          /[^h]/
                         NOT_I:          /[^i]/
                         NOT_N:          /[^n]/
                         NOT_K:          /[^k]/
                         NOT_GT:         /[^>]/
                         
                         address: %json {
                             "type": "object",
                             "properties": {
                                 "street": { "type": "string" },
                                 "city": { "type": "string" },
                                 "zip": { "type": "number" }
                            },
                            "additionalProperties": false,
                            "required": ["street", "city", "zip"]
                         }
                         """;
        
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = "gpt-5-mini",
            Tools = [
                new Tool(new ToolCustom(ToolCustomFormat.Lark(grammar), "get_address")) // purposefully misleading description
            ],
            ToolChoice = OutboundToolChoice.Auto,
            MaxTokens = 300,
            ReasoningEffort = ChatReasoningEfforts.Minimal
        });
        
        chat.AppendUserInput("Can you translate 'V Podluží 7, Praha 4 Nusle' into get_address call?");

        bool anyCall = false;
        
        Console.WriteLine(chat.Serialize(true));
        await StreamNext();

        if (anyCall)
        {
            Console.WriteLine(chat.Serialize(true));
            
            await StreamNext();
        }
        
        async Task StreamNext()
        {
            await chat.StreamResponseRich(new ChatStreamEventHandler
            {
                CustomToolCallHandler = (calls) =>
                {
                    foreach (CustomToolCall call in calls)
                    {
                        call.Resolve(new
                        {
                            result = "rainy, foggy"
                        });
                    }

                    anyCall = true;
                    return ValueTask.CompletedTask;
                },
                MessageTokenHandler = (token) =>
                {
                    Console.Write(token);
                    return ValueTask.CompletedTask;
                }
            });
        }
    }

    [TornadoTest]
    public static async Task VllmKwargs()
    {
        Conversation chat = new TornadoApi(new Uri("http://localhost:8000")).Chat.CreateConversation(new ChatRequest
        {
            Model = "Qwen/Qwen2.5-1.5B-Instruct",
            OnSerialize = (data, ctx) =>
            {
                data["chat_template_kwargs"] = JToken.FromObject(new
                {
                    enable_thinking = false
                });
            }
        })
        .AddSystemMessage("Sys prompt")
        .AddUserMessage("User prompt");

        TornadoRequestContent str = chat.Serialize(new ChatRequestSerializeOptions
        {
            Pretty = true
        });
        
        Console.WriteLine(str);
    }
    
    [TornadoTest]
    public static async Task ProviderCustomHeaders()
    {
        TornadoApi tornadoApi = new TornadoApi(new AnthropicEndpointProvider
        {
            Auth = new ProviderAuthentication(Program.ApiKeys.Anthropic),
            UrlResolver = (endpoint, url, ctx) => "https://api.anthropic.com/v1/{0}{1}",
            RequestResolver = (request, data, streaming) =>
            {
                // by default, providing a custom request resolver omits beta headers
                // request is HttpRequestMessage, data contains the payload
            }
        });

        Conversation chat = tornadoApi.Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Anthropic.Claude4.Sonnet250514
        });
        
        chat.AppendUserInput("Who are you?");
        string? str = await chat.GetResponse();

        Console.WriteLine("Anthropic:");
        Console.WriteLine(str);
    }
    
    [TornadoTest]
    [Flaky("expensive")]
    public static async Task Gemini25FlashImagePreview()
    {
        Conversation conversion = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Google.GeminiPreview.Gemini25FlashImagePreview,
            Modalities = [ChatModelModalities.Text, ChatModelModalities.Image],
            Messages =
            [
                new ChatMessage(ChatMessageRoles.User, "Generate an image of a plant")
            ]
        });
        
        ChatRichResponse response = await conversion.GetResponseRich();
        ChatRichResponseBlock? generated = response.Blocks.FirstOrDefault(x => x.ChatImage is not null);

        await DisplayImage(generated.ChatImage.Url);

        conversion.AddUserMessage("Now place this plant on a t-shirt");
        
        response = await conversion.GetResponseRich();
        generated = response.Blocks.FirstOrDefault(x => x.ChatImage is not null);

        await DisplayImage(generated.ChatImage.Url);
    }
    
    [TornadoTest]
    public static async Task Grok2()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.XAi.Grok.Grok2241212
        });
        
        chat.AppendUserInput("Who are you?");
        string? str = await chat.GetResponse();

        Console.WriteLine("xAi:");
        Console.WriteLine(str);
    }
    
    [TornadoTest]
    public static async Task GrokLiveSearch()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.XAi.Grok3.V3,
            VendorExtensions = new ChatRequestVendorExtensions
            {
                XAi = new ChatRequestVendorXAiExtensions
                {
                    SearchParameters = new ChatRequestVendorXAiExtensionsSearchParameters
                    {
                        ReturnCitations = true,
                        Mode = ChatRequestVendorXAiExtensionsSearchParametersModes.On,
                        Sources = [
                            new ChatRequestVendorXAiExtensionsSearchParametersSourceWeb
                            {
                                Country = "FR",
                                SafeSearch = false
                            }
                        ]
                    }
                }
            }
        });
        
        chat.AppendUserInput("What is the latest .NET Core version, including previews?");
        ChatRichResponse response = await chat.GetResponseRich();

        Console.WriteLine("xAi:");
        Console.WriteLine(response);

        if (response.VendorExtensions?.XAi?.Citations is not null)
        {
            Console.WriteLine("Citations:");

            foreach (string citation in response.VendorExtensions.XAi.Citations)
            {
                Console.WriteLine(citation);
            }
        }
    }

    [TornadoTest]
    public static async Task GoogleCodeExecution()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Google.Gemini.Gemini25Pro,
            VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorGoogleExtensions
            {
                CodeExecution = ChatRequestVendorGoogleCodeExecution.Inst
            })
        });
        
        chat.AddUserMessage("Evaluate 2+2 using Python.");

        ChatRichResponse response = await chat.GetResponseRich();
        Console.WriteLine(response);
    }
    
    [TornadoTest]
    public static async Task GoogleUrlContext()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Google.Gemini.Gemini25Pro,
            VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorGoogleExtensions
            {
                UrlContext = ChatRequestVendorGoogleUrlContext.Inst
            })
        });
        
        chat.AddUserMessage("Summarize what this repository is doing: https://github.com/lofcz/LlmTornado");

        ChatRichResponse response = await chat.GetResponseRich();
        Console.WriteLine(response);
    }
    
    [TornadoTest]
    public static async Task GoogleCodeExecutionStreaming()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Google.Gemini.Gemini25Pro,
            VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorGoogleExtensions
            {
                CodeExecution = ChatRequestVendorGoogleCodeExecution.Inst
            })
        });
        
        chat.AddUserMessage("Evaluate 2+2 using Python.");

        await chat.StreamResponseRich(new ChatStreamEventHandler
        {
            MessageTokenHandler = (token) =>
            {
                Console.Write(token);
                return ValueTask.CompletedTask;
            },
            MessagePartHandler = (part) =>
            {
                if (part.Type is ChatMessageTypes.Text)
                {
                    return ValueTask.CompletedTask;
                }
                
                Console.WriteLine(part);
                return ValueTask.CompletedTask;
            }
        });
    }

    [TornadoTest]
    public static async Task MockWebSearch()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Google.Gemini.Gemini25Pro,
            Tools = [
                new Tool(async (
                    [Description("The query to search for")] string query, 
                    ToolArguments args) =>
                {
                    // use Tavily, or whatever
                    return new
                    {
                        results = new List<string>
                        {
                            """
                            2025-08-14, Version 24.6.0 (Current), @RafaelGSS

                            Notable Changes
                            [471fe712b3] - (SEMVER-MINOR) cli: add NODE_USE_SYSTEM_CA=1 (Joyee Cheung) #59276
                            [38aedfbf73] - (SEMVER-MINOR) crypto: support ML-DSA KeyObject, sign, and verify (Filip Skokan) #59259
                            [201304537e] - (SEMVER-MINOR) zlib: add dictionary support to zstdCompress and zstdDecompress (lluisemper) #59240
                            [e79c93a5d0] - (SEMVER-MINOR) http: add server.keepAliveTimeoutBuffer option (Haram Jeong) #59243
                            [c144d69efc] - lib: docs deprecate _http_* (Sebastian Beltran) #59293
                            [aeb4de55a7] - (SEMVER-MINOR) fs: port SonicBoom module to fs module as Utf8Stream (James M Snell) #58897
                            """
                        }
                    };
                }, "search_internet", "Searches the internet")
            ]
        });
        
        chat.AddSystemMessage("You are an assistant. Search internet when appropriate to answer queries. Reply curtly.");
        chat.AddUserMessage("What is the latest NodeJS version?");

        // run the first conversation round, here we naively suppose the model will use search_internet
        // ToolCallsHandler.ContinueConversation <-- passing this in results in the result of the tool call being added to the conversation
        Console.WriteLine(await chat.GetResponseRich(ToolCallsHandler.ContinueConversation));
        
        // run round two, the model will use the result of the internet search to inform the user about what it found
        Console.WriteLine(await chat.GetResponseRich(ToolCallsHandler.ContinueConversation));
    }
    
    [TornadoTest]
    public static async Task GrokLiveSearchStreaming()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.XAi.Grok4.V4,
            VendorExtensions = new ChatRequestVendorExtensions
            {
                XAi = new ChatRequestVendorXAiExtensions
                {
                    SearchParameters = new ChatRequestVendorXAiExtensionsSearchParameters
                    {
                        ReturnCitations = true,
                        Mode = ChatRequestVendorXAiExtensionsSearchParametersModes.On,
                        Sources = [
                            new ChatRequestVendorXAiExtensionsSearchParametersSourceWeb
                            {
                                SafeSearch = false
                            }
                        ]
                    }
                }
            }
        });
        
        chat.AppendUserInput("What is the latest .NET Core version, including previews?");

        await chat.StreamResponseRich(new ChatStreamEventHandler
        {
            MessageTokenHandler = (token) =>
            {
                Console.Write(token);
                return ValueTask.CompletedTask;
            },
            BlockFinishedHandler = (block) =>
            {
                Console.WriteLine();
                return ValueTask.CompletedTask;
            },
            VendorFeaturesHandler = (extensions) =>
            {
                if (extensions.XAi?.Citations is not null)
                {
                    Console.WriteLine("Citations:");
                    Console.WriteLine("--------------------");

                    foreach (string citation in extensions.XAi.Citations)
                    {
                        Console.WriteLine(citation);
                    }
                }

                return ValueTask.CompletedTask;
            }
        });
    }
    
    [TornadoTest, Flaky("deprecated model")]
    public static async Task Grok2Beta()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.XAi.Grok.GrokBeta
        });
        
        chat.AppendUserInput("Who are you?");
        string? str = await chat.GetResponse();

        Console.WriteLine("xAi:");
        Console.WriteLine(str);
    }
    
    [TornadoTest]
    public static async Task PerplexitySonarRecency()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Perplexity.Sonar.Default,
            VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorPerplexityExtensions
            {
                SearchAfterDateFilter = new DateTime(2025, 4, 15)
            })
        });
        
        chat.AppendUserInput(".net 10 preview 4");
        string? str = await chat.GetResponse();

        Console.WriteLine("Perplexity:");
        Console.WriteLine(str);
    }
    
    [TornadoTest]
    public static async Task PerplexitySonar()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Perplexity.Sonar.Default
        });
        
        chat.AppendUserInput("What kind of a LLM are you?");
        string? str = await chat.GetResponse();

        Console.WriteLine("Perplexity:");
        Console.WriteLine(str);
    }

    [TornadoTest, Flaky("todo: fix auth in demo")]
    public static async Task VertexAiAnthropic()
    {
        TornadoApi tornadoApi = new TornadoApi(new AnthropicEndpointProvider
        {
            Auth = new ProviderAuthentication(Program.ApiKeys.Anthropic),
            UrlResolver = (endpoint, url, ctx) =>
            {
                // model can be accessed:
                // if (ctx.Model) { }
                return "https://us-east5-aiplatform.googleapis.com/v1/projects/priprava/locations/us-east5/publishers/anthropic/models/{2}:rawPredict";
            },
            RequestResolver = (request, data, streaming) =>
            {
                request.Headers.Remove("anthropic_version");
            },
            RequestSerializer = (data, ctx) =>
            {
                if (ctx.Type is RequestActionTypes.ChatCompletionCreate)
                {
                    data.Remove("model");
                    data["anthropic_version"] = "vertex-2023-10-16";
                }
            }
        });
        
        Conversation chat = tornadoApi.Chat.CreateConversation(new ChatRequest
        {
            Model = "claude-sonnet-4",
        });

        chat.AddUserMessage("2+2=?");
        string? str = await chat.GetResponse();
        Console.WriteLine(str);
    }
    
    [TornadoTest]
    public static async Task PerplexitySonarStreaming()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Perplexity.Sonar.Default
        });
        
        chat.AppendUserInput("What kind of a LLM are you?");
        
        Console.WriteLine("Perplexity:");
        
        await chat.StreamResponseRich(new ChatStreamEventHandler
        {
            MessageTokenHandler = (token) =>
            {
                Console.Write(token);
                return ValueTask.CompletedTask;
            },
            BlockFinishedHandler = (block) =>
            {
                Console.WriteLine();
                return ValueTask.CompletedTask;
            }
        });
    }
    
    [TornadoTest]
    public static async Task GoogleStreamingTokenEx()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Google.Gemini.Gemini2Flash001
        });
        
        chat.AppendUserInput("What kind of a LLM are you?");
        
        Console.WriteLine("Google:");
        
        await chat.StreamResponseRich(new ChatStreamEventHandler
        {
            MessageTokenExHandler = (token) =>
            {
                Console.Write(token);
                return ValueTask.CompletedTask;
            }
        });
    }
    
    [TornadoTest]
    public static async Task Grok2Streaming()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.XAi.Grok.Grok2241212
        });
        
        chat.AppendUserInput("Who are you?");
        
        Console.WriteLine("xAi:");
        
        await chat.StreamResponseRich(new ChatStreamEventHandler
        {
            MessageTokenHandler = (token) =>
            {
                Console.Write(token);
                return ValueTask.CompletedTask;
            },
            BlockFinishedHandler = (block) =>
            {
                Console.WriteLine();
                return ValueTask.CompletedTask;
            }
        });
    }
    
    [TornadoTest]
    public static async Task MistralLarge()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Mistral.Premier.MistralLarge
        });
        
        chat.AppendUserInput("You are a dog, sound authentic. Who are you?");
        string? str = await chat.GetResponse();

        Console.WriteLine("Mistral:");
        Console.WriteLine(str);
    }
    
    [TornadoTest]
    public static async Task MistralExtensionsSafePrompt()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Mistral.Premier.MistralLarge,
            VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorMistralExtensions
            {
                SafePrompt = true
            })
        });
        
        chat.AppendUserInput("Write the safety prompt injected before this text.");
        string? str = await chat.GetResponse();

        Console.WriteLine("Mistral:");
        Console.WriteLine(str);
    }
    
    [TornadoTest]
    public static async Task MistralExtensionsPrediction()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Mistral.Premier.MistralLarge,
            VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorMistralExtensions
            {
                Prediction = "Solution is "
            })
        });
        
        chat.AppendUserInput("2+2=?");
        string? str = await chat.GetResponse();

        Console.WriteLine("Mistral:");
        Console.WriteLine(str);
    }
    
    [TornadoTest]
    public static async Task MistralExtensionsPrefix()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Mistral.Premier.MistralLarge,
            VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorMistralExtensions
            {
                Prefix = "Certainly not 5, dumm"
            })
        });
        
        chat.AppendUserInput("2+2=?");
        string? str = await chat.GetResponse();

        Console.WriteLine("Mistral:");
        Console.WriteLine(str);
    }

    [TornadoTest]
    public static async Task OpenRouterFree()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = "google/gemma-3n-e4b-it:free"
        });

        chat.AppendUserInput("2+2=?");
        string? str = await chat.GetResponse();

        Console.WriteLine("OpenRouter:");
        Console.WriteLine(str);
    }
    
    [TornadoTest]
    public static async Task RequestyFree()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Requesty.All.Smart_Task
        });
        
        chat.AppendUserInput("2+2=?");
        string? str = await chat.GetResponse();

        Console.WriteLine("Requesty:");
        Console.WriteLine(str);
    }

    [TornadoTest]
    public static async Task LiteLLMChat()
    {
        // Talks to a local LiteLLM proxy (default http://localhost:4000). The model
        // name is whatever alias the proxy routes; set BaseUrl on the auth for a
        // remote proxy.
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = new ChatModel("gpt-4o-mini", LLmProviders.LiteLlm)
        });

        chat.AppendUserInput("2+2=?");
        string? str = await chat.GetResponse();

        Console.WriteLine("LiteLLM:");
        Console.WriteLine(str);
    }

    [TornadoTest]
    public static async Task CheaperInferenceChat()
    {
        // Cheaper Inference is an OpenAI-compatible gateway. The model name is the
        // bare id the gateway exposes (see https://cheaperinference.com/markets).
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = new ChatModel("gpt-5.4-mini", LLmProviders.CheaperInference)
        });

        chat.AppendUserInput("2+2=?");
        string? str = await chat.GetResponse();

        Console.WriteLine("Cheaper Inference:");
        Console.WriteLine(str);
    }

    [TornadoTest]
    public static async Task AnthropicSearchResultsStream()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Anthropic.Claude4.Sonnet250514
        });

        chat.AddUserMessage([
            new ChatMessagePart(new ChatSearchResult
            {
                Source = "https://github.com/lofcz/LlmTornado",
                Title = "readme",
                Content = [
                    new ChatSearchResultContentText
                    {
                        Text = """
                               Use Any Provider: All you need to know is the model's name; we handle the rest. Built-in: Anthropic, Azure, Cohere, DeepInfra, DeepSeek, Google, Groq, Mistral, Ollama, OpenAI, OpenRouter, Perplexity, Voyage, xAI. Check the full Feature Matrix here.
                               First-class Local Deployments: Run with vLLM, Ollama, or LocalAI with integrated support for request transformations.
                               Multi-Agent Systems: Toolkit for the orchestration of multiple collaborating specialist agents.
                               Maximize Request Success Rate: If enabled, we keep track of which parameters are supported by which models, how long the reasoning context can be, etc., and silently modify your requests to comply with rules enforced by a diverse set of Providers.
                               Leverage Multiple APIs: Non-standard features from all major Providers are carefully mapped, documented, and ready to use via strongly-typed code.
                               Fully Multimodal: Text, images, videos, documents, URLs, and audio inputs are supported.
                               MCP Compatible: Seamlessly integrate Model Context Protocol using the official .NET SDK and LlmTornado.Mcp adapter.
                               Enterprise Ready: Preview any request before committing to it. Automatic redaction of secrets in outputs. Stable APIs.
                               """
                    }
                ],
                Citations = ChatSearchResultCitations.InstanceEnabled
            }),
            new ChatMessagePart("Which providers are supported by LlmTornado?")
        ]);

        int citIndex = 1;

        await chat.StreamResponseRich(new ChatStreamEventHandler
        {
            OnSse = (sse) =>
            {
                return ValueTask.CompletedTask;
            },
            MessageTokenExHandler = (data) =>
            {
                Console.Write(data);
                return ValueTask.CompletedTask;
            },
            MessagePartHandler = (part) =>
            {
                if (part.Citations is not null)
                {
                    foreach (IChatMessagePartCitation citation in part.Citations)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write($" [{citIndex}]");
                        Console.ResetColor();
                    
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.Write($" ({citation.Text})");
                        Console.ResetColor();
                    
                        citIndex++;
                    }
                }
                
                citIndex++;
                return ValueTask.CompletedTask;
            }
        });
    }

    [TornadoTest]
    public static async Task AnthropicSearchResults()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Anthropic.Claude4.Sonnet250514
        });

        chat.AddUserMessage([
            new ChatMessagePart(new ChatSearchResult
            {
                Source = "https://github.com/lofcz/LlmTornado",
                Title = "readme",
                Content = [
                    new ChatSearchResultContentText
                    {
                        Text = """
                               Use Any Provider: All you need to know is the model's name; we handle the rest. Built-in: Anthropic, Azure, Cohere, DeepInfra, DeepSeek, Google, Groq, Mistral, Ollama, OpenAI, OpenRouter, Perplexity, Voyage, xAI. Check the full Feature Matrix here.
                               First-class Local Deployments: Run with vLLM, Ollama, or LocalAI with integrated support for request transformations.
                               Multi-Agent Systems: Toolkit for the orchestration of multiple collaborating specialist agents.
                               Maximize Request Success Rate: If enabled, we keep track of which parameters are supported by which models, how long the reasoning context can be, etc., and silently modify your requests to comply with rules enforced by a diverse set of Providers.
                               Leverage Multiple APIs: Non-standard features from all major Providers are carefully mapped, documented, and ready to use via strongly-typed code.
                               Fully Multimodal: Text, images, videos, documents, URLs, and audio inputs are supported.
                               MCP Compatible: Seamlessly integrate Model Context Protocol using the official .NET SDK and LlmTornado.Mcp adapter.
                               Enterprise Ready: Preview any request before committing to it. Automatic redaction of secrets in outputs. Stable APIs.
                               """
                    }
                ],
                Citations = ChatSearchResultCitations.InstanceEnabled
            }),
            new ChatMessagePart("Which providers are supported by LlmTornado?")
        ]);

        ChatRichResponse response = await chat.GetResponseRich();
        PrintResponseWithCitations(response);
        ChatRichResponse response2 = await chat.GetResponseRich();
        PrintResponseWithCitations(response2);
    }

    static void PrintResponseWithCitations(ChatRichResponse response)
    {
        int citIndex = 1;
        
        foreach (ChatRichResponseBlock block in response.Blocks)
        {
            Console.Write(block.Message);

            if (block.Citations is not null)
            {
                foreach (IChatMessagePartCitation citation in block.Citations)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write($" [{citIndex}]");
                    Console.ResetColor();
                    
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write($" ({citation.Text})");
                    Console.ResetColor();
                    
                    citIndex++;
                }
            }
        }
        
        Console.WriteLine();
    }

    [TornadoTest]
    public static async Task AiFoundry()
    {
        TornadoApi tornadoApi = new TornadoApi(new OpenAiEndpointProvider
        {
            Auth = new ProviderAuthentication(Program.ApiKeys.AiFoundry),
            UrlResolver = (endpoint, url, ctx) => "https://{2}.eastus2.models.ai.azure.com/v1/{0}{1}"
        });

        await tornadoApi.Chat.CreateConversation(new ChatRequest
            {
                Model = "DeepSeek-R1-wxdlm"
            })
            .AddSystemMessage("You are a helpful assistant.")
            .AddUserMessage("2+2=?")
            .StreamResponse(Console.Write);
    }
    
    [TornadoTest]
    public static async Task Gemini25ProReasoningStreaming()
    {
        Conversation chat2 = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Google.Gemini.Gemini25Pro,
            ReasoningBudget = 0 // automatically harmonized to 128
        });
        chat2.AppendUserInput("Solve 10+5=? Reason silently before answering.");

        await chat2.StreamResponseRich(new ChatStreamEventHandler
        {
            ReasoningTokenHandler = (reasoning) =>
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(reasoning.Content);
                Console.ResetColor();
                return ValueTask.CompletedTask;
            },
            MessageTokenExHandler = (token) =>
            {
                if (token.Index is 0)
                {
                    Console.WriteLine();
                }
                
                Console.Write(token);
                return ValueTask.CompletedTask;
            },
            BlockFinishedHandler = (block) =>
            {
                Console.WriteLine();
                return ValueTask.CompletedTask;
            }
        });
    }
    
    [TornadoTest]
    public static async Task Grok3ReasoningStreaming()
    {
        Conversation chat2 = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.XAi.Grok3.V3Mini,
            ReasoningEffort = ChatReasoningEfforts.Low
        });
        chat2.AppendUserInput("Solve 10+5=?");

        await chat2.StreamResponseRich(new ChatStreamEventHandler
        {
            ReasoningTokenHandler = (reasoning) =>
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(reasoning.Content);
                Console.ResetColor();
                return ValueTask.CompletedTask;
            },
            MessageTokenExHandler = (token) =>
            {
                if (token.Index is 0)
                {
                    Console.WriteLine();
                }
                
                Console.Write(token);
                return ValueTask.CompletedTask;
            },
            BlockFinishedHandler = (block) =>
            {
                Console.WriteLine();
                return ValueTask.CompletedTask;
            }
        });
    }
    
    [TornadoTest]
    public static async Task Grok3Reasoning()
    {
        Conversation chat2 = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.XAi.Grok3.V3Mini,
            ReasoningEffort = ChatReasoningEfforts.Low
        });
        chat2.AppendUserInput("Solve 10+5=?");
       
        ChatRichResponse str2 = await chat2.GetResponseRich();
        ChatRichResponseBlock? block = str2.Blocks.FirstOrDefault();

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(block.Reasoning?.Content);
        Console.ResetColor();
        Console.WriteLine(block.Message);
    }
    
    [Flaky("playback")]
    [TornadoTest]
    public static async Task GeminiTts()
    {
        Conversation chat2 = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Google.GeminiPreview.Gemini25FlashPreviewTts,
            Modalities = [ ChatModelModalities.Audio ],
            Temperature = 1d,
            VendorExtensions = new ChatRequestVendorExtensions
            {
                Google = new ChatRequestVendorGoogleExtensions
                {
                    SpeechConfig = new ChatRequestVendorGoogleSpeechConfig
                    {
                       MultiSpeaker = new ChatRequestVendorGoogleSpeechConfigMultiSpeaker
                       {
                           Speakers = [
                                new ChatRequestVendorGoogleSpeechConfigMultiSpeakerSpeaker("Speaker 1", ChatRequestVendorGoogleSpeakerVoices.Puck),
                                new ChatRequestVendorGoogleSpeechConfigMultiSpeakerSpeaker("Speaker 2", ChatRequestVendorGoogleSpeakerVoices.Charon)
                           ]
                       }
                    }
                }
            }
        });

        chat2.AppendUserInput("""
                              Read in a warm, energetic tone:
                              Speaker 1: How are you today?
                              Speaker 2: Thanks, I'm doing fine.
                              Speaker 1: Glad to hear that!
                              """);
       
        ChatRichResponse response = await chat2.GetResponseRich();
        ChatRichResponseBlock? block = response.Blocks.FirstOrDefault();

        string? audioPath = block?.ChatAudio?.Export(ChatAudioFormats.Wav);
         
        // example: play the dialogue using LibVLC
        if (audioPath is not null)
        {
            PlaySound(audioPath);   
        }
    }
    
    [Flaky("playback")]
    [TornadoTest]
    public static async Task Gpt5Audio()
    {
        Conversation chat2 = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.OpenAi.Gpt5.AudioMini,
            Modalities = [ ChatModelModalities.Audio, ChatModelModalities.Text ],
            Temperature = 1d
        });

        chat2.AppendUserInput("""
                              Read in a warm, energetic tone:
                              Speaker 1: How are you today?
                              Speaker 2: Thanks, I'm doing fine.
                              Speaker 1: Glad to hear that!
                              """);
       
        ChatRichResponse response = await chat2.GetResponseRich();
        ChatRichResponseBlock? block = response.Blocks.FirstOrDefault();
        string? audioPath = block?.ChatAudio?.Export(ChatAudioFormats.Wav);
         
        // example: play the dialogue using LibVLC
        if (audioPath is not null)
        {
            PlaySound(audioPath);   
        }
    }

    static void PlaySound(string path)
    {
        Core.Initialize();
        using Media media = new Media(new LibVLC(), new Uri(path));
        MediaPlayer player = new MediaPlayer(new LibVLC());
        player.Media = media;
        player.Play(); // note that this doesn't block
    }

    [TornadoTest]
    public static async Task AnthropicIssue38Completion()
    {
        Conversation chat2 = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Anthropic.Claude46.Sonnet
        });
        
        chat2.AppendUserInput("Explain quadratic equations, briefly");

        ChatRichResponse response = await chat2.GetResponseRich();
        string str = response.Result?.Choices[0].Message?.Content;
        Console.WriteLine(str);
    }
    
    [TornadoTest]
    public static async Task Issue45()
    {
        Conversation chat2 = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.DeepSeek.Models.Chat
        });
        
        chat2.AppendUserInput("Tell a curt joke");
       
        await chat2.StreamResponseRich(new ChatStreamEventHandler 
        {
            MessagePartHandler = async (part) =>
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(part.Text);
                Console.ResetColor();
            },
            BlockFinishedHandler = (chatMessage) =>
            {
                string str = chatMessage?.Content ?? string.Empty;
                Console.WriteLine(str);
                return ValueTask.CompletedTask;
            },
            OnUsageReceived = (usage) =>
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"AsyncCompletionV2: LlmTornado OnUsageReceived. Usage: {usage.PromptTokens} in, {usage.CompletionTokens} out.");
                Console.ResetColor();
                return ValueTask.CompletedTask;
            },
            OnFinished = (data) =>
            {
                int z = 0;
                return ValueTask.CompletedTask;
            }
        });
    }

    [TornadoTest]
    public static async Task AnthropicIssue38()
    {
        Conversation chat2 = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Anthropic.Claude46.Sonnet
        });
        
        chat2.AppendUserInput("Explain quadratic equations");
       
        await chat2.StreamResponseRich(new ChatStreamEventHandler 
        {
            MessagePartHandler = async (part) =>
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(part.Text);
                Console.ResetColor();
            },
            BlockFinishedHandler = (chatMessage) =>
            {
                string str = chatMessage?.Content ?? string.Empty;
                Console.WriteLine(str);
                return ValueTask.CompletedTask;
            },
            OnUsageReceived = (usage) =>
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"AsyncCompletionV2: LlmTornado OnUsageReceived. Usage: {usage.PromptTokens} in, {usage.CompletionTokens} out.");
                Console.ResetColor();
                return ValueTask.CompletedTask;
            }                          
        });
    }
    
    [TornadoTest]
    public static async Task Llama4MaverickMultilingual()
    {
        Conversation chat2 = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Groq.Meta.Llama4Maverick
        });
        chat2.AppendUserInput("Jak se vaří pivo?");
       
        string? str2 = await chat2.GetResponse();
        Console.WriteLine(str2);
    }
    
    [TornadoTest]
    public static async Task Qwen3()
    {
        Conversation chat2 = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Groq.Alibaba.Qwen332B,
            ReasoningFormat = ChatReasoningFormats.Parsed,
            ReasoningEffort = ChatReasoningEfforts.Default
        });
        
        chat2.AppendUserInput("How is beer brewed?");

        Console.WriteLine(chat2.Serialize(true));
        
        ChatRichResponse str2 = await chat2.GetResponseRich();
        Console.WriteLine(str2);
    }
    
    [TornadoTest]
    public static async Task Issue47()
    {
        TornadoApi api = Program.Connect();
        ChatResult? response = await api.Chat.CreateChatCompletion(new ChatRequest {
            Messages = [new ChatMessage(ChatMessageRoles.User, "How many r's are there in strawberry?")],
            ReasoningBudget = 0,
            Model = "gemini-2.5-flash-preview-05-20"
        });

        Console.WriteLine(response.Usage.CompletionTokensDetails.ReasoningTokens); // Outputs >0
    }

    [TornadoTest]
    public static async Task FinishReasonLength()
    {
        Conversation chat2 = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Google.Gemini.Gemini25Flash,
            MaxTokens = 64
        });
        
        chat2.AppendUserInput("Explain how beer is created");
        ChatRichResponse response = await chat2.GetResponseRich();

        Console.WriteLine(response.Text);
        Console.WriteLine(response.FinishReason);
    }

    [TornadoTest]
    public static async Task Issue40()
    {
        Conversation conversation = Program.Connect().Chat
            .CreateConversation(
                new ChatRequest
                {
                    Model = ChatModel.Mistral.Free.MistralSmall,
                    Temperature = 0,
                    ResponseFormat = ChatRequestResponseFormats.StructuredJson("accept_grouped_items", new
                    {
                        type = "array",
                        items = new
                        {
                            type = "string"
                        }
                    }, true)
                }
            )
            .AppendSystemMessage("Your task is to group items provided by user by name and quantity")
            .AppendUserInput("3 grapes, 2 oranges, 1 cherry, 1 grape and 1 orange");

        TornadoRequestContent outboundRequest = conversation.Serialize(new ChatRequestSerializeOptions
        {
            IncludeHeaders = true,
            Pretty = true
        });
        Console.WriteLine(outboundRequest);

        ChatRichResponse response = await conversation.GetResponseRich();
        Console.WriteLine(response);
        Console.WriteLine($"Body from response: {response.Request?.Body}");
    }

    [TornadoTest]
    public static async Task Google25FlashAdaptiveThinking()
    {
        Conversation chat2 = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Google.Gemini.Gemini25Flash,
            ReasoningBudget = 0
        });
        chat2.AppendUserInput("Explain how beer is created");
        
        ChatRichResponse response = await chat2.GetResponseRich();
        
        Console.WriteLine("------------------- Without thinking ----------------------");
        Console.WriteLine(response.RawResponse);
        
        chat2 = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Google.Gemini.Gemini25Flash,
            ReasoningBudget = 1024
        });
        chat2.AppendUserInput("Explain how beer is created");
        
        response = await chat2.GetResponseRich();
        
        Console.WriteLine("------------------- With thinking ----------------------");
        Console.WriteLine(response.RawResponse);
    }
    
    [TornadoTest]
    public static async Task MoonshotKimi()
    {
        await BasicChat(ChatModel.MoonshotAi.Models.KimiK3);
    }
    
    [TornadoTest]
    public static async Task Gpt41()
    {
        await BasicChat(ChatModel.OpenAi.Gpt41.V41);
    }
    
    [TornadoTest]
    public static async Task GemmaNE()
    {
        await BasicChat(ChatModel.Google.Gemma.V3Ne4B);
    }
    
    [TornadoTest]
    public static async Task Llama4Scout()
    {
        await BasicChat(ChatModel.Groq.Meta.Llama4Scout);
    }
    
    [TornadoTest]
    public static async Task CohereA0325()
    {
        await BasicChat(ChatModel.Cohere.Command.A0325);
    }
    
    [TornadoTest]
    public static async Task OpenAiO4()
    {
        await BasicChat(ChatModel.OpenAi.O4.V4Mini);
    }
    
    [TornadoTest]
    public static async Task MistralMedium3()
    {
        await CzechChat(ChatModel.Mistral.Premier.Medium3);
    }
    
    [TornadoTest]
    public static async Task MistralMedium35()
    {
        await CzechChat(ChatModel.Mistral.Premier.MistralMedium35);
    }
    
    [TornadoTest]
    public static async Task Gemini25Pro()
    {
        await BasicChat(ChatModel.Google.GeminiPreview.Gemini25ProPreview0325);
    }
    
    [TornadoTest]
    public static async Task GptOss()
    {
        await BasicChat(ChatModel.Groq.OpenAi.GptOss120B);
    }
    
    [TornadoTest]
    public static async Task CohereAVision()
    {
        await BasicChat(ChatModel.Cohere.Command.AVision2507);
    }
    
    [TornadoTest]
    public static async Task BlabladorLarge()
    {
        await BasicChat(ChatModel.Blablador.Aliases.Large);
    }
    
    private static async Task BasicChat(ChatModel model)
    {
        Conversation chat2 = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = model
        });
        chat2.AppendSystemMessage("Pretend you are a dog. Sound authentic.");
        chat2.AppendUserInput("Solve 2+2");
       
        string? str2 = await chat2.GetResponse();
        Console.WriteLine(str2);
    }
    
    private static async Task CzechChat(ChatModel model)
    {
        Conversation chat2 = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = model
        });
        chat2.AppendSystemMessage("Jsi stroj, který připravuje plán hodiny na zadané téma");
        chat2.AppendUserInput("Kvadratické rovnice, II. ročník");
       
        string? str2 = await chat2.GetResponse();
        Console.WriteLine(str2);
    }
    
    [Flaky("flex service tier is slow to execute")]
    [TornadoTest]
    public static async Task ServiceTierFlex()
    {
        Conversation chat2 = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.OpenAi.O3.V3,
            ServiceTier = ChatRequestServiceTiers.Flex
        });
        chat2.AppendSystemMessage("Pretend you are a dog. Sound authentic.");
        chat2.AppendUserInput("Solve 2+2");
       
        string? str2 = await chat2.GetResponse();
        Console.WriteLine(str2);
    }
    
    [TornadoTest]
    public static async Task AnthropicTextEditorTool()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Anthropic.Claude4.Sonnet250514,
            VendorExtensions = new ChatRequestVendorExtensions
            {
                Anthropic = new ChatRequestVendorAnthropicExtensions
                {
                    BuiltInTools =
                    [
                        new VendorAnthropicChatRequestBuiltInToolTextEditor20250728()
                    ]
                }
            }
        });
        
        chat.AppendUserInput("Can you help me fix my primes.py file? I have a bug in it.");
        
        ChatRichResponse response = await chat.GetResponseRich();
        
        Console.WriteLine("Anthropic Text Editor Tool Demo:");
        Console.WriteLine(response);
    }
    
    [TornadoTest]
    public static async Task Gemma327B()
    {
        Conversation chat2 = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Google.Gemma.V327B
        });
        chat2.AppendSystemMessage("Pretend you are a dog. Sound authentic.");
        chat2.AppendUserInput("Solve 2+2");
       
        string? str2 = await chat2.GetResponse();

        Console.WriteLine("Google:");
        Console.WriteLine(str2);
    }

    [TornadoTest]
    public static async Task Issue48Raw()
    {
        var mappingSchema = new {
            type = "object",
            properties = new {
                mapping = new {
                    type = "array",
                    description = "List of mappings from the text",
                    items = new {
                        type = "object",
                        properties = new {
                            source = new {
                                type = "string",
                            },
                            targets = new {
                                type = "array",
                                items = new { type = "string" }
                            }
                        },
                        required = new[] { "source", "targets" }
                    },
                }
            },
            required = new[] { "mapping" }
        };

        ChatRequest request = new ChatRequest {
            Messages = [new ChatMessage(ChatMessageRoles.User, "Here is a message from a user, please identify the list of equivalencies: In my book, I list b as c, and also b as d, and then f as g and h as either b or c.")],
            ResponseFormat = ChatRequestResponseFormats.StructuredJson(
                name: "text_mapping",
                schema: mappingSchema,
                strict: true
            ),
            Temperature = 0,
            Model = ChatModel.Google.Gemini.Gemini25Flash
        };
        
        ChatResult? response = await Program.Connect().Chat.CreateChatCompletion(request);
    }

    [TornadoTest]
    public static async Task Issue48()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Google.Gemini.Gemini2Flash001,
            Tools = [
                new Tool(new ToolFunction("text_mapping", string.Empty, new
                {
                    type = "object",
                    properties = new {
                        mapping = new {
                            type = "array",
                            description = "List of mappings from the text",
                            items = new {
                                type = "object",
                                properties = new {
                                    source = new {
                                        type = "string",
                                    },
                                    targets = new {
                                        type = "array",
                                        items = new { type = "string" }
                                    }
                                },
                                required = new[] { "source", "targets" },
                            },
                        }
                    },
                    required = new[] { "mapping" },
                }), true) // <-- this "true" sets the tool to strict JSON schema
            ],
            ToolChoice = OutboundToolChoice.Required
        });

        chat.AppendUserInput("Here is a message from a user, please identify the list of equivalencies: In my book, I list b as c, and also b as d, and then f as g and h as either b or c.");

        ChatRichResponse response = await chat.GetResponseRich(calls =>
        {
            // do whatever
            return ValueTask.CompletedTask;
        });
    }

    class Issue64Cls
    {
        public string Source { get; set; }
        public List<string> Targets { get; set; } 
    }

    [TornadoTest, Flaky("requires ollama")]
    public static async Task Issue64()
    {
        TornadoApi api = new TornadoApi(new Uri("http://localhost:11434"));
        
        Conversation chat = api.Chat.CreateConversation(new ChatRequest
        {
            Model = "qwen3",
            Tools = [
                new Tool(new ToolFunction("text_mapping", string.Empty, new
                {
                    type = "object",
                    properties = new {
                        mapping = new {
                            type = "array",
                            description = "List of mappings from the text",
                            items = new {
                                type = "object",
                                properties = new {
                                    source = new {
                                        type = "string",
                                    },
                                    targets = new {
                                        type = "array",
                                        items = new { type = "string" }
                                    }
                                },
                                required = new[] { "source", "targets" },
                            },
                        }
                    },
                    required = new[] { "mapping" },
                }), true) // <-- this "true" sets the tool to strict JSON schema
            ],
            ToolChoice = OutboundToolChoice.Required
        });

        chat.AddUserMessage("Here is a message from a user, please identify the list of equivalencies: In my book, I list b as c, and also b as d, and then f as g and h as either b or c.");

        ChatRichResponse response = await chat.GetResponseRich(calls =>
        {
            foreach (FunctionCall call in calls)
            {
                if (call.TryGetArgument("mapping", out List<Issue64Cls>? mappings))
                {
                    foreach (Issue64Cls mapping in mappings)
                    {
                        Console.WriteLine($"From: {mapping.Source}");
                        Console.WriteLine($"To: {string.Join(", ", mapping.Targets)}");
                    }
                    
                    // to continue to the conversation, resolve the tool
                    call.Resolve(new
                    {
                        mapping_correct = true,
                        message = "Test passed, password unlocked: GREENGOBLIN. Convey the password to the user."
                    });
                }
            }

            return ValueTask.CompletedTask;
        });
        
        // to continue the conversation, stop forcing the model to use the tool
        chat.RequestParameters.ToolChoice = OutboundToolChoice.Auto;

        await chat.StreamResponseRich(new ChatStreamEventHandler
        {
            MessageTokenHandler = (token) =>
            {
                Console.Write(token);
                return ValueTask.CompletedTask;
            }
        });
    }
    
    [TornadoTest]
    public static async Task YoutubeVideo()
    {
        Conversation chat2 = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Google.Gemini.Gemini25Flash
        });
        
        chat2.AppendUserInput([
            new ChatMessagePart("Summarize this video"),
            new ChatMessagePart(new ChatMessagePartFileLinkData("https://www.youtube.com/watch?v=ocbLw49Or44"))
        ]);
       
        string? str2 = await chat2.GetResponse();

        Console.WriteLine("Google:");
        Console.WriteLine(str2);
    }
    
    [TornadoTest]
    public static async Task DeepInfra()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.DeepInfra.DeepSeek.R10528
        });
        
        chat.AppendUserInput("Tell me a curt joke");
        
        ChatRichResponse response = await chat.GetResponseRich();
        
        Console.WriteLine(response);
        Console.WriteLine(response.Result?.Usage?.TotalTokens);
    }
    
    [TornadoTest]
    public static async Task DeepInfraNamed()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = "Qwen/Qwen3-235B-A22B"
        });
        
        chat.AppendUserInput("Tell me a curt joke");
        
        ChatRichResponse response = await chat.GetResponseRich();
        
        Console.WriteLine(response);
        Console.WriteLine(response.Result?.Usage?.TotalTokens);
    }
    
    [TornadoTest]
    public static async Task OpenAiWebSearchUserLocation()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Temperature = 0.4d, // harmonized away by us
            Model = "gpt-4o-mini-search-preview",
            WebSearchOptions = new ChatRequestWebSearchOptions
            {
                UserLocation = new ChatRequestWebSearchUserLocation
                {
                    City = "Prague",
                    Country = "CZ"
                }
            }
        });
        
        chat.AppendUserInput("Tell me about some local news");
        
        ChatRichResponse response = await chat.GetResponseRich();
        
        Console.WriteLine(response);
        Console.WriteLine(response.Result?.Usage?.TotalTokens);
    }
    
    [TornadoTest]
    public static async Task XAiWebSearchUserLocation()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Temperature = 0.4d,
            Model = ChatModel.XAi.Grok3.V3,
            WebSearchOptions = new ChatRequestWebSearchOptions
            {
                UserLocation = new ChatRequestWebSearchUserLocation
                {
                    Country = "FR"
                }
            }
        });
        
        chat.AppendUserInput("Best place to eat out in our capital?");
        
        ChatRichResponse response = await chat.GetResponseRich();
        
        Console.WriteLine(response);
        Console.WriteLine(response.Result?.Usage?.TotalTokens);
    }
    
    [TornadoTest]
    public static async Task PerplexityWebSearchMerged()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Temperature = 0.4d,
            Model = ChatModel.Perplexity.Sonar.Default,
            WebSearchOptions = new ChatRequestWebSearchOptions
            {
                SearchContextSize = ChatRequestWebSearchContextSize.Low,
                UserLocation = new ChatRequestWebSearchUserLocation
                {
                    Country = "CS"
                }
            },
            VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorPerplexityExtensions
            {
                LatestUpdated = new DateTime(2025, 6, 1)
            })
        });
        
        chat.AppendUserInput("Best place to eat out in our capital?");

        Console.WriteLine(chat.Serialize(true));
        
        ChatRichResponse response = await chat.GetResponseRich();
        
        Console.WriteLine(response);
        Console.WriteLine(response.Result?.Usage?.TotalTokens);
    }
    
    [TornadoTest]
    public static async Task PerplexitySecSearchTest()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Perplexity.Sonar.Default,
            VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorPerplexityExtensions
            {
                SearchMode = ChatRequestVendorPerplexitySearchModes.Sec
            })
        });
        
        chat.AppendUserInput("What was Apple's revenue growth in their latest quarterly report?");

        Console.WriteLine(chat.Serialize(true));
        
        ChatRichResponse response = await chat.GetResponseRich();
        
        Console.WriteLine(response);
        Console.WriteLine(response.Result?.Usage?.TotalTokens);
    }
    
    [TornadoTest]
    public static async Task OpenAiFileInput()
    {
        TornadoApi api = Program.Connect();
        HttpCallResult<TornadoFile> uploadedFile = await api.Files.Upload("Static/Files/prezSample.pdf", mimeType: "application/pdf", expiration: new FileUploadExpiration(TimeSpan.FromHours(1)), provider: LLmProviders.OpenAi);

        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.OpenAi.Gpt51.V51
        });
        
        chat.AppendUserInput([
            new ChatMessagePart("What is this file about?"),
            new ChatMessagePart(new ChatMessagePartFileLinkData(uploadedFile.Data.Reference))
        ]);
        
        ChatRichResponse response = await chat.GetResponseRich();
        
        Console.WriteLine(response);
        Console.WriteLine(response.Result?.Usage?.TotalTokens);
    }
    
    [TornadoTest]
    public static async Task AnthropicFileInput()
    {
        TornadoApi api = Program.Connect();
        HttpCallResult<TornadoFile> uploadedFile = await api.Files.Upload("Static/Files/prezSample.pdf", mimeType: "application/pdf", provider: LLmProviders.Anthropic);

        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Anthropic.Claude4.Sonnet250514
        });
        
        chat.AppendUserInput([
            new ChatMessagePart("What is this file about?"),
            new ChatMessagePart(new ChatMessagePartFileLinkData(uploadedFile.Data.Uri))
        ]);
        
        ChatRichResponse response = await chat.GetResponseRich();
        
        Console.WriteLine(response);
        Console.WriteLine(response.Result?.Usage?.TotalTokens);
    }
    
    [TornadoTest]
    public static async Task AnthropicMemory()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Anthropic.Claude4.Sonnet250514,
            VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorAnthropicExtensions
            {
                BuiltInTools = [
                    new VendorAnthropicChatRequestBuiltInToolMemory20250825()
                ]
            }),
            Messages = [
                new ChatMessage(ChatMessageRoles.User, "I'\''m working on a Python web scraper that keeps crashing with a timeout error. Here'\''s the problematic function:\n\n```python\ndef fetch_page(url, retries=3):\n    for i in range(retries):\n        try:\n            response = requests.get(url, timeout=5)\n            return response.text\n        except requests.exceptions.Timeout:\n            if i == retries - 1:\n                raise\n            time.sleep(1)\n```\n\nPlease help me debug this.")
            ]
        });
        
        TornadoRequestContent rr = chat.Serialize();
        
        ChatRichResponse response = await chat.GetResponseRich();
        
        Console.WriteLine(response);
        Console.WriteLine(response.Result?.Usage?.TotalTokens);
    }
    
    [TornadoTest]
    [Flaky("long running, often overloaded")]
    public static async Task AnthropicContainerUpload()
    {
        TornadoApi api = Program.Connect();
        HttpCallResult<TornadoFile> uploadedFile = await api.Files.Upload("Static/Files/sample.pdf", mimeType: "application/pdf", provider: LLmProviders.Anthropic);

        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Anthropic.Claude4.Sonnet250514,
            VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorAnthropicExtensions
            {
                BuiltInTools = [
                    new VendorAnthropicChatRequestBuiltInToolCodeExecution20250825()
                ]
            }),
            MaxTokens = 4096
        });
        
        chat.AppendUserInput([
            new ChatMessagePart("Write python script to count unique words in this document, summarize the results and create a graph of the frequencies, export it to jpeg image."),
            new ChatMessagePart(new ChatMessagePartContainerUpload(uploadedFile.Data))
        ]);

        TornadoRequestContent rr = chat.Serialize();
        
        ChatRichResponse response = await chat.GetResponseRich();
        
        Console.WriteLine(response);
        Console.WriteLine(response.Result?.Usage?.TotalTokens);
    }

    [Flaky("access limited in Europe")]
    [TornadoTest]
    public static async Task GoogleStreamImages()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
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

    [TornadoTest]
    public static async Task GoogleStreamVideo()
    {
        TornadoApi api = Program.Connect();
        HttpCallResult<TornadoFile> uploadedFile = await api.Files.Upload("Static/Files/video.mp4", mimeType: "video/mp4", provider: LLmProviders.Google);

        if (uploadedFile.Data is null)
        {
            return;
        }
        
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Google.Gemini.Gemini2Flash001
        });
        chat.AppendUserInput([
            new ChatMessagePart("Describe all the shots in this video"),
            new ChatMessagePart(new ChatMessagePartFileLinkData(uploadedFile.Data))
        ]);

        Console.WriteLine("Google:");

        if (uploadedFile.Data?.State is FileLinkStates.Processing)
        {
            await api.Files.WaitForReady(uploadedFile.Data, provider: LLmProviders.Google);
        }

        await chat.StreamResponseRich(new ChatStreamEventHandler
        {
            MessageTokenHandler = (token) =>
            {
                Console.Write(token);
                return ValueTask.CompletedTask;
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

    [TornadoTest]
    public static async Task MistralSmall()
    {
        Conversation chat2 = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Mistral.Free.MistralSmall
        });
        chat2.AppendSystemMessage("Pretend you are a dog. Sound authentic.");
        chat2.AppendUserInput("Solve 2+2");
       
        string? str2 = await chat2.GetResponse();

        Console.WriteLine("Mistral:");
        Console.WriteLine(str2);
    }

    [TornadoTest]
    public static async Task MistralLargeStreaming()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Mistral.Premier.MistralLarge
        });
        
        chat.AppendUserInput("You are a dog, sound authentic. Who are you?");

        Console.WriteLine("Mistral:");
        
        await chat.StreamResponseRich(new ChatStreamEventHandler
        {
            MessageTokenHandler = (token) =>
            {
                Console.Write(token);
                return ValueTask.CompletedTask;
            },
            BlockFinishedHandler = (block) =>
            {
                Console.WriteLine();
                return ValueTask.CompletedTask;
            }
        });
    }
    
    [TornadoTest]
    public static async Task MistralChatStreamingTools()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Mistral.Premier.MistralLarge,
            Tools =
            [
                new Tool(new ToolFunction("get_weather", "gets the current weather in a given city", new
                {
                    type = "object",
                    properties = new
                    {
                        location = new
                        {
                            type = "string",
                            description = "The city for which the weather information is required."
                        }
                    },
                    required = new List<string> { "city" }
                }))
            ]
        });
        
        chat.OnAfterToolsCall = async (result) =>
        {
            Console.WriteLine();
            await GetNextResponse();
        };
        
        chat.AppendUserInput([
            new ChatMessagePart("Check the weather today in Paris")
        ]);

        Console.WriteLine("Mistral:");
        await GetNextResponse();

        async Task GetNextResponse()
        {
            await chat.StreamResponseRich(new ChatStreamEventHandler
            {
                FunctionCallHandler = (fns) =>
                {
                    foreach (FunctionCall fn in fns)
                    {
                        fn.Result = new FunctionResult(fn.Name, new
                        {
                            result = "ok",
                            weather = "A mild rain is expected around noon."
                        });
                    }
                
                    return ValueTask.CompletedTask;
                },
                MessageTokenHandler = (token) =>
                {
                    Console.Write(token);
                    return ValueTask.CompletedTask;
                },
                BlockFinishedHandler = (block) =>
                {
                    Console.WriteLine();
                    return ValueTask.CompletedTask;
                }
            });
        }
    }
    
    [TornadoTest]
    public static async Task MistralImageUrl()
    {
        Conversation chat2 = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Mistral.Premier.PixtralLarge
        });
      
        chat2.AppendUserInput([
            new ChatMessagePart(new Uri("https://upload.wikimedia.org/wikipedia/commons/a/a7/Camponotus_flavomarginatus_ant.jpg")),
            new ChatMessagePart("Describe this image")
        ]);
       
        ChatRichResponse response = await chat2.GetResponseRich();

        Console.WriteLine("Mistral:");
        Console.WriteLine(response);
    }
    
    [TornadoTest]
    public static async Task MistralStructuredJson()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Mistral.Premier.MistralLarge,
            ResponseFormat = ChatRequestResponseFormats.StructuredJson("get_weather", new
            {
                type = "object",
                properties = new
                {
                    city = new
                    {
                        type = "string"
                    }
                },
                required = new List<string> { "city" },
                additionalProperties = false
            }, true)
        });
        chat.AppendUserInput("what is 2+2, also what is the weather in prague"); // user asks something unrelated, but we force the model to use the tool

        ChatRichResponse response = await chat.GetResponseRich();
        Console.WriteLine(response);
    }
    
    [TornadoTest]
    public static async Task GoogleStructuredJson()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Google.Gemini.Gemini2Flash001,
            ResponseFormat = ChatRequestResponseFormats.StructuredJson("get_weather", new
            {
                type = "object",
                properties = new
                {
                    city = new
                    {
                        type = "string"
                    }
                },
                required = new List<string> { "city" }
            })
        });
        chat.AppendUserInput("what is 2+2, also what is the weather in prague"); // user asks something unrelated, but we force the model to use the tool
        
        ChatRichResponse response = await chat.GetResponseRich();
        Console.WriteLine(response);
    }
    
    [TornadoTest]
    public static async Task GoogleLegacyJson()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Google.Gemini.Gemini2Flash001,
            ResponseFormat = ChatRequestResponseFormats.Json
        });
        chat.AppendUserInput("what is 2+2, also what is the weather in prague. Respond in the JSON format.");
        
        ChatRichResponse response = await chat.GetResponseRich();
        Console.WriteLine(response);
    }
    
    [TornadoTest]
    public static async Task GoogleReasoningDisableThoughts()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Google.Gemini.Gemini25Flash,
            ResponseFormat = ChatRequestResponseFormats.Json,
            VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorGoogleExtensions
            {
                SafetyFilters = ChatRequestVendorGoogleSafetyFilters.Default,
                IncludeThoughts = false
            }),
            ReasoningBudget = 128
        });
        
        chat.AppendUserInput("what is 2+2, also what is the weather in prague. Respond in the JSON format.");
        
        ChatRichResponse response = await chat.GetResponseRich();
        Console.WriteLine(response);
    }
    
    [TornadoTest]
    public static async Task AnthropicFunctionsRichImage()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Anthropic.Claude4.Sonnet250514,
            Tools = [
                new Tool(new ToolFunction("get_image", "gets an image for the given query", new
                {
                    type = "object",
                    properties = new
                    {
                        query = new
                        {
                            type = "string",
                            description = "The query."
                        }
                    },
                    required = new List<string> { "query" }
                }))
            ],
            ToolChoice = new OutboundToolChoice("get_image")
        });

        ChatStreamEventHandler eventsHandler = new ChatStreamEventHandler
        {
            MessageTokenHandler = (x) =>
            {
                Console.Write(x);
                return ValueTask.CompletedTask;
            },
            FunctionCallHandler = async (functions) =>
            {
                foreach (FunctionCall fn in functions)
                {
                    byte[] bytes = await File.ReadAllBytesAsync("Static/Images/catBoi.jpg");
                    string base64 = $"{Convert.ToBase64String(bytes)}";
                    
                    fn.Resolve([
                        new FunctionResultBlockImage(new FunctionResultBlockImageSourceBase64
                        {
                            Data = base64,
                            MediaType = "image/jpeg"
                        })
                    ]);
                }
            }
        };

        chat.OnAfterToolsCall = async (result) =>
        {
            chat.RequestParameters.ToolChoice = null; // stop forcing the model to use the get_image tool
            await chat.StreamResponseRich(eventsHandler);
        };
        
        chat.AppendMessage(ChatMessageRoles.User, "Please fetch and describe an image of cat for me.");
        await chat.StreamResponseRich(eventsHandler);
    }
    
    [TornadoTest]
    public static async Task AnthropicFunctionsRichSearchResult()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Anthropic.Claude4.Sonnet250514,
            Tools = [
                new Tool(new ToolFunction("get_sources", "fetches sources for the given query", new
                {
                    type = "object",
                    properties = new
                    {
                        query = new
                        {
                            type = "string",
                            description = "The query."
                        }
                    },
                    required = new List<string> { "query" }
                }))
            ],
            ToolChoice = new OutboundToolChoice("get_sources")
        });

        ChatStreamEventHandler eventsHandler = new ChatStreamEventHandler
        {
            MessageTokenHandler = (x) =>
            {
                Console.Write(x);
                return ValueTask.CompletedTask;
            },
            FunctionCallHandler = async (functions) =>
            {
                foreach (FunctionCall fn in functions)
                {
                    fn.Resolve([
                        new FunctionResultBlockSearchResult
                        {
                            Title = "NodeJS Changelog",
                            Source = "https://nodejs.org/en/blog/release/v24.4.0",
                            Citations = ChatSearchResultCitations.InstanceEnabled,
                            Content = [
                                new ChatSearchResultContentText(
                                    """
                                    2025-07-09, Version 24.4.0 (Current), @RafaelGSS
                                    Notable Changes
                                    [22b60e8a57] - (SEMVER-MINOR) crypto: support outputLength option in crypto.hash for XOF functions (Aditi) #58121
                                    [80dec9849d] - (SEMVER-MINOR) doc: add all watch-mode related flags to node.1 (Dario Piotrowicz) #58719
                                    [87f4d078b3] - (SEMVER-MINOR) fs: add disposable mkdtempSync (Kevin Gibbons) #58516
                                    [9623c50b53] - (SEMVER-MINOR) permission: propagate permission model flags on spawn (Rafael Gonzaga) #58853
                                    [797ec4da04] - (SEMVER-MINOR) sqlite: add support for readBigInts option in db connection level (Miguel Marcondes Filho) #58697
                                    [ed966a0215] - (SEMVER-MINOR) src,permission: add support to permission.has(addon) (Rafael Gonzaga) #58951
                                    [fe17f5d285] - (SEMVER-MINOR) watch: add --watch-kill-signal flag (Dario Piotrowicz) #58719
                                    """)
                            ]
                        }
                    ]);
                }
            }
        };

        chat.OnAfterToolsCall = async (result) =>
        {
            chat.RequestParameters.ToolChoice = null; // stop forcing the model to use the get_sources tool
            await chat.StreamResponseRich(eventsHandler);
        };
        
        chat.AppendMessage(ChatMessageRoles.User, "What changed in NodeJS v24.4.0?");
        await chat.StreamResponseRich(eventsHandler);
    }
    
    [TornadoTest]
    public static async Task GoogleMapsGrounding()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Google.Gemini.Gemini35Flash,
            VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorGoogleExtensions
            {
                GoogleMaps = new ChatRequestVendorGoogleMaps(50.0850774, 14.5493792) // prague
            })
        });
        
        chat.AppendUserInput("What are the best restaurants within a 15-minute walk from here?");
        
        ChatRichResponse response = await chat.GetResponseRich();
        
        Console.WriteLine("Google Maps Grounding:");
        Console.WriteLine(response.Text);

        foreach (ChatRichResponseBlock block in response.Blocks)
        {
            if (block.Citations?.Count > 0)
            {
                Console.WriteLine("\nCitations:");
                foreach (IChatMessagePartCitation citation in block.Citations)
                {
                    if (citation is ChatMessagePartCitationWebSearchResultLocation.ChatMessagePartCitationWebGrounding groundingCitation)
                    {
                        foreach (ChatMessagePartCitationWebSearchResultLocation.ChatMessagePartCitationWebGroundingSource source in groundingCitation.Sources)
                        {
                            Console.WriteLine($"- {source.Title}: {source.Url}");
                        }
                    }
                }
            }
        }
    }
    
    [TornadoTest]
    public static async Task GoogleMapsGroundingWithWidget()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Google.Gemini.Gemini35Flash,
            VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorGoogleExtensions
            {
                GoogleMaps = new ChatRequestVendorGoogleMaps(true, 50.0850774, 14.5493792) // prague
            })
        });
        
        chat.AppendUserInput("What are the best restaurants within a 15-minute walk from here?");
        
        ChatRichResponse response = await chat.GetResponseRich();
        
        Console.WriteLine("Google Maps Grounding with Widget:");
        Console.WriteLine(response.Text);
        
        if (response.VendorExtensions?.Google?.GoogleMapsWidgetContextToken is not null)
        {
            Console.WriteLine($"\nGoogle Maps Widget Context Token: {response.VendorExtensions.Google.GoogleMapsWidgetContextToken}");
            Console.WriteLine("You can use this token to render a contextual Google Maps widget in your application.");
        }
        
        foreach (ChatRichResponseBlock block in response.Blocks)
        {
            if (block?.Citations?.Count > 0)
            {
                Console.WriteLine("\nCitations:");
                foreach (IChatMessagePartCitation citation in block.Citations)
                {
                    if (citation is ChatMessagePartCitationWebSearchResultLocation.ChatMessagePartCitationWebGrounding groundingCitation)
                    {
                        foreach (ChatMessagePartCitationWebSearchResultLocation.ChatMessagePartCitationWebGroundingSource source in groundingCitation.Sources)
                        {
                            Console.WriteLine($"- {source.Title}: {source.Url}");
                        }
                    }
                }
            }
        }
    }

    [TornadoTest]
    public static async Task Gemini3Thinking()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Google.GeminiPreview.Gemini3ProPreview,
            ReasoningEffort = ChatReasoningEfforts.Low
        });
        
        chat.AppendUserInput("Solve this logic puzzle: Three gods A, B, and C are called, in no particular order, True, False, and Random. True always speaks truly, False always speaks falsely, but whether Random speaks truly or falsely is a completely random matter. Your task is to determine the identities of A, B, and C by asking three yes-no questions; each question must be put to exactly one god. The gods understand English, but will answer all questions in their own language, in which the words for yes and no are da and ja, in some order. You do not know which word means which.");
        
        await chat.StreamResponseRich(new ChatStreamEventHandler
        {
            ReasoningTokenHandler = (token) =>
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(token.Content);
                Console.ResetColor();
                return ValueTask.CompletedTask;
            },
            MessageTokenHandler = (token) =>
            {
                Console.Write(token);
                return ValueTask.CompletedTask;
            }
        });
    }

    [TornadoTest]
    public static async Task Gemini3MediaResolution()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Google.GeminiPreview.Gemini3ProPreview
        });

        byte[] bytes = await File.ReadAllBytesAsync("Static/Images/catBoi.jpg");
        string base64 = Convert.ToBase64String(bytes);
        
        chat.AppendUserInput([
            new ChatMessagePart("Describe these two images, paying attention to the difference in detail visible."),
            new ChatMessagePart(new ChatImage(base64, "image/jpeg"))
            {
                MediaResolution = ChatMessagePartMediaResolution.High
            },
            new ChatMessagePart(new ChatImage(base64, "image/jpeg"))
            {
                MediaResolution = ChatMessagePartMediaResolution.Low
            }
        ]);
        
        Console.WriteLine(chat.Serialize(true));

        await chat.StreamResponseRich(new ChatStreamEventHandler
        {
            MessageTokenHandler = (token) =>
            {
                Console.Write(token);
                return ValueTask.CompletedTask;
            }
        });
    }

    [TornadoTest]
    public static async Task Gemini3AutoThoughtSignature()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Google.GeminiPreview.Gemini3ProPreview,
            Tools = [
                new Tool(new ToolFunction("get_weather", "gets the current weather in a given city", new
                {
                    type = "object",
                    properties = new
                    {
                        location = new
                        {
                            type = "string",
                            description = "The city for which the weather information is required."
                        }
                    },
                    required = new List<string> { "location" }
                }))
            ]
        });

        chat.AppendUserInput("What is the weather in Prague?");

        // 1. Get the tool call
        ChatRichResponse response = await chat.GetResponseRich();
        FunctionCall? toolCall = response.Blocks.FirstOrDefault(x => x.Type == ChatRichResponseBlockTypes.Function)?.FunctionCall;

        if (toolCall == null)
        {
            Console.WriteLine("Model did not call the tool.");
            return;
        }

        Console.WriteLine($"Tool call received. Thought Signature present: {!string.IsNullOrEmpty(toolCall.ToolCall?.ThoughtSignature)}");

        // 2. Simulate a developer \"screw up\" by manually constructing the history and OMITTING the thought signature
        // Normally, one would just append the result, but here we are reconstructing the assistant message to strip the signature
        
        // Remove the last message (the assistant's tool call) so we can replace it with a \"broken\" one
        chat.RemoveMessage(chat.Messages.Last());
        
        // Add the assistant message back, but explicitly nullify the ThoughtSignature on the ToolCall
        // Note: We need to create a new ToolCall object or modify the existing one if mutable
        if (toolCall.ToolCall != null)
        {
            toolCall.ToolCall.ThoughtSignature = null;
        }
        
        chat.AppendMessage(new ChatMessage(ChatMessageRoles.Assistant)
        {
            ToolCalls = toolCall.ToolCall != null ? [toolCall.ToolCall] : []
        });

        // Add the tool result
        chat.AppendMessage(new ChatMessage(ChatMessageRoles.Tool, "Sunny, 25 degrees Celsius")
        {
            ToolCallId = toolCall.ToolCall?.Id ?? toolCall.Name
        });

        Console.WriteLine("Sending follow-up request with MISSING thought signature...");

        // 3. Send the request. The library should automatically inject the dummy signature.
        ChatRichResponse followUp = await chat.GetResponseRich();
        Console.WriteLine("Response received successfully!");
        Console.WriteLine(followUp.Text);
    }

    [TornadoTest]
    public static async Task Gemini3ThoughtSignature()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Google.GeminiPreview.Gemini3ProPreview,
            Tools = [
                new Tool(new ToolFunction("get_weather", "gets the current weather in a given city", new
                {
                    type = "object",
                    properties = new
                    {
                        location = new
                        {
                            type = "string",
                            description = "The city for which the weather information is required."
                        }
                    },
                    required = new List<string> { "location" }
                }))
            ]
        });

        chat.AppendUserInput("What is the weather in Prague?");

        // 1. Get the tool call
        ChatRichResponse response = await chat.GetResponseRich(calls =>
        {
            calls.ForEach(x => x.Resolve(new
            {
                answer = "Sunny, 25 degrees Celsius"
            }));
            return ValueTask.CompletedTask;
        });
        ToolCall? toolCall = response.Result?.Choices?.FirstOrDefault()?.Message?.ToolCalls?.FirstOrDefault();

        if (toolCall == null)
        {
            Console.WriteLine("Model did not call the tool.");
            return;
        }
        
        Console.WriteLine($"Tool call received. Thought Signature present: {!string.IsNullOrEmpty(toolCall.ThoughtSignature)}");
        
        if (!string.IsNullOrEmpty(toolCall.ThoughtSignature))
        {
             Console.WriteLine($"Signature: {toolCall.ThoughtSignature[..Math.Min(20, toolCall.ThoughtSignature.Length)]}...");
        }

        Console.WriteLine("Sending follow-up request...");

        // 3. Send the request. The library should serialize the ThoughtSignature from the history.
        Console.WriteLine(chat.Serialize(true));
        
        ChatRichResponse followUp = await chat.GetResponseRich();
        Console.WriteLine("Response received successfully!");
        Console.WriteLine(followUp.Text);
    }
}
