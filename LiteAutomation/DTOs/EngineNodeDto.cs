using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LiteAutomation.DTOs
{
    public class EngineNodeDto<T>
    {
        [JsonPropertyName("elementData")] public T? ElementData { get; set; }
        [JsonPropertyName("qualityFlags")] public List<string> QualityFlags { get; set; } = new List<string>();
    }
}