using System.Collections.Generic;
using LlmTornado.Decision.Models;
using LlmTornado.Code;
using Newtonsoft.Json;

namespace LlmTornado.Decision;

/// <summary>
/// A request for the decision API. Evaluates a state against a map of typed questions and returns one answer per question.
/// </summary>
public class DecisionRequest : ISerializableRequest
{
    /// <summary>
    /// Creates a new request with the given model and state. Add questions with the <c>AddNoul</c>, <c>AddChoice</c> and <c>AddScore</c> helpers, or via <see cref="Questions"/>.
    /// </summary>
    /// <param name="model">The model to use.</param>
    /// <param name="state">The content to evaluate: a string, or structured data (object / array).</param>
    public DecisionRequest(DecisionModel model, object state)
    {
        Model = model;
        State = state;
    }
    
    /// <summary>
    /// Creates a new request with the given model, state and questions.
    /// </summary>
    /// <param name="model">The model to use.</param>
    /// <param name="state">The content to evaluate: a string, or structured data (object / array).</param>
    /// <param name="questions">Questions keyed by an id you choose; answers come back under the same ids.</param>
    public DecisionRequest(DecisionModel model, object state, Dictionary<string, DecisionQuestion> questions)
    {
        Model = model;
        State = state;
        Questions = questions;
    }

    /// <summary>
    /// The model that handles the request.
    /// </summary>
    [JsonProperty("model")]
    [JsonConverter(typeof(IModelConverter))]
    public DecisionModel Model { get; set; }

    /// <summary>
    /// The content to evaluate. A plain string for text, or structured data (object / array) for things like chat logs, records, or application state.
    /// </summary>
    [JsonProperty("state")]
    public object State { get; set; }

    /// <summary>
    /// Typed questions keyed by an id you choose. The id is not sent to the model; the matching answer is returned under the same id.
    /// All questions are evaluated in parallel against the same state.
    /// </summary>
    [JsonProperty("questions")]
    public Dictionary<string, DecisionQuestion> Questions { get; set; } = [];

    /// <summary>
    /// Adds a yes/no question.
    /// </summary>
    public DecisionRequest AddNoul(string id, object instructions, object? @true = null, object? @false = null)
    {
        Questions[id] = @true is null && @false is null ? new DecisionNoul(instructions) : new DecisionNoul(instructions, @true, @false);
        return this;
    }
    
    /// <summary>
    /// Adds a choice question.
    /// </summary>
    public DecisionRequest AddChoice(string id, object instructions, Dictionary<string, object?> criteria)
    {
        Questions[id] = new DecisionChoice(instructions, criteria);
        return this;
    }
    
    /// <summary>
    /// Adds a choice question with bare option names.
    /// </summary>
    public DecisionRequest AddChoice(string id, object instructions, params string[] options)
    {
        Questions[id] = new DecisionChoice(instructions, options);
        return this;
    }
    
    /// <summary>
    /// Adds a score question.
    /// </summary>
    public DecisionRequest AddScore(string id, object instructions, params string[] levels)
    {
        Questions[id] = new DecisionScore(instructions, levels);
        return this;
    }
    
    /// <summary>
    /// Adds a score question with structured level descriptions.
    /// </summary>
    public DecisionRequest AddScore(string id, object instructions, List<object> levels)
    {
        Questions[id] = new DecisionScore(instructions, levels);
        return this;
    }

    [JsonIgnore]
    internal string? UrlOverride { get; set; }
    
    internal void OverrideUrl(string url)
    {
        UrlOverride = url;
    }
    
    /// <summary>
    /// Serializes the request.
    /// </summary>
    public TornadoRequestContent Serialize(IEndpointProvider provider, RequestSerializeOptions options)
    {
        return SerializeInternal(provider, options);
    }
    
    /// <summary>
    /// Serializes the request.
    /// </summary>
    public TornadoRequestContent Serialize(IEndpointProvider provider)
    {
        return SerializeInternal(provider, null);
    }
    
    /// <summary>
    /// Serializes the request.
    /// </summary>
    internal TornadoRequestContent SerializeInternal(IEndpointProvider provider, RequestSerializeOptions? options)
    {
        // Null option descriptions in choice criteria are meaningful for the API, so nulls are kept here;
        // optional properties opt out individually via NullValueHandling.Ignore.
        string body = JsonConvert.SerializeObject(this, options?.Pretty ?? false ? Formatting.Indented : Formatting.None, SerializerSettings);
        return new TornadoRequestContent(body, Model, UrlOverride ?? EndpointBase.BuildRequestUrl(null, provider, CapabilityEndpoints.Decision, Model), provider, CapabilityEndpoints.Decision);
    }
    
    private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
    {
        NullValueHandling = NullValueHandling.Include
    };
}
