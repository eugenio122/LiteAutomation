using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace LiteAutomation.DTOs
{
    // =========================================================
    // O PADRÃO OURO: SCORED LOCATOR (O Valor e a Confiança)
    // =========================================================
    public class LocatorData
    {
        [JsonPropertyName("value")] public string? Value { get; set; }
        [JsonPropertyName("confidence")] public int Confidence { get; set; }
    }
}
