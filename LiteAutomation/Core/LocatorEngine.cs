using System.Collections.Generic;
using System.Linq;
using System.Text;
using LiteAutomation.DTOs;
using LiteAutomation.Enums;
using LiteTools.Core.Languages;

namespace LiteAutomation.Core
{
    public static class LocatorEngine
    {
        // 🚀 CORREÇÃO AQUI: Recebe o CapturedDataDto (Coração do nó) no lugar do antigo MicroStepDto
        public static string GetDiagnostics(CapturedDataDto? capturedData, AutomationStrategy strategy, string indent = "            ")
        {
            if (strategy != AutomationStrategy.Smart_Selector || capturedData == null) return "";

            var sb = new StringBuilder();
            var uiaFlags = capturedData.Uia?.QualityFlags ?? new List<string>();
            var bidiFlags = capturedData.WebDriverBiDi?.QualityFlags ?? new List<string>();

            var allFlags = new List<string>();
            allFlags.AddRange(uiaFlags);
            allFlags.AddRange(bidiFlags);

            if (allFlags.Count > 0)
            {
                sb.AppendLine($"{indent}{string.Format(LanguageManager.GetString("DiagQualityFlags"), string.Join(", ", allFlags))}");
            }

            if (uiaFlags.Contains("A11Y_GAP_WARNING") || uiaFlags.Contains("MISSING_ACCESSIBLE_NAME"))
            {
                sb.AppendLine($"{indent}{LanguageManager.GetString("DiagSemanticAlert")}");
                sb.AppendLine($"{indent}{LanguageManager.GetString("DiagSemanticSuggestion")}");
            }

            if (bidiFlags.Contains("WARNING_BRITTLE_LOCATOR") || bidiFlags.Contains("WARNING_AMBIGUOUS_LOCATOR") || bidiFlags.Count == 0)
            {
                sb.AppendLine($"{indent}{LanguageManager.GetString("DiagDomAlert")}");
                sb.AppendLine($"{indent}{LanguageManager.GetString("DiagDomSuggestion")}");
            }

            return sb.ToString().TrimEnd('\r', '\n');
        }

        public static string GetBestPlaywrightLocator(SelectorSetDto? selectorSet)
        {
            string safeValue = GetBestRawLocator(selectorSet);
            if (safeValue.StartsWith("/*")) return $"Page.Locator(\"{safeValue}\")";
            if (safeValue.StartsWith("/") || safeValue.StartsWith("(")) return $"Page.Locator(\"xpath={safeValue}\")";
            if (safeValue.StartsWith("css=")) return $"Page.Locator(\"{safeValue}\")";
            return $"Page.Locator(\"{safeValue}\")";
        }

        public static string GetBestSeleniumLocator(SelectorSetDto? selectorSet)
        {
            string safeValue = GetBestRawLocator(selectorSet);
            if (safeValue.StartsWith("/*")) return $"By.XPath(\"{safeValue}\")";
            if (safeValue.StartsWith("/") || safeValue.StartsWith("(")) return $"By.XPath(\"{safeValue}\")";
            return $"By.CssSelector(\"{safeValue}\")";
        }

        public static string GetBestRawLocator(SelectorSetDto? selectorSet)
        {
            if (selectorSet == null) return LanguageManager.GetString("LocNotFound");

            var candidates = new List<LocatorData>();
            AddIfValid(candidates, selectorSet.CustomAttribute);
            AddIfValid(candidates, selectorSet.Id);
            AddIfValid(candidates, selectorSet.AriaLabel);
            AddIfValid(candidates, selectorSet.Name);
            AddIfValid(candidates, selectorSet.Placeholder);
            AddIfValid(candidates, selectorSet.Alt);
            AddIfValid(candidates, selectorSet.Text);
            AddIfValid(candidates, selectorSet.Title);
            AddIfValid(candidates, selectorSet.Css);
            AddIfValid(candidates, selectorSet.XpathRelative);
            AddIfValid(candidates, selectorSet.XpathAbsolute);

            if (candidates.Count == 0) return LanguageManager.GetString("LocAmbiguous");

            var bestLocator = candidates.OrderByDescending(c => c.Confidence).First();
            return bestLocator.Value!.Replace("\"", "'");
        }

        private static void AddIfValid(List<LocatorData> list, LocatorData? locator)
        {
            if (locator != null && !string.IsNullOrWhiteSpace(locator.Value) && locator.Confidence >= 0)
                list.Add(locator);
        }
    }
}