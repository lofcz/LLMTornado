using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LlmTornado.Threads;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Chat.Vendors.Anthropic;

// Retains native blocks separately from the shared ChatResult projection so replay is lossless.
internal sealed class AnthropicThreadStreamAccumulator
{
    private readonly SortedDictionary<int, JObject> blocks = new();
    private readonly Dictionary<int, StringBuilder> inputs = new();
    private readonly HashSet<int> finished = new();
    internal JObject? Message { get; private set; }
    internal bool Complete { get; private set; }

    internal void Add(ServerSentEvent ev)
    {
        JObject data = JObject.Parse(ev.Data);
        string? type = (string?)data["type"] ?? ev.EventType;
        int index = (int?)data["index"] ?? 0;
        switch (type)
        {
            case "error":
                throw new IOException($"Anthropic streaming error: {data["error"]}");
            case "message_start":
                Message = (JObject?)data["message"]?.DeepClone();
                break;
            case "content_block_start":
                blocks[index] = (JObject)data["content_block"]!.DeepClone();
                break;
            case "content_block_delta":
                if (!blocks.TryGetValue(index, out JObject? block)) throw new IOException("Content delta arrived before its block.");
                JObject delta = (JObject)data["delta"]!;
                switch ((string?)delta["type"])
                {
                    case "text_delta": Append(block, "text", delta); break;
                    case "thinking_delta": Append(block, "thinking", delta); break;
                    case "signature_delta": Append(block, "signature", delta); break;
                    case "compaction_delta": Append(block, "content", delta); break;
                    case "citations_delta":
                        if (block["citations"] is not JArray) block["citations"] = new JArray();
                        ((JArray)block["citations"]!).Add(delta["citation"]!.DeepClone());
                        break;
                    case "input_json_delta":
                        if (!inputs.TryGetValue(index, out StringBuilder? input)) inputs[index] = input = new StringBuilder();
                        input.Append((string?)delta["partial_json"]);
                        break;
                    default:
                        // An unknown delta cannot safely be replayed as a complete signed/tool block.
                        throw new IOException($"Unsupported Anthropic content delta: {delta["type"]}");
                }
                break;
            case "content_block_stop":
                if (!blocks.TryGetValue(index, out JObject? stopped)) throw new IOException("Block stop arrived before its block.");
                if (inputs.TryGetValue(index, out StringBuilder? json) && json.Length > 0)
                    stopped["input"] = JToken.Parse(json.ToString());
                finished.Add(index);
                break;
            case "message_delta":
                if (Message is null) throw new IOException("Message delta arrived before message_start.");
                if (data["delta"] is JObject fields)
                    foreach (JProperty field in fields.Properties()) Message[field.Name] = field.Value.DeepClone();
                if (data["usage"] is JObject usage)
                {
                    if (Message["usage"] is not JObject) Message["usage"] = new JObject();
                    ((JObject)Message["usage"]!).Merge(usage, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Replace });
                }
                break;
            case "message_stop":
                if (Message is null || blocks.Keys.Any(x => !finished.Contains(x)))
                    throw new IOException("message_stop arrived before all content blocks completed.");
                Message["content"] = new JArray(blocks.Values.Select(x => x.DeepClone()));
                Complete = true;
                break;
        }
    }

    internal JArray PartialContent() => new(blocks.Where(x => finished.Contains(x.Key)
        || (string?)x.Value["type"] == "text" && !string.IsNullOrEmpty((string?)x.Value["text"]))
        .Select(x => x.Value.DeepClone()));

    private static void Append(JObject block, string field, JObject delta)
        => block[field] = ((string?)block[field] ?? "") + (string?)delta[field];
}
