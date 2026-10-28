using LlmTornado.Code;
using LlmTornado.Common;

namespace LlmTornado.Tests;

[TestFixture]
public class AnthropicFilesListQueryTests
{
    [Test]
    public void ToQueryParams_Anthropic_MapsAfterToAfterId()
    {
        Dictionary<string, object>? parameters = new ListQuery(limit: 5, after: "file_011").ToQueryParams(LLmProviders.Anthropic);

        Assert.Multiple(() =>
        {
            Assert.That(parameters, Is.Not.Null);
            Assert.That(parameters!["after_id"], Is.EqualTo("file_011"));
            Assert.That(parameters.ContainsKey("before_id"), Is.False);
            Assert.That(parameters["limit"], Is.EqualTo(5));
        });
    }

    [Test]
    public void ToQueryParams_Anthropic_MapsBeforeToBeforeId()
    {
        Dictionary<string, object>? parameters = new ListQuery(before: "file_022").ToQueryParams(LLmProviders.Anthropic);

        Assert.Multiple(() =>
        {
            Assert.That(parameters!["before_id"], Is.EqualTo("file_022"));
            Assert.That(parameters.ContainsKey("after_id"), Is.False);
        });
    }

    [Test]
    public void ToQueryParams_Anthropic_PageTokenStillWorksAsAfterId()
    {
        Dictionary<string, object>? parameters = new ListQuery { PageToken = "file_033" }.ToQueryParams(LLmProviders.Anthropic);

        Assert.That(parameters!["after_id"], Is.EqualTo("file_033"));
    }

    [Test]
    public void ToQueryParams_Anthropic_AfterWinsOverPageToken()
    {
        Dictionary<string, object>? parameters = new ListQuery(after: "file_011") { PageToken = "file_033" }.ToQueryParams(LLmProviders.Anthropic);

        Assert.That(parameters!["after_id"], Is.EqualTo("file_011"));
    }
}
