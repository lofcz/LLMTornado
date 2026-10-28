using LlmTornado.Chat.Vendors.Anthropic;
using LlmTornado.Mcp;

namespace LlmTornado.Tests;

[TestFixture]
public class AnthropicMcpAuthorizationTokenTests
{
    private static AnthropicMcpServer Convert(Dictionary<string, string>? headers) =>
        new MCPServer("docs", "https://mcp.example.com/mcp", additionalConnectionHeaders: headers).ToAnthropicMcpServerAsync();

    [Test]
    public void ToAnthropicMcpServer_BearerHeader_SendsTheBareToken()
    {
        AnthropicMcpServer server = Convert(new Dictionary<string, string> { ["Authorization"] = "Bearer secret-token" });

        Assert.That(server.AuthorizationToken, Is.EqualTo("secret-token"));
    }

    [Test]
    public void ToAnthropicMcpServer_BearerSchemeIsCaseInsensitive()
    {
        AnthropicMcpServer server = Convert(new Dictionary<string, string> { ["authorization"] = "bearer secret-token" });

        Assert.That(server.AuthorizationToken, Is.EqualTo("secret-token"));
    }

    [Test]
    public void ToAnthropicMcpServer_HeaderWithoutScheme_IsKeptAsIs()
    {
        AnthropicMcpServer server = Convert(new Dictionary<string, string> { ["X-Auth-Token"] = "secret-token" });

        Assert.That(server.AuthorizationToken, Is.EqualTo("secret-token"));
    }

    [Test]
    public void ToAnthropicMcpServer_WithoutAuthHeader_LeavesTheTokenUnset()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Convert(null).AuthorizationToken, Is.Null);
            Assert.That(Convert(new Dictionary<string, string> { ["X-Trace"] = "1" }).AuthorizationToken, Is.Null);
        });
    }
}
