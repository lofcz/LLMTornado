using System.Globalization;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Chat.Vendors.XAi;
using LlmTornado.Code;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

[TestFixture]
public class XAiSearchParametersDateTests
{
    private static JObject SerializeSearchParameters(DateTime? from, DateTime? to)
    {
        TornadoApi api = new TornadoApi("test-key");
        ChatRequest request = new ChatRequest
        {
            Model = ChatModel.XAi.Grok3.V3,
            Messages = [new ChatMessage(ChatMessageRoles.User, "Hello")],
            VendorExtensions = new ChatRequestVendorExtensions
            {
                XAi = new ChatRequestVendorXAiExtensions
                {
                    SearchParameters = new ChatRequestVendorXAiExtensionsSearchParameters
                    {
                        FromDate = from,
                        ToDate = to
                    }
                }
            }
        };

        TornadoRequestContent serialized = request.Serialize(api.GetProvider(LLmProviders.XAi));
        return (JObject)JObject.Parse(serialized.Body.ToString()!)["search_parameters"]!;
    }

    [Test]
    public void SearchParameters_SerializeDatesAsIsoDates()
    {
        JObject search = SerializeSearchParameters(new DateTime(2026, 3, 7), new DateTime(2026, 10, 29));

        Assert.That(search["from_date"]?.ToString(), Is.EqualTo("2026-03-07"));
        Assert.That(search["to_date"]?.ToString(), Is.EqualTo("2026-10-29"));
    }

    [Test]
    public void SearchParameters_SerializeDatesIndependentOfCurrentCulture()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;

        try
        {
            // Thai Buddhist calendar would render 2026 as 2569 under a culture-sensitive format.
            CultureInfo.CurrentCulture = new CultureInfo("th-TH");
            JObject search = SerializeSearchParameters(new DateTime(2026, 3, 7), null);

            Assert.That(search["from_date"]?.ToString(), Is.EqualTo("2026-03-07"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
