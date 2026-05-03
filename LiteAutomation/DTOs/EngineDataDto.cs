using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace LiteAutomation.DTOs
{
    public class EngineDataDto
    {
        [JsonPropertyName("elementData")] public ElementDataDto? ElementData { get; set; }
        [JsonPropertyName("qualityFlags")] public List<string>? QualityFlags { get; set; }
    }
}
