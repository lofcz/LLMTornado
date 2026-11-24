using LlmTornado.Videos;
using LlmTornado.Videos.Models;
using LlmTornado.Videos.Vendors.OpenAi;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

/// <summary>
/// VideoImage is documented as a public URL or raw base64. The OpenAI JSON path
/// (UseJsonContent / characters) must not silently drop raw base64 images.
/// </summary>
[TestFixture]
public class VideoOpenAiJsonImageTests
{
    [TestCase("QUJD", "image/png", "data:image/png;base64,QUJD")]
    [TestCase("QUJD", null, "data:image/jpeg;base64,QUJD")]
    [TestCase("data:image/png;base64,QUJD", "image/png", "data:image/png;base64,QUJD")]
    [TestCase("https://example.com/a.png", "image/png", "https://example.com/a.png")]
    public void SerializeJson_SendsInputReference(string content, string? mimeType, string expected)
    {
        VideoGenerationRequest request = new("animate")
        {
            Model = VideoModel.OpenAi.Sora.Sora2,
            Image = new VideoImage(content) { MimeType = mimeType },
            OpenAiExtensions = new VideoOpenAiExtensions { UseJsonContent = true }
        };

        JObject body = JObject.Parse(JsonConvert.SerializeObject(VendorOpenAiVideoRequest.SerializeJson(request)));
        Assert.That(body["input_reference"]?["image_url"]?.Value<string>(), Is.EqualTo(expected));
    }
}
