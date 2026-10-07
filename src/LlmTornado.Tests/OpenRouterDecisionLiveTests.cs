using LlmTornado.Code;
using LlmTornado.Decision;
using LlmTornado.Decision.Models;
using LlmTornado.Demo;
using LlmTornado.Models;

namespace LlmTornado.Tests;

/// <summary>
/// Live integration tests for OpenRouter Decisions. Explicit selection and an OPENROUTER_API_KEY
/// or the "OpenRouter" entry of the Demo apiKey.json are required. Account privacy restrictions apply.
/// </summary>
[TestFixture]
public class OpenRouterDecisionLiveTests
{
    private TornadoApi? _api;

    public static IEnumerable<DecisionModel> DecisionModels
    {
        get
        {
            string? model = Environment.GetEnvironmentVariable("OPENROUTER_DECISION_MODEL");
            return string.IsNullOrWhiteSpace(model)
                ? new[] { DecisionModel.OpenRouter.All.Gpt6LunaDecisions, DecisionModel.OpenRouter.All.Jev113 }
                : new[] { new DecisionModel(model, LLmProviders.OpenRouter) };
        }
    }

    public static IEnumerable<TestCaseData> DecisionCases => DecisionModels
        .Select(model => new TestCaseData(model).SetArgDisplayNames(model.Name));

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        string? envKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
        if (!string.IsNullOrWhiteSpace(envKey))
        {
            _api = new TornadoApi(LLmProviders.OpenRouter, envKey);
            return;
        }

        if (await Program.SetupApi() && !string.IsNullOrWhiteSpace(Program.ApiKeys.OpenRouter))
        {
            _api = new TornadoApi(LLmProviders.OpenRouter, Program.ApiKeys.OpenRouter);
        }
    }

    private TornadoApi Api()
    {
        if (_api is null)
        {
            Assert.Ignore("OpenRouter API key not configured. Set OPENROUTER_API_KEY or provide apiKey.json.");
        }

        return _api!;
    }

    [TestCaseSource(nameof(DecisionCases))]
    [Explicit("Requires OpenRouter API key and makes real production API calls")]
    public async Task CreateDecision_AllQuestionTypes_ReturnsTypedAnswers(DecisionModel model)
    {
        TornadoApi api = Api();
        using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        DecisionRequest request = new DecisionRequest(model,
                "Help! My payouts have been failing for 3 days. Can I please talk to a real person?")
            .AddNoul("is_urgent", "Does this convey urgency?")
            .AddChoice("department", "Which team should handle this?", new Dictionary<string, object?>
            {
                ["billing"] = "Payments, invoicing, refunds",
                ["technical"] = "Bugs, outages, integrations",
                ["sales"] = "Pricing, upgrades, new accounts"
            })
            .AddScore("frustration", "How frustrated is the customer?", "Calm", "Frustrated", "Very angry");

        DecisionResult? result = await api.Decision.CreateDecision(request, timeout.Token);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Model, Is.Not.Null.And.Not.Empty);
        Assert.That(result.Provider?.Provider, Is.EqualTo(LLmProviders.OpenRouter));
        Assert.That(result.Answers.Keys, Is.EquivalentTo(new[] { "is_urgent", "department", "frustration" }));
        Assert.That(result.Usage?.InputTokens, Is.GreaterThan(0));
        Assert.That(result.Usage?.OutputTokens, Is.GreaterThanOrEqualTo(0));
        if (result.Usage?.Cost is { } cost)
        {
            Assert.That(cost, Is.GreaterThanOrEqualTo(0m));
        }

        AssertAnswers(result);
    }

    internal static void AssertAnswers(DecisionResult result)
    {
        DecisionNoulAnswer? urgent = result.GetNoul("is_urgent");
        Assert.That(urgent, Is.Not.Null);
        Assert.That(urgent!.Noul, Is.InRange(0.0, 1.0));

        DecisionChoiceAnswer? department = result.GetChoice("department");
        Assert.That(department, Is.Not.Null);
        Assert.That(department!.Choice, Is.EqualTo("billing"));
        if (department.Probabilities.Count > 0)
        {
            Assert.That(department.Probabilities.Keys, Is.EquivalentTo(new[] { "billing", "technical", "sales" }));
            Assert.That(department.Probabilities.Values, Has.All.InRange(0.0, 1.0));
            Assert.That(department.Probabilities.Values.Sum(), Is.EqualTo(1.0).Within(0.01));
        }
        Assert.That(department.Confidence, Is.InRange(0.0, 1.0));

        DecisionScoreAnswer? frustration = result.GetScore("frustration");
        Assert.That(frustration, Is.Not.Null);
        Assert.That(frustration!.Score, Is.InRange(0.0, 2.0));
        Assert.That(frustration.Confidence, Is.InRange(0.0, 1.0));
        if (frustration.Legend.Count > 0)
        {
            Assert.That(frustration.Legend.Keys, Is.EquivalentTo(new[] { "0", "1", "2" }));
            Assert.That(frustration.Legend["0"], Is.EqualTo("Calm"));
        }
        if (frustration.Probabilities.Count > 0)
        {
            Assert.That(frustration.Probabilities.Keys, Is.EquivalentTo(new[] { "0", "1", "2" }));
            Assert.That(frustration.Probabilities.Values, Has.All.InRange(0.0, 1.0));
            Assert.That(frustration.Probabilities.Values.Sum(), Is.EqualTo(1.0).Within(0.01));
        }
    }

    [Test]
    [Explicit("Requires OpenRouter API key and makes real production API calls")]
    public async Task GetModels_ListsSelectedDecisionModelsAndModalities()
    {
        TornadoApi api = Api();
        using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var result = await api.Models.GetModelsSafe(LLmProviders.OpenRouter, timeout.Token);

        Assert.That(result.Ok, Is.True, result.Response);
        Assert.That(result.Exception, Is.Null);
        Assert.That(result.Data, Is.Not.Null);
        foreach (DecisionModel selected in DecisionModels)
        {
            RetrievedModel? model = result.Data!.SingleOrDefault(x => x.Id == selected.Name);
            Assert.That(model, Is.Not.Null, selected.Name);
            Assert.That(model!.Description, Is.Not.Null.And.Not.Empty, selected.Name);
            Assert.That(model.Architecture?.OutputModalities, Does.Contain("decisions"), selected.Name);
            Assert.That(model.Architecture?.InputModalities, Does.Contain("text"), selected.Name);
        }
    }
}
