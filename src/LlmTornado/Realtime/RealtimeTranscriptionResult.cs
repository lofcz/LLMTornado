using System.Collections.Generic;
using System.Threading.Tasks;

namespace LlmTornado.Realtime;

/// <summary>
/// Aggregated result from a streaming Realtime transcription run.
/// </summary>
public class RealtimeTranscriptionResult
{
    /// <summary>Incremental transcript deltas in arrival order.</summary>
    public List<string> Deltas { get; } = [];

    /// <summary>Concatenated delta text.</summary>
    public string? PartialTranscript { get; set; }

    /// <summary>Final transcript from the completed event, when received.</summary>
    public string? FinalTranscript { get; set; }

    /// <summary>Error messages collected during the session.</summary>
    public List<string> Errors { get; } = [];

    internal TaskCompletionSource<bool> Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal async Task DispatchAsync(RealtimeServerEvent evt, RealtimeTranscriptionStreamEventHandler handler)
    {
        bool terminal = false;
        switch (evt.Type)
        {
            case RealtimeEventTypes.TranscriptionDelta:
                Deltas.Add(evt.Delta ?? string.Empty);
                break;
            case RealtimeEventTypes.TranscriptionCompleted:
                FinalTranscript = evt.Transcript;
                terminal = true;
                break;
            case RealtimeEventTypes.TranscriptionFailed:
            case RealtimeEventTypes.Error:
                Errors.Add(evt.Error?.Message ?? evt.Error?.Code ?? "Transcription failed.");
                terminal = true;
                break;
        }

        await handler.DispatchAsync(evt).ConfigureAwait(false);
        if (terminal)
        {
            Completed.TrySetResult(evt.Type is RealtimeEventTypes.TranscriptionCompleted);
        }
    }
}
