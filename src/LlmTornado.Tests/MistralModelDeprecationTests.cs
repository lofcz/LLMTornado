using System.Reflection;
using LlmTornado.Chat.Models;
using LlmTornado.Chat.Models.Mistral;

namespace LlmTornado.Tests;

/// <summary>
/// Mistral model ids that Mistral has retired are marked obsolete, with the replacement named.
/// </summary>
[TestFixture]
public class MistralModelDeprecationTests
{
    private static readonly string[] RetiredIds =
    [
        "mistral-medium-2505", "mistral-medium-2508", "mistral-small-2506", "devstral-small-2505", "devstral-small-2507",
        "devstral-small-2512", "devstral-medium-2507", "devstral-2512", "magistral-small-2506", "magistral-small-2507",
        "magistral-small-2509", "magistral-medium-2506", "magistral-medium-2507", "magistral-medium-2509", "open-mistral-nemo",
        "mistral-large-2411", "pixtral-large-2411", "mistral-saba-2502", "ministral-3b-2410", "ministral-8b-2410", "codestral-2501",
        "open-codestral-mamba", "mistral-small-2503", "pixtral-12b-2409", "labs-mistral-small-creative", "voxtral-mini-2507"
    ];

    private static IEnumerable<FieldInfo> StaticModelFields() =>
        new[] { typeof(ChatModelMistralPremier), typeof(ChatModelMistralFree), typeof(ChatModelMistralResearch) }
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(f => f.FieldType == typeof(ChatModel));

    [TestCaseSource(nameof(RetiredIds))]
    public void RetiredModel_IsMarkedObsoleteWithReplacement(string id)
    {
        FieldInfo field = StaticModelFields().Single(f => ((ChatModel)f.GetValue(null)!).Name == id);
        ObsoleteAttribute? attr = field.GetCustomAttribute<ObsoleteAttribute>();

        Assert.That(attr, Is.Not.Null);
        Assert.That(attr!.Message, Does.Contain("Retired").And.Contain("on the Mistral API. Use ").And.Contain(".Mistral."));
    }

    [Test]
    public void CurrentModels_AreNotObsolete()
    {
        Assert.That(typeof(ChatModelMistralPremier).GetField(nameof(ChatModelMistralPremier.ModelMistralMedium35))!.GetCustomAttribute<ObsoleteAttribute>(), Is.Null);
        Assert.That(typeof(ChatModelMistralFree).GetField(nameof(ChatModelMistralFree.ModelMistralSmall2603))!.GetCustomAttribute<ObsoleteAttribute>(), Is.Null);
    }
}
