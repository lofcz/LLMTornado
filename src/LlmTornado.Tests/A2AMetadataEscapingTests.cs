using A2A;
using LlmTornado.A2A;
using LlmTornado.Chat;
using LlmTornado.Code;

namespace LlmTornado.Tests;

[TestFixture]
public class A2AMetadataEscapingTests
{
    private const string Tricky = "say \"hi\"\\ \n\tend";

    private static ChatMessage Message() => new(ChatMessageRoles.Assistant, "hello")
    {
        Refusal = Tricky,
        Name = Tricky
    };

    [Test]
    public void ToA2AAgentMessage_RefusalAndNameWithSpecialChars_RoundTrip()
    {
        AgentMessage msg = Message().ToA2AAgentMessage();

        Assert.That(msg.Metadata!["refusal"].GetString(), Is.EqualTo(Tricky));
        Assert.That(msg.Metadata!["userName"].GetString(), Is.EqualTo(Tricky));
    }

    [Test]
    public void ToA2AArtifact_RefusalAndNameWithSpecialChars_RoundTrip()
    {
        Artifact artifact = Message().ToA2AArtifact();

        Assert.That(artifact.Metadata!["refusal"].GetString(), Is.EqualTo(Tricky));
        Assert.That(artifact.Metadata!["userName"].GetString(), Is.EqualTo(Tricky));
    }

    [Test]
    public void ExecutableCodeWithSpecialCharsInCustomLanguage_RoundTrip()
    {
        ChatMessagePartExecutableCode code = new() { Code = "x", CustomLanguage = Tricky };

        TextPart part = code.ToA2ATextPart();

        Assert.That(part.Metadata!["Language"].GetString(), Is.EqualTo(Tricky));
    }
}
