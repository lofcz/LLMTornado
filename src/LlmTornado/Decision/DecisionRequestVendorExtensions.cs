using LlmTornado.Decision.Vendors.OpenRouter;

namespace LlmTornado.Decision;

/// <summary>Provider-specific decision request settings.</summary>
public class DecisionRequestVendorExtensions
{
    /// <summary>OpenRouter routing and observability settings.</summary>
    public DecisionRequestVendorOpenRouterExtensions? OpenRouter { get; set; }

    /// <summary>Creates provider-specific request settings.</summary>
    public DecisionRequestVendorExtensions()
    {
    }

    /// <summary>Creates request settings for OpenRouter.</summary>
    public DecisionRequestVendorExtensions(DecisionRequestVendorOpenRouterExtensions openRouter)
    {
        OpenRouter = openRouter;
    }
}
