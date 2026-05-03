using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LiteAutomation.DTOs
{
    /// <summary>
    /// Representa a "Âncora Visual" (Ecrã/Página) onde as ações ocorreram.
    /// Contém os metadados do ecrã e a lista de Micro-Steps (Ações reais).
    /// </summary>
    public class MainStepDto
    {
        [JsonPropertyName("stepId")] public string? StepId { get; set; }
        [JsonPropertyName("stepIndex")] public int StepIndex { get; set; }
        [JsonPropertyName("stepName")] public string? StepName { get; set; }
        [JsonPropertyName("isEvidenceOnly")] public bool IsEvidenceOnly { get; set; }

        [JsonPropertyName("capturedData")] public CapturedDataDto? CapturedData { get; set; }

        // 🚀 MAPEAMENTO EXATO: O seu novo Fat Payload envelopado!
        [JsonPropertyName("observedContext")] public ObservedContextDto? ObservedContext { get; set; }

        [JsonPropertyName("microSteps")] public List<MicroStepDto> MicroSteps { get; set; } = new List<MicroStepDto>();
    }
}