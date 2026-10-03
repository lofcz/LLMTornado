using System.Collections.Immutable;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.ChatFunctions;
using LlmTornado.Code;
using LlmTornado.Code.DiffMatchPatch;
using LlmTornado.Common;
using LlmTornado.Images;
using LlmTornado.Images.Models;
using LlmTornado.Responses;
using LlmTornado.Responses.Events;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Demo;

public class ResponsesDemo : DemoBase
{
    [TornadoTest]
    public static async Task ResponseSimpleText()
    {
        ResponseResult result = await Program.Connect().Responses.CreateResponse(new ResponseRequest
        {
            Model = ChatModel.OpenAi.Gpt41.V41Mini,
            Instructions = "You are a helpful assistant",
            InputItems = [
                new ResponseInputMessage(ChatMessageRoles.User, "how are you?")
            ],
            Include = [ 
                ResponseIncludeFields.MessageOutputTextLogprobs
            ]
        });

        ResponseOutputMessageItem itm = result.Output.OfType<ResponseOutputMessageItem>().FirstOrDefault();
        Assert.That(result.Output.OfType<ResponseOutputMessageItem>().Count(), Is.EqualTo(1));

        ResponseOutputTextContent? text = itm.Content.OfType<ResponseOutputTextContent>().FirstOrDefault();
        Console.WriteLine(text.Text);
    }
    
    public struct math_reasoning
    {
        public math_step[] steps { get; set; }
        public string final_answer { get; set; }

        public void ConsoleWrite()
        {
            Console.WriteLine($"Final answer: {final_answer}");
            Console.WriteLine("Reasoning steps:");
            foreach (math_step step in steps)
            {
                Console.WriteLine($"  - Explanation: {step.explanation}");
                Console.WriteLine($"    Output: {step.output}");
            }
        }
    }

    private static List<ResponseApplyPatchToolCallItem> ExtractApplyPatchCalls(ResponseResult response)
    {
        return response.Output?.OfType<ResponseApplyPatchToolCallItem>().ToList() ?? [];
    }

    private static bool TryApplyPatchOperation(ResponseApplyPatchOperation? operation, Dictionary<string, string> workspace, out string logMessage)
    {
        if (operation is null || string.IsNullOrWhiteSpace(operation.Path))
        {
            logMessage = "No operation payload provided.";
            return false;
        }

        switch (operation.Type)
        {
            case ResponseApplyPatchOperationType.CreateFile:
                if (string.IsNullOrEmpty(operation.Diff))
                {
                    logMessage = "No diff provided for create_file operation.";
                    return false;
                }

                bool created = DiffPatchEngine.TryApply(string.Empty, operation.Diff, out string createdContent, out string? createError, format: PatchFormat.V4a);
                if (!created)
                {
                    logMessage = $"Failed to create '{operation.Path}': {createError}";
                    return false;
                }

                workspace[operation.Path] = createdContent;
                logMessage = $"Created {operation.Path}";
                return true;

            case ResponseApplyPatchOperationType.UpdateFile:
                if (!workspace.TryGetValue(operation.Path, out string existingContent))
                {
                    logMessage = $"File '{operation.Path}' not found.";
                    return false;
                }

                if (string.IsNullOrEmpty(operation.Diff))
                {
                    logMessage = "No diff provided for update_file operation.";
                    return false;
                }

                bool updated = DiffPatchEngine.TryApply(existingContent, operation.Diff, out string newContent, out string? updateError, format: PatchFormat.V4a);
                if (!updated)
                {
                    logMessage = $"Failed to apply diff to {operation.Path}: {updateError}";
                    return false;
                }

                workspace[operation.Path] = newContent;
                logMessage = $"Updated {operation.Path}";
                return true;

            case ResponseApplyPatchOperationType.DeleteFile:
                bool removed = workspace.Remove(operation.Path);
                logMessage = removed
                    ? $"Deleted {operation.Path}"
                    : $"File '{operation.Path}' did not exist.";
                return removed;

            default:
                logMessage = $"Unsupported operation type {operation.Type}.";
                return false;
        }
    }

    public struct math_step
    {
        public string explanation { get; set; }
        public string output { get; set; }
    }
    
    [TornadoTest]
    public static async Task ResponseStructuredOutput()
    {
        ResponseResult response = await Program.Connect().Responses.CreateResponse(new ResponseRequest
        {
            Model = ChatModel.OpenAi.Gpt41.V41,
            Instructions = "You are an assistant specialized on solving math problems.",
            Text = ResponseTextFormatConfiguration.CreateJsonSchema(new
            {
                type = "object",
                properties = new
                {
                    final_answer = new
                    {
                        type = "string",
                        description = "final answer to the problem"
                    },
                    steps = new
                    {
                        type = "array",
                        items = new
                        {
                            type = "object",
                            properties = new
                            {
                                explanation = new
                                {
                                    type = "string",
                                    description = "explanation of the step, curt"
                                },
                                output = new
                                {
                                    type = "string",
                                    description = "output of the step"
                                }
                            },
                            required = new List<string> { "explanation", "output" },
                            additionalProperties = false
                        }
                    }
                },
                required = new List<string> { "final_answer", "steps" },
                additionalProperties = false
            }, "math_solver", strict: true),
            InputItems =
            [
                new ResponseInputMessage(ChatMessageRoles.User, [
                    new ResponseInputContentText("2x + 4 - x = 8")
                ])
            ]
        });
        
        foreach (IResponseOutputItem outputItem in response.Output)
        {
            if (outputItem is ResponseOutputMessageItem msg)
            {
                foreach (IResponseOutputContent part in msg.Content)
                {
                    if (part is ResponseOutputTextContent text)
                    {
                        // the output JSON
                        Console.WriteLine(text.Text);
                    }
                }
            }
        }
    }
    
