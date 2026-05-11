using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LiteAutomation.DTOs
{
    public class ObservedContextDto
    {
        [JsonPropertyName("url")] public string? Url { get; set; }
        [JsonPropertyName("pageTitle")] public string? PageTitle { get; set; }
        [JsonPropertyName("viewportWidth")] public int ViewportWidth { get; set; }
        [JsonPropertyName("viewportHeight")] public int ViewportHeight { get; set; }
        [JsonPropertyName("visibleElements")] public List<VisibleElementDto> VisibleElements { get; set; } = new List<VisibleElementDto>();
    }
}