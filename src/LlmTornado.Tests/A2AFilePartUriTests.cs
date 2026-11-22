using A2A;
using LlmTornado.A2A;
using LlmTornado.Chat;
using LlmTornado.Code;

namespace LlmTornado.Tests;

[TestFixture]
public class A2AFilePartUriTests
{
    private static ChatMessagePart Convert(string url, string? mimeType)
    {
        FilePart part = new() { File = new FileContent(new Uri(url)) { MimeType = mimeType } };
        return part.ToTornadoMessagePart()!;
    }

    [Test]
    public void UriWithImageMimeType_IsImage()
    {
        ChatMessagePart part = Convert("https://example.com/a.png", "image/png");

        Assert.That(part.Type, Is.EqualTo(ChatMessageTypes.Image));
    }

    [Test]
    public void UriWithoutMimeType_StaysImage()
    {
        ChatMessagePart part = Convert("https://example.com/a", null);

        Assert.That(part.Type, Is.EqualTo(ChatMessageTypes.Image));
    }

    [Test]
    public void UriWithAudioMimeType_IsAudio()
    {
        ChatMessagePart part = Convert("https://example.com/a.mp3", "audio/mpeg");

        Assert.That(part.Type, Is.EqualTo(ChatMessageTypes.Audio));
        Assert.That(part.Audio, Is.Not.Null);
    }

    [Test]
    public void UriWithVideoMimeType_IsVideo()
    {
        ChatMessagePart part = Convert("https://example.com/a.mp4", "video/mp4");

        Assert.That(part.Type, Is.EqualTo(ChatMessageTypes.Video));
        Assert.That(part.Video, Is.Not.Null);
    }

    [Test]
    public void UriWithPdfMimeType_IsFileLinkNotImage()
    {
        ChatMessagePart part = Convert("https://example.com/a.pdf", "application/pdf");

        Assert.That(part.Type, Is.EqualTo(ChatMessageTypes.FileLink));
        Assert.That(part.Image, Is.Null);
        Assert.That(part.FileLinkData!.FileUri, Is.EqualTo("https://example.com/a.pdf"));
        Assert.That(part.FileLinkData.MimeType, Is.EqualTo("application/pdf"));
    }

    [Test]
    public void ChatImageWithRawBase64_DoesNotThrow_AndKeepsBytes()
    {
        ChatImage img = new("iVBORw0KGgo=") { MimeType = "image/png" };

        FilePart part = img.ToA2AFilePart();

        Assert.That(part.File, Is.InstanceOf<FileContent>());
        Assert.That(part.File.Bytes, Is.EqualTo("iVBORw0KGgo="));
        Assert.That(part.File.MimeType, Is.EqualTo("image/png"));
    }

    [Test]
    public void ChatImageWithJpegBase64StartingWithSlash_KeepsBytes()
    {
        FilePart part = new ChatImage("/9j/4AAQSkZJRg==") { MimeType = "image/jpeg" }.ToA2AFilePart();

        Assert.That(part.File.Uri, Is.Null);
        Assert.That(part.File.Bytes, Is.EqualTo("/9j/4AAQSkZJRg=="));
        Assert.That(part.File.MimeType, Is.EqualTo("image/jpeg"));
    }

    [Test]
    public void ChatImageWithHttpUrl_KeepsUri()
    {
        FilePart part = new ChatImage("https://example.com/a.png").ToA2AFilePart();

        Assert.That(part.File.Uri?.ToString(), Is.EqualTo("https://example.com/a.png"));
    }

    [Test]
    public void ChatImageWithDataUri_KeepsUri_AndRoundTripsToImage()
    {
        const string dataUri = "data:image/jpeg;base64,/9j/4AAQSkZJRg==";
        FilePart part = new ChatImage(dataUri) { MimeType = "image/jpeg" }.ToA2AFilePart();

        Assert.That(part.File.Uri, Is.Not.Null);
        Assert.That(part.File.Bytes, Is.Null);
        Assert.That(part.ToTornadoMessagePart()!.Type, Is.EqualTo(ChatMessageTypes.Image));
    }

    [Test]
    public void ChatImageWithGcsUri_KeepsUri()
    {
        FilePart part = new ChatImage("gs://bucket/img.png").ToA2AFilePart();

        Assert.That(part.File.Bytes, Is.Null);
        Assert.That(part.File.Uri?.ToString(), Is.EqualTo("gs://bucket/img.png"));
    }
}
