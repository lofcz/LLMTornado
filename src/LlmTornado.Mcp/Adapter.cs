using System.Collections;
using LlmTornado.Chat.Vendors.Anthropic;
using LlmTornado.Common;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Newtonsoft.Json.Linq;
using Tool = LlmTornado.Common.Tool;

namespace LlmTornado.Mcp;

public static class McpExtensions
{
    private static string ToBase64(ReadOnlyMemory<byte> data)
    {
#if MODERN
        return Convert.ToBase64String(data.Span);
#else
        return Convert.ToBase64String(data.ToArray());
#endif
    }
    
    /// <summary>
    /// The MCP connector takes the bare OAuth token and adds the "Bearer" scheme itself,
    /// so a full "Bearer xxx" header value has to be reduced to "xxx".
    /// </summary>
    private static string? ToAuthorizationToken(string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return null;
        }

        string value = headerValue.Trim();
        const string scheme = "Bearer ";

        return value.StartsWith(scheme, StringComparison.OrdinalIgnoreCase) ? value.Substring(scheme.Length).TrimStart() : value;
    }

    public static AnthropicMcpServer ToAnthropicMcpServerAsync(this MCPServer client)
    {
        AnthropicMcpServer server = new AnthropicMcpServer
        {
            Name = client.ServerLabel,
            Url = client.ServerUrl,
            AuthorizationToken = ToAuthorizationToken(client.AdditionalConnectionHeaders?.FirstOrDefault(key => key.Key.ToLower().Contains("auth")).Value),
            Configuration = new AnthropicMcpConfiguration
            {
                Enabled = true,
                AllowedTools = client.AllowedTools ?? []
            }
        };

        return server;
    }

    /// <summary>
    /// Creates the MCP toolset for a server produced by <see cref="ToAnthropicMcpServerAsync"/>.
    /// </summary>
    public static AnthropicMcpToolset ToAnthropicMcpToolset(this MCPServer client)
    {
        AnthropicMcpServer server = client.ToAnthropicMcpServerAsync();
        return AnthropicMcpToolset.FromLegacyConfiguration(server.Name, server.Configuration);
    }

    public static async ValueTask<List<Tool>> ListTornadoToolsAsync(this McpClient client)
    {
        IList<McpClientTool> tools = await client.ListToolsAsync();
        List<Tool> converted = tools.Select(x => x.ToTornadoTool()).ToList();
        return converted;
    }
    
    private static Dictionary<string, object?>? DeepConvert(Dictionary<string, object?>? args)
    {
        if (args is null) 
            return null;

        Dictionary<string, object?> result = [];
        
        foreach (KeyValuePair<string, object?> kvp in args)
        {
            result[kvp.Key] = ConvertValue(kvp.Value);
        }
        
        return result;
    }

    private static object? ConvertValue(object? value)
    {
        if (value is null) return null;
        
        try
        {
            return value switch
            {
                JArray jArray => jArray.Select(ConvertValue).ToList(),
                JObject jObject => jObject.Properties()
                    .ToDictionary(
                        prop => prop.Name, 
                        prop => ConvertValue(prop.Value)
                    ),
                JValue jValue => ConvertValue(jValue.Value),
                JToken { Type: JTokenType.Null } => null,
                JToken jToken => ConvertValue(jToken.ToObject<object>()),
                
                IDictionary<string, object?> dict => dict.ToDictionary(
                    kvp => kvp.Key,
                    kvp => ConvertValue(kvp.Value)
                ),
                IList list => list.Cast<object?>().Select(ConvertValue).ToList(),
                IEnumerable enumerable and not string => enumerable.Cast<object?>().Select(ConvertValue).ToList(),
                _ => value
            };
        }
        catch
        {
            return value;
        }
    }

    public static Tool ToTornadoTool(this McpClientTool tool)
    {
        Tool tornadoTool = new Tool(new ToolFunction(tool.Name, tool.Description, tool.JsonSchema))
        {
            RemoteTool = new McpTool
            {
                CallAsync = async (args, progress, serializer, fillContent, ct) =>
                {
                    Dictionary<string, object?>? convertedArgs = DeepConvert(args);
                    
                    CallToolResult callToolResult = await tool.CallAsync(convertedArgs, progress is null
                        ? null
                        : new Progress<ProgressNotificationValue>(x =>
                        {
                            progress.Report(new ToolCallProgress
                            {
                                Message = x.Message,
                                Progress = x.Progress,
                                Total = x.Total
                            });
                        }), new RequestOptions
                        {
                            JsonSerializerOptions = serializer
                        }, ct ?? CancellationToken.None);

                    string? result = null;
                    
                    if (fillContent && callToolResult.Content.Count > 0)
                    {
                        ContentBlock firstBlock = callToolResult.Content[0];

                        result = firstBlock switch
                        {
                            TextContentBlock textBlock => textBlock.Text,
                            ImageContentBlock imageBlock => ToBase64(imageBlock.Data),
                            AudioContentBlock audioBlock => ToBase64(audioBlock.Data),
                            EmbeddedResourceBlock embeddedResourceBlock => embeddedResourceBlock.Resource.Uri,
                            ResourceLinkBlock resourceLinkBlock => resourceLinkBlock.Uri,
                            _ => result
                        };
                    }

                    McpContent mcpContent = new McpContent
                    {
                        IsError = callToolResult.IsError ?? false,
                        StructuredContent = callToolResult.StructuredContent,
                        McpContentBlocks = callToolResult.Content.Select(x =>
                        {
                            IMcpContentBlock? blockToAdd = null;
                            
                            switch (x)
                            {
                                case TextContentBlock textBlock:
                                {
                                    blockToAdd = new McpContentBlockText
                                    {
                                        Annotations = x.Annotations?.ToMcpAnnotations(),
                                        Text = textBlock.Text,
                                        Type = x.Type,
                                        NativeBlock = x.ToNativeBlock()
                                    };

                                    break;
                                }
                                case ImageContentBlock imageBlock:
                                {
                                    blockToAdd = new McpContentBlockImage
                                    {
                                        Annotations = x.Annotations?.ToMcpAnnotations(),
                                        Data = ToBase64(imageBlock.Data),
                                        MimeType = imageBlock.MimeType,
                                        Type = x.Type,
                                        NativeBlock = x.ToNativeBlock()
                                    };
                                    
                                    break;
                                }
                                case AudioContentBlock audioBlock:
                                {
                                    blockToAdd = new McpContentBlockAudio
                                    {
                                        Annotations = x.Annotations?.ToMcpAnnotations(),
                                        Data = ToBase64(audioBlock.Data),
                                        MimeType = audioBlock.MimeType,
                                        Type = x.Type,
                                        NativeBlock = x.ToNativeBlock()
                                    };
                                    
                                    break;
                                }
                                case EmbeddedResourceBlock embeddedResourceBlock:
                                {
                                    McpContentBlockEmbeddedResourceContents content = embeddedResourceBlock.Resource switch
                                    {
                                        TextResourceContents textResource => new McpContentBlockEmbeddedResourceContentsText { Text = textResource.Text },
                                        BlobResourceContents blobResource => new McpContentBlockEmbeddedResourceContentsBlob { Blob = ToBase64(blobResource.Blob) },
                                        _ => new McpContentBlockEmbeddedResourceContentsUnknown { Native = x }
                                    };

                                    blockToAdd = new McpContentBlockEmbeddedResource
                                    {
                                        Annotations = x.Annotations?.ToMcpAnnotations(),
                                        Resource = content,
                                        Type = x.Type,
                                        NativeBlock = x.ToNativeBlock()
                                    };
                                    
                                    break;
                                }
                                case ResourceLinkBlock resourceLinkBlock:
                                {
                                    blockToAdd = new McpContentBlockLinkResource
                                    {
                                        Annotations = x.Annotations?.ToMcpAnnotations(),
                                        Name = resourceLinkBlock.Name,
                                        Uri = resourceLinkBlock.Uri,
                                        MimeType = resourceLinkBlock.MimeType,
                                        Description = resourceLinkBlock.Description,
                                        Size = resourceLinkBlock.Size,
                                        Type = x.Type,
                                        NativeBlock = x.ToNativeBlock()
                                    };
                                    
                                    break;
                                }
                            }

                            return blockToAdd ?? new McpContentBlockUnknown
                            {
                                NativeBlock = x.ToNativeBlock(),
                                Annotations = x.Annotations?.ToMcpAnnotations(),
                                Type = x.Type
                            };
                        }).ToList()
                    };
                    
                    FunctionResult fr = new FunctionResult
                    {
                        Content = result ?? string.Empty,
                        RemoteContent = mcpContent,
                        InvocationSucceeded = !callToolResult.IsError,
                        ContentJsonType = typeof(string),
                        Name = tool.Name
                    };

                    return fr;
                }
            }
        };

        return tornadoTool;
    }
}