using LlmTornado.Videos;
using LlmTornado.Videos.Models;
using LlmTornado.Videos.Vendors.MiniMax;
using LlmTornado.Videos.Vendors.XAi;
using LlmTornado.Videos.Vendors.Zai;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

/// <summary>
/// VideoImage is documented as a public URL or raw base64. MiniMax, Z.AI and xAI
/// video APIs accept inline images only as http(s) URLs or data URIs.
/// </summary>
[TestFixture]
public class VideoImageDataUriTests
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        NullValueHandling = NullValueHandling.Ignore
    };

    [Test]
    public void MiniMaxHailuo_WrapsRawBase64Frames()
    {
        JObject body = MiniMaxV1(VideoModel.MiniMax.Hailuo.Hailuo23, Raw("image/png"), Raw("image/jpeg"));
        Assert.That(body["first_frame_image"]!.Value<string>(), Is.EqualTo("data:image/png;base64,QUJD"));
        Assert.That(body["last_frame_image"]!.Value<string>(), Is.EqualTo("data:image/jpeg;base64,QUJD"));
    }

    [Test]
    public void MiniMaxH3_WrapsRawBase64Frames()
    {
        JObject body = MiniMaxV2(VideoModel.MiniMax.H3.H3, Raw("image/png"), Raw("image/webp"));
        Assert.That(FrameUrl(body, "first_frame"), Is.EqualTo("data:image/png;base64,QUJD"));
        Assert.That(FrameUrl(body, "last_frame"), Is.EqualTo("data:image/webp;base64,QUJD"));
    }

    [Test]
    public void MiniMaxH3_WrapsRawBase64ReferenceImage()
    {
        VideoGenerationRequest request = new("animate")
        {
            Model = VideoModel.MiniMax.H3.H3,
            ReferenceImages = [new VideoReferenceImage(Raw("image/png"))]
        };
        JObject body = Json(VendorMiniMaxVideoGenerationV2Request.FromRequest(request));
        Assert.That(FrameUrl(body, "reference_image"), Is.EqualTo("data:image/png;base64,QUJD"));
    }

    [Test]
    public void ZaiCogVideoX_WrapsRawBase64Image()
    {
        JObject body = Zai(VideoModel.Zai.CogVideoX.V3, Raw("image/png"), null);
        Assert.That(body["image_url"]!.Value<string>(), Is.EqualTo("data:image/png;base64,QUJD"));
    }

    [Test]
    public void ZaiCogVideoX_WrapsRawBase64FirstAndLastFrame()
    {
        JObject body = Zai(VideoModel.Zai.CogVideoX.V3, Raw("image/png"), Raw("image/jpeg"));
        Assert.That(body["image_url"]!.Values<string>(), Is.EqualTo(new[] { "data:image/png;base64,QUJD", "data:image/jpeg;base64,QUJD" }));
    }

    [Test]
    public void ZaiVidu_WrapsRawBase64StartAndEnd()
    {
        JObject body = Zai(VideoModel.Zai.Vidu.Q1StartEnd, Raw("image/png"), Raw("image/jpeg"));
        Assert.That(body["image_url"]!.Values<string>(), Is.EqualTo(new[] { "data:image/png;base64,QUJD", "data:image/jpeg;base64,QUJD" }));
    }

    [Test]
    public void ZaiVidu_WrapsRawBase64ReferenceImages()
    {
        VideoGenerationRequest request = new("animate")
        {
            Model = VideoModel.Zai.Vidu.V2Reference,
            ReferenceImages =
            [
                new VideoReferenceImage(Raw("image/png")),
                new VideoReferenceImage(Raw("image/jpeg"))
            ]
        };
        JObject body = Json(VendorZaiVideoGenerationRequest.FromRequest(request));
        Assert.That(body["image_url"]!.Values<string>(), Is.EqualTo(new[] { "data:image/png;base64,QUJD", "data:image/jpeg;base64,QUJD" }));
    }

    [Test]
    public void XAi_WrapsRawBase64Image()
    {
        JObject body = XAi(Raw("image/jpeg"), null);
        Assert.That(body["image"]!["url"]!.Value<string>(), Is.EqualTo("data:image/jpeg;base64,QUJD"));
    }

    [Test]
    public void XAi_WrapsRawBase64ReferenceImages()
    {
        JObject body = XAi(null, [Raw("image/png"), Raw("image/webp")]);
        JArray urls = (JArray)body["reference_images"]!;
        Assert.That(urls[0]!["url"]!.Value<string>(), Is.EqualTo("data:image/png;base64,QUJD"));
        Assert.That(urls[1]!["url"]!.Value<string>(), Is.EqualTo("data:image/webp;base64,QUJD"));
    }

    [TestCase("https://example.com/a.png", "image/png", "https://example.com/a.png")]
    [TestCase("http://example.com/a.png", "image/png", "http://example.com/a.png")]
    [TestCase("data:image/png;base64,QUJD", "image/jpeg", "data:image/png;base64,QUJD")]
    [TestCase("QUJD", null, "QUJD")]
    public void MiniMaxHailuo_LeavesOtherInputsUntouched(string content, string? mimeType, string expected)
    {
        JObject body = MiniMaxV1(VideoModel.MiniMax.Hailuo.Hailuo23, new VideoImage(content) { MimeType = mimeType }, null);
        Assert.That(body["first_frame_image"]!.Value<string>(), Is.EqualTo(expected));
    }

    [TestCase("https://example.com/a.png", "image/png", "https://example.com/a.png")]
    [TestCase("data:image/png;base64,QUJD", "image/png", "data:image/png;base64,QUJD")]
    [TestCase("QUJD", null, "QUJD")]
    public void Zai_LeavesOtherInputsUntouched(string content, string? mimeType, string expected)
    {
        JObject body = Zai(VideoModel.Zai.CogVideoX.V3, new VideoImage(content) { MimeType = mimeType }, null);
        Assert.That(body["image_url"]!.Value<string>(), Is.EqualTo(expected));
    }

    [TestCase("https://example.com/a.png", "image/png", "https://example.com/a.png")]
    [TestCase("data:image/jpeg;base64,QUJD", "image/png", "data:image/jpeg;base64,QUJD")]
    [TestCase("QUJD", null, "QUJD")]
    public void XAi_LeavesOtherInputsUntouched(string content, string? mimeType, string expected)
    {
        JObject body = XAi(new VideoImage(content) { MimeType = mimeType }, null);
        Assert.That(body["image"]!["url"]!.Value<string>(), Is.EqualTo(expected));
    }

    private static VideoImage Raw(string mimeType) => new("QUJD", mimeType);

    private static JObject MiniMaxV1(VideoModel model, VideoImage? image, VideoImage? lastFrame)
    {
        return Json(VendorMiniMaxVideoGenerationRequest.FromRequest(Request(model, image, lastFrame)));
    }

    private static JObject MiniMaxV2(VideoModel model, VideoImage? image, VideoImage? lastFrame)
    {
        return Json(VendorMiniMaxVideoGenerationV2Request.FromRequest(Request(model, image, lastFrame)));
    }

    private static JObject Zai(VideoModel model, VideoImage? image, VideoImage? lastFrame)
    {
        return Json(VendorZaiVideoGenerationRequest.FromRequest(Request(model, image, lastFrame)));
    }

    private static JObject XAi(VideoImage? image, List<VideoImage>? references)
    {
        VideoGenerationRequest request = Request(VideoModel.XAi.Grok.ImagineVideo15, image, null);
        if (references is not null)
        {
            request.ReferenceImages = references.Select(x => new VideoReferenceImage(x)).ToList();
        }

        return Json(VendorXAiVideoGenerationRequest.FromRequest(request));
    }

    private static VideoGenerationRequest Request(VideoModel model, VideoImage? image, VideoImage? lastFrame)
    {
        return new VideoGenerationRequest("animate")
        {
            Model = model,
            Image = image,
            LastFrame = lastFrame
        };
    }

    private static JObject Json(object request)
    {
        return JObject.Parse(JsonConvert.SerializeObject(request, Settings));
    }

    private static string FrameUrl(JObject body, string role)
    {
        return body["content"]!
            .First(item => item["role"]?.Value<string>() == role)["image_url"]!["url"]!
            .Value<string>()!;
    }
}
