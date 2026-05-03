using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace LiteAutomation.DTOs
{
    public class VisibleElementDto
    {
        [JsonPropertyName("elementType")] public string? ElementType { get; set; }
        [JsonPropertyName("isEnabled")] public bool IsEnabled { get; set; }
        [JsonPropertyName("centerX")] public int CenterX { get; set; }
        [JsonPropertyName("centerY")] public int CenterY { get; set; }
        [JsonPropertyName("capturedData")] public CapturedDataDto? CapturedData { get; set; }
    }
}
