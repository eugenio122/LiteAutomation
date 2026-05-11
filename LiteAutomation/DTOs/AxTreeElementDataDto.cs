using System.Text.Json.Serialization;

namespace LiteAutomation.DTOs
{
    public class AxTreeElementDataDto
    {
        [JsonPropertyName("semantic")] public SemanticDto? Semantic { get; set; }
        [JsonPropertyName("boundingRectangle")] public string? BoundingRectangle { get; set; }
    }
}