using System.Reflection;
using LlmTornado.Chat.Models;

namespace LlmTornado.Tests;

/// <summary>
/// OpenAI model ids that OpenAI has retired are marked obsolete, with the replacement named.
/// </summary>
[TestFixture]
public class OpenAiModelDeprecationTests
{
    private static readonly string[] RetiredIds =
    [
        "gpt-3.5-turbo", "gpt-3.5-turbo-0125", "gpt-3.5-turbo-1106", "gpt-3.5-turbo-instruct", "gpt-4", "gpt-4-0613", "gpt-4-1106-preview",
        "gpt-4-0125-preview", "gpt-4-turbo", "gpt-4-turbo-2024-04-09", "gpt-4-1106-vision-preview", "gpt-4-32k", "gpt-4-32k-0613",
        "gpt-4o-2024-05-13", "o1-pro", "o1-2024-12-17", "o1-preview", "o1-preview-2024-09-12", "o1-mini", "o1-mini-2024-09-12",
        "o3-mini", "o3-mini-2025-01-31", "o4-mini", "gpt-4.1-nano"
    ];

    private static IEnumerable<FieldInfo> StaticModelFields() =>
        new[]
            {
                typeof(ChatModelOpenAiGpt35), typeof(ChatModelOpenAiGpt4), typeof(ChatModelOpenAiGpt41),
                typeof(ChatModelOpenAiO3), typeof(ChatModelOpenAiO4)
            }
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(f => f.FieldType == typeof(ChatModel));

    [TestCaseSource(nameof(RetiredIds))]
    public void RetiredModel_IsMarkedObsoleteWithReplacement(string id)
    {
        FieldInfo field = StaticModelFields().Single(f => ((ChatModel)f.GetValue(null)!).Name == id);
        ObsoleteAttribute? attr = field.GetCustomAttribute<ObsoleteAttribute>();

        Assert.That(attr, Is.Not.Null);
        Assert.That(attr!.Message, Does.Contain("Retired").And.Contain("on the OpenAI API. Use ").And.Contain("ChatModel.OpenAi.Gpt56."));
    }

    [Test]
    public void RetiredModel_InstanceAliasIsAlsoObsolete()
    {
        Assert.That(typeof(ChatModelOpenAiGpt35).GetField(nameof(ChatModel.OpenAi.Gpt35.Turbo))!.GetCustomAttribute<ObsoleteAttribute>(), Is.Not.Null);
        Assert.That(typeof(ChatModelOpenAiO3).GetField(nameof(ChatModel.OpenAi.O3.Mini))!.GetCustomAttribute<ObsoleteAttribute>(), Is.Not.Null);
    }

    [Test]
    public void CurrentModels_AreNotObsolete()
    {
        Assert.That(typeof(ChatModelOpenAiGpt4).GetField(nameof(ChatModelOpenAiGpt4.ModelOMini))!.GetCustomAttribute<ObsoleteAttribute>(), Is.Null);
        Assert.That(typeof(ChatModelOpenAiGpt41).GetField(nameof(ChatModelOpenAiGpt41.ModelV41Mini))!.GetCustomAttribute<ObsoleteAttribute>(), Is.Null);
        Assert.That(typeof(ChatModelOpenAiO4).GetField(nameof(ChatModelOpenAiO4.ModelV4MiniDeepResearch))!.GetCustomAttribute<ObsoleteAttribute>(), Is.Null);
    }
}
