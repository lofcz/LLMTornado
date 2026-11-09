using LlmTornado.Images;
using LlmTornado.Chat;
using LlmTornado.Code;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

[TestFixture]
public class OpenAiCompatibleImageDataUriTests
{
    [TestCase(LLmProviders.OpenAi, "gpt-4o")]
    [TestCase(LLmProviders.Mistral, "pixtral-large-latest")]
    [TestCase(LLmProviders.Groq, "llama-3.2-11b-vision-preview")]
    public void ImageUrl_WrapsRawBase64InDataUri(LLmProviders providerType, string model)
    {
        string url = Serialize(providerType, model, new ChatImage("QUJD", "image/png"));
        Assert.That(url, Is.EqualTo("data:image/png;base64,QUJD"));
    }

    [Test]
    public void ImageUrl_ZaiKeepsRawBase64()
    {
        string url = Serialize(LLmProviders.Zai, "glm-4.5v", new ChatImage("QUJD", "image/png"));
        Assert.That(url, Is.EqualTo("QUJD"));
    }

    [TestCase("data:image/png;base64,QUJD", "image/png", "data:image/png;base64,QUJD")]
    [TestCase("https://example.com/image.png", "image/png", "https://example.com/image.png")]
    [TestCase("QUJD", null, "QUJD")]
    public void ImageUrl_LeavesOtherInputsUntouched(string input, string? mimeType, string expected)
    {
        string url = Serialize(LLmProviders.OpenAi, "gpt-4o", new ChatImage(input) { MimeType = mimeType });
        Assert.That(url, Is.EqualTo(expected));
    }

    private static string Serialize(LLmProviders providerType, string model, ChatImage image)
    {
        ChatRequest request = new()
        {
            Model = model,
            Messages = [new ChatMessage(ChatMessageRoles.User, [new ChatMessagePart(image)])]
        };
        IEndpointProvider provider = new TornadoApi(providerType, "test-key").GetProvider(providerType);
        JObject body = JObject.Parse(request.Serialize(provider).Body.ToString()!);
        return body["messages"]![0]!["content"]![0]!["image_url"]!["url"]!.ToString();
    }
}