    [TornadoTest]
    public static async Task ResponseSimpleTextUsingChat()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
            {
                Model = ChatModel.OpenAi.Gpt41.V41Mini,
                MaxTokens = 2000,
                ResponseRequestParameters = new ResponseRequest()
            })
            .AppendSystemMessage("You are a helpful assistant")
            .AppendUserInput([
                new ChatMessagePart("What kind of dog breed is this?"),
                new ChatMessagePart(
                    "https://as2.ftcdn.net/v2/jpg/05/94/28/01/1000_F_594280104_vZXB6JZANIRywkZcUQntU07p5KGpuZ7S.jpg",
                    ImageDetail.Auto)
            ]);

        TornadoRequestContent z = chat.Serialize();
        
        RestDataOrException<ChatRichResponse> response = await chat.GetResponseRichSafe();
        
        Console.WriteLine(response.Data.Text);
    }
    
    [TornadoTest]
    public static async Task ResponseStructuredJsonUsingChat()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
            {
                Model = ChatModel.OpenAi.Gpt41.V41Mini,
                MaxTokens = 2000,
                ResponseRequestParameters = new ResponseRequest(),
                ResponseFormat = ChatRequestResponseFormats.StructuredJson("get_translation", new
                {
                    type = "object",
                    properties = new
                    {
                        translation = new
                        {
                            type = "string",
                        }
                    },
                    required = new List<string> { "translation" },
                    additionalProperties = false
                })
            })
            .AppendSystemMessage("You are a helpful assistant that translates from English to French")
            .AppendUserInput([new ChatMessagePart("Hello, how are you?")]);
        
        RestDataOrException<ChatRichResponse> response = await chat.GetResponseRichSafe();
        
        Console.WriteLine(response.Data.Text);
    }
    
    [TornadoTest]
    public static async Task ResponseJsonUsingChat()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
            {
                Model = ChatModel.OpenAi.Gpt41.V41Mini,
                MaxTokens = 2000,
                ResponseRequestParameters = new ResponseRequest(),
                ResponseFormat = ChatRequestResponseFormats.Json
            })
            .AppendSystemMessage("You are a helpful assistant that translates from English to French")
            .AppendUserInput([new ChatMessagePart("Hello, how are you? (respond in JSON format in property french_translation)")]);
        
        RestDataOrException<ChatRichResponse> response = await chat.GetResponseRichSafe();
        
        Console.WriteLine(response.Data.Text);
    }
    
    [TornadoTest]
    public static async Task StreamResponseSimpleTextUsingChat()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
            {
                Model = ChatModel.OpenAi.O4.V4Mini,
                MaxTokens = 4000,
                ResponseRequestParameters = new ResponseRequest
                {
                    Reasoning = new ReasoningConfiguration
                    {
                        Effort = ResponseReasoningEfforts.Medium,
                        Summary = ResponseReasoningSummaries.Auto
                    }
                }
            })
            .AppendSystemMessage("You are a helpful assistant")
            .AppendUserInput([
                new ChatMessagePart("How to explain theory of relativity to a 15 years old student?")
            ]);
        
        await chat.StreamResponseRich(new ChatStreamEventHandler
        {
            OnResponseEvent = (evt) =>
            {
                // "evt" is IResponseEvent
                return ValueTask.CompletedTask;
            },
            MessageTokenHandler = (delta) =>
            {
                Console.Write(delta);
                return ValueTask.CompletedTask;
            },
            ReasoningTokenHandler = (reasoningDelta) =>
            {
                Console.Write(reasoningDelta.Content);
                return ValueTask.CompletedTask;
            }
        });
    }
    
    [TornadoTest]
    public static async Task ResponseStreamingToolsUsingChat()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.OpenAi.Gpt41.V41,
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
            ],
            ResponseRequestParameters = new ResponseRequest()
        });
        
        chat.OnAfterToolsCall = async (result) =>
        {
            Console.WriteLine();
            await GetNextResponse();
        };
        
        chat.AppendUserInput([
            new ChatMessagePart("Check the weather today in Paris and Prague")
        ]);

        await GetNextResponse();

        async Task GetNextResponse()
        {
            await chat.StreamResponseRich(new ChatStreamEventHandler
            {
                FunctionCallHandler = (fns) =>
                {
                    foreach (FunctionCall fn in fns)
                    {
                        fn.Result = fn.Arguments.Contains("Prague") ? new FunctionResult(fn.Name, "Sunny all day with occasionally clouds") : new FunctionResult(fn.Name, "A mild rain is expected around noon.");
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
    public static async Task ResponseChatToolsUsingChat()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.OpenAi.Gpt41.V41,
            ResponseRequestParameters = new ResponseRequest(),
            Tools = [
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
                }), false)
            ],
            ToolChoice = new OutboundToolChoice("get_weather")
        });

        chat.OnAfterToolsCall = async (result) =>
        {
            chat.RequestParameters.ToolChoice = null; // stop forcing the model to use the get_weather tool
            RestDataOrException<ChatRichResponse> responseResult = await chat.GetResponseRichSafe();
            Console.WriteLine(responseResult.Data.Text);;
        };
        
        chat.AppendMessage(ChatMessageRoles.System, "You are a helpful assistant");
        Guid msgId = Guid.NewGuid();
        chat.AppendMessage(ChatMessageRoles.User, "Fetch the weather information for Prague and Paris.", msgId);

        RestDataOrException<ChatRichResponse> response = await chat.GetResponseRichSafe(functions =>
        {
            foreach (FunctionCall fn in functions)
            {
                fn.Result = fn.Arguments.Contains("Prague") ? new FunctionResult(fn.Name, "Sunny all day") : new FunctionResult(fn.Name, "A mild rain is expected around noon.");
            }

            return ValueTask.CompletedTask;
        });

        string r = response.Data.GetText();
        Console.WriteLine(r);
    }
    
    [TornadoTest]
    public static async Task ResponseSimpleTool()
    {
        ResponseResult result = await Program.Connect().Responses.CreateResponse(new ResponseRequest
        {
            Model = ChatModel.OpenAi.Gpt41.V41,
            InputItems =
            [
                new ResponseInputMessage(ChatMessageRoles.User, "What is the weather in prague?")
            ],
            Tools =
            [
                new ResponseFunctionTool
                {
                    Name = "get_weather",
                    Description = "fetches weather in a given city",
                    Parameters = JObject.FromObject(new
                    {
                        type = "object",
                        properties = new
                        {
                            location = new
                            {
                                type = "string",
                                description = "name of the location"
                            }
                        },
                        required = new List<string> { "location" },
                        additionalProperties = false
                    }),
                    Strict = true
                }
            ]
        });

        ResponseFunctionToolCallItem? fn = result.Output.OfType<ResponseFunctionToolCallItem>().FirstOrDefault();
        Assert.That(fn, Is.NotNull);
        
        result = await Program.Connect().Responses.CreateResponse(new ResponseRequest
        {
            PreviousResponseId = result.Id,
            Model = ChatModel.OpenAi.Gpt41.V41,
            InputItems =
            [
                new FunctionToolCallOutput(fn.CallId, new
                {
                    weather = "sunny, no rain, mild fog, humididy: 65%",
                    confidence = "very_high"
                }.ToJson())
            ]
        });

        ResponseOutputMessageItem? itm = result.Output.OfType<ResponseOutputMessageItem>().FirstOrDefault();
        Assert.That(itm, Is.NotNull);

        ResponseOutputTextContent? text = itm.Content.OfType<ResponseOutputTextContent>().FirstOrDefault();
        Assert.That(text, Is.NotNull);
        
        Console.WriteLine(text.Text);
    }

    [TornadoTest]
    public static async Task ResponseSimpleTextStream()
    {
        await Program.Connect().Responses.StreamResponseRich(new ResponseRequest
        {
            Model = ChatModel.OpenAi.Gpt41.V41Mini,
            InputItems = [
                new ResponseInputMessage(ChatMessageRoles.User, "How are you?")
            ]
        }, new ResponseStreamEventHandler
        {
            OnEvent = (data) =>
            {
                if (data is ResponseEventOutputTextDelta delta)
                {
                    Console.Write(delta.Delta);
                }
                
                return ValueTask.CompletedTask;
            }
        });
    }
    
    [TornadoTest]
    public static async Task ResponseSimpleFunctionsStream()
    {
        string fnCallId = string.Empty;
        
        ResponsesSession session = Program.Connect().Responses.CreateSession(new ResponseStreamEventHandler
        {
            OnEvent = (data) =>
            {
                if (data is ResponseEventOutputTextDelta delta)
                {
                    Console.Write(delta.Delta);
                }

                if (data is ResponseEventOutputItemDone itemDone)
                {
                    if (itemDone.Item is ResponseFunctionToolCallItem fn)
                    {
                        // call the function
                        fnCallId = fn.CallId;
                    }
                }
                
                return ValueTask.CompletedTask;
            }
        });

        await session.StreamResponseRich(new ResponseRequest
        {
            Model = ChatModel.OpenAi.Gpt41.V41,
            InputItems =
            [
                new ResponseInputMessage(ChatMessageRoles.User, "What is the weather in prague?")
            ],
            Tools =
            [
                new ResponseFunctionTool
                {
                    Name = "get_weather",
                    Description = "fetches weather in a given city",
                    Parameters = JObject.FromObject(new
                    {
                        type = "object",
                        properties = new
                        {
                            location = new
                            {
                                type = "string",
                                description = "name of the location"
                            }
                        },
                        required = new List<string> { "location" },
                        additionalProperties = false
                    }),
                    Strict = true
                }
            ]
        });
        
        await session.StreamResponseRich(new ResponseRequest
        {
            Model = ChatModel.OpenAi.Gpt41.V41,
            InputItems = [
                new FunctionToolCallOutput(fnCallId, new
                {
                    weather = "sunny, no rain, mild fog, humididy: 65%",
                    confidence = "very_high"
                }.ToJson())
            ]
        });
    }
    
    [TornadoTest]
    public static async Task ResponseDeepResearchBackground()
    {
        ResponseResult result = await Program.Connect().Responses.CreateResponse(new ResponseRequest
        {
            Model = ChatModel.OpenAi.O4.V4MiniDeepResearch,
            Background = true,
            InputItems = [
                new ResponseInputMessage(ChatMessageRoles.User, "Research detailed information about latest development in the Ukraine war and predict how long will Pokrovsk hold.")
            ],
            Tools = [
                new ResponseWebSearchTool(),
                new ResponseCodeInterpreterTool()
            ]
        });

        int z = 0;
    }
    
    [TornadoTest]
    public static async Task ResponseBackground()
    {
        ResponseResult result = await Program.Connect().Responses.CreateResponse(new ResponseRequest
        {
            Model = ChatModel.OpenAi.O4.V4Mini,
            Background = true,
            InputItems = [
                new ResponseInputMessage(ChatMessageRoles.User, "2+2=?")
            ]
        });

        Console.WriteLine(result.Id);
    }
    
    [TornadoTest]
    public static async Task ResponseBuiltInComputerTool()
    {
        EndpointBase.SetRequestsTimeout(20000);
        
        byte[] bytes = await File.ReadAllBytesAsync("Static/Images/empty.jpg");
        string base64 = $"data:image/jpeg;base64,{Convert.ToBase64String(bytes)}";
        
        ResponseResult result = await Program.Connect().Responses.CreateResponse(new ResponseRequest
        {
            Model = ChatModel.OpenAi.Gpt54.V54,
            Background = false,
            InputItems = [
                new ResponseInputMessage(ChatMessageRoles.User, [
                    new ResponseInputContentText("Use the computer tool to inspect the UI. Start by taking a screenshot."),
                    ResponseInputContentImage.CreateImageUrl(base64),
                ])
            ],
            Tools = [
                ResponseComputerTool.Default
            ],
            Reasoning = new ReasoningConfiguration
            {
                Summary = ResponseReasoningSummaries.Concise
            }
        });

        ResponseComputerToolCallItem? computerCall = result.Output
            .OfType<ResponseComputerToolCallItem>()
            .FirstOrDefault();

        Console.WriteLine(computerCall is null
            ? "No computer_call returned"
            : $"Computer call: {computerCall.CallId}, actions: {computerCall.GetExecutableActions().Count}");
    }

    [TornadoTest]
    public static async Task ResponseFileSearch()
    {
        EndpointBase.SetRequestsTimeout(20000);
        
        ResponseResult result = await Program.Connect().Responses.CreateResponse(new ResponseRequest
        {
            Model = ChatModel.OpenAi.Gpt41.V41,
            Background = false,
            InputItems = [
                new ResponseInputMessage(ChatMessageRoles.User, [
                    new ResponseInputContentText("Summarize all available files. Do not ask for further input."),
                ])
            ],
            Include = [ 
                ResponseIncludeFields.FileSearchCallResults
            ],
            Tools = [
                new ResponseFileSearchTool
                {
                    VectorStoreIds = [ "vs_6869bbe2a93481919d52952ac7773144" ]
                }
            ]
        });

        Console.WriteLine(result.OutputText);
        
        int z = 0;
    }
    
    [TornadoTest]
    public static async Task ResponseReasoning()
    {
        EndpointBase.SetRequestsTimeout(20000);
        
        ResponseResult result = await Program.Connect().Responses.CreateResponse(new ResponseRequest
        {
            Model = ChatModel.OpenAi.O4.V4Mini,
            InputItems = [
                new ResponseInputMessage(ChatMessageRoles.User, [
                    new ResponseInputContentText("Write a bash script that takes a matrix represented as a string with format \"[1,2],[3,4],[5,6]\" and prints the transpose in the same format.")
                ])
            ],
            Reasoning = new ReasoningConfiguration
            {
                Effort = ResponseReasoningEfforts.Medium
            },
            Include = [ 
                ResponseIncludeFields.ReasoningEncryptedContent 
            ],
            Store = false
        });

        Console.WriteLine(result.OutputText);
        int z = 0;
    }
    
    [TornadoTest]
    public static async Task ResponseReasoningStreaming()
    {
        EndpointBase.SetRequestsTimeout(20000);

        await Program.Connect().Responses.StreamResponseRich(new ResponseRequest
        {
            Model = ChatModel.OpenAi.O4.V4Mini,
            InputItems =
            [
                new ResponseInputMessage(ChatMessageRoles.User, [
                    new ResponseInputContentText("Write a bash script that takes a matrix represented as a string with format \"[1,2],[3,4],[5,6]\" and prints the transpose in the same format.")
                ])
            ],
            Reasoning = new ReasoningConfiguration
            {
                Effort = ResponseReasoningEfforts.Medium
            },
            Include =
            [
                ResponseIncludeFields.ReasoningEncryptedContent
            ],
            Store = false
        }, new ResponseStreamEventHandler
        {
            OnEvent = (data) =>
            {
                if (data.EventType is ResponseEventTypes.ResponseOutputTextDelta && data is ResponseEventOutputTextDelta delta)
                {
                    Console.Write(delta.Delta);
                }
                
                return ValueTask.CompletedTask;
            }
        });

        int z = 0;
    }
    
    [TornadoTest, Flaky("long running")]
    public static async Task ResponseDeepResearchMcp()
    {
        EndpointBase.SetRequestsTimeout(20000);
        
        ResponseResult result = await Program.Connect().Responses.CreateResponse(new ResponseRequest
        {
            Model = ChatModel.OpenAi.Gpt41.V41,
            Background = false,
            InputItems = [
                new ResponseInputMessage(ChatMessageRoles.User, "Research detailed information about latest development in the Ukraine war and predict how long will Pokrovsk hold. Create some images describing the situation. Run python code for quantitative analysis. Please send e-mail analysis to EMAIL using the MCP.")
            ],
            Tools = [
                new ResponseWebSearchTool(),
                new ResponseCodeInterpreterTool(),
                new ResponseMcpTool
                {
                    ServerLabel = "mailgun_mcp",
                    ServerUrl = "https://mcp.pipedream.net/id/mailgun",
                    RequireApproval = ResponseMcpRequireApprovalOption.Never
                },
                new ResponseImageGenerationTool
                {
                    Model = ImageModel.OpenAi.Gpt.V1
                }
            ]
        });

        int z = 0;
    }

    [TornadoTest]
    public static async Task ResponseApplyPatchTool()
    {
        TornadoApi api = Program.Connect();
        const string sampleFilePath = "src/Sample/TaskRunner.cs";
        string initialFileContents = """
                                     using System;

                                     namespace DemoApp
                                     {
                                         public class TaskRunner
                                         {
                                             public int ProcessTasks(int[] values)
                                             {
                                                 int total = 0;
                                                 foreach (int value in values)
                                                 {
                                                     total += value;
                                                 }

                                                 return total;
                                             }
                                         }
                                     }
                                     """;

        Dictionary<string, string> workspace = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [sampleFilePath] = initialFileContents
        };

        // Demonstrate headerless patch format (GPT-5-1 style)
        const string headerlessDiff = """
            @@
            -    public class TaskProcessor
            +    /// <summary>Processes tasks and computes totals</summary>
            +    public class TaskProcessor
                 {
            -        public int ProcessTasks(List<int> items)
            +        /// <summary>Computes the total from a list of items</summary>
            +        public int ComputeTotal(List<int> items)
                     {
                         int total = 0;
                         foreach (int item in items)
                         {
                             total += item;
                         }
                         return total;
                     }
                 }
            """;
        bool headerlessParsed = DiffPatchEngine.TryApply(initialFileContents, headerlessDiff, out string headerlessResult, out string? headerlessError, format: PatchFormat.V4a);
        Assert.That(headerlessParsed, Is.True, headerlessError ?? "Headerless diff parsing failed.");
        Assert.That(headerlessResult.Contains("ComputeTotal", StringComparison.Ordinal), Is.True);
        Assert.That(headerlessResult.Contains("/// <summary>Computes the total"), Is.True);

        string userPrompt =
            $"You are editing {sampleFilePath}. Please rename the ProcessTasks method to ComputeTotal, add XML documentation, and log when processing starts.\n\nCurrent file contents:\n```csharp\n{initialFileContents}\n```";

        ResponseResult response = await api.Responses.CreateResponse(new ResponseRequest
        {
            Model = ChatModel.OpenAi.Gpt51.V51,
            InputItems = [
                new ResponseInputMessage(ChatMessageRoles.User, userPrompt)
            ],
            Tools = [
                new ResponseApplyPatchTool()
            ],
            Background = false
        });

        List<ResponseApplyPatchToolCallItem> pendingPatches = ExtractApplyPatchCalls(response);
        int iteration = 0;

        while (pendingPatches.Count > 0 && iteration++ < 6)
        {
            List<ResponseInputItem> toolOutputs = [];

            foreach (ResponseApplyPatchToolCallItem patch in pendingPatches)
            {
                bool success = TryApplyPatchOperation(patch.Operation, workspace, out string logMessage);

                toolOutputs.Add(new ApplyPatchCallOutput
                {
                    CallId = patch.CallId ?? patch.Id ?? $"patch_call_{iteration}",
                    Status = success ? ResponseApplyPatchCallOutputStatus.Completed : ResponseApplyPatchCallOutputStatus.Failed,
                    Output = logMessage
                });
            }

            response = await api.Responses.CreateResponse(new ResponseRequest
            {
                Model = ChatModel.OpenAi.Gpt51.V51,
                PreviousResponseId = response.Id,
                InputItems = toolOutputs,
                Tools = [
                    new ResponseApplyPatchTool()
                ],
                Background = false
            });

            pendingPatches = ExtractApplyPatchCalls(response);
        }

        Assert.That(response.Output.OfType<ResponseOutputMessageItem>().Any(), Is.True);
        if (workspace.TryGetValue(sampleFilePath, out string updatedFile))
        {
            Console.WriteLine($"Updated {sampleFilePath}:{Environment.NewLine}{updatedFile}");
        }
    }
    
    [TornadoTest]
    public static async Task ResponseShellTool()
    {
        ResponseResult response = await Program.Connect().Responses.CreateResponse(new ResponseRequest
        {
            Model = ChatModel.OpenAi.Gpt51.V51,
            InputItems = [
                new ResponseInputMessage(ChatMessageRoles.User, "Show the current working directory and list files.")
            ],
            Tools = [
                new ResponseShellTool()
            ],
            Background = false
        });

        ResponseShellToolCallItem? shellCall = response.Output.OfType<ResponseShellToolCallItem>().FirstOrDefault();
        Assert.That(shellCall, Is.NotNull);

        ShellCallOutput shellOutput = new ShellCallOutput
        {
            CallId = shellCall!.CallId ?? string.Empty,
            MaxOutputLength = shellCall.Action.MaxOutputLength,
            Output = [
                new ResponseShellCommandOutput
                {
                    Stdout = "/demo\nREADME.md\nsrc\n",
                    Stderr = string.Empty,
                    Outcome = new ResponseShellCommandOutcome
                    {
                        Type = ResponseShellCommandOutcomeType.Exit,
                        ExitCode = 0
                    }
                }
            ]
        };

        ResponseResult followup = await Program.Connect().Responses.CreateResponse(new ResponseRequest
        {
            Model = ChatModel.OpenAi.Gpt51.V51,
            PreviousResponseId = response.Id,
            InputItems = [ shellOutput ],
            Tools = [
                new ResponseShellTool()
            ],
            Background = false
        });

        Assert.That(followup.Output.OfType<ResponseOutputMessageItem>().Any(), Is.True);
    }
    
    [TornadoTest]
    public static async Task ResponseWebSocketMultiTurn()
    {
        TornadoApi api = Program.Connect();
        await using ResponsesWebSocketConnection connection = await api.Responses.ConnectWebSocketAsync();

        ResponseResult? first = await connection.CreateResponseAsync(new ResponseRequest
        {
            Model = ChatModel.OpenAi.Gpt41.V41Mini,
            Store = false,
            InputItems =
            [
                new ResponseInputMessage(ChatMessageRoles.User, "Reply with exactly: ws-demo-1")
            ]
        });

        AssertTrue(first?.OutputText?.Contains("ws-demo-1", StringComparison.OrdinalIgnoreCase) == true);

        ResponseResult? second = await connection.CreateResponseAsync(new ResponseRequest
        {
            Model = ChatModel.OpenAi.Gpt41.V41Mini,
            Store = false,
            InputItems =
            [
                new ResponseInputMessage(ChatMessageRoles.User, "Reply with exactly: ws-demo-2")
            ]
        });

        AssertTrue(second?.OutputText?.Contains("ws-demo-2", StringComparison.OrdinalIgnoreCase) == true);
        Assert.That(connection.CurrentResponseId, Is.EqualTo(second!.Id));
    }

    [TornadoTest]
    public static async Task ResponseCountInputTokens()
    {
        TornadoApi api = Program.Connect();
        
        ResponseInputTokensResult result = await api.Responses.CountInputTokens(new ResponseRequest
        {
            Model = ChatModel.OpenAi.Gpt41.V41Mini,
            Instructions = "You are a helpful assistant",
            InputItems = [
                new ResponseInputMessage(ChatMessageRoles.User, "What is the capital of France?")
            ]
        });

        Console.WriteLine($"Input tokens: {result.InputTokens}");
        Assert.That(result.InputTokens, Is.GreaterThan(0));
        Assert.That(result.Object, Is.EqualTo("response.input_tokens"));
    }

    [TornadoTest, Flaky("only for dev")]
    public static async Task Deserialize()
    {
        string text = await File.ReadAllTextAsync("Static/Json/Sensitive/response1.json");
        ResponseResult result = text.JsonDecode<ResponseResult>();
        string data = result.ToJson();
        int z = 0;
    }
    
    [TornadoTest, Flaky("only for dev")]
    public static async Task ResponseDeepResearchBackgroundGet()
    {
        ResponseResult? result = await Program.Connect().Responses.GetResponse("<id>");

        int z = 0;
    }
    
    [TornadoTest]
    public static async Task ResponseDelete()
    {
        TornadoApi api = Program.Connect();
     
        ResponseResult createResult = await api.Responses.CreateResponse(new ResponseRequest
        {
            Model = ChatModel.OpenAi.O4.V4Mini,
            Background = true,
            InputItems = [
                new ResponseInputMessage(ChatMessageRoles.User, "2+2=?")
            ]
        });
        
        ResponseDeleted result = await api.Responses.DeleteResponse(createResult.Id);
        Assert.That(result.Deleted, Is.True);
    }
    
    [TornadoTest]
    public static async Task ResponseCancel()
    {
        TornadoApi api = Program.Connect();
     
        ResponseResult createResult = await api.Responses.CreateResponse(new ResponseRequest
        {
            Model = ChatModel.OpenAi.O4.V4Mini,
            Background = true,
            InputItems = [
                new ResponseInputMessage(ChatMessageRoles.User, "2+2=?")
            ]
        });
        
        ResponseResult result = await api.Responses.CancelResponse(createResult.Id);
        int z = 0;
    }
    
    [TornadoTest]
    public static async Task ResponseListItems()
    {
        TornadoApi api = Program.Connect();
        ResponseRequest request = new ResponseRequest
        {
            Model = ChatModel.OpenAi.O4.V4Mini,
            Background = true,
            InputItems =
            [
                new ResponseInputMessage(ChatMessageRoles.User, [
                    new ResponseInputContentText("2+2")
                ])
            ]
        };
        
        ResponseResult createResult = await api.Responses.CreateResponse(request);
        ListResponse<ResponseInputItem> result = await api.Responses.ListResponseInputItems(createResult.Id, new ListQuery(100));
        int z = 0;
    }
    
    [TornadoTest]
    public static async Task ResponseReusablePromptList()
    {
        TornadoApi api = Program.Connect();
        ResponseRequest request = new ResponseRequest
        {
            Model = ChatModel.OpenAi.O4.V4Mini,
            Background = true,
            Instructions = "You are a helpful assistant",
            Prompt = new PromptConfiguration
            {
                Id = "pmpt_686bb61c674081979cef4c95e2baaa570e95896814dffabf",
                Variables = new Dictionary<string, IPromptVariable>
                {
                    { "imagename", new PromptVariableString("cat") },
                    { "image", new PromptVariableString("test") }
                }
            },
            InputItems = [
                new ResponseInputMessage(ChatMessageRoles.User, [
                    new ResponseInputContentText("Can you describe it?")
                ])
            ]
        };
        
        ResponseResult createResult = await api.Responses.CreateResponse(request);
        ListResponse<ResponseInputItem> result = await api.Responses.ListResponseInputItems(createResult.Id, new ListQuery(100));
    }
    
    [TornadoTest]
    public static async Task ResponseReusablePrompt()
    {
        TornadoApi api = Program.Connect();
        ResponseRequest request = new ResponseRequest
        {
            Model = ChatModel.OpenAi.O4.V4Mini,
            Prompt = new PromptConfiguration
            {
                Id = "pmpt_686bb61c674081979cef4c95e2baaa570e95896814dffabf",
                Variables = new Dictionary<string, IPromptVariable>
                {
                    { "imagename", new PromptVariableString("cats") },
                    { "image", new PromptVariableString(string.Empty) }
                },
                Version = "2"
            },
            InputItems = [
                new ResponseInputMessage(ChatMessageRoles.User, [
                    new ResponseInputContentText("What are you expert on?")
                ])
            ]
        };
        
        ResponseResult createResult = await api.Responses.CreateResponse(request);
        Console.WriteLine(createResult.OutputText);
    }
    
    [TornadoTest]
    public static async Task ResponseReusablePromptComplex()
    {
        byte[] bytes = await File.ReadAllBytesAsync("Static/Images/catBoi.jpg");
        string base64 = $"data:image/jpeg;base64,{Convert.ToBase64String(bytes)}";
        
        TornadoApi api = Program.Connect();
        ResponseRequest request = new ResponseRequest
        {
            Model = ChatModel.OpenAi.O4.V4Mini,
            Prompt = new PromptConfiguration
            {
                Id = "pmpt_686bb61c674081979cef4c95e2baaa570e95896814dffabf",
                Variables = new Dictionary<string, IPromptVariable>
                {
                    { "imagename", new PromptVariableString("cats") },
                    { 
                        "image", 
                        new ResponseInputContentImage
                        {
                            ImageUrl = base64
                        } 
                    }
                },
                Version = "2"
            },
            InputItems = [
                new ResponseInputMessage(ChatMessageRoles.User, [
                    new ResponseInputContentText("Can you describe the image?")
                ])
            ]
        };
        
        ResponseResult createResult = await api.Responses.CreateResponse(request);
        Console.WriteLine(createResult.OutputText);
    }

    /// <summary>
    /// Tests the unified patch parser's ability to handle headerless chunks (patches without @@ -a,b +c,d @@ headers).
    /// This is required for GPT-5-1 support where patches may lack explicit line number metadata.
    /// </summary>
    [TornadoTest]
    public static async Task TestHeaderlessPatchParsing()
    {
        await Task.CompletedTask; // No async operations, but keeping consistent method signature

        Console.WriteLine("=== Testing Headerless Patch Parsing ===\n");

        // Test Case 1: Simple headerless patch with additions
        string originalCode1 = "Hello World";
        string headerlessDiff1 = """
            @@
            -Hello World
            +Hello Beautiful World
            """;

        bool success1 = DiffPatchEngine.TryApply(originalCode1, headerlessDiff1, out string result1, out string? error1, format: PatchFormat.V4a);
        Assert.That(success1, Is.True, $"Simple headerless patch failed: {error1}");
        Assert.That(result1, Is.EqualTo("Hello Beautiful World"));
        Console.WriteLine($"✓ Test 1 passed: Simple replacement\n  Original: {originalCode1}\n  Result:   {result1}\n");

        // Test Case 2: Headerless patch with multiple lines
        string originalCode2 = """
            public class Calculator
            {
                public int Add(int a, int b)
                {
                    return a + b;
                }
            }
            """;

        string headerlessDiff2 = """
            @@
            -public class Calculator
            +/// <summary>Calculator class for basic operations</summary>
            +public class Calculator
             {
                 public int Add(int a, int b)
                 {
                     return a + b;
                 }
             }
            """;

        bool success2 = DiffPatchEngine.TryApply(originalCode2, headerlessDiff2, out string result2, out string? error2, format: PatchFormat.V4a);
        Assert.That(success2, Is.True, $"Multi-line headerless patch failed: {error2}");
        Assert.That(result2.Contains("/// <summary>Calculator class for basic operations</summary>"), Is.True);
        Console.WriteLine($"✓ Test 2 passed: Multi-line patch with documentation\n");

        // Test Case 3: Headerless patch for file creation (empty source)
        string headerlessDiff3 = """
            @@
            +using System;
            +
            +namespace Demo
            +{
            +    public class HelloWorld
            +    {
            +        public static void Main()
            +        {
            +            Console.WriteLine("Hello World");
            +        }
            +    }
            +}
            """;

        bool success3 = DiffPatchEngine.TryApply(string.Empty, headerlessDiff3, out string result3, out string? error3, format: PatchFormat.V4a);
        Assert.That(success3, Is.True, $"File creation with headerless patch failed: {error3}");
        Assert.That(result3.Contains("public class HelloWorld"), Is.True);
        Assert.That(result3.Contains("using System;"), Is.True);
        Console.WriteLine($"✓ Test 3 passed: File creation from empty source\n");

        // Test Case 4: Multiple headerless patches in sequence
        string headerlessDiff4 = """
            @@
            +First line
            +Second line
            @@
            +Third line
            +Fourth line
            """;

        bool success4 = DiffPatchEngine.TryApply(string.Empty, headerlessDiff4, out string result4, out string? error4, format: PatchFormat.V4a);
        Assert.That(success4, Is.True, $"Multiple headerless patches failed: {error4}");
        Assert.That(result4.Contains("First line"), Is.True);
        Assert.That(result4.Contains("Fourth line"), Is.True);
        Console.WriteLine($"✓ Test 4 passed: Multiple headerless patches\n");
        
        // Test Case 6: Parse patches explicitly to verify structure
        string headerlessDiff6 = """
            @@
            +Hello
            -World
             Test
            """;

        ImmutableList<Patch> patches = DiffPatchEngine.Parse(headerlessDiff6);
        Assert.That(patches.Count, Is.EqualTo(1), "Should parse exactly one headerless patch");
        Assert.That(patches[0].Start1, Is.EqualTo(0), "Headerless patch should have synthetic start1=0");
        Assert.That(patches[0].Start2, Is.EqualTo(0), "Headerless patch should have synthetic start2=0");
        Assert.That(patches[0].Diffs.Count, Is.EqualTo(3), "Should have 3 diff operations");
        Console.WriteLine($"✓ Test 6 passed: Patch structure verification\n  Parsed patch with {patches[0].Diffs.Count} diffs\n  Start positions: ({patches[0].Start1}, {patches[0].Start2})\n");

        Console.WriteLine("=== All Headerless Patch Tests Passed ===");
    }

    /// <summary>
    /// Complete demonstration of the GPT-5.1 apply_patch tool with V4A format.
    /// Shows the full iterative workflow: request patches → apply → report results → continue.
    /// </summary>
    [TornadoTest]
    public static async Task TestApplyPatchToolWorkflow()
    {
        TornadoApi api = Program.Connect();

        Console.WriteLine("=== GPT-5.1 Apply Patch Tool Workflow Demo ===\n");

        // Step 1: Set up a workspace with some files
        Dictionary<string, string> workspace = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["math_utils.py"] = """
                def fibonacci(n):
                    if n <= 1:
                        return n
                    return fib(n-1) + fib(n-2)
                
                def factorial(n):
                    if n <= 1:
                        return 1
                    return n * fact(n-1)
                """,
            ["README.md"] = """
                # Math Utilities
                
                A collection of math functions.
                """
        };

        Console.WriteLine("Initial workspace:");
        foreach (KeyValuePair<string, string> file in workspace)
        {
            Console.WriteLine($"  📄 {file.Key} ({file.Value.Length} bytes)");
        }
        Console.WriteLine();

        // Step 2: Ask GPT-5.1 to fix recursive function calls
        string userRequest = """
            I have a Python project with the following files:
            
            math_utils.py:
            ```python
            def fibonacci(n):
                if n <= 1:
                    return n
                return fib(n-1) + fib(n-2)
            
            def factorial(n):
                if n <= 1:
                    return 1
                return n * fact(n-1)
            ```
            
            README.md:
            ```markdown
            # Math Utilities
            
            A collection of math functions.
            ```
            
            Please fix the bugs in math_utils.py where the recursive calls use
            abbreviated function names (fib, fact) instead of the full names.
            Also add a usage example to the README.
            """;

        Console.WriteLine("User Request:");
        Console.WriteLine(userRequest);
        Console.WriteLine("\n--- Calling GPT-5.1 with apply_patch tool ---\n");

        ResponseResult response = await api.Responses.CreateResponse(new ResponseRequest
        {
            Model = ChatModel.OpenAi.Gpt51.V51,
            InputItems = [
                new ResponseInputMessage(ChatMessageRoles.User, userRequest)
            ],
            Tools = [
                new ResponseApplyPatchTool()
            ],
            Background = false
        });

        // Step 3: Extract and apply patch operations
        List<ResponseApplyPatchToolCallItem> patchCalls = response.Output
            ?.OfType<ResponseApplyPatchToolCallItem>()
            .ToList() ?? [];

        Console.WriteLine($"Received {patchCalls.Count} patch operation(s):\n");

        List<ApplyPatchCallOutput> results = V4APatchHarness.ProcessPatchBatch(patchCalls, workspace);

        foreach (ApplyPatchCallOutput result in results)
        {
            string statusIcon = result.Status == ResponseApplyPatchCallOutputStatus.Completed ? "✅" : "❌";
            Console.WriteLine($"{statusIcon} {result.Output}");
        }
        Console.WriteLine();

        // Step 4: Report results back to GPT-5.1
        Console.WriteLine("--- Sending results back to GPT-5.1 ---\n");

        ResponseResult followup = await api.Responses.CreateResponse(new ResponseRequest
        {
            Model = ChatModel.OpenAi.Gpt51.V51,
            PreviousResponseId = response.Id,
            InputItems = results.Cast<ResponseInputItem>().ToList(),
            Tools = [
                new ResponseApplyPatchTool()
            ],
            Background = false
        });

        // Step 5: Get model's explanation
        ResponseOutputMessageItem? message = followup.Output?.OfType<ResponseOutputMessageItem>().FirstOrDefault();
        if (message is not null && message.Content.Count > 0)
        {
            Console.WriteLine("GPT-5.1 Response:");
            ResponseOutputTextContent? textContent = message.Content.OfType<ResponseOutputTextContent>().FirstOrDefault();
            if (textContent is not null)
            {
                Console.WriteLine(textContent.Text);
            }
            Console.WriteLine();
        }

        // Verify the fixes were applied
        Console.WriteLine("Final workspace state:");
        foreach (KeyValuePair<string, string> file in workspace)
        {
            Console.WriteLine($"\n📄 {file.Key}:");
            Console.WriteLine(file.Value);
        }

        // Assertions
        Assert.That(workspace["math_utils.py"].Contains("return fibonacci(n-1) + fibonacci(n-2)"), Is.True);
        Assert.That(workspace["math_utils.py"].Contains("return n * factorial(n-1)"), Is.True);
        // Check that usage/example information was added (model might use "Usage", "Example", "usage example", etc.)
        Assert.That(workspace["README.md"].Contains("usage") || workspace["README.md"].Contains("Usage") || 
                    workspace["README.md"].Contains("example") || workspace["README.md"].Contains("Example"), Is.True);

        Console.WriteLine("\n=== Apply Patch Tool Workflow Demo Complete ===");
    }
    
    [TornadoTest]
    public static async Task TestNewlinePreservation()
    {
        await Task.CompletedTask;

        Console.WriteLine("=== Testing Newline Preservation in V4A Patches ===\n");

        // Test 1: Simple multiline text
        string original = "line1\nline2\nline3";
        string headerlessDiff = "@@\n line1\n-line2\n+line2modified\n line3";

        bool success = DiffPatchEngine.TryApply(original, headerlessDiff, out string result, out string? error, format: PatchFormat.V4a);
        
        Console.WriteLine($"Original:\n{original}");
        Console.WriteLine($"\nResult:\n{result}");
        Console.WriteLine($"\nSuccess: {success}");
        if (!success) Console.WriteLine($"Error: {error}");
        
        Assert.That(success, Is.True, error);
        Assert.That(result, Is.EqualTo("line1\nline2modified\nline3"));

        // Test 2: Python code
        string pythonCode = "def fibonacci(n):\n    if n <= 1:\n        return n\n    return fib(n-1) + fib(n-2)";
        string pythonDiff = "@@\n def fibonacci(n):\n     if n <= 1:\n         return n\n-    return fib(n-1) + fib(n-2)\n+    return fibonacci(n-1) + fibonacci(n-2)";

        bool success2 = DiffPatchEngine.TryApply(pythonCode, pythonDiff, out string result2, out string? error2, format: PatchFormat.V4a);
        
        Console.WriteLine($"\n\nPython Original:\n{pythonCode}");
        Console.WriteLine($"\nPython Result:\n{result2}");
        Console.WriteLine($"\nSuccess: {success2}");
        if (!success2) Console.WriteLine($"Error: {error2}");
        
        Assert.That(success2, Is.True, error2);
        Assert.That(result2.Contains("fibonacci(n-1)"), Is.True);

        Console.WriteLine("\n=== Newline Preservation Tests Passed ===");
    }

    /// <summary>
    /// Demonstrates error handling when patches fail to apply.
    /// </summary>
    [TornadoTest]
    public static async Task TestApplyPatchErrorHandling()
    {
        await Task.CompletedTask; // Synchronous test with async signature for consistency

        Console.WriteLine("=== V4A Patch Error Handling Demo ===\n");

        Dictionary<string, string> workspace = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["existing.txt"] = "Hello World"
        };

        // Test 1: File not found
        Console.WriteLine("Test 1: Update non-existent file");
        ResponseApplyPatchOperation op1 = new ResponseApplyPatchOperation
        {
            Type = ResponseApplyPatchOperationType.UpdateFile,
            Path = "nonexistent.txt",
            Diff = "@@\n-old\n+new"
        };

        bool success1 = V4APatchHarness.TryApplyOperation(op1, workspace, out string result1);
        Console.WriteLine($"  Result: {(success1 ? "✅" : "❌")} {result1}");
        Assert.That(success1, Is.False);
        Assert.That(result1.Contains("not found"), Is.True);

        // Test 2: File already exists
        Console.WriteLine("\nTest 2: Create file that already exists");
        ResponseApplyPatchOperation op2 = new ResponseApplyPatchOperation
        {
            Type = ResponseApplyPatchOperationType.CreateFile,
            Path = "existing.txt",
            Diff = "@@\n+new content"
        };

        bool success2 = V4APatchHarness.TryApplyOperation(op2, workspace, out string result2);
        Console.WriteLine($"  Result: {(success2 ? "✅" : "❌")} {result2}");
        Assert.That(success2, Is.False);
        Assert.That(result2.Contains("already exists"), Is.True);

        // Test 3: Invalid context (patch won't apply)
        Console.WriteLine("\nTest 3: Patch with invalid context");
        ResponseApplyPatchOperation op3 = new ResponseApplyPatchOperation
        {
            Type = ResponseApplyPatchOperationType.UpdateFile,
            Path = "existing.txt",
            Diff = "@@\n-This line doesn't exist\n+replacement"
        };

        bool success3 = V4APatchHarness.TryApplyOperation(op3, workspace, out string result3);
        Console.WriteLine($"  Result: {(success3 ? "✅" : "❌")} {result3}");
        Assert.That(success3, Is.False);

        // Test 4: Successful operation
        Console.WriteLine("\nTest 4: Valid patch operation");
        ResponseApplyPatchOperation op4 = new ResponseApplyPatchOperation
        {
            Type = ResponseApplyPatchOperationType.UpdateFile,
            Path = "existing.txt",
            Diff = "@@\n-Hello World\n+Hello Universe"
        };

        bool success4 = V4APatchHarness.TryApplyOperation(op4, workspace, out string result4);
        Console.WriteLine($"  Result: {(success4 ? "✅" : "❌")} {result4}");
        Assert.That(success4, Is.True);
        Assert.That(workspace["existing.txt"], Is.EqualTo("Hello Universe"));

        Console.WriteLine("\n=== Error Handling Tests Passed ===");
    }

    /// <summary>
    /// Demonstrates batch processing of multiple patch operations.
    /// </summary>
    [TornadoTest]
    public static async Task TestApplyPatchBatchProcessing()
    {
        await Task.CompletedTask; // Synchronous test

        Console.WriteLine("=== V4A Batch Patch Processing Demo ===\n");

        Dictionary<string, string> workspace = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["config.yaml"] = "version: 1.0\nenvironment: dev",
            ["app.py"] = "def main():\n    pass"
        };

        List<ResponseApplyPatchToolCallItem> patchCalls =
        [
            // Create a new file
            new ResponseApplyPatchToolCallItem
            {
                Id = "patch_001",
                CallId = "call_001",
                Operation = new ResponseApplyPatchOperation
                {
                    Type = ResponseApplyPatchOperationType.CreateFile,
                    Path = "requirements.txt",
                    Diff = "@@\n+flask>=2.0.0\n+pydantic>=2.0.0\n+pytest>=7.0.0"
                }
            },
            // Update existing file
            new ResponseApplyPatchToolCallItem
            {
                Id = "patch_002",
                CallId = "call_002",
                Operation = new ResponseApplyPatchOperation
                {
                    Type = ResponseApplyPatchOperationType.UpdateFile,
                    Path = "config.yaml",
                    Diff = "@@\n version: 1.0\n-environment: dev\n+environment: production"
                }
            },
            // Update another file
            new ResponseApplyPatchToolCallItem
            {
                Id = "patch_003",
                CallId = "call_003",
                Operation = new ResponseApplyPatchOperation
                {
                    Type = ResponseApplyPatchOperationType.UpdateFile,
                    Path = "app.py",
                    Diff = "@@\n def main():\n-    pass\n+    print('Hello, World!')\n+    return 0"
                }
            },
            // Delete a file
            new ResponseApplyPatchToolCallItem
            {
                Id = "patch_004",
                CallId = "call_004",
                Operation = new ResponseApplyPatchOperation
                {
                    Type = ResponseApplyPatchOperationType.DeleteFile,
                    Path = "config.yaml"
                }
            }
        ];

        Console.WriteLine("Processing batch of 4 patch operations...\n");

        List<ApplyPatchCallOutput> results = V4APatchHarness.ProcessPatchBatch(patchCalls, workspace);

        foreach (ApplyPatchCallOutput result in results)
        {
            string statusIcon = result.Status == ResponseApplyPatchCallOutputStatus.Completed ? "✅" : "❌";
            Console.WriteLine($"{statusIcon} [{result.CallId}] {result.Output}");
        }

        Console.WriteLine("\nFinal workspace:");
        foreach (KeyValuePair<string, string> file in workspace.OrderBy(f => f.Key))
        {
            Console.WriteLine($"  📄 {file.Key}");
        }

        // Assertions
        Assert.That(results.Count, Is.EqualTo(4));
        Assert.That(results.Count(r => r.Status == ResponseApplyPatchCallOutputStatus.Completed), Is.EqualTo(4));
        Assert.That(workspace.ContainsKey("requirements.txt"), Is.True);
        Assert.That(workspace.ContainsKey("config.yaml"), Is.False); // Should be deleted
        Assert.That(workspace["app.py"].Contains("Hello, World!"), Is.True);

        Console.WriteLine("\n=== Batch Processing Test Passed ===");
    }

    [TornadoTest]
    public static async Task ResponseGpt53Codex()
    {
        ResponseResult result = await Program.Connect().Responses.CreateResponse(new ResponseRequest
        {
            Model = ChatModel.OpenAi.Codex.Gpt53Codex,
            Reasoning = new ReasoningConfiguration(ResponseReasoningEfforts.Low),
            InputItems =
            [
                new ResponseInputMessage(ChatMessageRoles.User, "Write a one-line Python function named add that returns the sum of two integers.")
            ],
            MaxOutputTokens = 512
        });

        Assert.That(result.Status, Is.EqualTo(ResponseMessageStatuses.Completed));
        Assert.That(result.Model?.Name, Is.EqualTo("gpt-5.3-codex"));

        ResponseOutputMessageItem message = result.Output.OfType<ResponseOutputMessageItem>().First();
        ResponseOutputTextContent text = message.Content.OfType<ResponseOutputTextContent>().First();
        AssertTrue(text.Text.Contains("def add", StringComparison.Ordinal));
        Console.WriteLine(text.Text);
    }

    [TornadoTest]
    public static async Task ResponseGpt52Codex()
    {
        ResponseResult result = await Program.Connect().Responses.CreateResponse(new ResponseRequest
        {
            Model = ChatModel.OpenAi.Gpt52.V52Codex,
            Reasoning = new ReasoningConfiguration(ResponseReasoningEfforts.Low),
            InputItems =
            [
                new ResponseInputMessage(ChatMessageRoles.User, "Write a one-line Python function named multiply that returns the product of two integers.")
            ],
            MaxOutputTokens = 512
        });

        Assert.That(result.Status, Is.EqualTo(ResponseMessageStatuses.Completed));
        Assert.That(result.Model?.Name, Is.EqualTo("gpt-5.2-codex"));

        ResponseOutputMessageItem message = result.Output.OfType<ResponseOutputMessageItem>().First();
        ResponseOutputTextContent text = message.Content.OfType<ResponseOutputTextContent>().First();
        AssertTrue(text.Text.Contains("def multiply", StringComparison.Ordinal));
        Console.WriteLine(text.Text);
    }

    [TornadoTest]
    public static async Task ResponseGpt53CodexPhaseFollowUp()
    {
        TornadoApi api = Program.Connect();

        ResponseResult first = await api.Responses.CreateResponse(new ResponseRequest
        {
            Model = ChatModel.OpenAi.Codex.Gpt53Codex,
            Reasoning = new ReasoningConfiguration(ResponseReasoningEfforts.Low),
            InputItems =
            [
                new ResponseInputMessage(ChatMessageRoles.User, "Reply with exactly two short sentences about C#. Do not use tools.")
            ],
            MaxOutputTokens = 256
        });

        ResponseOutputMessageItem firstMessage = first.Output.OfType<ResponseOutputMessageItem>().First();
        AssertTrue(!string.IsNullOrEmpty(firstMessage.Content.OfType<ResponseOutputTextContent>().First().Text));

        ResponseResult followUp = await api.Responses.CreateResponse(new ResponseRequest
        {
            Model = ChatModel.OpenAi.Codex.Gpt53Codex,
            PreviousResponseId = first.Id,
            Reasoning = new ReasoningConfiguration(ResponseReasoningEfforts.Low),
            InputItems =
            [
                new ResponseInputMessage(ChatMessageRoles.User, "Now reply with one sentence about F#.")
            ],
            MaxOutputTokens = 256
        });

        ResponseOutputMessageItem followUpMessage = followUp.Output.OfType<ResponseOutputMessageItem>().First();
        string followUpText = followUpMessage.Content.OfType<ResponseOutputTextContent>().First().Text;
        AssertTrue(!string.IsNullOrEmpty(followUpText));
        Console.WriteLine($"First phase: {firstMessage.Phase}, follow-up phase: {followUpMessage.Phase}");
        Console.WriteLine(followUpText);
    }

    [TornadoTest]
    public static async Task ResponseCompactEndpoint()
    {
        TornadoApi api = Program.Connect();
        ChatModel model = ChatModel.OpenAi.Gpt5.V5Mini;

        List<ResponseInputItem> conversation =
        [
            new ResponseInputMessage(ChatMessageRoles.User, "Let's begin a long coding task."),
            new ResponseInputMessage(ChatMessageRoles.User, new string('x', 8_000))
        ];

        ResponseResult first = await api.Responses.CreateResponse(new ResponseRequest
        {
            Model = model,
            InputItems = conversation,
            Store = false,
            MaxOutputTokens = 256
        });

        conversation.AddRange(first.Output.ToInputItems());
        conversation.Add(new ResponseInputMessage(ChatMessageRoles.User, "Summarize in one sentence."));

        ResponseCompactResult compacted = await api.Responses.CompactResponse(new ResponseCompactRequest(model, conversation));

        Assert.That(compacted.Object, Is.EqualTo("response.compaction"));
        AssertNotNull(compacted.GetLatestCompaction()?.EncryptedContent);
        AssertTrue(!string.IsNullOrEmpty(compacted.GetLatestCompaction()!.EncryptedContent));

        List<ResponseInputItem> nextInput = compacted.Output.ToInputItems();
        nextInput.Add(new ResponseInputMessage(ChatMessageRoles.User, "What was the original task about?"));

        ResponseResult followUp = await api.Responses.CreateResponse(new ResponseRequest
        {
            Model = model,
            InputItems = nextInput,
            Store = false,
            MaxOutputTokens = 256
        });

        AssertNotNull(followUp.OutputText);
        AssertTrue(!string.IsNullOrEmpty(followUp.OutputText));
        Console.WriteLine($"Compact usage: {compacted.Usage?.TotalTokens} tokens");
        Console.WriteLine(followUp.OutputText);
    }

    [TornadoTest]
    public static async Task ResponseServerSideCompaction()
    {
        TornadoApi api = Program.Connect();
        ChatModel model = ChatModel.OpenAi.Gpt5.V5Mini;

        ResponseResult response = await api.Responses.CreateResponse(new ResponseRequest
        {
            Model = model,
            InputItems =
            [
                new ResponseInputMessage(ChatMessageRoles.User, "Track a multi-step refactor for a billing service."),
                new ResponseInputMessage(ChatMessageRoles.User, new string('y', 12_000))
            ],
            Store = false,
            MaxOutputTokens = 256
        }.WithServerSideCompaction(1_000));

        AssertNotNull(response.OutputText);
        AssertTrue(!string.IsNullOrEmpty(response.OutputText));

        ResponseCompactionOutputItem? compaction = response.GetLatestCompaction();
        if (compaction is not null)
        {
            Console.WriteLine($"Server-side compaction emitted item {compaction.Id}");
            AssertTrue(!string.IsNullOrEmpty(compaction.EncryptedContent));
        }
        else
        {
            Console.WriteLine("Server-side compaction not triggered for this request size.");
        }

        Console.WriteLine(response.OutputText);
    }
}
