using LlmTornado.Chat;
using LlmTornado.Chat.Vendors.Anthropic;
using LlmTornado.Chat.Vendors.Cohere;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

[TestFixture]
public class MultipleStopSequencesTests
{
    private static ChatRequest CreateRequest(params string[] stops)
    {
        return new ChatRequest
        {
            MultipleStopSequences = stops,
            Messages = [new ChatMessage(ChatMessageRoles.User, "test")]
        };
    }

    [Test]
    public void Anthropic_SendsEveryStopSequence()
    {
        VendorAnthropicChatRequest vendor = new VendorAnthropicChatRequest(CreateRequest("END", "STOP"), new AnthropicEndpointProvider());

        Assert.That(vendor.StopSequences, Is.EqualTo(new[] { "END", "STOP" }));
    }

    [Test]
    public void Cohere_SendsEveryStopSequence()
    {
        VendorCohereChatRequest vendor = new VendorCohereChatRequest(CreateRequest("END", "STOP"), new CohereEndpointProvider());

        Assert.That(vendor.StopSequences, Is.EqualTo(new[] { "END", "STOP" }));
    }

    [Test]
    public void SingleStopSequence_IsKeptWhole()
    {
        VendorAnthropicChatRequest anthropic = new VendorAnthropicChatRequest(CreateRequest("Human: "), new AnthropicEndpointProvider());
        VendorCohereChatRequest cohere = new VendorCohereChatRequest(CreateRequest("Human: "), new CohereEndpointProvider());

        Assert.That(anthropic.StopSequences, Is.EqualTo(new[] { "Human: " }));
        Assert.That(cohere.StopSequences, Is.EqualTo(new[] { "Human: " }));
    }

    [Test]
    public void Anthropic_SerializesStopSequencesWithCorrectWireName()
    {
        VendorAnthropicChatRequest vendor = new VendorAnthropicChatRequest(CreateRequest("END", "STOP"), new AnthropicEndpointProvider());
        JObject json = JObject.Parse(JsonConvert.SerializeObject(vendor));

        Assert.That(json["stop_sequences"]?.ToObject<string[]>(), Is.EqualTo(new[] { "END", "STOP" }));
        Assert.That(json.ContainsKey("stop_seqences"), Is.False);
    }

    [Test]
    public void Anthropic_OmitsStopSequencesWhenNotSet()
    {
        ChatRequest request = new ChatRequest { Messages = [new ChatMessage(ChatMessageRoles.User, "test")] };
        VendorAnthropicChatRequest vendor = new VendorAnthropicChatRequest(request, new AnthropicEndpointProvider());
        JObject json = JObject.Parse(JsonConvert.SerializeObject(vendor));

        Assert.That(json.ContainsKey("stop_sequences"), Is.False);
    }
}
