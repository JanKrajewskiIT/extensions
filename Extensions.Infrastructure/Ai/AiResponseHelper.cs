using System.Text.Json;

namespace Extensions.Infrastructure.Ai;

/// <summary>
/// Shared utilities for parsing AI (LLM) responses.
/// Handles markdown code-fence stripping and JSON deserialization with cached options.
/// </summary>
public static class AiResponseHelper
{
    private static readonly JsonSerializerOptions CaseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Strips markdown code fences (```json ... ```) from an AI response and returns clean content.
    /// </summary>
    public static string CleanJsonResponse(string response)
    {
        response = response.Trim();

        if (response.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            response = response[7..];
        else if (response.StartsWith("```", StringComparison.Ordinal))
            response = response[3..];

        if (response.EndsWith("```", StringComparison.Ordinal))
            response = response[..^3];

        return response.Trim();
    }

    /// <summary>
    /// Cleans an AI response text, then deserializes it as JSON into <typeparamref name="T"/>.
    /// Throws <see cref="InvalidOperationException"/> if the response is null/empty or deserialization fails.
    /// </summary>
    public static T DeserializeResponse<T>(string? responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
            throw new InvalidOperationException("AI returned empty response");

        var cleaned = CleanJsonResponse(responseText);

        return JsonSerializer.Deserialize<T>(cleaned, CaseInsensitiveOptions)
            ?? throw new InvalidOperationException("Failed to parse AI response as JSON");
    }
}
