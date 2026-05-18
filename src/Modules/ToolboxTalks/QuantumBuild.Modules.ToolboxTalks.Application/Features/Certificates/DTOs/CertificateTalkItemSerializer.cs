using System.Text.Json;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.Certificates.DTOs;

public static class CertificateTalkItemSerializer
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Tolerantly deserializes IncludedTalksJson, handling both legacy string-array shape
    /// ["Title A","Title B"] and current object-array shape [{"title":"...","code":"..."}].
    /// </summary>
    public static List<CertificateTalkItem> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            return [];

        if (root.GetArrayLength() == 0)
            return [];

        var firstKind = root[0].ValueKind;

        if (firstKind == JsonValueKind.String)
        {
            // Legacy shape: plain string array
            return root.EnumerateArray()
                .Select(e => new CertificateTalkItem(e.GetString() ?? string.Empty, null))
                .ToList();
        }

        // Current shape: object array
        return JsonSerializer.Deserialize<List<CertificateTalkItem>>(json, _options) ?? [];
    }
}
