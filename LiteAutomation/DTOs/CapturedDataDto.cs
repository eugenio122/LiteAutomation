using System.Text.Json.Serialization;

namespace LiteAutomation.DTOs
{
    /// <summary>
    /// A gaveta tripla simétrica que abriga as verdades de todos os motores.
    /// </summary>
    public class CapturedDataDto
    {
        [JsonPropertyName("UIA")] public EngineNodeDto<UiaElementDataDto>? Uia { get; set; }
        [JsonPropertyName("AX_Tree")] public EngineNodeDto<AxTreeElementDataDto>? AxTree { get; set; }
        [JsonPropertyName("WebDriver_BiDi")] public EngineNodeDto<BiDiElementDataDto>? WebDriverBiDi { get; set; }
    }
}