using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LiteAutomation.DTOs
{
    /// <summary>
    /// Representa o comando executável real (Clique, Preenchimento, etc.).
    /// </summary>
    public class MicroStepDto
    {
        [JsonPropertyName("stepId")] public string? StepId { get; set; }
        [JsonPropertyName("actionType")] public string? ActionType { get; set; }
        [JsonPropertyName("triggerEngine")] public string? TriggerEngine { get; set; }
        [JsonPropertyName("capturedData")] public CapturedDataDto? CapturedData { get; set; }
    }
}