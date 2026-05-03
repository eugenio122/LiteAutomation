using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace LiteAutomation.DTOs
{
    public class SelectorSetDto
    {
        [JsonPropertyName("customAttribute")] public LocatorData? CustomAttribute { get; set; }
        [JsonPropertyName("id")] public LocatorData? Id { get; set; }
        [JsonPropertyName("name")] public LocatorData? Name { get; set; }
        [JsonPropertyName("ariaLabel")] public LocatorData? AriaLabel { get; set; }
        [JsonPropertyName("placeholder")] public LocatorData? Placeholder { get; set; }
        [JsonPropertyName("alt")] public LocatorData? Alt { get; set; }
        [JsonPropertyName("text")] public LocatorData? Text { get; set; }
        [JsonPropertyName("title")] public LocatorData? Title { get; set; }
        [JsonPropertyName("css")] public LocatorData? Css { get; set; }
        [JsonPropertyName("xpathRelative")] public LocatorData? XpathRelative { get; set; }
        [JsonPropertyName("xpathAbsolute")] public LocatorData? XpathAbsolute { get; set; }
    }
}
