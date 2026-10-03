using System;
using System.Collections.Generic;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Audio.Models.OpenRouter;

/// <summary>
/// OpenRouter speech models from the October 3, 2026 catalog.
/// </summary>
public class AudioModelOpenRouterSpeech : IVendorModelClassProvider
{
    /// <summary>bytedance-seed/seed-audio-1-0</summary>
    public static readonly AudioModel ModelSeedAudio10 = new AudioModel("bytedance-seed/seed-audio-1-0", LLmProviders.OpenRouter, 0);

    /// <inheritdoc cref="ModelSeedAudio10"/>
    public readonly AudioModel SeedAudio10 = ModelSeedAudio10;

    /// <summary>canopylabs/orpheus-3b-0.1-ft</summary>
    public static readonly AudioModel ModelOrpheus3b01Ft = new AudioModel("canopylabs/orpheus-3b-0.1-ft", LLmProviders.OpenRouter, 4096);

    /// <inheritdoc cref="ModelOrpheus3b01Ft"/>
    public readonly AudioModel Orpheus3b01Ft = ModelOrpheus3b01Ft;

    /// <summary>deepgram/aura-2</summary>
    public static readonly AudioModel ModelAura2 = new AudioModel("deepgram/aura-2", LLmProviders.OpenRouter, 0);

    /// <inheritdoc cref="ModelAura2"/>
    public readonly AudioModel Aura2 = ModelAura2;

    /// <summary>deepgram/flux-tts</summary>
    public static readonly AudioModel ModelFluxTts = new AudioModel("deepgram/flux-tts", LLmProviders.OpenRouter, 0);

    /// <inheritdoc cref="ModelFluxTts"/>
    public readonly AudioModel FluxTts = ModelFluxTts;

    /// <summary>fish-audio/s1</summary>
    public static readonly AudioModel ModelFishS1 = new AudioModel("fish-audio/s1", LLmProviders.OpenRouter, 0);

    /// <inheritdoc cref="ModelFishS1"/>
    public readonly AudioModel FishS1 = ModelFishS1;

    /// <summary>fish-audio/s2-pro</summary>
    public static readonly AudioModel ModelFishS2Pro = new AudioModel("fish-audio/s2-pro", LLmProviders.OpenRouter, 0);

    /// <inheritdoc cref="ModelFishS2Pro"/>
    public readonly AudioModel FishS2Pro = ModelFishS2Pro;

    /// <summary>fish-audio/s2.1-pro</summary>
    public static readonly AudioModel ModelFishS21Pro = new AudioModel("fish-audio/s2.1-pro", LLmProviders.OpenRouter, 0);

    /// <inheritdoc cref="ModelFishS21Pro"/>
    public readonly AudioModel FishS21Pro = ModelFishS21Pro;

    /// <summary>fish-audio/s2.1-pro-free:free</summary>
    public static readonly AudioModel ModelFishS21ProFree = new AudioModel("fish-audio/s2.1-pro-free:free", LLmProviders.OpenRouter, 0);

    /// <inheritdoc cref="ModelFishS21ProFree"/>
    public readonly AudioModel FishS21ProFree = ModelFishS21ProFree;

    /// <summary>google/gemini-3.1-flash-tts-preview</summary>
    public static readonly AudioModel ModelGemini31FlashTtsPreview = new AudioModel("google/gemini-3.1-flash-tts-preview", LLmProviders.OpenRouter, 32768);

    /// <inheritdoc cref="ModelGemini31FlashTtsPreview"/>
    public readonly AudioModel Gemini31FlashTtsPreview = ModelGemini31FlashTtsPreview;

    /// <summary>google/gemini-3.8-flash-lite-tts</summary>
    public static readonly AudioModel ModelGemini38FlashLiteTts = new AudioModel("google/gemini-3.8-flash-lite-tts", LLmProviders.OpenRouter, 32768);

    /// <inheritdoc cref="ModelGemini38FlashLiteTts"/>
    public readonly AudioModel Gemini38FlashLiteTts = ModelGemini38FlashLiteTts;

    /// <summary>google/gemini-3.8-flash-tts</summary>
    public static readonly AudioModel ModelGemini38FlashTts = new AudioModel("google/gemini-3.8-flash-tts", LLmProviders.OpenRouter, 32768);

    /// <inheritdoc cref="ModelGemini38FlashTts"/>
    public readonly AudioModel Gemini38FlashTts = ModelGemini38FlashTts;

    /// <summary>hexgrad/kokoro-82m</summary>
    public static readonly AudioModel ModelKokoro82m = new AudioModel("hexgrad/kokoro-82m", LLmProviders.OpenRouter, 4096);

    /// <inheritdoc cref="ModelKokoro82m"/>
    public readonly AudioModel Kokoro82m = ModelKokoro82m;

    /// <summary>microsoft/mai-voice-2</summary>
    public static readonly AudioModel ModelMaiVoice2 = new AudioModel("microsoft/mai-voice-2", LLmProviders.OpenRouter, 0);

    /// <inheritdoc cref="ModelMaiVoice2"/>
    public readonly AudioModel MaiVoice2 = ModelMaiVoice2;

