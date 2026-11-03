using LlmTornado.Demo;
using LlmTornado.Internal.Press.Publisher;
using PuppeteerSharp;
using PuppeteerSharp.BrowserData;

namespace LlmTornado.Tests;

/// <summary>
/// The computer-use demo and the Medium publisher open pages they do not control, so they must start the
/// installed, auto-updated Chrome with its sandbox on, not a pinned download with the sandbox turned off.
/// </summary>
[TestFixture]
public class BrowserLaunchOptionsTests
{
    private static readonly string[] SandboxOffFlags = ["--no-sandbox", "--disable-setuid-sandbox"];

    [Test]
    public void ComputerUseDemo_UsesStableChromeWithSandbox()
    {
        AssertStableChromeWithSandbox(ChatDemo.CreateComputerUseLaunchOptions());
    }

    [TestCase(true)]
    [TestCase(false)]
    public void MediumPublisher_UsesStableChromeWithSandbox(bool headless)
    {
        LaunchOptions options = MediumPublisher.CreateLaunchOptions(headless);

        AssertStableChromeWithSandbox(options);
        Assert.That(options.Headless, Is.EqualTo(headless));
    }

    private static void AssertStableChromeWithSandbox(LaunchOptions options)
    {
        Assert.Multiple(() =>
        {
            Assert.That(options.Channel, Is.EqualTo(ChromeReleaseChannel.Stable));
            Assert.That(options.ExecutablePath, Is.Null);
            Assert.That(options.Args ?? [], Has.None.AnyOf(SandboxOffFlags));
        });
    }
}
