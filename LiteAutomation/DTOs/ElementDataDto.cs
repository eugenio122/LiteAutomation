using System.Text.Json.Serialization;

namespace LiteAutomation.DTOs
{
    public class ElementDataDto
    {
        [JsonPropertyName("semantic")] public SemanticDto? Semantic { get; set; }
        [JsonPropertyName("selectorSet")] public SelectorSetDto? SelectorSet { get; set; }
        [JsonPropertyName("url")] public string? Url { get; set; }
        [JsonPropertyName("value")] public string? Value { get; set; }
    }
}