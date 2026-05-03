using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace LiteAutomation.DTOs
{
    // O Envelope do Contexto da Tela
    public class ObservedContextDto
    {
        [JsonPropertyName("url")] public string? Url { get; set; }
        [JsonPropertyName("pageTitle")] public string? PageTitle { get; set; }
        [JsonPropertyName("visibleElements")] public List<VisibleElementDto>? VisibleElements { get; set; }
    }
}
