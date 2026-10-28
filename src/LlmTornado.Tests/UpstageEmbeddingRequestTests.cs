using LlmTornado.Code;
using LlmTornado.Embedding;
using LlmTornado.Embedding.Models;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

[TestFixture]
public class UpstageEmbeddingRequestTests
{
    [Test]
    public void Serialize_Upstage_ProducesOpenAiCompatibleBody()
    {
        EmbeddingRequest request = new EmbeddingRequest(LlmTornado.Embedding.Models.Upstage.EmbeddingModelUpstage.ModelEmbeddingQuery, "hello");

        TornadoRequestContent serialized = request.Serialize(new TornadoApi(LLmProviders.Upstage, "test-key").GetProvider(LLmProviders.Upstage));
        JObject body = JObject.Parse(serialized.Body.ToString()!);

        Assert.That(body["model"]?.Value<string>(), Is.EqualTo("embedding-query"));
        Assert.That(body["input"]?.Value<string>(), Is.EqualTo("hello"));
        Assert.That(body.ContainsKey("dimensions"), Is.False);
    }
}
