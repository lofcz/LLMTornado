using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Decision.Models;
using LlmTornado.Code;
using LlmTornado.Common;

namespace LlmTornado.Decision;

/// <summary>
/// The decision endpoint evaluates a state (text or structured data) against a set of typed questions
/// (choice, score, noul) and returns structured answers with calibrated probabilities. Backed by TypeSafe System One models (Jev).
/// </summary>
public class DecisionEndpoint : EndpointBase
{
    /// <summary>
    /// Constructor of the api endpoint. Rather than instantiating this yourself, access it through an instance of
    /// <see cref="TornadoApi" /> as <see cref="TornadoApi.Decision" />.
    /// </summary>
    /// <param name="api"></param>
    internal DecisionEndpoint(TornadoApi api) : base(api)
    {
    }
    
    /// <summary>
    /// The name of the endpoint, which is the final path segment in the API URL.
    /// </summary>
    protected override CapabilityEndpoints Endpoint => CapabilityEndpoints.Decision;
    
    /// <summary>
    /// Ask the API to evaluate the questions against the state.
    /// </summary>
    /// <param name="request">The request to send to the API.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>Asynchronously returns the decision result.</returns>
    public async Task<DecisionResult?> CreateDecision(DecisionRequest request, CancellationToken token = default)
    {
        HttpCallResult<DecisionResult> result = await CreateDecisionSafe(request, token).ConfigureAwait(false);
        
        if (result.Exception is not null)
        {
            throw result.Exception;
        }
        
        return result.Data;
    }
    
    /// <summary>
    /// Ask the API to evaluate the questions against the state.
    /// </summary>
    /// <param name="model">The model to use.</param>
    /// <param name="state">The content to evaluate: a string, or structured data (object / array).</param>
    /// <param name="questions">Questions keyed by an id you choose; answers come back under the same ids.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>Asynchronously returns the decision result.</returns>
    public Task<DecisionResult?> CreateDecision(DecisionModel model, object state, Dictionary<string, DecisionQuestion> questions, CancellationToken token = default)
    {
        return CreateDecision(new DecisionRequest(model, state, questions), token);
    }

    /// <summary>
    /// Ask the API to evaluate the questions against the state. This method doesn't throw exceptions (even if the network layer fails).
    /// </summary>
    /// <param name="request">The request to send to the API.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>Asynchronously returns the decision result.</returns>
    public async Task<HttpCallResult<DecisionResult>> CreateDecisionSafe(DecisionRequest request, CancellationToken token = default)
    {
        IEndpointProvider provider = Api.GetProvider(request.Model);
        TornadoRequestContent requestBody = request.Serialize(provider);
        return await HttpPost<DecisionResult>(provider, Endpoint, requestBody.Url, requestBody.Body, request.Model, request, token).ConfigureAwait(false);
    }
}
