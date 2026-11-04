using System.Collections.Generic;
using System.Linq;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Chat.Vendors.Anthropic;
using LlmTornado.ChatFunctions;
using LlmTornado.Code;
using LlmTornado.Vendor.Anthropic;
using Newtonsoft.Json;

namespace LlmTornado.Tokenize.Vendors;

internal class VendorAnthropicTokenizeRequest
{
    [JsonProperty("model")]
    public string Model { get; set; }

    [JsonProperty("messages")]
    public List<VendorAnthropicChatRequestMessage> Messages { get; set; } = [];

    [JsonProperty("system")]
    [JsonConverter(typeof(VendorAnthropicChatRequestMessageContent.VendorAnthropicChatRequestMessageContentJsonConverter))]
    public VendorAnthropicChatRequestMessageContent? System { get; set; }

    [JsonProperty("tools")]
    public List<VendorAnthropicToolFunction>? Tools { get; set; }

    public VendorAnthropicTokenizeRequest(TokenizeRequest request, IEndpointProvider provider)
    {
        if (request.SourceChatRequest?.VendorExtensions?.Anthropic?.Thread?.Type == "continue")
            throw new System.ArgumentException("Token counting requires the complete conversation, system and tools, not a thread continuation. Supply a separate full-history TokenizeRequest.");
        Model = request.Model?.Name ?? string.Empty;

        if (request.Messages is not null)
        {
            foreach (ChatMessage msg in request.Messages)
            {
                if (msg.Role is ChatMessageRoles.System)
                {
                    if (Messages.Count == 0)
                    {
                        System = new VendorAnthropicChatRequestMessageContent(msg);
                    }
                    else
                    {
                        Messages.Add(new VendorAnthropicChatRequestMessage(ChatMessageRoles.System, msg));
                    }
                }
                else
                {
                    Messages.Add(new VendorAnthropicChatRequestMessage(msg.Role ?? ChatMessageRoles.User, msg));
                }
            }
        }

        // Include tools if provided
        AnthropicApiVersions.ValidateMessages(Messages, provider);

        if (request.Tools is not null)
        {
            Tools = request.Tools.Where(x => x.Function is not null).Select(t => new VendorAnthropicToolFunction(t)).ToList();
        }
    }
}

