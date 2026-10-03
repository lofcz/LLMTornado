using System;
using Newtonsoft.Json;

namespace LlmTornado.Realtime;

/// <summary>
/// Client event helpers for Realtime WebSocket sessions.
/// </summary>
public static class RealtimeClientEvents
{
    /// <summary>Sends a GA <c>session.update</c> for transcription sessions.</summary>
    public static object SessionUpdate(RealtimeTranscriptionSessionConfig session, string? eventId = null) =>
        new { type = "session.update", event_id = eventId, session };

    /// <summary>Append base64-encoded PCM audio to the input buffer.</summary>
    public static object InputAudioBufferAppend(string base64Audio, string? eventId = null) =>
        new { type = "input_audio_buffer.append", event_id = eventId, audio = base64Audio };

    /// <summary>Append raw PCM16 bytes to the input buffer.</summary>
    public static object InputAudioBufferAppend(ReadOnlySpan<byte> pcm16, string? eventId = null) =>
        InputAudioBufferAppend(Convert.ToBase64String(pcm16.ToArray()), eventId);

    /// <summary>Commit the input buffer (manual turn detection for transcription).</summary>
    public static object InputAudioBufferCommit(string? eventId = null) =>
        new { type = "input_audio_buffer.commit", event_id = eventId };

    /// <summary>Clear the input audio buffer.</summary>
    public static object InputAudioBufferClear(string? eventId = null) =>
        new { type = "input_audio_buffer.clear", event_id = eventId };

    /// <summary>Translation session update (<c>session.update</c>).</summary>
    public static object TranslationSessionUpdate(object session, string? eventId = null) =>
        new { type = "session.update", event_id = eventId, session };

    /// <summary>Append base64 PCM16 to a translation session input buffer.</summary>
    public static object TranslationInputAudioBufferAppend(string base64Audio, string? eventId = null) =>
        new { type = "session.input_audio_buffer.append", event_id = eventId, audio = base64Audio };

    /// <summary>Gracefully close a translation session.</summary>
    public static object TranslationSessionClose(string? eventId = null) =>
        new { type = "session.close", event_id = eventId };
}
