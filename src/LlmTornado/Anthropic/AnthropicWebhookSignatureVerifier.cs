using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace LlmTornado.Anthropic;

/// <summary>Verifies organization events using Standard Webhooks HMAC-SHA256 and timestamp replay protection.</summary>
public static class AnthropicWebhookSignatureVerifier
{
    /// <summary>Verifies the exact raw body before deserializing. Throws CryptographicException on invalid delivery.</summary>
    public static AnthropicOrganizationWebhookEvent Verify(string payload, IReadOnlyDictionary<string, string> headers,
        string signingSecret, TimeSpan? timestampTolerance = null)
    {
        string? id = Header(headers, "webhook-id");
        string? timestamp = Header(headers, "webhook-timestamp");
        string? signatures = Header(headers, "webhook-signature");
        TimeSpan tolerance = timestampTolerance ?? TimeSpan.FromMinutes(5);
        if (tolerance < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timestampTolerance));
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(signatures) || string.IsNullOrEmpty(signingSecret)
            || !long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out long seconds)
            || Math.Abs((double)DateTimeOffset.UtcNow.ToUnixTimeSeconds() - seconds) > tolerance.TotalSeconds)
            throw new CryptographicException("Missing, invalid, or expired webhook headers.");

        byte[] secret;
        try { secret = Convert.FromBase64String(signingSecret.StartsWith("whsec_", StringComparison.Ordinal) ? signingSecret.Substring(6) : signingSecret); }
        catch (FormatException ex) { throw new CryptographicException("Invalid webhook signing secret.", ex); }
        if (secret.Length == 0) throw new CryptographicException("Empty webhook signing secret.");
        using HMACSHA256 hmac = new(secret);
        byte[] expected = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{id}.{timestamp}.{payload}"));
        foreach (string signature in signatures!.Split(' '))
        {
            if (!signature.StartsWith("v1,", StringComparison.Ordinal)) continue;
            byte[] actual;
            try { actual = Convert.FromBase64String(signature.Substring(3)); }
            catch (FormatException) { continue; }
            if (actual.Length != expected.Length) continue;
            int difference = 0;
            for (int i = 0; i < expected.Length; i++) difference |= expected[i] ^ actual[i];
            if (difference != 0) continue;
            AnthropicOrganizationWebhookEvent? result = JsonConvert.DeserializeObject<AnthropicOrganizationWebhookEvent>(payload);
            if (result?.Id != id || result.Type != "event" || result.Data is null)
                throw new CryptographicException("Invalid webhook event envelope.");
            return result;
        }
        throw new CryptographicException("Invalid webhook signature.");
    }

    private static string? Header(IReadOnlyDictionary<string, string> headers, string name)
    {
        foreach (KeyValuePair<string, string> pair in headers)
            if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)) return pair.Value;
        return null;
    }
}
