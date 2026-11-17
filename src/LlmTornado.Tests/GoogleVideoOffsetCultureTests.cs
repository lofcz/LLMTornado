using System.Globalization;
using LlmTornado.Chat.Vendors.Google;
using Newtonsoft.Json;

namespace LlmTornado.Tests;

[TestFixture]
public class GoogleVideoOffsetCultureTests
{
    [Test]
    public void VideoMetadata_OffsetsRoundTripIndependentOfCurrentCulture()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            VendorGoogleChatRequest.VendorGoogleChatRequestMetadataVideo metadata = new VendorGoogleChatRequest.VendorGoogleChatRequestMetadataVideo
            {
                StartOffset = TimeSpan.FromSeconds(1.5),
                EndOffset = TimeSpan.FromSeconds(3.25)
            };

            string json = JsonConvert.SerializeObject(metadata);
            Assert.That(json, Does.Contain("\"startOffset\":\"1.5s\""));
            Assert.That(json, Does.Contain("\"endOffset\":\"3.25s\""));

            VendorGoogleChatRequest.VendorGoogleChatRequestMetadataVideo? parsed = JsonConvert.DeserializeObject<VendorGoogleChatRequest.VendorGoogleChatRequestMetadataVideo>("{\"startOffset\":\"1.5s\",\"endOffset\":\"3.25s\"}");
            Assert.That(parsed?.StartOffset, Is.EqualTo(TimeSpan.FromSeconds(1.5)));
            Assert.That(parsed?.EndOffset, Is.EqualTo(TimeSpan.FromSeconds(3.25)));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
