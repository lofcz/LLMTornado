using System;
using Newtonsoft.Json;

namespace LlmTornado.Chat.Vendors.Anthropic;

/// <summary>
/// Server-held Messages API history. Keep the complete conversation locally for replay when the thread expires.
/// Continue only from a completed response, sending new messages without system or tools.
/// </summary>
public sealed class AnthropicMessageThread
{
    /// <summary>Required beta header for both create and continue requests.</summary>
    public const string BetaHeader = "message-threads-2026-08-12";

    /// <summary>Either create or continue.</summary>
    [JsonProperty("type")]
    public string Type { get; set; } = "create";

    /// <summary>The latest completed response ID. Required only for continue.</summary>
    [JsonProperty("previous_message_id", NullValueHandling = NullValueHandling.Ignore)]
    public string? PreviousMessageId { get; set; }

    /// <summary>Stores a full conversation, including the resulting response.</summary>
    public static AnthropicMessageThread Create() => new();

    /// <summary>Appends new messages to the last completed response.</summary>
    public static AnthropicMessageThread Continue(string previousMessageId) => new() { Type = "continue", PreviousMessageId = previousMessageId };

    internal void Validate()
    {
        if (Type is not ("create" or "continue"))
            throw new ArgumentException("A message thread type must be create or continue.");
        if (Type == "continue" && string.IsNullOrWhiteSpace(PreviousMessageId))
            throw new ArgumentException("Continuing a message thread requires the previous completed message ID.");
        if (Type == "create" && PreviousMessageId is not null)
            throw new ArgumentException("Creating a message thread cannot specify previous_message_id.");
    }
}
