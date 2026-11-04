using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

[TestFixture]
public class GoogleImageDataUriTests
{
    private static IEndpointProvider Provider => new TornadoApi(LLmProviders.Google, "test-key").GetProvider(LLmProviders.Google);

    private static JToken? InlineData(ChatImage image)
    {
        ChatRequest request = new()
        {
            Model = "gemini-2.5-flash",
            Messages = [new ChatMessage(ChatMessageRoles.User, [new ChatMessagePart(image)])]
        };

        JObject body = JObject.Parse(request.Serialize(Provider).Body.ToString()!);
        return body["contents"]?[0]?["parts"]?[0]?["inlineData"];
    }

    [Test]
    public void DataUri_WithoutMimeType_StripsPrefixAndInfersMimeType()
    {
        JToken? inline = InlineData(new ChatImage("data:image/png;base64,QUJD"));
        Assert.That(inline?["data"]?.ToString(), Is.EqualTo("QUJD"));
        Assert.That(inline?["mimeType"]?.ToString(), Is.EqualTo("image/png"));
    }

    [Test]
    public void DataUri_WithExplicitMimeType_StripsPrefixAndKeepsExplicitMimeType()
    {
        JToken? inline = InlineData(new ChatImage("data:image/png;base64,QUJD") { MimeType = "image/jpeg" });
        Assert.That(inline?["data"]?.ToString(), Is.EqualTo("QUJD"));
        Assert.That(inline?["mimeType"]?.ToString(), Is.EqualTo("image/jpeg"));
    }

    [Test]
    public void RawBase64_WithMimeType_IsUnchanged()
    {
        JToken? inline = InlineData(new ChatImage("QUJD") { MimeType = "image/png" });
        Assert.That(inline?["data"]?.ToString(), Is.EqualTo("QUJD"));
        Assert.That(inline?["mimeType"]?.ToString(), Is.EqualTo("image/png"));
    }
}
