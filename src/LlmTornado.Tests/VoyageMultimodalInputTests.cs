using LlmTornado.Chat;
using LlmTornado.Code;
using LlmTornado.Embedding;
using LlmTornado.Embedding.Models;
using LlmTornado.Embedding.Vendors.Voyage;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

[TestFixture]
public class VoyageMultimodalInputTests
{
    private static JObject Serialize(ChatImage image)
    {
        TornadoApi api = new TornadoApi(LLmProviders.Voyage, "test-key");
        IEndpointProvider provider = api.GetProvider(LLmProviders.Voyage);

        EmbeddingRequest request = new EmbeddingRequest
        {
            Model = EmbeddingModel.Voyage.Gen4.Large,
            MultimodalInput = [[new ChatMessagePart(image)]]
        };

        VendorVoyageEmbeddingRequest vendor = new VendorVoyageEmbeddingRequest(request, provider);
        return JObject.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(vendor));
    }

    private static JToken Content(JObject body) => body["inputs"]![0]!["content"]![0]!;

    [Test]
    public void ImageUrl_IsSentAsImageUrl()
    {
        JToken content = Content(Serialize(new ChatImage("https://example.com/cat.png")));

        Assert.That(content["type"]!.ToString(), Is.EqualTo("image_url"));
        Assert.That(content["image_url"]!.ToString(), Is.EqualTo("https://example.com/cat.png"));
    }

    [Test]
    public void ImageDataUri_IsSentAsImageBase64()
    {
        JToken content = Content(Serialize(new ChatImage("data:image/png;base64,AAAA")));

        Assert.That(content["type"]!.ToString(), Is.EqualTo("image_base64"));
        Assert.That(content["image_base64"]!.ToString(), Is.EqualTo("data:image/png;base64,AAAA"));
    }

    [Test]
    public void ImageRawBase64_IsSentAsImageBase64WithDataUriPrefix()
    {
        JToken content = Content(Serialize(new ChatImage("AAAA", "image/png")));

        Assert.That(content["type"]!.ToString(), Is.EqualTo("image_base64"));
        Assert.That(content["image_base64"]!.ToString(), Is.EqualTo("data:image/png;base64,AAAA"));
        Assert.That(content["image_url"], Is.Null);
    }

    [Test]
    public void ImageRawBase64_WithoutMimeType_DefaultsToJpeg()
    {
        JToken content = Content(Serialize(new ChatImage("AAAA")));

        Assert.That(content["type"]!.ToString(), Is.EqualTo("image_base64"));
        Assert.That(content["image_base64"]!.ToString(), Is.EqualTo("data:image/jpeg;base64,AAAA"));
    }
}
