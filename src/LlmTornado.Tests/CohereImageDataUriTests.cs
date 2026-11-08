using LlmTornado.Images;
using LlmTornado.Chat;
using LlmTornado.Code;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

[TestFixture]
public class CohereImageDataUriTests
{
    [TestCase("QUJD", "image/png", "data:image/png;base64,QUJD")]
    [TestCase("QUJD", "image/jpeg", "data:image/jpeg;base64,QUJD")]
    [TestCase("data:image/png;base64,QUJD", "image/png", "data:image/png;base64,QUJD")]
    [TestCase("data:image/png;base64,QUJD", null, "data:image/png;base64,QUJD")]
    [TestCase("https://example.com/image.png", "image/png", "https://example.com/image.png")]
    [TestCase("http://example.com/image.png", "image/png", "http://example.com/image.png")]
    [TestCase("QUJD", null, "QUJD")]
    public void ImageUrl_EncodesRawBase64WithMimeType(string input, string? mimeType, string expected)
    {
        ChatRequest request = new()
        {
            Model = "command-a-vision-07-2025",
            Messages = [new ChatMessage(ChatMessageRoles.User,
                [new ChatMessagePart(new ChatImage(input) { MimeType = mimeType, Detail = ImageDetail.High })])]
        };
        IEndpointProvider provider = new TornadoApi(LLmProviders.Cohere, "test-key").GetProvider(LLmProviders.Cohere);
        JObject body = JObject.Parse(request.Serialize(provider).Body.ToString()!);
        JToken? image = body["messages"]?[0]?["content"]?[0]?["image_url"];
        Assert.That(image?["url"]?.ToString(), Is.EqualTo(expected));
        Assert.That(image?["detail"]?.ToString(), Is.EqualTo("high"));
    }
}
