using System;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Code;
using LlmTornado.Common;

namespace LlmTornado.Anthropic;

/// <summary>Native Usage and Cost Analytics API for Console and Enterprise organizations.</summary>
public sealed class AnthropicAnalyticsEndpoint : AnthropicResourceEndpoint
{
    internal AnthropicAnalyticsEndpoint(TornadoApi api) : base(api) { }
    /// <inheritdoc />
    protected override CapabilityEndpoints Endpoint => CapabilityEndpoints.AnthropicAnalytics;
    /// <inheritdoc />
    protected override string BetaHeader => "analytics-usage-cost-2026-08-24";

    /// <summary>Lists usage rows, preserving the opaque next_page and data_refreshed_at values.</summary>
    public Task<HttpCallResult<AnthropicPage<AnthropicAnalyticsUsageRow>>> Usage(AnthropicAnalyticsQuery query, CancellationToken cancellationToken = default)
    {
        Validate(query);
        return Call<AnthropicPage<AnthropicAnalyticsUsageRow>>("/usage", HttpVerbs.Get, query: query, cancellationToken: cancellationToken);
    }
    /// <summary>Lists cost rows. Cost amounts are decimal strings denominated in cents.</summary>
    public Task<HttpCallResult<AnthropicPage<AnthropicAnalyticsCostRow>>> Cost(AnthropicAnalyticsQuery query, CancellationToken cancellationToken = default)
    {
        Validate(query);
        return Call<AnthropicPage<AnthropicAnalyticsCostRow>>("/cost", HttpVerbs.Get, query: query, cancellationToken: cancellationToken);
    }
    private static void Validate(AnthropicAnalyticsQuery query)
    {
        if (query is null || string.IsNullOrWhiteSpace(query.StartingAt)) throw new ArgumentException("starting_at is required.", nameof(query));
        if (query.TimeBucketWidth is not null && string.IsNullOrWhiteSpace(query.EndingAt)) throw new ArgumentException("ending_at is required with time_bucket_width.", nameof(query));
    }
}
