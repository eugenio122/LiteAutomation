using System.Text.Json.Serialization;

namespace LiteAutomation.DTOs
{
    public class UiaElementDataDto
    {
        [JsonPropertyName("semantic")] public SemanticDto? Semantic { get; set; }
        [JsonPropertyName("uiaClassName")] public string? UiaClassName { get; set; }
        [JsonPropertyName("boundingRectangle")] public string? BoundingRectangle { get; set; }
    }
}