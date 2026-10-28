using A2A;
using LlmTornado.A2A;
using LlmTornado.Chat;

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
}
