# Changelog

## 3.8.72 - 2026-10-07

- Add OpenRouter alpha Decisions support for Luna Decisions, Jev 1.13, and the other registered native Decision models.
- Add provider routing, ZDR restrictions, trace metadata, generation identifiers, and request costs for OpenRouter Decisions.
- Include all output modalities in OpenRouter model queries, including models that support both text and decisions.
- Add explicit OpenRouter live tests for Luna Decisions and Jev 1.13, plus local regression tests.
- Change `DecisionScoreAnswer.Legend` from `Dictionary<string, string>` to `Dictionary<string, object>` to preserve structured descriptions.
  Consumers that require string values must handle object and array values.
