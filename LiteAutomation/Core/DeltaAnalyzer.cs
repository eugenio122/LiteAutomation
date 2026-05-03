using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LiteAutomation.DTOs;
using LiteAutomation.Enums;

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

                // 1. CABEÇALHO DO PASSO
                rawIntents.Add(new AutomationIntent
                {
                    Type = IntentType.Unknown,
                    IsNewStepHeader = true,
                    StepId = $"{mainStep.StepIndex}.0",
                    StepDescription = string.IsNullOrWhiteSpace(mainStep.StepName) ? $"Passo {mainStep.StepIndex}" : mainStep.StepName
                });

                // 🚀 NOVA LÓGICA DE EVIDÊNCIA (O GERADOR DO "ENTÃO")
                if (mainStep.IsEvidenceOnly || mainStep.MicroSteps == null || mainStep.MicroSteps.Count == 0)
                {
                    config.LocatorOverrides.TryGetValue($"{mainStep.StepIndex}.1", out string overrideLoc);
                    string rawLoc = ExtractRawLocator(overrideLoc, null);
                    if (string.IsNullOrEmpty(rawLoc) || rawLoc.Contains("VAZIO")) rawLoc = "By.TagName(\"body\")";

                    rawIntents.Add(new AutomationIntent
                    {
                        Type = IntentType.AssertVisible,
                        StepId = $"{mainStep.StepIndex}.1",
                        TargetLocator = rawLoc,
                        Diagnostics = "📸 Evidência/Validação: Passo de validação estática inserido pelo testador.",
                        FriendlyErrorMessage = $"🛑 Falha na Validação (Passo {mainStep.StepIndex}): A tela de evidência não foi carregada."
                    });
                    continue; // Pula o resto da análise matemática, pois não há ações de usuário aqui.
                }

                // 2. DELTA ENGINE 1: NAVEGAÇÃO
                string mainUrl = mainStep.MicroSteps?.FirstOrDefault()?.CapturedData?.WebDriverBiDi?.ElementData?.Url ?? "";
                if (!string.IsNullOrWhiteSpace(mainUrl) && mainUrl != currentUrl && !mainUrl.StartsWith("chrome"))
                {
                    if (isFirstNavigation)
                    {
                        rawIntents.Add(new AutomationIntent { Type = IntentType.NavigateToUrl, Value = mainUrl, FriendlyErrorMessage = $"🛑 Falha Crítica: Não foi possível navegar para '{mainUrl}'." });
                        isFirstNavigation = false;
                    }
                    else if (Uri.TryCreate(mainUrl, UriKind.Absolute, out Uri uriResult))
                    {
                        rawIntents.Add(new AutomationIntent { Type = IntentType.WaitUrlChange, Value = uriResult.AbsolutePath, Diagnostics = "⏱️ Transição de página detectada", FriendlyErrorMessage = "🛑 Falha de Navegação: A tela demorou a carregar." });
                    }
                    currentUrl = mainUrl;
                }

                // 🧠 DELTA ENGINE 2: O COMPARADOR DE CONTEXTO
                var beforeElements = mainStep.ObservedContext?.VisibleElements ?? new List<VisibleElementDto>();
                var afterElements = (i + 1 < steps.Count) ? steps[i + 1].ObservedContext?.VisibleElements ?? new List<VisibleElementDto>() : new List<VisibleElementDto>();

                var beforeMap = beforeElements.Where(e => !string.IsNullOrWhiteSpace(e.CapturedData?.WebDriverBiDi?.ElementData?.SelectorSet?.XpathAbsolute?.Value)).ToDictionary(e => e.CapturedData!.WebDriverBiDi!.ElementData!.SelectorSet!.XpathAbsolute!.Value);
                var afterMap = afterElements.Where(e => !string.IsNullOrWhiteSpace(e.CapturedData?.WebDriverBiDi?.ElementData?.SelectorSet?.XpathAbsolute?.Value)).ToDictionary(e => e.CapturedData!.WebDriverBiDi!.ElementData!.SelectorSet!.XpathAbsolute!.Value);

                int disappearedCount = beforeMap.Keys.Count(k => !afterMap.ContainsKey(k));
                var newElementsAppeared = afterMap.Values.Where(e => !beforeMap.ContainsKey(e.CapturedData!.WebDriverBiDi!.ElementData!.SelectorSet!.XpathAbsolute!.Value)).ToList();
                int appearedCount = newElementsAppeared.Count;

                // 3. MAPEAMENTO DE MICROSTEPS
                if (mainStep.MicroSteps != null)
                {
                    int microIndex = 1;
                    foreach (var micro in mainStep.MicroSteps)
                    {
                        string displayStep = micro.StepId ?? $"{mainStep.StepIndex}.{microIndex}";
                        string action = micro.ActionType?.ToLower() ?? "unknown";
                        var bidi = micro.CapturedData?.WebDriverBiDi?.ElementData;
                        var uia = micro.CapturedData?.Uia?.ElementData;
                        string value = bidi?.Value ?? "";

                        config.LocatorOverrides.TryGetValue(displayStep, out string overrideLoc);
                        string rawLocator = ExtractRawLocator(overrideLoc, bidi?.SelectorSet);
                        string diagnostics = LocatorEngine.GetDiagnostics(micro, config.Strategy);

                        string role = bidi?.Semantic?.Role?.Value?.ToLower() ?? uia?.Semantic?.Role?.Value?.ToLower() ?? "";
                        string name = bidi?.Semantic?.AccessibleName?.Value ?? uia?.Semantic?.AccessibleName?.Value ?? bidi?.SelectorSet?.Text?.Value ?? "";

                        bool isGhostElement = role == "document" || role == "banner" || role == "main" ||
                                              role == "contentinfo" || role == "region" || role == "generic" ||
                                              rawLocator.ToLower().Contains("body") || rawLocator.ToLower().Contains("html") ||
                                              (name.Length >= 35 && role != "button" && role != "link" && role != "menuitem" && role != "combobox");

                        var intent = new AutomationIntent { StepId = displayStep, TargetLocator = rawLocator, Diagnostics = diagnostics };

                        if (action == "click")
                        {
                            if (isGhostElement)
                            {
                                if (appearedCount > 0 && disappearedCount == 0)
                                {
                                    intent.Type = IntentType.Hover; intent.FriendlyErrorMessage = $"🛑 Falha no Passo {displayStep} (Hover): O elemento não apareceu.";
                                }
                                else
                                {
                                    intent.Type = IntentType.Blur; intent.TargetLocator = "By.TagName(\"body\")"; intent.FriendlyErrorMessage = $"🛑 Falha no Passo {displayStep} (Blur): Não foi possível fechar o menu.";
                                }
                            }
                            else
                            {
                                intent.Type = IntentType.Click; intent.FriendlyErrorMessage = $"🛑 Falha no Passo {displayStep} (Clique): O elemento estava bloqueado.";
                            }
                        }
                        else if (action == "mouseover" || action == "mouseenter" || action == "hover")
                        {
                            intent.Type = IntentType.Hover; intent.FriendlyErrorMessage = $"🛑 Falha no Passo {displayStep} (Hover).";
                        }
                        else if (action == "change" || action == "input" || action == "fill")
                        {
                            intent.Type = IntentType.InputText; intent.Value = value; intent.FriendlyErrorMessage = $"🛑 Falha no Passo {displayStep} (Preenchimento).";
                        }
                        else if (action.StartsWith("keypress_"))
                        {
                            intent.Type = IntentType.KeyPress; intent.Key = action.Replace("keypress_", ""); intent.FriendlyErrorMessage = $"🛑 Falha no Passo {displayStep} (Teclado).";
                        }
                        else if (action == "scroll")
                        {
                            intent.Type = IntentType.ScrollTo; intent.FriendlyErrorMessage = $"🛑 Falha no Passo {displayStep} (Scroll).";
                        }
                        else { intent.Type = IntentType.Unknown; }

                        rawIntents.Add(intent);
                        microIndex++;
                    }
                }

                // 4. DELTA ENGINE 3: EFEITO FEEDBACK
                if (appearedCount > 0)
                {
                    var newFeedbacks = newElementsAppeared.Where(e => { var r = e.CapturedData?.Uia?.ElementData?.Semantic?.Role?.Value?.ToLower() ?? e.CapturedData?.WebDriverBiDi?.ElementData?.Semantic?.Role?.Value?.ToLower(); return r == "alert" || r == "dialog" || r == "status" || r == "alerta" || r == "diálogo"; }).ToList();
                    foreach (var alert in newFeedbacks)
                    {
                        string alertLoc = LocatorEngine.GetBestRawLocator(alert.CapturedData?.WebDriverBiDi?.ElementData?.SelectorSet);
                        rawIntents.Add(new AutomationIntent { Type = IntentType.AssertVisible, TargetLocator = alertLoc, FriendlyErrorMessage = "🛑 Validação de Negócio Falhou: A mensagem esperada não apareceu." });
                    }
                    foreach (var afterEl in afterMap.Values)
                    {
                        var xpath = afterEl.CapturedData?.WebDriverBiDi?.ElementData?.SelectorSet?.XpathAbsolute?.Value;
                        if (!string.IsNullOrWhiteSpace(xpath) && beforeMap.TryGetValue(xpath, out var beforeEl) && !beforeEl.IsEnabled && afterEl.IsEnabled)
                        {
                            string btnLoc = LocatorEngine.GetBestRawLocator(afterEl.CapturedData?.WebDriverBiDi?.ElementData?.SelectorSet);
                            rawIntents.Add(new AutomationIntent { Type = IntentType.AssertEnabled, TargetLocator = btnLoc, FriendlyErrorMessage = "🛑 Validação de Regra Falhou: Esperava-se que o elemento ficasse habilitado." });
                        }
                    }
                }
            }

            // 🧹 FILTRO DESDUPLICADOR
            var cleanIntents = new List<AutomationIntent>();
            AutomationIntent lastIntent = null;
            foreach (var intent in rawIntents)
            {
                if (intent.Type == IntentType.Unknown && !intent.IsNewStepHeader) continue;
                if (lastIntent != null && !intent.IsNewStepHeader && intent.Type == lastIntent.Type && (intent.Type == IntentType.Blur || intent.Type == IntentType.Hover)) continue;
                cleanIntents.Add(intent);
                if (!intent.IsNewStepHeader) lastIntent = intent;
            }

            return cleanIntents;
        }

        private string ExtractRawLocator(string overrideCode, SelectorSetDto? fallbackSet)
        {
            if (string.IsNullOrWhiteSpace(overrideCode)) return LocatorEngine.GetBestRawLocator(fallbackSet);
            var match = Regex.Match(overrideCode, "\"(.*?)\"");
            if (match.Success)
            {
                string val = match.Groups[1].Value;
                if (val.StartsWith("xpath=")) return val.Substring(6);
                if (val.StartsWith("css=")) return val.Substring(4);
                return val;
            }
            return overrideCode;
        }
    }
}