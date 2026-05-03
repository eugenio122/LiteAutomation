using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace LiteAutomation.DTOs
{
    public class SemanticDto
    {
        [JsonPropertyName("automationId")] public LocatorData? AutomationId { get; set; }
        [JsonPropertyName("accessibleName")] public LocatorData? AccessibleName { get; set; }
        [JsonPropertyName("role")] public LocatorData? Role { get; set; }
        [JsonPropertyName("helpText")] public LocatorData? HelpText { get; set; }
    }

}
