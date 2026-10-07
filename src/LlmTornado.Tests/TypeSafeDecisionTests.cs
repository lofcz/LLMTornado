using LlmTornado.Decision;
using LlmTornado.Decision.Models;
using LlmTornado.Code;
using LlmTornado.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

/// <summary>
/// Unit tests for the TypeSafe provider and the decision (System One) endpoint.
/// </summary>
[TestFixture]
public class TypeSafeDecisionTests
{
    private static IEndpointProvider Provider()
    {
        return new TornadoApi([new ProviderAuthentication(LLmProviders.TypeSafe, "ts-test")]).GetProvider(LLmProviders.TypeSafe);
    }

    [Test]
    public void TypeSafe_ResolvesProvider()
    {
        Assert.That(Provider().Provider, Is.EqualTo(LLmProviders.TypeSafe));
    }

    [Test]
    public void TypeSafe_DecisionUrl_PointsToSystemOne()
    {
        string url = Provider().ApiUrl(CapabilityEndpoints.Decision, null, DecisionModel.TypeSafe.Jev.Latest);
        Assert.That(url, Is.EqualTo("https://api.typesafe.ai/v1/systemone"));
    }

    [Test]
    public void TypeSafe_HonorsApiUrlFormatOverride()
    {
        TornadoApi api = new TornadoApi([new ProviderAuthentication(LLmProviders.TypeSafe, "ts-test")])
        {
            ApiUrlFormat = "https://gateway.internal/{0}/{1}"
        };

        string url = api.GetProvider(LLmProviders.TypeSafe).ApiUrl(CapabilityEndpoints.Decision, null, DecisionModel.TypeSafe.Jev.Latest);
        Assert.That(url, Is.EqualTo("https://gateway.internal/v1/systemone"));
    }

    [Test]
    public void TypeSafe_OutboundMessage_UsesBearerOnly()
    {
        HttpRequestMessage req = Provider().OutboundMessage("https://api.typesafe.ai/v1/systemone", HttpMethod.Post, null, false, null);

        Assert.That(req.Headers.Authorization?.Scheme, Is.EqualTo("Bearer"));
        Assert.That(req.Headers.Authorization?.Parameter, Is.EqualTo("ts-test"));
        Assert.That(req.Headers.Contains("api-key"), Is.False);
    }

    [Test]
    public void JevModels_AreDiscoverable()
    {
        Assert.That(DecisionModel.TypeSafe.Jev.Latest.Name, Is.EqualTo("jev-latest"));
        Assert.That(DecisionModel.TypeSafe.Jev.Preview.Name, Is.EqualTo("jev-preview"));
        Assert.That(DecisionModel.TypeSafe.Jev.V1_13.Name, Is.EqualTo("jev-1.13.0"));
        Assert.That(DecisionModel.TypeSafe.OwnsModel("jev-latest"), Is.True);
        Assert.That(DecisionModel.TypeSafe.Jev.Latest.Provider, Is.EqualTo(LLmProviders.TypeSafe));
        Assert.That(DecisionModel.TypeSafe.AllModels, Has.Count.EqualTo(3));
    }

    [Test]
    public void StringModel_ResolvesToTypeSafe()
    {
        DecisionModel known = "jev-1.13.0";
        DecisionModel unknown = "jev-2.0.0";

        Assert.That(known.Provider, Is.EqualTo(LLmProviders.TypeSafe));
        Assert.That(unknown.Provider, Is.EqualTo(LLmProviders.TypeSafe));
        Assert.That(unknown.Name, Is.EqualTo("jev-2.0.0"));
    }

    [Test]
    public void Request_SerializesAllQuestionTypes()
    {
        DecisionRequest request = new DecisionRequest(DecisionModel.TypeSafe.Jev.Latest, "Help! My payouts have been failing for 3 days.")
            .AddNoul("is_urgent", "Does this convey urgency?", "Explicitly time-sensitive", "No urgency expressed")
            .AddNoul("wants_refund", "Is a refund requested?")
            .AddChoice("department", "Which team should handle this?", new Dictionary<string, object?>
            {
                ["billing"] = "Payments, invoicing, refunds",
                ["other"] = null
            })
            .AddScore("frustration", "How frustrated is the customer?", "Calm", "Frustrated", "Very angry");

        TornadoRequestContent serialized = request.Serialize(Provider());
        JObject body = JObject.Parse(serialized.Body.ToString()!);

        Assert.That(serialized.Url, Is.EqualTo("https://api.typesafe.ai/v1/systemone"));
        Assert.That(body["model"]?.ToString(), Is.EqualTo("jev-latest"));
        Assert.That(body["state"]?.ToString(), Is.EqualTo("Help! My payouts have been failing for 3 days."));

        JObject questions = (JObject)body["questions"]!;
        Assert.That(questions.Properties().Select(p => p.Name), Is.EquivalentTo(new[] { "is_urgent", "wants_refund", "department", "frustration" }));

        Assert.That(questions["is_urgent"]!["type"]?.ToString(), Is.EqualTo("noul"));
        Assert.That(questions["is_urgent"]!["instructions"]?.ToString(), Is.EqualTo("Does this convey urgency?"));
        Assert.That(questions["is_urgent"]!["criteria"]!["true"]?.ToString(), Is.EqualTo("Explicitly time-sensitive"));
        Assert.That(questions["is_urgent"]!["criteria"]!["false"]?.ToString(), Is.EqualTo("No urgency expressed"));

        // optional criteria omitted when not provided
        Assert.That(questions["wants_refund"]!["type"]?.ToString(), Is.EqualTo("noul"));
        Assert.That(((JObject)questions["wants_refund"]!).ContainsKey("criteria"), Is.False);

        Assert.That(questions["department"]!["type"]?.ToString(), Is.EqualTo("choice"));
        Assert.That(questions["department"]!["criteria"]!["billing"]?.ToString(), Is.EqualTo("Payments, invoicing, refunds"));
        // null option descriptions are meaningful and must survive serialization
        Assert.That(((JObject)questions["department"]!["criteria"]!).ContainsKey("other"), Is.True);
        Assert.That(questions["department"]!["criteria"]!["other"]!.Type, Is.EqualTo(JTokenType.Null));

        Assert.That(questions["frustration"]!["type"]?.ToString(), Is.EqualTo("score"));
        Assert.That(questions["frustration"]!["criteria"]!.Select(x => x.ToString()), Is.EqualTo(new[] { "Calm", "Frustrated", "Very angry" }));
    }

