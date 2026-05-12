using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using LiteAutomation.DTOs;
using LiteAutomation.Enums;
using LiteTools.Core.Languages;

namespace LiteAutomation.Core
{
    public static class LocatorEngine
    {
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
                sb.AppendLine($"{indent}{string.Format(LanguageManager.GetString("DiagQualityFlags"), string.Join(", ", allFlags.Distinct()))}");
            }

            if (uiaFlags.Contains("A11Y_GAP_WARNING") || uiaFlags.Contains("MISSING_ACCESSIBLE_NAME"))
            {
                sb.AppendLine($"{indent}{LanguageManager.GetString("DiagSemanticAlert")}");
                sb.AppendLine($"{indent}{LanguageManager.GetString("DiagSemanticSuggestion")}");
            }

            // O novo motor de ambiguidade no DeltaAnalyzer joga a flag "WARNING_AMBIGUOUS_LOCATOR" aqui
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

            // Agora, a unicidade e o cálculo de ambiguidade ficam no DeltaAnalyzer (Score < 0 não existe mais)
            if (candidates.Count == 0) return LanguageManager.GetString("LocAmbiguous");

            var bestLocator = candidates.OrderByDescending(c => c.Confidence).First();

            // Limpa escapes, lixos visuais e uniformiza aspas simples
            return CleanVisualGarbage(bestLocator.Value!);
        }

        private static void AddIfValid(List<LocatorData> list, LocatorData? locator)
        {
            // Note que não verificamos mais "locator.Confidence >= 0" porque delegamos o motor de ambiguidade O(N)
            if (locator != null && !string.IsNullOrWhiteSpace(locator.Value))
                list.Add(locator);
        }

        /// <summary>
        /// Aplica a remoção de escapes e um Trim visual nos localizadores que dependem de texto,
        /// ignorando ícones e caracteres espúrios das extremidades (ex: "> Entrar" vira "Entrar").
        /// </summary>
        private static string CleanVisualGarbage(string rawLocator)
        {
            if (string.IsNullOrWhiteSpace(rawLocator)) return rawLocator;

            // 1. Uniformiza aspas simples e remove escapes JSON antigos
            string clean = rawLocator.Replace("\\\"", "'").Replace("\"", "'").Replace("\\'", "'");

            // 2. Limpeza Cirúrgica para XPath baseados em texto
            if (clean.Contains("text()=") || clean.Contains("contains(text()"))
            {
                // Busca tudo que estiver dentro de aspas simples ('texto alvo')
                var match = Regex.Match(clean, @"text\(\)[=,]\s*'([^']+)'");
                if (match.Success)
                {
                    string innerText = match.Groups[1].Value;

                    // Remove caracteres não-alfanuméricos e não-acentuados das bordas (mantém espaços no meio)
                    string cleanedText = Regex.Replace(innerText, @"^[^a-zA-Z0-9À-ÿ]+|[^a-zA-Z0-9À-ÿ]+$", "").Trim();

                    if (!string.IsNullOrEmpty(cleanedText) && cleanedText != innerText)
                    {
                        clean = clean.Replace($"'{innerText}'", $"'{cleanedText}'");
                    }
                }
            }

            return clean;
        }
    }
}