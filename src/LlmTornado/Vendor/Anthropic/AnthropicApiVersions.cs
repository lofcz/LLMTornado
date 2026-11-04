using System;
using System.Collections.Generic;
using LlmTornado.Chat.Vendors.Anthropic;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Vendor.Anthropic;

/// <summary>Supported Anthropic protocol revisions. The URL version remains v1.</summary>
public static class AnthropicApiVersions
{
    /// <summary>Legacy protocol, retained as the default for compatibility.</summary>
    public const string Legacy = "2023-06-01";
    /// <summary>Removes sampling parameters and tightens message content validation.</summary>
    public const string October2026 = "2026-10-21";

    internal static bool UsesOctober2026(IEndpointProvider provider) =>
        provider is AnthropicEndpointProvider { ApiRevision: October2026 };

    internal static void ValidateMessages(IEnumerable<VendorAnthropicChatRequestMessage> messages, IEndpointProvider provider)
    {
        if (!UsesOctober2026(provider)) return;
        JArray serialized = JArray.FromObject(messages, JsonSerializer.Create(EndpointBase.NullSettings));
        foreach (JObject message in serialized)
        {
            if (message["content"] is not JArray content) continue;
            if ((string?)message["role"] == "assistant")
            {
                foreach (JToken block in content)
                    if ((string?)block["type"] == "document")
                        throw new ArgumentException("Anthropic API 2026-10-21 requires document blocks in user messages.");
            }
            else if ((string?)message["role"] == "user")
            {
                foreach (JToken block in content.Descendants())
                    if (block is JObject obj && (string?)obj["type"] == "text" && obj["citations"] is JArray { Count: > 0 })
                        throw new ArgumentException("Anthropic API 2026-10-21 does not accept citations on text in user messages.");
            }
        }
    }
}
