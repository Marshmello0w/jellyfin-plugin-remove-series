using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.RemoveSeries.Helpers;

public sealed class PatchRequestPayload
{
    [JsonPropertyName("contents")]
    public string? Contents { get; set; }
}

