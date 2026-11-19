using LlmTornado.Code;
using LlmTornado.Chat;
using LlmTornado.Responses;

namespace LlmTornado.Tests.Responses;

[TestFixture]
public class ResponsesImageInputTests
{
    private static string GetImageUrl(ChatImage image)
    {
        ChatMessage message = new ChatMessage(ChatMessageRoles.User, [new ChatMessagePart(image)]);
        List<ResponseInputItem> items = ResponseHelpers.ToResponseInputItems([message]);

        ResponseInputMessage input = (ResponseInputMessage)items.Single();
        ResponseInputContentImage content = (ResponseInputContentImage)input.Content.Single();
        return content.ImageUrl!;
    }

    [Test]
    public void RawBase64WithMimeType_IsSentAsDataUri()
    {
        Assert.That(GetImageUrl(new ChatImage("iVBORw0KGgo=", "image/png")), Is.EqualTo("data:image/png;base64,iVBORw0KGgo="));
    }

    [Test]
    public void HttpsUrlAndDataUri_AreNotModified()
    {
        Assert.That(GetImageUrl(new ChatImage("https://example.com/a.png", "image/png")), Is.EqualTo("https://example.com/a.png"));
        Assert.That(GetImageUrl(new ChatImage("data:image/png;base64,AAAA", "image/png")), Is.EqualTo("data:image/png;base64,AAAA"));
    }
}
