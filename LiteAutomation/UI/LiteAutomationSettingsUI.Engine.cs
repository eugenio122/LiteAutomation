using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using LiteAutomation.Core;
using LiteAutomation.Core.Security;
using LiteAutomation.DTOs;
using LiteAutomation.Enums;
using LiteAutomation.Factories;
using LiteAutomation.Diagnostics;
using FastColoredTextBoxNS;
using LiteTools.Core.Languages;

namespace LiteAutomation.UI
{
    public partial class LiteAutomationSettingsUI
    {
        private class LocatorOption
        {
            public string Category { get; set; }
            public string Name { get; set; }
            public string DisplayName { get; set; }
            public string CodeSnippet { get; set; }
            public int Confidence { get; set; }
            public bool IsSemantic { get; set; }
            public override string ToString() => DisplayName;

            public LocatorOption Clone() => (LocatorOption)this.MemberwiseClone();
        }

        private Dictionary<string, List<LocatorOption>> _locatorCache = new Dictionary<string, List<LocatorOption>>();
        private List<MainStepDto> _cachedStepsRef;

        private void PopulateDecisionGrid()
        {
            if (!_workspace.HasData) return;

            if (_workspace.RawSteps != _cachedStepsRef)
            {
                _locatorCache.Clear();
                _cachedStepsRef = _workspace.RawSteps;
            }

            gridLocators.SuspendLayout();
            gridLocators.Rows.Clear();
            _currentConfig.LocatorOverrides.Clear();

            var strategy = (AutomationStrategy)cmbStrategy.SelectedItem;
            var framework = (TestFramework)cmbFramework.SelectedItem;
            bool isPlaywright = framework == TestFramework.Playwright;

            try
            {
                foreach (var main in _workspace.RawSteps)
                {
                    if (!main.IsActive || main.PendingConfirmation)
                        continue;

                    int stepIdx = main.StepIndex ?? 0;
                    var visibleElements = main.ObservedContext?.VisibleElements;

                    // 1. A ÂNCORA DO PRINT (X.0)
                    string displayStep0 = $"{stepIdx}.0";
                    string cacheKey0 = $"{framework}_{displayStep0}";

                    List<LocatorOption> baseOptions0;
                    if (!_locatorCache.TryGetValue(cacheKey0, out baseOptions0))
                    {
                        baseOptions0 = new List<LocatorOption>();
                        string codeSnippet = isPlaywright ? "Page.Locator(\"body\")" : "By.TagName(\"body\")";
                        baseOptions0.Add(new LocatorOption { Category = LanguageManager.GetString("CatSemantic"), Name = LanguageManager.GetString("SdetValidationBody"), CodeSnippet = codeSnippet, Confidence = 100, IsSemantic = true });
                        _locatorCache[cacheKey0] = baseOptions0;
                    }

                    var displayOptions0 = baseOptions0.Select(o => o.Clone()).ToList();
                    foreach (var opt in displayOptions0) opt.DisplayName = $"{LanguageManager.GetString("PrefixSemantic")} ➔ [{opt.Confidence}] {opt.Name}";

                    int rowIndex0 = gridLocators.Rows.Add();
                    var row0 = gridLocators.Rows[rowIndex0];
                    row0.Cells[0].Value = displayStep0;
                    row0.Cells[1].Value = LanguageManager.GetString("SdetEvidence");

                    var comboCell0 = (DataGridViewComboBoxCell)row0.Cells[2];
                    comboCell0.DataSource = displayOptions0;
                    comboCell0.DisplayMember = "DisplayName";
                    comboCell0.ValueMember = "CodeSnippet";
                    comboCell0.Value = displayOptions0[0].CodeSnippet;

                    _currentConfig.LocatorOverrides[displayStep0] = displayOptions0[0].CodeSnippet;

                    // 2. AS INTERAÇÕES
                    var cleanTrail = DeltaAnalyzer.GetCleanInteractionTrail(main.InteractionTrail);

                    if (!main.IsEvidenceOnly && cleanTrail.Count > 0)
                    {
                        int interactionIndex = 1;
                        foreach (var interaction in cleanTrail)
                        {
                            string displayStep = $"{stepIdx}.{interactionIndex}";
                            string cacheKey = $"{framework}_{displayStep}";

                            List<LocatorOption> baseOptions;

                            if (!_locatorCache.TryGetValue(cacheKey, out baseOptions))
                            {
                                baseOptions = new List<LocatorOption>();

                                // 🚀 O(1): ACESSO DIRETO SEM BUSCA NA ÁRVORE!
                                var uia = interaction.Uia?.ElementData;
                                var bidi = interaction.WebDriverBiDi?.ElementData;

                                if (uia != null && !string.IsNullOrWhiteSpace(uia.Semantic?.AccessibleName?.Value))
                                {
                                    string role = uia.Semantic.Role?.Value?.ToLower() ?? LanguageManager.GetString("SdetElement");

                                    // 🚀 LGPD e CLEAN JSON: Higieniza o texto extraído da tela
                                    string rawName = PIISanitizer.Sanitize(uia.Semantic.AccessibleName.Value).Replace("\\\"", "'").Replace("\"", "'").Replace("\n", " ").Replace("\r", "");
                                    string shortName = rawName.Length > 35 ? rawName.Substring(0, 35).Trim() : rawName;

                                    int score = uia.Semantic.AccessibleName.Confidence;

                                    // 🚀 MOTOR DE AMBIGUIDADE (Substitui o antigo Confidence < 0 nativo)
                                    if (IsLocatorAmbiguous(shortName, visibleElements))
                                        score = -Math.Abs(score == 0 ? 80 : score);

                                    string codeSnippet = "";

                                    if (uia.Semantic.AutomationId?.Value == "RootWebArea" || role == "documento" || role == "região")
                                    {
                                        codeSnippet = isPlaywright ? "Page.Locator(\"body\")" : "By.TagName(\"body\")";
                                        baseOptions.Add(new LocatorOption { Category = LanguageManager.GetString("CatSemantic"), Name = LanguageManager.GetString("SdetBgBody"), CodeSnippet = codeSnippet, Confidence = 100, IsSemantic = true });
                                    }
                                    else
                                    {
                                        if (isPlaywright)
                                            codeSnippet = $"Page.GetByRole(AriaRole.{MapToAriaRole(role) ?? "Button"}, new() {{ Name = \"{shortName}\"{(rawName.Length > 35 ? ", Exact = false" : "")} }})";
                                        else
                                            codeSnippet = rawName.Length > 35
                                                ? $"By.XPath(\"//*[contains(@aria-label, '{shortName}') or contains(text(), '{shortName}')]\")"
                                                : $"By.XPath(\"//*[@aria-label='{rawName}' or text()='{rawName}']\")";

                                        baseOptions.Add(new LocatorOption { Category = LanguageManager.GetString("CatSemantic"), Name = $"{role} '{shortName}'", CodeSnippet = codeSnippet, Confidence = score, IsSemantic = true });
                                    }
                                }

                                if (bidi?.SelectorSet != null)
                                {
                                    AddOptionFromBidi(baseOptions, bidi.SelectorSet, isPlaywright, visibleElements);
                                    AddOptionIfValid(baseOptions, bidi.SelectorSet.CustomAttribute, LanguageManager.GetString("SdetCustomAttr"), isPlaywright, "Page.Locator(\"{0}\")", "By.CssSelector(\"{0}\")", false, LanguageManager.GetString("CatDom"), visibleElements);
                                    AddOptionIfValid(baseOptions, bidi.SelectorSet.Id, "ID", isPlaywright, "Page.Locator(\"{0}\")", "By.CssSelector(\"{0}\")", false, LanguageManager.GetString("CatDom"), visibleElements);
                                    AddOptionIfValid(baseOptions, bidi.SelectorSet.Name, "Name", isPlaywright, "Page.Locator(\"{0}\")", "By.CssSelector(\"{0}\")", false, LanguageManager.GetString("CatDom"), visibleElements);
                                    AddOptionIfValid(baseOptions, bidi.SelectorSet.Text, LanguageManager.GetString("SdetVisibleText"), isPlaywright, "Page.Locator(\"xpath={0}\")", "By.XPath(\"{0}\")", false, LanguageManager.GetString("CatDom"), visibleElements);
                                    AddOptionIfValid(baseOptions, bidi.SelectorSet.Css, "CSS", isPlaywright, "Page.Locator(\"css={0}\")", "By.CssSelector(\"{0}\")", false, LanguageManager.GetString("CatDom"), visibleElements);
                                    AddOptionIfValid(baseOptions, bidi.SelectorSet.XpathRelative, LanguageManager.GetString("SdetRelativeXPath"), isPlaywright, "Page.Locator(\"xpath={0}\")", "By.XPath(\"{0}\")", false, LanguageManager.GetString("CatDom"), visibleElements);
                                    AddOptionIfValid(baseOptions, bidi.SelectorSet.XpathAbsolute, LanguageManager.GetString("SdetAbsoluteXPath"), isPlaywright, "Page.Locator(\"xpath={0}\")", "By.XPath(\"{0}\")", false, LanguageManager.GetString("CatDom"), visibleElements);
                                    AddOptionIfValid(baseOptions, bidi.SelectorSet.AriaLabel, "Aria-Label", isPlaywright, "Page.Locator(\"css={0}\")", "By.CssSelector(\"{0}\")", true, LanguageManager.GetString("CatSemantic"), visibleElements);
                                }

                                if (baseOptions.Count == 0 || !baseOptions.Any(o => o.Confidence >= 80))
                                {
                                    if (!string.IsNullOrWhiteSpace(interaction.ElementId))
                                    {
                                        int score = IsLocatorAmbiguous(interaction.ElementId, visibleElements) ? -85 : 85;
                                        baseOptions.Add(new LocatorOption { Category = LanguageManager.GetString("CatDom"), Name = "ID Nativo do Evento", CodeSnippet = isPlaywright ? $"Page.Locator(\"#{interaction.ElementId}\")" : $"By.Id(\"{interaction.ElementId}\")", Confidence = score, IsSemantic = false });
                                    }

                                    if (!string.IsNullOrWhiteSpace(interaction.VisibleText))
                                    {
                                        // 🚀 LGPD e CLEAN JSON no Texto Nativo
                                        string safeText = PIISanitizer.Sanitize(interaction.VisibleText).Replace("\\\"", "'").Replace("\"", "'");
                                        int score = IsLocatorAmbiguous(safeText, visibleElements) ? -80 : 80;
                                        baseOptions.Add(new LocatorOption { Category = LanguageManager.GetString("CatSemantic"), Name = "Texto Nativo do Evento", CodeSnippet = isPlaywright ? $"Page.Locator(\"text={safeText}\")" : $"By.XPath(\"//*[normalize-space(text())='{safeText}']\")", Confidence = score, IsSemantic = true });
                                    }

                                    if (!string.IsNullOrWhiteSpace(interaction.Classes))
                                    {
                                        string cleanClasses = string.Join(".", interaction.Classes.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
                                        int score = IsLocatorAmbiguous(cleanClasses, visibleElements) ? -65 : 65;
                                        baseOptions.Add(new LocatorOption { Category = LanguageManager.GetString("CatDom"), Name = "CSS Class do Evento", CodeSnippet = isPlaywright ? $"Page.Locator(\"{interaction.TagName}.{cleanClasses}\")" : $"By.CssSelector(\"{interaction.TagName}.{cleanClasses}\")", Confidence = score, IsSemantic = false });
                                    }

                                    string bidiXpath = bidi?.SelectorSet?.XpathAbsolute?.Value;
                                    if (!string.IsNullOrWhiteSpace(bidiXpath))
                                        baseOptions.Add(new LocatorOption { Category = LanguageManager.GetString("CatDom"), Name = $"XPath Absolute BiDi", CodeSnippet = isPlaywright ? $"Page.Locator(\"xpath={bidiXpath}\")" : $"By.XPath(\"{bidiXpath}\")", Confidence = 50, IsSemantic = false });

                                    if (!string.IsNullOrWhiteSpace(interaction.TagName) && baseOptions.Count == 0)
                                        baseOptions.Add(new LocatorOption { Category = LanguageManager.GetString("CatDom"), Name = $"Tag Nativa ({interaction.TagName})", CodeSnippet = isPlaywright ? $"Page.Locator(\"{interaction.TagName}\")" : $"By.TagName(\"{interaction.TagName}\")", Confidence = 10, IsSemantic = false });
                                }

                                if (baseOptions.Count == 0)
                                    baseOptions.Add(new LocatorOption { Category = LanguageManager.GetString("CatDom"), Name = LanguageManager.GetString("SdetUnavailable"), CodeSnippet = isPlaywright ? "Page.Locator(\"/* VAZIO */\")" : "By.XPath(\"/* VAZIO */\")", Confidence = 0, IsSemantic = false });

                                baseOptions = baseOptions.GroupBy(o => o.CodeSnippet).Select(g => g.First()).ToList();
                                _locatorCache[cacheKey] = baseOptions;
                            }

                            var displayOptions = baseOptions.Select(o => o.Clone()).OrderByDescending(o => o.Confidence).ThenByDescending(o => o.IsSemantic).ToList();
                            foreach (var opt in displayOptions) opt.DisplayName = $"{(opt.Category == LanguageManager.GetString("CatSemantic") ? LanguageManager.GetString("PrefixSemantic") : LanguageManager.GetString("PrefixDom"))} ➔ [{opt.Confidence}] {opt.Name}";

                            int rowIndex = gridLocators.Rows.Add();
                            var row = gridLocators.Rows[rowIndex];

                            row.Cells[0].Value = displayStep;
                            row.Cells[1].Value = interaction.InteractionType?.Replace("keypress_", LanguageManager.GetString("SdetKey")) ?? "click";

                            var comboCell = (DataGridViewComboBoxCell)row.Cells[2];
                            comboCell.DataSource = displayOptions;
                            comboCell.DisplayMember = "DisplayName";
                            comboCell.ValueMember = "CodeSnippet";
                            comboCell.Value = displayOptions[0].CodeSnippet;

                            _currentConfig.LocatorOverrides[displayStep] = displayOptions[0].CodeSnippet;
                            interactionIndex++;
                        }
                    }
                }
            }
            catch (Exception ex) { LiteLogger.Error(LanguageManager.GetString("ErrGridPopulate"), ex); }
            finally { gridLocators.ResumeLayout(); }
        }

        private void AddOptionFromBidi(List<LocatorOption> list, SelectorSetDto set, bool isPlaywright, List<VisibleElementDto> visibleElements)
        {
            void TryAdd(LocatorData loc, string name)
            {
                if (loc != null && !string.IsNullOrWhiteSpace(loc.Value))
                {
                    string cleanVal = loc.Value.Replace("\\\"", "'").Replace("\"", "'");
                    string snippet = isPlaywright ? LocatorEngine.GetBestPlaywrightLocator(new SelectorSetDto { Id = new LocatorData { Value = cleanVal } }) : LocatorEngine.GetBestSeleniumLocator(new SelectorSetDto { Id = new LocatorData { Value = cleanVal } });

                    int score = loc.Confidence;
                    if (IsLocatorAmbiguous(cleanVal, visibleElements)) score = -Math.Abs(score == 0 ? 10 : score);

                    list.Add(new LocatorOption { Name = name, CodeSnippet = snippet, Confidence = score, IsSemantic = false, Category = LanguageManager.GetString("CatDom") });
                }
            }
            TryAdd(set.Id, "ID"); TryAdd(set.CustomAttribute, "TestID"); TryAdd(set.Name, "Name");
        }

        private void AddOptionIfValid(List<LocatorOption> options, LocatorData locator, string name, bool isPW, string templatePW, string templateSel, bool isSem, string cat, List<VisibleElementDto> visibleElements)
        {
            if (locator != null && !string.IsNullOrWhiteSpace(locator.Value))
            {
                // Limpeza visual profunda antes de montar o Snippet de Código
                string cleanVal = locator.Value.Replace("\\\"", "'").Replace("\"", "'");
                string codeSnippet = isPW ? string.Format(templatePW, cleanVal) : string.Format(templateSel, cleanVal);

                int score = locator.Confidence;
                if (IsLocatorAmbiguous(cleanVal, visibleElements)) score = -Math.Abs(score == 0 ? 10 : score);

                options.Add(new LocatorOption { Category = cat, Name = name, CodeSnippet = codeSnippet, Confidence = score, IsSemantic = isSem });
            }
        }

        /// <summary>
        /// Motor de Ambiguidade Local (UI)
        /// Navega pelos elementos mapeados no Snapshot da tela (Fronteira de Página) 
        /// e pune o score do LocatorOption caso o seletor apareça mais de uma vez.
        /// </summary>
        private bool IsLocatorAmbiguous(string cleanLoc, List<VisibleElementDto> nodes)
        {
            if (nodes == null || string.IsNullOrWhiteSpace(cleanLoc)) return false;

            cleanLoc = cleanLoc.Replace("css=", "").Replace("xpath=", "").Replace("By.CssSelector(", "").Replace("By.XPath(", "").Replace("By.Id(", "").Replace("\")", "").Replace("\"", "").Replace("'", "").Trim();
            if (string.IsNullOrEmpty(cleanLoc)) return false;

            int matchCount = 0;

            void Traverse(List<VisibleElementDto> currentNodes)
            {
                if (matchCount > 1) return;
                foreach (var node in currentNodes)
                {
                    var set = node.CapturedData?.WebDriverBiDi?.ElementData?.SelectorSet;
                    if (set != null)
                    {
                        bool isMatch = false;
                        if (!string.IsNullOrEmpty(set.CustomAttribute?.Value) && set.CustomAttribute.Value.Replace("\"", "'").Contains(cleanLoc)) isMatch = true;
                        else if (!string.IsNullOrEmpty(set.Id?.Value) && set.Id.Value.Contains(cleanLoc)) isMatch = true;
                        else if (!string.IsNullOrEmpty(set.Css?.Value) && set.Css.Value.Replace("\"", "'").Contains(cleanLoc)) isMatch = true;
                        else if (!string.IsNullOrEmpty(set.XpathRelative?.Value) && set.XpathRelative.Value.Replace("\"", "'").Contains(cleanLoc)) isMatch = true;
                        else if (!string.IsNullOrEmpty(set.XpathAbsolute?.Value) && set.XpathAbsolute.Value.Replace("\"", "'").Contains(cleanLoc)) isMatch = true;
                        else if (!string.IsNullOrEmpty(set.Name?.Value) && set.Name.Value.Contains(cleanLoc)) isMatch = true;

                        if (isMatch) matchCount++;
                    }

                    if (node.Children != null && node.Children.Count > 0)
                        Traverse(node.Children);
                }
            }

            Traverse(nodes);
            return matchCount > 1;
        }

        private string MapToAriaRole(string role)
        {
            if (role.Contains("botão") || role.Contains("button")) return "Button";
            if (role.Contains("link") || role == "a") return "Link";
            if (role.Contains("editar") || role.Contains("input")) return "Textbox";
            if (role.Contains("seleção") || role.Contains("checkbox")) return "Checkbox";
            return null;
        }

        private void AtualizarPreview()
        {
            if (!_workspace.HasData) return;

            _currentConfig.Platform = (AutomationPlatform)cmbPlatform.SelectedItem;
            _currentConfig.Strategy = (AutomationStrategy)cmbStrategy.SelectedItem;
            _currentConfig.Pattern = (DesignPattern)cmbPattern.SelectedItem;
            _currentConfig.Framework = (TestFramework)cmbFramework.SelectedItem;
            _currentConfig.Language = (ScriptLanguage)cmbLanguage.SelectedItem;

            try
            {
                string generatedCode = CodeGeneratorFactory.Create(_currentConfig).GenerateCode(_workspace, _currentConfig, "LiteScenario");
                codeEditor.ReadOnly = false;
                codeEditor.Text = generatedCode;
                EditorConfigurator.ApplyLanguageTheme(codeEditor, _currentConfig.Language, _currentConfig.Pattern, _isDarkMode);
                codeEditor.ReadOnly = true;
            }
            catch (Exception ex)
            {
                codeEditor.ReadOnly = false; codeEditor.Text = $"{LanguageManager.GetString("ErrGeneration")}{ex.Message}"; codeEditor.ReadOnly = true;
            }
        }
    }
}