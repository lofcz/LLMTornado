using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LlmTornado.Decision;

/// <summary>
/// Types of decision questions.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum DecisionQuestionTypes
{
    /// <summary>
    /// Unknown type.
    /// </summary>
    [EnumMember(Value = "unknown")]
    Unknown,
    
    /// <summary>
    /// Yes/no question. The answer is the probability that the answer is yes.
    /// </summary>
    [EnumMember(Value = "noul")]
    Noul,
    
    /// <summary>
    /// Picks one option from a defined set. The answer carries the chosen option, a probability per option, and confidence.
    /// </summary>
    [EnumMember(Value = "choice")]
    Choice,
    
    /// <summary>
    /// Rates the state against ordered levels. The answer carries a probability-weighted score, a probability per level, and confidence.
    /// </summary>
    [EnumMember(Value = "score")]
    Score
}

/// <summary>
/// A typed question evaluated against the state of a <see cref="DecisionRequest"/>.
/// </summary>
public abstract class DecisionQuestion
{
    /// <summary>
    /// Type of the question.
    /// </summary>
    [JsonProperty("type", Order = -2)]
    public abstract DecisionQuestionTypes Type { get; }

    /// <summary>
    /// What the model should decide. A plain string, or structured data (object or array) holding the question in one field
    /// and the data it refers to in others. Refer to the data fields by name in backticks.
    /// </summary>
    [JsonProperty("instructions", Order = -1)]
    public object Instructions { get; set; }

    /// <summary>
    /// Creates a new question.
    /// </summary>
    protected DecisionQuestion(object instructions)
    {
        Instructions = instructions;
    }
}

/// <summary>
/// Optional descriptions of what a yes and a no mean for a <see cref="DecisionNoul"/> question.
/// </summary>
public class DecisionNoulCriteria
{
    /// <summary>
    /// What a yes (value near 1) means. String, object or array.
    /// </summary>
    [JsonProperty("true", NullValueHandling = NullValueHandling.Ignore)]
    public object? True { get; set; }
    
    /// <summary>
    /// What a no (value near 0) means. String, object or array.
    /// </summary>
    [JsonProperty("false", NullValueHandling = NullValueHandling.Ignore)]
    public object? False { get; set; }

    /// <summary>
    /// Creates empty criteria.
    /// </summary>
    public DecisionNoulCriteria()
    {
    }
    
    /// <summary>
    /// Creates criteria describing yes and no.
    /// </summary>
    public DecisionNoulCriteria(object? @true, object? @false)
    {
        True = @true;
        False = @false;
    }
}

/// <summary>
/// A yes/no question. Returns the probability the answer is yes. Ask one yes/no question per noul.
/// </summary>
public class DecisionNoul : DecisionQuestion
{
    /// <inheritdoc />
    public override DecisionQuestionTypes Type => DecisionQuestionTypes.Noul;

    /// <summary>
    /// Optional descriptions of what a yes and a no mean.
    /// </summary>
    [JsonProperty("criteria", NullValueHandling = NullValueHandling.Ignore)]
    public DecisionNoulCriteria? Criteria { get; set; }

    /// <summary>
    /// Creates a yes/no question.
    /// </summary>
    /// <param name="instructions">The yes/no question to evaluate.</param>
    public DecisionNoul(object instructions) : base(instructions)
    {
    }
    
    /// <summary>
    /// Creates a yes/no question with descriptions of what yes and no mean.
    /// </summary>
    /// <param name="instructions">The yes/no question to evaluate.</param>
    /// <param name="true">What a yes (value near 1) means.</param>
    /// <param name="false">What a no (value near 0) means.</param>
    public DecisionNoul(object instructions, object? @true, object? @false) : base(instructions)
    {
        Criteria = new DecisionNoulCriteria(@true, @false);
    }
}

/// <summary>
/// Picks one option from a set you define. Returns the chosen option and the full probability distribution.
/// </summary>
public class DecisionChoice : DecisionQuestion
{
    /// <summary>
    /// Maximum number of options accepted by the API.
    /// </summary>
    public const int MaxOptions = 255;
    
    /// <inheritdoc />
    public override DecisionQuestionTypes Type => DecisionQuestionTypes.Choice;

    /// <summary>
    /// Map of option name to its rubric description (string, object or array). Use null when an option needs no extra detail.
    /// At most <see cref="MaxOptions"/> options. Consider including an "other" / "none of the above" option.
    /// </summary>
    [JsonProperty("criteria")]
    public Dictionary<string, object?> Criteria { get; set; }

    /// <summary>
    /// Creates a choice question.
    /// </summary>
    /// <param name="instructions">What the model should decide.</param>
    /// <param name="criteria">Map of option name to description; null values are allowed.</param>
    public DecisionChoice(object instructions, Dictionary<string, object?> criteria) : base(instructions)
    {
        Criteria = criteria;
    }
    
    /// <summary>
    /// Creates a choice question from bare option names without descriptions.
    /// </summary>
    /// <param name="instructions">What the model should decide.</param>
    /// <param name="options">Option names.</param>
    public DecisionChoice(object instructions, IEnumerable<string> options) : base(instructions)
    {
        Criteria = [];
        
        foreach (string option in options)
        {
            Criteria[option] = null;
        }
    }
}

/// <summary>
/// Rates the state along a rubric you define. Returns a probability-weighted value across your levels.
/// </summary>
public class DecisionScore : DecisionQuestion
{
    /// <summary>
    /// Minimum number of levels accepted by the API.
    /// </summary>
    public const int MinLevels = 2;
    
    /// <summary>
    /// Maximum number of levels accepted by the API.
    /// </summary>
    public const int MaxLevels = 10;
    
    /// <inheritdoc />
    public override DecisionQuestionTypes Type => DecisionQuestionTypes.Score;

    /// <summary>
    /// Ordered level descriptions (string, object or array each), lowest first. Between <see cref="MinLevels"/> and <see cref="MaxLevels"/> levels.
    /// The returned score ranges from 0 to (number of levels - 1).
    /// </summary>
    [JsonProperty("criteria")]
    public List<object> Criteria { get; set; }

    /// <summary>
    /// Creates a score question.
    /// </summary>
    /// <param name="instructions">What the model should rate.</param>
    /// <param name="criteria">Ordered level descriptions, lowest first.</param>
    public DecisionScore(object instructions, List<object> criteria) : base(instructions)
    {
        Criteria = criteria;
    }
    
    /// <summary>
    /// Creates a score question from plain string levels.
    /// </summary>
    /// <param name="instructions">What the model should rate.</param>
    /// <param name="criteria">Ordered level descriptions, lowest first.</param>
    public DecisionScore(object instructions, params string[] criteria) : base(instructions)
    {
        Criteria = [..criteria];
    }
}
