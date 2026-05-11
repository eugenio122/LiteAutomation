using System.Text.Json.Serialization;

namespace LiteAutomation.DTOs
{
    public class InteractionDto
    {
        [JsonPropertyName("interactionType")] public string? InteractionType { get; set; }
        [JsonPropertyName("timestamp")] public string? Timestamp { get; set; }
        [JsonPropertyName("tagName")] public string? TagName { get; set; }
        [JsonPropertyName("elementId")] public string? ElementId { get; set; }
        [JsonPropertyName("classes")] public string? Classes { get; set; }
        [JsonPropertyName("inputType")] public string? InputType { get; set; }
        [JsonPropertyName("visibleText")] public string? VisibleText { get; set; }
        [JsonPropertyName("value")] public string? Value { get; set; }
        [JsonPropertyName("boundingBox")] public string? BoundingBox { get; set; }
        [JsonPropertyName("scrollX")] public int ScrollX { get; set; }
        [JsonPropertyName("scrollY")] public int ScrollY { get; set; }
        [JsonPropertyName("urlPath")] public string? UrlPath { get; set; }

        // As gavetas ricas agora vêm direto no evento!
        [JsonPropertyName("WebDriver_BiDi")] public EngineNodeDto<BiDiElementDataDto>? WebDriverBiDi { get; set; }
        [JsonPropertyName("UIA")] public EngineNodeDto<UiaElementDataDto>? Uia { get; set; }
        [JsonPropertyName("AX_Tree")] public EngineNodeDto<AxTreeElementDataDto>? AxTree { get; set; }
    }
}