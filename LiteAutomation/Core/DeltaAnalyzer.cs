using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LiteAutomation.DTOs;
using LiteAutomation.Enums;
using LiteTools.Core.Languages;
using LiteAutomation.Core.Security;

namespace LiteAutomation.Core
{
    public class DeltaAnalyzer
    {
        public List<AutomationIntent> Analyze(List<MainStepDto> steps, GeneratorConfig config)
        {
            var rawIntents = new List<AutomationIntent>();

            // 🚀 MAQUINA DE ESTADOS: Agrupa as interações por fronteira de página (URLs)
            var mapper = new StateMapper();
            var states = mapper.MapStates(steps);

            for (int i = 0; i < steps.Count; i++)
            {
                var mainStep = steps[i];
                if (!mainStep.IsActive || mainStep.PendingConfirmation) continue;
                int stepIdx = mainStep.StepIndex ?? (i + 1);

                // Recupera o estado (página) que engloba este passo para validar ambiguidade
                var currentState = states.FirstOrDefault(s => s.Steps.Contains(mainStep));

                // 1. A ÂNCORA DO PRINT
                config.LocatorOverrides.TryGetValue($"{stepIdx}.0", out string overrideLoc0);
                string rawLoc0 = ExtractRawLocator(overrideLoc0, null, null);
                if (string.IsNullOrEmpty(rawLoc0) || rawLoc0.Contains("VAZIO")) rawLoc0 = "css=body";

                rawIntents.Add(new AutomationIntent
                {
                    Type = IntentType.AssertVisible,
                    IsNewStepHeader = true,
                    StepId = $"{stepIdx}.0",
                    TargetLocator = rawLoc0,
                    // 🚀 LGPD: Sanitiza descrições que possam conter dados digitados
                    StepDescription = string.IsNullOrWhiteSpace(mainStep.StepName) ? $"{LanguageManager.GetString("LogStep")} {stepIdx}" : PIISanitizer.Sanitize(mainStep.StepName),
                    FriendlyErrorMessage = string.Format(LanguageManager.GetString("ErrValidationFailed"), stepIdx),
                    Diagnostics = LanguageManager.GetString("DiagEvidenceValidation")
                });

                // 2. AS INTERAÇÕES LIMPAS
                var cleanTrail = GetCleanInteractionTrail(mainStep.InteractionTrail);

                if (!mainStep.IsEvidenceOnly && cleanTrail.Count > 0)
                {
                    int interactionIndex = 1;
                    foreach (var interaction in cleanTrail)
                    {
                        string displayStep = $"{stepIdx}.{interactionIndex}";
                        string action = interaction.InteractionType?.ToLower() ?? "unknown";

                        // 🚀 LGPD DOUBLE CHECK: Sanitiza o valor extraído antes de ir para o código
                        string value = PIISanitizer.Sanitize(interaction.Value ?? "");
                        interaction.VisibleText = PIISanitizer.Sanitize(interaction.VisibleText ?? "");

                        // 🚀 ACESSO O(1) DIRETO NA GAVETA DO EVENTO
                        var bidi = interaction.WebDriverBiDi?.ElementData;

                        // Fake temporário apenas para a classe de Diagnósticos não quebrar
                        var pseudoCapturedData = new CapturedDataDto { Uia = interaction.Uia, AxTree = interaction.AxTree, WebDriverBiDi = interaction.WebDriverBiDi };

                        config.LocatorOverrides.TryGetValue(displayStep, out string overrideLoc);
                        string rawLocator = ExtractRawLocator(overrideLoc, bidi?.SelectorSet, interaction);

                        // 🚀 VERIFICAÇÃO DE AMBIGUIDADE O(N) EM MEMÓRIA
                        bool isAmbiguous = CheckAmbiguidade(rawLocator, currentState);
                        if (isAmbiguous && pseudoCapturedData.WebDriverBiDi != null)
                        {
                            pseudoCapturedData.WebDriverBiDi.QualityFlags ??= new List<string>();
                            if (!pseudoCapturedData.WebDriverBiDi.QualityFlags.Contains("WARNING_AMBIGUOUS_LOCATOR"))
                                pseudoCapturedData.WebDriverBiDi.QualityFlags.Add("WARNING_AMBIGUOUS_LOCATOR");
                        }

                        string diagnostics = pseudoCapturedData.WebDriverBiDi != null || pseudoCapturedData.Uia != null ? LocatorEngine.GetDiagnostics(pseudoCapturedData, config.Strategy) : "";

                        string rawLocLower = rawLocator.Trim().ToLower();
                        bool isGhostElement = rawLocLower == "body" || rawLocLower == "/html/body" || rawLocLower == "//body" || rawLocLower == "css=body" || rawLocLower == "by.tagname(\"body\")";

                        var intent = new AutomationIntent { StepId = displayStep, TargetLocator = rawLocator, Diagnostics = diagnostics };

                        if (action == "click")
                        {
                            if (isGhostElement) { intent.Type = IntentType.Blur; intent.TargetLocator = "By.TagName(\"body\")"; intent.FriendlyErrorMessage = string.Format(LanguageManager.GetString("ErrBlurFailed"), displayStep); }
                            else { intent.Type = IntentType.Click; intent.FriendlyErrorMessage = string.Format(LanguageManager.GetString("ErrClickFailed"), displayStep); }
                        }
                        else if (action == "change" || action == "input" || action == "fill") { intent.Type = IntentType.InputText; intent.Value = value; intent.FriendlyErrorMessage = string.Format(LanguageManager.GetString("ErrInputFailed"), displayStep); }
                        else if (action.StartsWith("keypress_")) { intent.Type = IntentType.KeyPress; intent.Key = action.Replace("keypress_", ""); intent.FriendlyErrorMessage = string.Format(LanguageManager.GetString("ErrKeyFailed"), displayStep); }
                        else { intent.Type = IntentType.Unknown; }

                        rawIntents.Add(intent);
                        interactionIndex++;
                    }
                }
            }

            return rawIntents.Where(i => i.Type != IntentType.Unknown).ToList();
        }

