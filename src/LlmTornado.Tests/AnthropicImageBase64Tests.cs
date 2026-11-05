using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

[TestFixture]
public class AnthropicImageBase64Tests
{
    private static JObject SerializeImageSource(string url, string? mimeType)
    {
        ChatMessage message = new ChatMessage(ChatMessageRoles.User,
        [
            new ChatMessagePart(mimeType is null ? new ChatImage(url) : new ChatImage(url, mimeType))
        ]);

        ChatRequest request = new ChatRequest
        {
            Model = ChatModel.Anthropic.Claude45.Sonnet250929,
            Messages = [message]
        };

        TornadoRequestContent serialized = request.Serialize(new TornadoApi("test-key").GetProvider(LLmProviders.Anthropic));
        JObject body = JObject.Parse(serialized.Body.ToString()!);
        return (JObject)body["messages"]![0]!["content"]![0]!["source"]!;
    }

    [Test]
    public void RawJpegBase64_StartingWithSlash_IsSentAsBase64()
    {
        // base64 of a JPEG starts with "/9j/", which Uri.TryCreate accepts as an absolute file path on Unix
        JObject source = SerializeImageSource("/9j/4AAQSkZJRgABAQAAAQABAAD/2wBD", "image/jpeg");

        Assert.That(source["type"]?.ToString(), Is.EqualTo("base64"));
        Assert.That(source["media_type"]?.ToString(), Is.EqualTo("image/jpeg"));
        Assert.That(source["data"]?.ToString(), Is.EqualTo("/9j/4AAQSkZJRgABAQAAAQABAAD/2wBD"));
    }

    [Test]
    public void HttpsUrl_IsSentAsUrl()
    {
        JObject source = SerializeImageSource("https://example.com/cat.jpg", null);

        Assert.That(source["type"]?.ToString(), Is.EqualTo("url"));
        Assert.That(source["url"]?.ToString(), Is.EqualTo("https://example.com/cat.jpg"));
    }

    [Test]
    public void DataUri_WithoutMimeType_StripsPrefixAndInfersMimeType()
    {
        JObject source = SerializeImageSource("data:image/png;base64,QUJD", null);

        Assert.That(source["type"]?.ToString(), Is.EqualTo("base64"));
        Assert.That(source["media_type"]?.ToString(), Is.EqualTo("image/png"));
        Assert.That(source["data"]?.ToString(), Is.EqualTo("QUJD"));
    }

    [Test]
    public void DataUri_WithExplicitMimeType_KeepsExplicitMimeType()
    {
        JObject source = SerializeImageSource("data:image/png;base64,QUJD", "image/jpeg");

        Assert.That(source["media_type"]?.ToString(), Is.EqualTo("image/jpeg"));
        Assert.That(source["data"]?.ToString(), Is.EqualTo("QUJD"));
    }

    [Test]
    public void RawBase64_WithoutMimeType_StillThrows()
    {
        Assert.Throws<Exception>(() => SerializeImageSource("QUJD", null));
    }
}
