using System.Text.Json.Serialization;

namespace AlbionDataAvalonia.Auth.Models;

// Response shape of GET {TrimsSilverIngestApiBase}/me.
public class TrimsSilverProfile
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }
}
