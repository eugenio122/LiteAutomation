using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LiteAutomation.DTOs;
using LiteAutomation.Enums;
using LiteTools.Core.Languages;

namespace LiteAutomation.Core
{
    public class DeltaAnalyzer
    {
        public List<AutomationIntent> Analyze(List<MainStepDto> steps, GeneratorConfig config)
        {
            var rawIntents = new List<AutomationIntent>();

            for (int i = 0; i < steps.Count; i++)
            {
                var mainStep = steps[i];
                if (!mainStep.IsActive || mainStep.PendingConfirmation) continue;
                int stepIdx = mainStep.StepIndex ?? (i + 1);

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
                    StepDescription = string.IsNullOrWhiteSpace(mainStep.StepName) ? $"{LanguageManager.GetString("LogStep")} {stepIdx}" : mainStep.StepName,
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
                        string value = interaction.Value ?? "";

                        // 🚀 ACESSO O(1) DIRETO NA GAVETA DO EVENTO
                        var bidi = interaction.WebDriverBiDi?.ElementData;

                        // Fake temporário apenas para a classe de Diagnósticos não quebrar
                        var pseudoCapturedData = new CapturedDataDto { Uia = interaction.Uia, AxTree = interaction.AxTree, WebDriverBiDi = interaction.WebDriverBiDi };

                        config.LocatorOverrides.TryGetValue(displayStep, out string overrideLoc);
                        string rawLocator = ExtractRawLocator(overrideLoc, bidi?.SelectorSet, interaction);

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
    }
}