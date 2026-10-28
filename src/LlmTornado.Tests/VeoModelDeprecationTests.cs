using System.Reflection;
using LlmTornado.Videos.Models.Google;

namespace LlmTornado.Tests;

/// <summary>
/// Veo 3.1 preview models were shut down on October 22, 2026 in favour of gemini-omni-1.1-flash.
/// </summary>
[TestFixture]
public class VeoModelDeprecationTests
{
    [TestCase(nameof(VideoModelGoogleVeo.ModelV31))]
    [TestCase(nameof(VideoModelGoogleVeo.ModelV31Fast))]
    [TestCase(nameof(VideoModelGoogleVeo.ModelV31Lite))]
    public void Veo31Preview_IsMarkedObsoleteWithOctober2026Shutdown(string field)
    {
        ObsoleteAttribute? attr = typeof(VideoModelGoogleVeo)
            .GetField(field, BindingFlags.Public | BindingFlags.Static)!
            .GetCustomAttribute<ObsoleteAttribute>();

        Assert.That(attr, Is.Not.Null);
        Assert.That(attr!.Message, Does.Contain("October 22, 2026"));
        Assert.That(attr.Message, Does.Contain("ModelOmni11Flash"));
    }
}
