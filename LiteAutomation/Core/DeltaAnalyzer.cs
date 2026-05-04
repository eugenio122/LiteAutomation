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
            string currentUrl = "";
            bool isFirstNavigation = true;

            for (int i = 0; i < steps.Count; i++)
            {
                var mainStep = steps[i];

                rawIntents.Add(new AutomationIntent
                {
                    Type = IntentType.Unknown,
                    IsNewStepHeader = true,
                    StepId = $"{mainStep.StepIndex}.0",
                    StepDescription = string.IsNullOrWhiteSpace(mainStep.StepName) ? $"{LanguageManager.GetString("LogStep")} {mainStep.StepIndex}" : mainStep.StepName
                });

                if (mainStep.IsEvidenceOnly || mainStep.MicroSteps == null || mainStep.MicroSteps.Count == 0)
                {
                    config.LocatorOverrides.TryGetValue($"{mainStep.StepIndex}.1", out string overrideLoc);
                    string rawLoc = ExtractRawLocator(overrideLoc, null);

                    if (string.IsNullOrEmpty(rawLoc) || rawLoc.Contains("VAZIO"))
                        rawLoc = "By.TagName(\"body\")";

                    rawIntents.Add(new AutomationIntent
                    {
                        Type = IntentType.AssertVisible,
                        StepId = $"{mainStep.StepIndex}.1",
                        TargetLocator = rawLoc,
                        Diagnostics = LanguageManager.GetString("DiagEvidenceValidation"),
                        FriendlyErrorMessage = string.Format(LanguageManager.GetString("ErrValidationFailed"), mainStep.StepIndex)
                    });
                    continue;
                }

                string mainUrl = mainStep.MicroSteps?.FirstOrDefault()?.CapturedData?.WebDriverBiDi?.ElementData?.Url ?? "";
                if (!string.IsNullOrWhiteSpace(mainUrl) && mainUrl != currentUrl && !mainUrl.StartsWith("chrome"))
                {
                    if (isFirstNavigation)
                    {
                        rawIntents.Add(new AutomationIntent { Type = IntentType.NavigateToUrl, Value = mainUrl, FriendlyErrorMessage = string.Format(LanguageManager.GetString("ErrCriticalNavigation"), mainUrl) });
                        isFirstNavigation = false;
                    }
                    else if (Uri.TryCreate(mainUrl, UriKind.Absolute, out Uri uriResult))
                    {
                        rawIntents.Add(new AutomationIntent { Type = IntentType.WaitUrlChange, Value = uriResult.AbsolutePath, Diagnostics = LanguageManager.GetString("DiagPageTransition"), FriendlyErrorMessage = LanguageManager.GetString("ErrNavigationTimeout") });
                    }
                    currentUrl = mainUrl;
                }

                if (mainStep.MicroSteps != null)
                {
                    int microIndex = 1;
                    foreach (var micro in mainStep.MicroSteps)
                    {
                        string displayStep = micro.StepId ?? $"{mainStep.StepIndex}.{microIndex}";
                        string action = micro.ActionType?.ToLower() ?? "unknown";
                        var bidi = micro.CapturedData?.WebDriverBiDi?.ElementData;
                        string value = bidi?.Value ?? "";

                        config.LocatorOverrides.TryGetValue(displayStep, out string overrideLoc);
                        string rawLocator = ExtractRawLocator(overrideLoc, bidi?.SelectorSet);
                        string diagnostics = LocatorEngine.GetDiagnostics(micro, config.Strategy);

                        // 🚀 REGRAS DE GHOST ELEMENT EXATAS (Impede que XPaths longos ou botões genéricos virem Blur)
                        string rawLocLower = rawLocator.Trim().ToLower();
                        bool isGhostElement = rawLocLower == "body" ||
                                              rawLocLower == "/html/body" ||
                                              rawLocLower == "//body" ||
                                              rawLocLower == "by.tagname(\"body\")" ||
                                              rawLocLower == "page.locator(\"body\")";

                        var intent = new AutomationIntent { StepId = displayStep, TargetLocator = rawLocator, Diagnostics = diagnostics };

                        if (action == "click")
                        {
                            if (isGhostElement)
                            {
                                intent.Type = IntentType.Blur; intent.TargetLocator = "By.TagName(\"body\")"; intent.FriendlyErrorMessage = string.Format(LanguageManager.GetString("ErrBlurFailed"), displayStep);
                            }
                            else
                            {
                                intent.Type = IntentType.Click; intent.FriendlyErrorMessage = string.Format(LanguageManager.GetString("ErrClickFailed"), displayStep);
                            }
                        }
                        else if (action == "mouseover" || action == "mouseenter" || action == "hover")
                        {
                            intent.Type = IntentType.Hover; intent.FriendlyErrorMessage = string.Format(LanguageManager.GetString("ErrHoverGeneric"), displayStep);
                        }
                        else if (action == "change" || action == "input" || action == "fill")
                        {
                            intent.Type = IntentType.InputText; intent.Value = value; intent.FriendlyErrorMessage = string.Format(LanguageManager.GetString("ErrInputFailed"), displayStep);
                        }
                        else if (action.StartsWith("keypress_"))
                        {
                            intent.Type = IntentType.KeyPress; intent.Key = action.Replace("keypress_", ""); intent.FriendlyErrorMessage = string.Format(LanguageManager.GetString("ErrKeyFailed"), displayStep);
                        }
                        else if (action == "scroll")
                        {
                            intent.Type = IntentType.ScrollTo; intent.FriendlyErrorMessage = string.Format(LanguageManager.GetString("ErrScrollFailed"), displayStep);
                        }
                        else { intent.Type = IntentType.Unknown; }

                        rawIntents.Add(intent);
                        microIndex++;
                    }
                }
            }

            var cleanIntents = new List<AutomationIntent>();
            foreach (var intent in rawIntents)
            {
                if (intent.Type == IntentType.Unknown && !intent.IsNewStepHeader) continue;
                cleanIntents.Add(intent);
            }

            return cleanIntents;
        }

        private string ExtractRawLocator(string overrideCode, SelectorSetDto? fallbackSet)
        {
            if (string.IsNullOrWhiteSpace(overrideCode)) return LocatorEngine.GetBestRawLocator(fallbackSet) ?? "";

            // 🚀 PARSER BLINDADO: Entende com aspas, sem aspas e até com a seta "->" do Painel SDET!
            var match = Regex.Match(overrideCode, "\"(.*?)\"");
            if (match.Success)
            {
                string val = match.Groups[1].Value;
                if (val.StartsWith("xpath=")) return val.Substring(6);
                if (val.StartsWith("css=")) return val.Substring(4);
                return val;
            }

            string cleanOverride = overrideCode;
            if (cleanOverride.Contains("->"))
            {
                cleanOverride = cleanOverride.Split(new[] { "->" }, StringSplitOptions.None).Last().Trim();
            }

            if (cleanOverride.StartsWith("xpath=")) return cleanOverride.Substring(6);
            if (cleanOverride.StartsWith("css=")) return cleanOverride.Substring(4);

            return cleanOverride;
        }
    }
}