    /// <summary>microsoft/mai-voice-2-flash</summary>
    public static readonly AudioModel ModelMaiVoice2Flash = new AudioModel("microsoft/mai-voice-2-flash", LLmProviders.OpenRouter, 0);

    /// <inheritdoc cref="ModelMaiVoice2Flash"/>
    public readonly AudioModel MaiVoice2Flash = ModelMaiVoice2Flash;

    /// <summary>microsoft/mai-voice-2.1</summary>
    public static readonly AudioModel ModelMaiVoice21 = new AudioModel("microsoft/mai-voice-2.1", LLmProviders.OpenRouter, 0);

    /// <inheritdoc cref="ModelMaiVoice21"/>
    public readonly AudioModel MaiVoice21 = ModelMaiVoice21;

    /// <summary>microsoft/mai-voice-2.1-flash</summary>
    public static readonly AudioModel ModelMaiVoice21Flash = new AudioModel("microsoft/mai-voice-2.1-flash", LLmProviders.OpenRouter, 0);

    /// <inheritdoc cref="ModelMaiVoice21Flash"/>
    public readonly AudioModel MaiVoice21Flash = ModelMaiVoice21Flash;

    /// <summary>minimax/speech-2.8-hd</summary>
    public static readonly AudioModel ModelSpeech28Hd = new AudioModel("minimax/speech-2.8-hd", LLmProviders.OpenRouter, 0);

    /// <inheritdoc cref="ModelSpeech28Hd"/>
    public readonly AudioModel Speech28Hd = ModelSpeech28Hd;

    /// <summary>minimax/speech-2.8-turbo</summary>
    public static readonly AudioModel ModelSpeech28Turbo = new AudioModel("minimax/speech-2.8-turbo", LLmProviders.OpenRouter, 0);

    /// <inheritdoc cref="ModelSpeech28Turbo"/>
    public readonly AudioModel Speech28Turbo = ModelSpeech28Turbo;

    /// <summary>mistralai/voxtral-mini-tts-2603</summary>
    public static readonly AudioModel ModelVoxtralMiniTts2603 = new AudioModel("mistralai/voxtral-mini-tts-2603", LLmProviders.OpenRouter, 4096);

    /// <inheritdoc cref="ModelVoxtralMiniTts2603"/>
    public readonly AudioModel VoxtralMiniTts2603 = ModelVoxtralMiniTts2603;

    /// <summary>qwen/qwen-audio-3.0-tts-flash</summary>
    public static readonly AudioModel ModelQwenAudio30TtsFlash = new AudioModel("qwen/qwen-audio-3.0-tts-flash", LLmProviders.OpenRouter, 0);

    /// <inheritdoc cref="ModelQwenAudio30TtsFlash"/>
    public readonly AudioModel QwenAudio30TtsFlash = ModelQwenAudio30TtsFlash;

    /// <summary>qwen/qwen-audio-3.0-tts-plus</summary>
    public static readonly AudioModel ModelQwenAudio30TtsPlus = new AudioModel("qwen/qwen-audio-3.0-tts-plus", LLmProviders.OpenRouter, 0);

    /// <inheritdoc cref="ModelQwenAudio30TtsPlus"/>
    public readonly AudioModel QwenAudio30TtsPlus = ModelQwenAudio30TtsPlus;

    /// <summary>sesame/csm-1b</summary>
    public static readonly AudioModel ModelCsm1b = new AudioModel("sesame/csm-1b", LLmProviders.OpenRouter, 4096);

    /// <inheritdoc cref="ModelCsm1b"/>
    public readonly AudioModel Csm1b = ModelCsm1b;

    /// <summary>x-ai/grok-voice-tts-1.0</summary>
    public static readonly AudioModel ModelGrokVoiceTts10 = new AudioModel("x-ai/grok-voice-tts-1.0", LLmProviders.OpenRouter, 15000);

    /// <inheritdoc cref="ModelGrokVoiceTts10"/>
    public readonly AudioModel GrokVoiceTts10 = ModelGrokVoiceTts10;

    /// <summary>All known OpenRouter speech models.</summary>
    public static List<IModel> ModelsAll => LazyModelsAll.Value;

    private static readonly Lazy<List<IModel>> LazyModelsAll = new Lazy<List<IModel>>(() => [
        ModelSeedAudio10,
        ModelOrpheus3b01Ft,
        ModelAura2,
        ModelFluxTts,
        ModelFishS1,
        ModelFishS2Pro,
        ModelFishS21Pro,
        ModelFishS21ProFree,
        ModelGemini31FlashTtsPreview,
        ModelGemini38FlashLiteTts,
        ModelGemini38FlashTts,
        ModelKokoro82m,
        ModelMaiVoice2,
        ModelMaiVoice2Flash,
        ModelMaiVoice21,
        ModelMaiVoice21Flash,
        ModelSpeech28Hd,
        ModelSpeech28Turbo,
        ModelVoxtralMiniTts2603,
        ModelQwenAudio30TtsFlash,
        ModelQwenAudio30TtsPlus,
        ModelCsm1b,
        ModelGrokVoiceTts10
    ]);

    /// <inheritdoc cref="ModelsAll"/>
    public List<IModel> AllModels => ModelsAll;

    internal AudioModelOpenRouterSpeech()
    {
    }
}