        public static List<InteractionDto> GetCleanInteractionTrail(List<InteractionDto> rawTrail)
        {
            if (rawTrail == null) return new List<InteractionDto>();
            var clean = new List<InteractionDto>();

            for (int i = 0; i < rawTrail.Count; i++)
            {
                var current = rawTrail[i];
                string action = current.InteractionType?.ToLower() ?? "";

                // EXTERMINA EVENTOS DO SISTEMA
                if (action == "focus" || action == "blur" || action == "mouseover" || action == "mouseenter" || action == "mouseleave")
                    continue;

                // 🚀 DEDUPLICAÇÃO DE DIGITAÇÃO CONTÍNUA (Ignora se for keypress como tab/enter, eles são mantidos separados)
                if (action == "change" || action == "input" || action == "fill")
                {
                    var lastAdded = clean.LastOrDefault();
                    if (lastAdded != null && (lastAdded.InteractionType?.ToLower() == "change" || lastAdded.InteractionType?.ToLower() == "input" || lastAdded.InteractionType?.ToLower() == "fill"))
                    {
                        bool isSameElement = (!string.IsNullOrEmpty(current.ElementId) && current.ElementId == lastAdded.ElementId) ||
                                             (!string.IsNullOrEmpty(current.BoundingBox) && current.BoundingBox == lastAdded.BoundingBox);

                        if (isSameElement)
                        {
                            if (!string.IsNullOrEmpty(current.Value)) lastAdded.Value = current.Value;
                            if (!string.IsNullOrEmpty(current.VisibleText)) lastAdded.VisibleText = current.VisibleText;
                            continue;
                        }
                    }
                }

                // DEDUPLICAÇÃO DE CLIQUE ANTES DA DIGITAÇÃO NO MESMO CAMPO
                if (action == "click")
                {
                    var nextAction = rawTrail.Skip(i + 1).FirstOrDefault(x =>
                        x.InteractionType?.ToLower() != "focus" &&
                        x.InteractionType?.ToLower() != "blur" &&
                        x.InteractionType?.ToLower() != "mouseover");

                    if (nextAction != null)
                    {
                        string nextType = nextAction.InteractionType?.ToLower() ?? "";
                        if (nextType == "change" || nextType == "input" || nextType == "fill" || nextType.StartsWith("keypress_"))
                        {
                            bool isSameElement = (!string.IsNullOrEmpty(current.ElementId) && current.ElementId == nextAction.ElementId) ||
                                                 (!string.IsNullOrEmpty(current.BoundingBox) && current.BoundingBox == nextAction.BoundingBox);

                            if (isSameElement) continue;
                        }
                    }
                }

                clean.Add(current);
            }
            return clean;
        }

