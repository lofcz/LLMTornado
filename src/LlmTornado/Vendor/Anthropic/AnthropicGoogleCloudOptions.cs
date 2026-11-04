using System;

namespace LlmTornado.Vendor.Anthropic;

/// <summary>Claude on Google Cloud, operated by Anthropic. The application owns credential acquisition and refresh.</summary>
public sealed class AnthropicGoogleCloudOptions
{
    /// <summary>Google Cloud project ID.</summary>
    public string ProjectId { get; set; } = string.Empty;
    /// <summary>Anthropic workspace ID. Not required for organization or user-profile routes.</summary>
    public string? WorkspaceId { get; set; }
    /// <summary>Returns a current Google OAuth token with cloud-platform scope for each request.</summary>
    public Func<string> AccessTokenProvider { get; set; } = null!;

    internal string GetUrl(string path)
    {
        if (string.IsNullOrWhiteSpace(ProjectId)) throw new ArgumentException("A Google Cloud project ID is required.");
        bool organization = path.StartsWith("organizations/", StringComparison.Ordinal) || path.StartsWith("user_profiles", StringComparison.Ordinal);
        if (!organization && string.IsNullOrWhiteSpace(WorkspaceId)) throw new ArgumentException("An Anthropic workspace ID is required for this Google Cloud route.");
        string scope = organization ? "claudeOrganization" : $"workspaces/{Uri.EscapeDataString(WorkspaceId!)}";
        return $"https://claude.googleapis.com/v1/projects/{Uri.EscapeDataString(ProjectId)}/locations/global/{scope}/invoke/v1/{path}";
    }
}
