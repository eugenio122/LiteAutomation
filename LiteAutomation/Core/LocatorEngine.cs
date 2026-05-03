using System.Collections.Generic;
using System.Linq;
using System.Text;
using LiteAutomation.DTOs;
using LiteAutomation.Enums;

namespace LiteAutomation.Core
{
    public static class LocatorEngine
    {
        // =====================================================================
        // O MOTOR DE DIAGNÓSTICO (Comentários Inteligentes no Preview/Report)
        // =====================================================================
        public static string GetDiagnostics(MicroStepDto micro, AutomationStrategy strategy, string indent = "            ")
        {
            // O Report detalhado de seletores só faz sentido na estratégia Smart Selector (Web)
            if (strategy != AutomationStrategy.Smart_Selector) return "";

            var sb = new StringBuilder();
            var uiaFlags = micro.CapturedData?.Uia?.QualityFlags ?? new List<string>();
            var bidiFlags = micro.CapturedData?.WebDriverBiDi?.QualityFlags ?? new List<string>();

            var allFlags = new List<string>();
            allFlags.AddRange(uiaFlags);
            allFlags.AddRange(bidiFlags);

            if (allFlags.Count > 0)
            {
                sb.AppendLine($"{indent}// 🚩 Quality Flags: {string.Join(", ", allFlags)}");
            }

            // Avalia o lado Semântico
            if (uiaFlags.Contains("A11Y_GAP_WARNING") || uiaFlags.Contains("MISSING_ACCESSIBLE_NAME"))
            {
                sb.AppendLine($"{indent}// ⚠️ ALERTA SEMÂNTICO: O elemento não possui acessibilidade adequada.");
                sb.AppendLine($"{indent}// 💡 Sugestão Automática: O Smart Selector priorizou atributos DOM confiáveis.");
            }

            // Avalia o lado DOM
            if (bidiFlags.Contains("WARNING_BRITTLE_LOCATOR") || bidiFlags.Contains("WARNING_AMBIGUOUS_LOCATOR") || bidiFlags.Count == 0)
            {
                sb.AppendLine($"{indent}// ⚠️ ALERTA DOM: A estrutura HTML do elemento é frágil ou ambígua.");
                sb.AppendLine($"{indent}// 💡 Sugestão Automática: O Smart Selector priorizou as tags de acessibilidade (ARIA/Role).");
            }

            return sb.ToString().TrimEnd('\r', '\n');
        }

        // =====================================================================
        // RESOLUÇÃO DE FALLBACK E POLIGLOTISMO
        // =====================================================================
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
            if (selectorSet == null) return "/* SELETOR NÃO ENCONTRADO */";

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

            if (candidates.Count == 0) return "/* TODOS OS SELETORES ESTÃO AMBÍGUOS OU VAZIOS */";

            var bestLocator = candidates.OrderByDescending(c => c.Confidence).First();

            // 🚀 CORREÇÃO PRESERVADA: Troca universal de aspas duplas por aspas simples!
            return bestLocator.Value!.Replace("\"", "'");
        }

        private static void AddIfValid(List<LocatorData> list, LocatorData? locator)
        {
            if (locator != null && !string.IsNullOrWhiteSpace(locator.Value) && locator.Confidence >= 0)
                list.Add(locator);
        }
    }
}