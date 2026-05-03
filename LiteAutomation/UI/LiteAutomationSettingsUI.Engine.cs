using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using LiteAutomation.Core;
using LiteAutomation.DTOs;
using LiteAutomation.Enums;
using LiteAutomation.Factories;
using LiteAutomation.Diagnostics;
using FastColoredTextBoxNS;

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
                    // 🚀 SE FOR UM PASSO NORMAL DE AÇÃO
                    if (main.MicroSteps != null && main.MicroSteps.Count > 0 && !main.IsEvidenceOnly)
                    {
                        int microIndex = 1;
                        foreach (var micro in main.MicroSteps)
                        {
                            string displayStep = micro.StepId ?? $"{main.StepIndex}.{microIndex}";
                            string cacheKey = $"{framework}_{displayStep}";

                            List<LocatorOption> baseOptions;

                            if (!_locatorCache.TryGetValue(cacheKey, out baseOptions))
                            {
                                baseOptions = new List<LocatorOption>();
                                var uia = micro.CapturedData?.Uia?.ElementData;
                                var bidi = micro.CapturedData?.WebDriverBiDi?.ElementData;

                                if (uia != null && !string.IsNullOrWhiteSpace(uia.Semantic?.AccessibleName?.Value))
                                {
                                    string role = uia.Semantic.Role?.Value?.ToLower() ?? "elemento";
                                    string rawName = uia.Semantic.AccessibleName.Value.Replace("\"", "'").Replace("\n", " ").Replace("\r", "");
                                    string shortName = rawName.Length > 35 ? rawName.Substring(0, 35).Trim() : rawName;
                                    int score = uia.Semantic.AccessibleName.Confidence >= 0 ? uia.Semantic.AccessibleName.Confidence : 80;
                                    string codeSnippet = "";

                                    if (uia.Semantic.AutomationId?.Value == "RootWebArea" || role == "documento" || role == "região")
                                    {
                                        codeSnippet = isPlaywright ? "Page.Locator(\"body\")" : "By.TagName(\"body\")";
                                        baseOptions.Add(new LocatorOption { Category = "SEMÂNTICO", Name = "Fundo da Tela (Body)", CodeSnippet = codeSnippet, Confidence = 100, IsSemantic = true });
                                    }
                                    else
                                    {
                                        if (isPlaywright)
                                            codeSnippet = $"Page.GetByRole(AriaRole.{MapToAriaRole(role) ?? "Button"}, new() {{ Name = \"{shortName}\"{(rawName.Length > 35 ? ", Exact = false" : "")} }})";
                                        else
                                            codeSnippet = rawName.Length > 35
                                                ? $"By.XPath(\"//*[contains(@aria-label, '{shortName}') or contains(text(), '{shortName}')]\")"
                                                : $"By.XPath(\"//*[@aria-label='{rawName}' or text()='{rawName}']\")";

                                        baseOptions.Add(new LocatorOption { Category = "SEMÂNTICO", Name = $"{role} '{shortName}'", CodeSnippet = codeSnippet, Confidence = score, IsSemantic = true });
                                    }
                                }

                                if (bidi?.SelectorSet != null)
                                {
                                    AddOptionFromBidi(baseOptions, bidi.SelectorSet, isPlaywright);
                                    AddOptionIfValid(baseOptions, bidi.SelectorSet.CustomAttribute, "Atributo Custom", isPlaywright, "Page.Locator(\"{0}\")", "By.CssSelector(\"{0}\")", false, "DOM");
                                    AddOptionIfValid(baseOptions, bidi.SelectorSet.Id, "ID", isPlaywright, "Page.Locator(\"{0}\")", "By.CssSelector(\"{0}\")", false, "DOM");
                                    AddOptionIfValid(baseOptions, bidi.SelectorSet.Name, "Name", isPlaywright, "Page.Locator(\"{0}\")", "By.CssSelector(\"{0}\")", false, "DOM");
                                    AddOptionIfValid(baseOptions, bidi.SelectorSet.Text, "Texto Visível", isPlaywright, "Page.Locator(\"xpath={0}\")", "By.XPath(\"{0}\")", false, "DOM");
                                    AddOptionIfValid(baseOptions, bidi.SelectorSet.Css, "CSS", isPlaywright, "Page.Locator(\"css={0}\")", "By.CssSelector(\"{0}\")", false, "DOM");
                                    AddOptionIfValid(baseOptions, bidi.SelectorSet.XpathAbsolute, "XPath Absoluto", isPlaywright, "Page.Locator(\"xpath={0}\")", "By.XPath(\"{0}\")", false, "DOM");
                                    AddOptionIfValid(baseOptions, bidi.SelectorSet.AriaLabel, "Aria-Label", isPlaywright, "Page.Locator(\"css={0}\")", "By.CssSelector(\"{0}\")", true, "SEMÂNTICO");
                                }

                                if (baseOptions.Count == 0)
                                    baseOptions.Add(new LocatorOption { Category = "DOM", Name = "Indisponível", CodeSnippet = isPlaywright ? "Page.Locator(\"/* VAZIO */\")" : "By.XPath(\"/* VAZIO */\")", Confidence = 0, IsSemantic = false });

                                baseOptions = baseOptions.GroupBy(o => o.CodeSnippet).Select(g => g.First()).ToList();
                                _locatorCache[cacheKey] = baseOptions;
                            }

                            var displayOptions = baseOptions.Select(o => o.Clone()).ToList();
                            foreach (var opt in displayOptions) opt.DisplayName = $"{(opt.Category == "SEMÂNTICO" ? "🧠 SEMÂNTICO" : "⚙️ DOM")} ➔ [{opt.Confidence}] {opt.Name}";
                            displayOptions = displayOptions.OrderByDescending(o => o.Confidence).ThenByDescending(o => o.IsSemantic).ToList();

                            int rowIndex = gridLocators.Rows.Add();
                            var row = gridLocators.Rows[rowIndex];

                            row.Cells[0].Value = displayStep;
                            row.Cells[1].Value = micro.ActionType?.Replace("keypress_", "tecla_") ?? "click";

                            var comboCell = (DataGridViewComboBoxCell)row.Cells[2];
                            comboCell.DataSource = displayOptions;
                            comboCell.DisplayMember = "DisplayName";
                            comboCell.ValueMember = "CodeSnippet";
                            comboCell.Value = displayOptions[0].CodeSnippet;

                            _currentConfig.LocatorOverrides[displayStep] = displayOptions[0].CodeSnippet;
                            microIndex++;
                        }
                    }
                    else
                    {
                        // 🚀 SE FOR UM PASSO DE EVIDÊNCIA (O "ENTÃO" DO BDD)
                        string displayStep = $"{main.StepIndex}.1";
                        string cacheKey = $"{framework}_{displayStep}";

                        List<LocatorOption> baseOptions;
                        if (!_locatorCache.TryGetValue(cacheKey, out baseOptions))
                        {
                            baseOptions = new List<LocatorOption>();
                            string codeSnippet = isPlaywright ? "Page.Locator(\"body\")" : "By.TagName(\"body\")";
                            baseOptions.Add(new LocatorOption { Category = "SEMÂNTICO", Name = "Validação da Tela (Body)", CodeSnippet = codeSnippet, Confidence = 100, IsSemantic = true });
                            _locatorCache[cacheKey] = baseOptions;
                        }

                        var displayOptions = baseOptions.Select(o => o.Clone()).ToList();
                        foreach (var opt in displayOptions) opt.DisplayName = $"🧠 SEMÂNTICO ➔ [{opt.Confidence}] {opt.Name}";

                        int rowIndex = gridLocators.Rows.Add();
                        var row = gridLocators.Rows[rowIndex];

                        row.Cells[0].Value = displayStep;
                        row.Cells[1].Value = "evidência"; // Indica que é um passo visual

                        var comboCell = (DataGridViewComboBoxCell)row.Cells[2];
                        comboCell.DataSource = displayOptions;
                        comboCell.DisplayMember = "DisplayName";
                        comboCell.ValueMember = "CodeSnippet";
                        comboCell.Value = displayOptions[0].CodeSnippet;

                        _currentConfig.LocatorOverrides[displayStep] = displayOptions[0].CodeSnippet;
                    }
                }
            }
            catch (Exception ex) { LiteLogger.Error("Erro silencioso ao preencher a Grid.", ex); }
            finally { gridLocators.ResumeLayout(); }
        }

        private void AddOptionFromBidi(List<LocatorOption> list, SelectorSetDto set, bool isPlaywright)
        {
            void TryAdd(LocatorData loc, string name) { if (loc != null && loc.Confidence >= 0) list.Add(new LocatorOption { Name = name, CodeSnippet = isPlaywright ? LocatorEngine.GetBestPlaywrightLocator(new SelectorSetDto { Id = loc }) : LocatorEngine.GetBestSeleniumLocator(new SelectorSetDto { Id = loc }), Confidence = loc.Confidence, IsSemantic = false, Category = "DOM" }); }
            TryAdd(set.Id, "ID"); TryAdd(set.CustomAttribute, "TestID"); TryAdd(set.Name, "Name");
        }

        private void AddOptionIfValid(List<LocatorOption> options, LocatorData locator, string name, bool isPW, string templatePW, string templateSel, bool isSem, string cat)
        {
            if (locator != null && !string.IsNullOrWhiteSpace(locator.Value) && locator.Confidence >= 0)
                options.Add(new LocatorOption { Category = cat, Name = name, CodeSnippet = isPW ? string.Format(templatePW, locator.Value.Replace("\"", "'")) : string.Format(templateSel, locator.Value.Replace("\"", "'")), Confidence = locator.Confidence, IsSemantic = isSem });
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
                codeEditor.ReadOnly = false; codeEditor.Text = $"// ❌ ERRO: {ex.Message}"; codeEditor.ReadOnly = true;
            }
        }
    }
}