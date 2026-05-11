using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LiteAutomation.DTOs
{
    /// <summary>
    /// Payload principal (O Snapshot).
    /// </summary>
    public class MainStepDto
    {
        [JsonPropertyName("stepId")] public string? StepId { get; set; }
        [JsonPropertyName("stepIndex")] public int? StepIndex { get; set; }
        [JsonPropertyName("stepName")] public string? StepName { get; set; }
        [JsonPropertyName("timestamp")] public DateTime Timestamp { get; set; }

        [JsonPropertyName("isEvidenceOnly")] public bool IsEvidenceOnly { get; set; }
        [JsonPropertyName("isActive")] public bool IsActive { get; set; }
        [JsonPropertyName("pendingConfirmation")] public bool PendingConfirmation { get; set; }
        [JsonPropertyName("isHydrated")] public bool IsHydrated { get; set; }
        [JsonPropertyName("triggerType")] public string? TriggerType { get; set; }
        [JsonPropertyName("contextImage")] public string? ContextImage { get; set; }

        [JsonPropertyName("interactionTrail")] public List<InteractionDto> InteractionTrail { get; set; } = new List<InteractionDto>();
        [JsonPropertyName("capturedData")] public CapturedDataDto? CapturedData { get; set; }
        [JsonPropertyName("observedContext")] public ObservedContextDto? ObservedContext { get; set; }
    }
}