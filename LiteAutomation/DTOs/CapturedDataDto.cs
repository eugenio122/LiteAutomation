using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace LiteAutomation.DTOs
{
    public class CapturedDataDto
    {
        [JsonPropertyName("UIA")] public EngineDataDto? Uia { get; set; }
        [JsonPropertyName("AX_Tree")] public EngineDataDto? AxTree { get; set; }
        [JsonPropertyName("WebDriver_BiDi")] public EngineDataDto? WebDriverBiDi { get; set; }
    }
}
