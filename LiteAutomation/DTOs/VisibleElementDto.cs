using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LiteAutomation.DTOs
{
    /// <summary>
    /// O nó real que mapeia a hierarquia (Substitui o antigo DomNodeDto fictício)
    /// </summary>
    public class VisibleElementDto
    {
        [JsonPropertyName("nodeId")] public string? NodeId { get; set; }
        [JsonPropertyName("elementType")] public string? ElementType { get; set; }
        [JsonPropertyName("isEnabled")] public bool IsEnabled { get; set; }
        [JsonPropertyName("isFocused")] public bool IsFocused { get; set; }
        [JsonPropertyName("isChecked")] public bool? IsChecked { get; set; }

        [JsonPropertyName("centerX")] public int CenterX { get; set; }
        [JsonPropertyName("centerY")] public int CenterY { get; set; }
        [JsonPropertyName("width")] public int Width { get; set; }
        [JsonPropertyName("height")] public int Height { get; set; }

        [JsonPropertyName("capturedData")] public CapturedDataDto? CapturedData { get; set; }
        [JsonPropertyName("children")] public List<VisibleElementDto> Children { get; set; } = new List<VisibleElementDto>();
        [JsonPropertyName("isSemanticAnchor")] public bool IsSemanticAnchor { get; set; }

        [JsonIgnore] public VisibleElementDto? Parent { get; set; }
    }
}