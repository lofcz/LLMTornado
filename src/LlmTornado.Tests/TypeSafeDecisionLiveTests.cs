using LlmTornado.Code;
using LlmTornado.Decision;
using LlmTornado.Decision.Models;
using LlmTornado.Demo;
using LlmTornado.Models;

namespace LlmTornado.Tests;

/// <summary>
/// Live integration tests for the TypeSafe decision endpoint. Skipped unless a key is configured via
/// the TYPESAFE_API_KEY environment variable or the "TypeSafe" entry of the Demo apiKey.json.
/// </summary>
[TestFixture]
public class TypeSafeDecisionLiveTests
{
    private TornadoApi? _api;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        string? envKey = Environment.GetEnvironmentVariable("TYPESAFE_API_KEY");
        if (!string.IsNullOrWhiteSpace(envKey))
        {
            _api = new TornadoApi(LLmProviders.TypeSafe, envKey);
            return;
        }

        if (await Program.SetupApi() && !string.IsNullOrWhiteSpace(Program.ApiKeys.TypeSafe))
        {
            _api = new TornadoApi(LLmProviders.TypeSafe, Program.ApiKeys.TypeSafe);
        }
    }

    private TornadoApi Api()
    {
        if (_api is null)
        {
            Assert.Ignore("TypeSafe API key not configured. Set TYPESAFE_API_KEY or provide apiKey.json.");
        }

        return _api!;
    }

    [Test]
    [Explicit("Requires TypeSafe API key and makes real production API calls")]
    public async Task CreateDecision_AllQuestionTypes_ReturnsTypedAnswers()
    {
        DecisionRequest request = new DecisionRequest(DecisionModel.TypeSafe.Jev.Latest,
                "Help! My payouts have been failing for 3 days. Can I please talk to a real person?")
            .AddNoul("is_urgent", "Does this convey urgency?")
            .AddChoice("department", "Which team should handle this?", new Dictionary<string, object?>
            {
                ["billing"] = "Payments, invoicing, refunds",
                ["technical"] = "Bugs, outages, integrations",
                ["sales"] = "Pricing, upgrades, new accounts"
            })
            .AddScore("frustration", "How frustrated is the customer?", "Calm", "Frustrated", "Very angry");

        DecisionResult? result = await Api().Decision.CreateDecision(request);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Model, Does.StartWith("jev-"));
        Assert.That(result.Answers, Has.Count.EqualTo(3));
        Assert.That(result.Usage?.InputTokens, Is.GreaterThan(0));

        DecisionNoulAnswer? urgent = result.GetNoul("is_urgent");
        Assert.That(urgent, Is.Not.Null);
        Assert.That(urgent!.Noul, Is.InRange(0.0, 1.0));

        DecisionChoiceAnswer? department = result.GetChoice("department");
        Assert.That(department, Is.Not.Null);
        Assert.That(department!.Choice, Is.EqualTo("billing"));
        Assert.That(department.Probabilities.Keys, Is.EquivalentTo(new[] { "billing", "technical", "sales" }));
        Assert.That(department.Probabilities.Values.Sum(), Is.EqualTo(1.0).Within(0.01));
        Assert.That(department.Confidence, Is.InRange(0.0, 1.0));

        DecisionScoreAnswer? frustration = result.GetScore("frustration");
        Assert.That(frustration, Is.Not.Null);
        Assert.That(frustration!.Score, Is.InRange(0.0, 2.0));
        Assert.That(frustration.Legend, Has.Count.EqualTo(3));
        Assert.That(frustration.Legend["0"], Is.EqualTo("Calm"));
    }

    [Test]
    [Explicit("Requires TypeSafe API key and makes real production API calls")]
    public async Task GetModels_ListsJevAliases()
    {
        List<RetrievedModel>? models = await Api().Models.GetModels(LLmProviders.TypeSafe);

        Assert.That(models, Is.Not.Null);
        Assert.That(models!.Select(x => x.Id), Does.Contain("jev-latest"));
        Assert.That(models.First(x => x.Id == "jev-latest").Description, Is.Not.Empty);
    }
}
