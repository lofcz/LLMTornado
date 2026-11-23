using A2A;
using LlmTornado.Chat;
using LlmTornado.Code;
using System.Text.Json;

namespace LlmTornado.A2A;
/// <summary>
/// ToDo:
/// - Handle more file types (e.g. video, other documents)
/// </summary>
public static partial class A2ATornadoExtension
{
    public static TextPart ToA2ATextPart(this string text) => new TextPart { Text = text };

    public static FilePart ToA2AFileWithUriPart(this Uri uri) => new FilePart
    {
        File = new FileContent(uri)
    };

    public static FilePart ToA2AFilePart(this ChatDocument doc)
    {
        return doc.Uri != null ? new FilePart { File = new FileContent(doc.Uri) } : new FilePart { File = new FileContent(doc.Base64) };
    }

    public static FilePart ToA2AFilePart(this ChatImage img)
    {
        // ChatImage.Url may hold raw base64 (with MimeType); on Unix a JPEG ("/9j/...") even parses as an absolute file Uri, so a file Uri is only accepted when the value really starts with "file:"
        if (Uri.TryCreate(img.Url, UriKind.Absolute, out Uri? uri) && (!uri.IsFile || img.Url.StartsWith("file:", StringComparison.OrdinalIgnoreCase)))
        {
            return new FilePart { File = new FileContent(uri) { MimeType = img.MimeType } };
        }

        return new FilePart { File = new FileContent(img.Url) { MimeType = img.MimeType } };
    }

    public static FilePart ToA2AFilePart(this ChatAudio audio)
    {
        if (audio.Url != null)
            return new FilePart { File = new FileContent(audio.Url) };
        
        return new FilePart { File = new FileContent(audio.Data) { MimeType = audio.MimeType } };
    }

    public static FilePart ToA2AFilePart(this ChatMessageAudio audio)
    {
       return new FilePart { File = new FileContent(audio.Data) { Name = audio.Id, MimeType = audio.MimeType } };
    }

    public static FilePart ToA2AFilePart(this ChatVideo video) => new FilePart { File = new FileContent(video.Url) };

    public static TextPart ToA2ATextPart(this ChatMessageReasoningData reasoning)
    {
        if (reasoning.IsRedacted ?? false)
        {
            JsonElement stringElement = JsonDocument.Parse($"{{\"message\": \"{reasoning.Content}\"}}").RootElement.GetProperty("message");
            return new TextPart { Text = "Reasoning", Metadata = new Dictionary<string, JsonElement> { { "Content", stringElement } } };
        }
        else
        {
            return new TextPart { Text = reasoning.Content ?? "Reasoning" };
        }
    }

    public static FilePart ToA2AFilePart(this ChatMessagePartFileLinkData linkdata)
    {
        return new FilePart
        {
            File = new FileContent(new Uri(linkdata.FileUri))
            {
                Name = linkdata.File?.Name ?? "", 
                MimeType = linkdata.MimeType
            }
        };
    }

    public static TextPart ToA2ATextPart(this ChatMessagePartExecutableCode code)
    {
        return new TextPart
        {
            Text = code.Code ?? "Code",
            Metadata = new Dictionary<string, JsonElement> {
                { "Language", JsonDocument.Parse($"\"{(code.CustomLanguage != null ? code.CustomLanguage : code.Language)}\"").RootElement }
            }
        };
    }
}