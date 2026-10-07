using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Decision;

/// <summary>
/// Represents a response from the decision API: one answer per question, keyed by the ids used in the request.
/// </summary>
public class DecisionResult : ApiResultBase
{
    /// <summary>Generation identifier returned by OpenRouter.</summary>
    [JsonProperty("id")]
    public string? Id { get; set; }

    /// <summary>Name of the upstream provider that served the decision.</summary>
    [JsonProperty("provider")]
    public string? UpstreamProvider { get; set; }

    /// <summary>
    /// One answer per question, keyed by the same ids used in <see cref="DecisionRequest.Questions"/>.
    /// </summary>
    [JsonProperty("answers")]
    public Dictionary<string, DecisionAnswer> Answers { get; set; } = [];

    /// <summary>
    /// Token usage for the request.
    /// </summary>
    [JsonProperty("usage")]
    public DecisionUsage? Usage { get; set; }

    /// <summary>
    /// Returns the answer to a noul question, or null if the id is unknown or belongs to a different question type.
    /// </summary>
    public DecisionNoulAnswer? GetNoul(string id)
    {
        return Answers.TryGetValue(id, out DecisionAnswer? answer) ? answer as DecisionNoulAnswer : null;
    }
    
    /// <summary>
    /// Returns the answer to a choice question, or null if the id is unknown or belongs to a different question type.
    /// </summary>
    public DecisionChoiceAnswer? GetChoice(string id)
    {
        return Answers.TryGetValue(id, out DecisionAnswer? answer) ? answer as DecisionChoiceAnswer : null;
    }
    
    /// <summary>
    /// Returns the answer to a score question, or null if the id is unknown or belongs to a different question type.
    /// </summary>
    public DecisionScoreAnswer? GetScore(string id)
    {
        return Answers.TryGetValue(id, out DecisionAnswer? answer) ? answer as DecisionScoreAnswer : null;
    }
}

/// <summary>
/// Token usage of a decision request. Output tokens are not billed by TypeSafe.
/// </summary>
public class DecisionUsage
{
    /// <summary>Request cost in USD, when returned by OpenRouter.</summary>
    [JsonProperty("cost")]
    public decimal? Cost { get; set; }

    /// <summary>
    /// Input tokens consumed (state plus all questions).
    /// </summary>
    [JsonProperty("input_tokens")]
    public int InputTokens { get; set; }
    
    /// <summary>
    /// Output tokens produced.
    /// </summary>
    [JsonProperty("output_tokens")]
    public int OutputTokens { get; set; }
}

/// <summary>
/// Base class of typed answers. The concrete type matches the question type: <see cref="DecisionNoulAnswer"/>, <see cref="DecisionChoiceAnswer"/>, or <see cref="DecisionScoreAnswer"/>.
/// </summary>
[JsonConverter(typeof(DecisionAnswerConverter))]
public abstract class DecisionAnswer
{
    /// <summary>
    /// Type of the answer, matching the question type.
    /// </summary>
    [JsonProperty("type")]
    public DecisionQuestionTypes Type { get; set; }
}

/// <summary>
/// Answer to a <see cref="DecisionNoul"/> question.
/// </summary>
public class DecisionNoulAnswer : DecisionAnswer
{
    /// <summary>
    /// Probability that the answer is yes, from 0 (strong no) to 1 (strong yes). Around 0.5 means uncertain.
    /// </summary>
    [JsonProperty("noul")]
    public double Noul { get; set; }
}

/// <summary>
/// Answer to a <see cref="DecisionChoice"/> question.
/// </summary>
public class DecisionChoiceAnswer : DecisionAnswer
{
    /// <summary>
    /// The highest-probability option.
    /// </summary>
    [JsonProperty("choice")]
    public string Choice { get; set; }
    
    /// <summary>
    /// Every option mapped to its probability. The values sum to 1.
    /// </summary>
    [JsonProperty("probabilities")]
    public Dictionary<string, double> Probabilities { get; set; } = [];
    
    /// <summary>
    /// How certain the model is, from 0 to 1, derived from the spread of <see cref="Probabilities"/>. 1 means all probability on one option.
    /// </summary>
    [JsonProperty("confidence")]
    public double Confidence { get; set; }
}

/// <summary>
/// Answer to a <see cref="DecisionScore"/> question.
/// </summary>
public class DecisionScoreAnswer : DecisionAnswer
{
    /// <summary>
    /// The probability-weighted position on the level spectrum, from 0 to (number of levels - 1). Can land between levels.
    /// </summary>
    [JsonProperty("score")]
    public double Score { get; set; }
    
    /// <summary>
    /// Each level index (as a string key) mapped back to its string, object or array description.
    /// </summary>
    [JsonProperty("legend")]
    public Dictionary<string, object> Legend { get; set; } = [];
    
    /// <summary>
    /// Each level index (as a string key) mapped to its probability. The values sum to 1.
    /// </summary>
    [JsonProperty("probabilities")]
    public Dictionary<string, double> Probabilities { get; set; } = [];
    
    /// <summary>
    /// How certain the model is, from 0 to 1, derived from the spread of <see cref="Probabilities"/>.
    /// </summary>
    [JsonProperty("confidence")]
    public double Confidence { get; set; }
}

/// <summary>
/// Answer of a type this version of the library doesn't know. The raw payload is preserved in <see cref="Raw"/>.
/// </summary>
public class DecisionUnknownAnswer : DecisionAnswer
{
    /// <summary>
    /// Raw JSON of the answer.
    /// </summary>
    public JObject? Raw { get; set; }
}

/// <summary>
/// Deserializes answers into their concrete type based on the "type" discriminator.
/// </summary>
internal class DecisionAnswerConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return typeof(DecisionAnswer).IsAssignableFrom(objectType);
    }

    public override bool CanWrite => false;

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        throw new NotSupportedException();
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType is JsonToken.Null)
        {
            return null;
        }
        
        JObject obj = JObject.Load(reader);
        string? type = obj["type"]?.Value<string>();

        DecisionAnswer answer = type switch
        {
            "noul" => new DecisionNoulAnswer(),
            "choice" => new DecisionChoiceAnswer(),
            "score" => new DecisionScoreAnswer(),
            _ => new DecisionUnknownAnswer { Raw = obj }
        };

        if (answer is DecisionUnknownAnswer)
        {
            answer.Type = DecisionQuestionTypes.Unknown;
            return answer;
        }
        
        // populate without re-entering this converter
        using JsonReader objReader = obj.CreateReader();
        serializer.Populate(objReader, answer);
        return answer;
    }
}
