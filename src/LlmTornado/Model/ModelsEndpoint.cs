using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Code;
using LlmTornado.Common;
using LlmTornado.Models.Vendors;
using Newtonsoft.Json;

namespace LlmTornado.Models;

/// <summary>
///     The API endpoint for querying available models
/// </summary>
public class ModelsEndpoint : EndpointBase
{
	/// <summary>
	///     Constructor of the api endpoint.  Rather than instantiating this yourself, access it through an instance of
	///     <see cref="TornadoApi" /> as <see cref="TornadoApi.Models" />.
	/// </summary>
	/// <param name="api"></param>
	internal ModelsEndpoint(TornadoApi api) : base(api)
    {
    }

	/// <summary>
	///     The name of the endpoint, which is the final path segment in the API URL.  For example, "models".
	/// </summary>
	protected override CapabilityEndpoints Endpoint => CapabilityEndpoints.Models;

	/// <summary>
	///     Get details about a particular Model from the API, specifically properties such as <see cref="Model.OwnedBy" /> and
	///     permissions.
	/// </summary>
	/// <param name="id">The id/name of the model to get more details about</param>
	/// <returns>Asynchronously returns the <see cref="Model" /> with all available properties</returns>
	public async Task<Model> GetModelDetails(string? id)
    {
        string resultAsString = await HttpGetContent(Api.GetProvider(LLmProviders.OpenAi), Endpoint, $"/{id}");
        Model? model = JsonConvert.DeserializeObject<Model>(resultAsString);
        return model;
    }

	/// <summary>
	///     Get details about a particular model from the provider Models API, including Anthropic capability metadata.
	/// </summary>
	/// <param name="id">The id/name of the model to get more details about</param>
	/// <param name="provider">The provider to query</param>
	/// <returns>Asynchronously returns the <see cref="RetrievedModel" /> with all available properties</returns>
	public async Task<RetrievedModel?> GetRetrievedModelDetails(string? id, LLmProviders provider = LLmProviders.OpenAi)
	{
		return (await HttpGet<RetrievedModel>(Api.GetProvider(provider), Endpoint, $"/{id}")).Data;
	}

	/// <summary>
	///     List all models of a given Provider.
	/// </summary>
	/// <param name="provider">The provider to query</param>
	/// <returns>Asynchronously returns the list of all <see cref="Model" />s</returns>
	/// <exception cref="Exception">Thrown if the request fails or the response can't be parsed. Use <see cref="GetModelsSafe" /> to receive the error as a result instead.</exception>
	public async Task<List<RetrievedModel>?> GetModels(LLmProviders provider = LLmProviders.OpenAi)
	{
		HttpCallResult<List<RetrievedModel>> result = await GetModelsSafe(provider).ConfigureAwait(false);

		if (result.Exception is not null)
		{
			throw result.Exception;
		}

		return result.Data;
	}

	/// <summary>
	///     List all models of a given Provider. This method doesn't throw exceptions (even if the network layer fails).
	/// </summary>
	/// <param name="provider">The provider to query</param>
	/// <param name="token">Cancellation token</param>
	/// <returns>Asynchronously returns the call result with the list of all <see cref="Model" />s, or the error</returns>
	public async Task<HttpCallResult<List<RetrievedModel>>> GetModelsSafe(LLmProviders provider = LLmProviders.OpenAi, CancellationToken token = default)
	{
		Dictionary<string, object>? queryPars = provider switch
		{
			LLmProviders.Google => new Dictionary<string, object> { { "pageSize", 1000 } },
			LLmProviders.Cohere => new Dictionary<string, object> { { "page_size", 1000 } },
			LLmProviders.Anthropic => new Dictionary<string, object> { { "limit", 1000 } },
			_ => null
		};

		HttpCallResult<RetrievedModelsResult> result = await HttpGet<RetrievedModelsResult>(Api.GetProvider(provider), Endpoint, queryParams: queryPars, ct: token).ConfigureAwait(false);

		return new HttpCallResult<List<RetrievedModel>>(result.Code, result.Response, result.Data?.Data, result.Ok, result.Request)
		{
			// a response that fails to parse carries its exception only on the request
			Exception = result.Exception ?? (result.Ok ? null : result.Request.Exception)
		};
	}
}