        private string ExtractRawLocator(string overrideCode, SelectorSetDto? fallbackSet, InteractionDto? interaction)
        {
            if (!string.IsNullOrWhiteSpace(overrideCode))
            {
                var match = Regex.Match(overrideCode, "\"(.*?)\"");
                if (match.Success)
                {
                    string val = match.Groups[1].Value;
                    if (val.StartsWith("xpath=")) return val.Substring(6);
                    if (val.StartsWith("css=")) return val.Substring(4);
                    return val;
                }
                string cleanOverride = overrideCode.Contains("->") ? overrideCode.Split(new[] { "->" }, StringSplitOptions.None).Last().Trim() : overrideCode;
                return cleanOverride.StartsWith("xpath=") ? cleanOverride.Substring(6) : (cleanOverride.StartsWith("css=") ? cleanOverride.Substring(4) : cleanOverride);
            }

            string bestLoc = LocatorEngine.GetBestRawLocator(fallbackSet);
            if (string.IsNullOrEmpty(bestLoc) || bestLoc.Contains("AMBÍGUOS") || bestLoc.Contains("AMBIGUOUS") || bestLoc.Contains("NOT FOUND"))
            {
                if (interaction != null)
                {
                    if (!string.IsNullOrWhiteSpace(interaction.ElementId)) return $"css=#{interaction.ElementId}";

                    // Fallback para XPath Absolute do Bidi se não houver um custom id
                    string bidiXpath = interaction.WebDriverBiDi?.ElementData?.SelectorSet?.XpathAbsolute?.Value;
                    if (!string.IsNullOrWhiteSpace(bidiXpath)) return $"xpath={bidiXpath}";

                    if (!string.IsNullOrWhiteSpace(interaction.VisibleText)) { string safeText = interaction.VisibleText.Replace("\"", "\\\"").Replace("'", "\\'"); return $"xpath=//*[normalize-space(text())='{safeText}']"; }
                    if (!string.IsNullOrWhiteSpace(interaction.Classes)) { string cleanClasses = string.Join(".", interaction.Classes.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)); return $"css={interaction.TagName}.{cleanClasses}"; }
                    if (!string.IsNullOrWhiteSpace(interaction.TagName)) return $"css={interaction.TagName}";
                }
                return "css=body";
            }
            return bestLoc;
        }

        /// <summary>
        /// Varre a árvore do DOM visível mapeada na tela para garantir que o seletor gerado é único.
        /// Substitui a verificação pesada do navegador por uma contagem em memória (O(N)).
        /// </summary>
        private bool CheckAmbiguidade(string rawLocator, PageState state)
        {
            if (state == null || state.BaseVisibleElements == null || state.BaseVisibleElements.Count == 0) return false;
            if (string.IsNullOrWhiteSpace(rawLocator) || rawLocator.Contains("body")) return false;

            // Limpa formatação externa para realizar um `Contains` limpo no DOM espelhado.
            string cleanLoc = rawLocator.Replace("css=", "").Replace("xpath=", "").Replace("By.CssSelector(", "").Replace("By.XPath(", "").Replace("By.Id(", "").Replace("\")", "").Replace("\"", "").Replace("'", "").Trim();
            if (string.IsNullOrEmpty(cleanLoc)) return false;

            int matchCount = 0;

            void Traverse(List<VisibleElementDto> nodes)
            {
                if (matchCount > 1) return; // Fast exit (Ambíguo)
                foreach (var node in nodes)
                {
                    var bidiSet = node.CapturedData?.WebDriverBiDi?.ElementData?.SelectorSet;
                    if (bidiSet != null)
                    {
                        bool isMatch = false;

                        if (!string.IsNullOrEmpty(bidiSet.CustomAttribute?.Value) && bidiSet.CustomAttribute.Value.Replace("\"", "'").Contains(cleanLoc)) isMatch = true;
                        else if (!string.IsNullOrEmpty(bidiSet.Id?.Value) && bidiSet.Id.Value.Contains(cleanLoc)) isMatch = true;
                        else if (!string.IsNullOrEmpty(bidiSet.Css?.Value) && bidiSet.Css.Value.Replace("\"", "'").Contains(cleanLoc)) isMatch = true;
                        else if (!string.IsNullOrEmpty(bidiSet.XpathRelative?.Value) && bidiSet.XpathRelative.Value.Replace("\"", "'").Contains(cleanLoc)) isMatch = true;
                        else if (!string.IsNullOrEmpty(bidiSet.XpathAbsolute?.Value) && bidiSet.XpathAbsolute.Value.Replace("\"", "'").Contains(cleanLoc)) isMatch = true;
                        else if (!string.IsNullOrEmpty(bidiSet.Name?.Value) && bidiSet.Name.Value.Contains(cleanLoc)) isMatch = true;

                        if (isMatch) matchCount++;
                    }

                    if (node.Children != null && node.Children.Count > 0)
                        Traverse(node.Children);
                }
            }

            Traverse(state.BaseVisibleElements);
            return matchCount > 1; // Retorna true apenas se houver colisões diretas
        }
    }
}