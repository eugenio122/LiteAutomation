using System.Text.Json.Serialization;

namespace LiteAutomation.DTOs
{
    public class BiDiElementDataDto
    {
        [JsonPropertyName("selectorSet")] public SelectorSetDto? SelectorSet { get; set; }
        [JsonPropertyName("url")] public string? Url { get; set; }
        [JsonPropertyName("value")] public string? Value { get; set; }
        [JsonPropertyName("frameworkId")] public string? FrameworkId { get; set; }
    }
}