    [Test]
    public void Request_SerializesStructuredStateAndInstructions()
    {
        DecisionRequest request = new DecisionRequest(DecisionModel.TypeSafe.Jev.Latest, new { source_text = "Invoice #4471" })
            .AddNoul("matches", new { extracted_value = "4471", question = "Does `extracted_value` appear in `source_text`?" });

        JObject body = JObject.Parse(request.Serialize(Provider()).Body.ToString()!);

        Assert.That(body["state"]!["source_text"]?.ToString(), Is.EqualTo("Invoice #4471"));
        Assert.That(body["questions"]!["matches"]!["instructions"]!["extracted_value"]?.ToString(), Is.EqualTo("4471"));
    }

    [Test]
    public void TypeSafe_ModelsUrl_PointsToModels()
    {
        string url = Provider().ApiUrl(CapabilityEndpoints.Models, null);
        Assert.That(url, Is.EqualTo("https://api.typesafe.ai/v1/models"));
    }

    [Test]
    public void TypeSafe_ModelList_DeserializesModelsEnvelope()
    {
        const string json = """
        {
          "models": [
            { "name": "jev-latest", "description": "Most recent stable release", "release_date": "2026-06-01" },
            { "name": "jev-preview", "description": "Most recent release including previews", "release_date": "2026-06-01" }
          ]
        }
        """;

        List<RetrievedModel>? models = Provider().InboundMessage<LlmTornado.Models.Vendors.RetrievedModelsResult>(json, null, null)?.Data;

        Assert.That(models, Is.Not.Null);
        Assert.That(models!.Select(x => x.Id), Is.EqualTo(new[] { "jev-latest", "jev-preview" }));
        Assert.That(models[0].Name, Is.EqualTo("jev-latest"));
        Assert.That(models[0].Description, Is.EqualTo("Most recent stable release"));
    }

    [Test]
    public void Result_DeserializesTypedAnswers()
    {
        const string json = """
        {
          "model": "jev-1.13.0",
          "answers": {
            "is_urgent": { "type": "noul", "noul": 0.95 },
            "department": {
              "type": "choice",
              "choice": "billing",
              "probabilities": { "billing": 0.88, "technical": 0.12, "sales": 0.0 },
              "confidence": 0.81
            },
            "frustration": {
              "type": "score",
              "score": 1.05,
              "legend": { "0": "Calm", "1": "Frustrated", "2": "Very angry" },
              "probabilities": { "0": 0.0, "1": 0.95, "2": 0.05 },
              "confidence": 0.92
            },
            "future": { "type": "vector", "values": [1, 2] }
          },
          "usage": { "input_tokens": 304, "output_tokens": 18 }
        }
        """;

        DecisionResult? result = JsonConvert.DeserializeObject<DecisionResult>(json);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Model, Is.EqualTo("jev-1.13.0"));
        Assert.That(result.Usage?.InputTokens, Is.EqualTo(304));
        Assert.That(result.Usage?.OutputTokens, Is.EqualTo(18));
        Assert.That(result.Answers, Has.Count.EqualTo(4));

        DecisionNoulAnswer? urgent = result.GetNoul("is_urgent");
        Assert.That(urgent, Is.Not.Null);
        Assert.That(urgent!.Type, Is.EqualTo(DecisionQuestionTypes.Noul));
        Assert.That(urgent.Noul, Is.EqualTo(0.95).Within(1e-9));

        DecisionChoiceAnswer? department = result.GetChoice("department");
        Assert.That(department, Is.Not.Null);
        Assert.That(department!.Choice, Is.EqualTo("billing"));
        Assert.That(department.Confidence, Is.EqualTo(0.81).Within(1e-9));
        Assert.That(department.Probabilities["technical"], Is.EqualTo(0.12).Within(1e-9));

        DecisionScoreAnswer? frustration = result.GetScore("frustration");
        Assert.That(frustration, Is.Not.Null);
        Assert.That(frustration!.Score, Is.EqualTo(1.05).Within(1e-9));
        Assert.That(frustration.Legend["2"], Is.EqualTo("Very angry"));
        Assert.That(frustration.Probabilities["1"], Is.EqualTo(0.95).Within(1e-9));

        // typed accessors return null on type mismatch instead of throwing
        Assert.That(result.GetScore("department"), Is.Null);
        Assert.That(result.GetNoul("missing"), Is.Null);

        // unknown answer types are preserved rather than failing the whole response
        Assert.That(result.Answers["future"], Is.TypeOf<DecisionUnknownAnswer>());
        Assert.That(((DecisionUnknownAnswer)result.Answers["future"]).Raw?["values"]?.Count(), Is.EqualTo(2));
    }
}
