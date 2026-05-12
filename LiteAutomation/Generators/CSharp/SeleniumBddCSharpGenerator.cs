using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using LiteAutomation.Core;
using LiteAutomation.Core.NLP;
using LiteAutomation.DTOs;
using LiteAutomation.Enums;
using LiteAutomation.Interfaces;
using LiteTools.Core.Languages;

namespace LiteAutomation.Generators.CSharp
{
    public class SeleniumBddCSharpGenerator : ICodeGenerator
    {
        private class StepInfo
        {
            public string Keyword { get; set; }
            public string GherkinText { get; set; }
            public string StepDefAttribute { get; set; }
            public string StepDefText { get; set; }
            public string MethodName { get; set; }
            public string MethodParams { get; set; }
            public string ActionCode { get; set; }
            public string ErrorMsg { get; set; }
            public string StepId { get; set; }
            public string Diagnostics { get; set; }
        }

        public string GenerateCode(WorkspaceState workspace, GeneratorConfig config, string testClassName = "GeneratedTest")
        {
            var sb = new StringBuilder();
            var examples = new Dictionary<string, string>();
            var generatedSteps = new List<StepInfo>();

            var analyzer = new DeltaAnalyzer();
            var validIntents = analyzer.Analyze(workspace.RawSteps, config)
                                       .Where(i => i.Type != IntentType.Unknown)
                                       .ToList();

            int lastActionIndex = validIntents.FindLastIndex(i =>
                i.Type == IntentType.Click ||
                i.Type == IntentType.InputText ||
                i.Type == IntentType.KeyPress ||
                i.Type == IntentType.Hover ||
                i.Type == IntentType.Blur ||
                i.Type == IntentType.ScrollTo ||
                i.Type == IntentType.NavigateToUrl);

            string lastContext = "";
            bool isCanonical = config.BddStyle == BddStyle.Canonical;

            string kwGiven = LanguageManager.GetString("BddKwGiven");
            string kwWhen = LanguageManager.GetString("BddKwWhen");
            string kwThen = LanguageManager.GetString("BddKwThen");
            string kwAnd = LanguageManager.GetString("BddKwAnd");

            var gherkinTextCounts = new Dictionary<string, int>();
            string currentPageName = LanguageManager.GetString("PageNameHome");

            for (int i = 0; i < validIntents.Count; i++)
            {
                var intent = validIntents[i];
                string rawText = "";
                string role = "";
                var ids = intent.StepId.Split('.');

                if (ids.Length == 2 && int.TryParse(ids[0], out int mIdxForName))
                {
                    var rawMain = workspace.RawSteps?.FirstOrDefault(s => s.StepIndex == mIdxForName);
                    if (rawMain != null && !string.IsNullOrWhiteSpace(rawMain.ObservedContext?.PageTitle))
                    {
                        string pTitle = rawMain.ObservedContext.PageTitle;
                        if (pTitle.Contains("-")) pTitle = pTitle.Split('-')[0];
                        if (pTitle.Contains("|")) pTitle = pTitle.Split('|')[0];

                        string rawPageVar = SemanticNlpEngine.GenerateVariableName(pTitle.Trim(), "");
                        if (rawPageVar.StartsWith("el")) rawPageVar = rawPageVar.Substring(2);
                        currentPageName = Capitalize(rawPageVar);
                    }
                }

                if (ids.Length == 2 && int.TryParse(ids[0], out int mIdx) && int.TryParse(ids[1], out int micIdx))
                {
                    var rawMain = workspace.RawSteps?.FirstOrDefault(s => s.StepIndex == mIdx);

                    var cleanTrail = DeltaAnalyzer.GetCleanInteractionTrail(rawMain?.InteractionTrail);
                    var interaction = micIdx > 0 ? cleanTrail.ElementAtOrDefault(micIdx - 1) : null;

                    if (interaction != null)
                    {
                        var bidi = interaction.WebDriverBiDi?.ElementData;
                        var uia = interaction.Uia?.ElementData;

                        role = uia?.Semantic?.Role?.Value ?? interaction.TagName ?? "";
                        bool isInput = interaction.InteractionType == "input" || interaction.InteractionType == "change" || interaction.InteractionType == "fill" || role == "textbox" || role == "editar";

                        if (isInput)
                        {
                            rawText = uia?.Semantic?.AccessibleName?.Value
                                      ?? bidi?.SelectorSet?.Placeholder?.Value
                                      ?? bidi?.SelectorSet?.AriaLabel?.Value
                                      ?? bidi?.SelectorSet?.Name?.Value
                                      ?? interaction.ElementId
                                      ?? "";
                        }
                        else
                        {
                            rawText = uia?.Semantic?.AccessibleName?.Value
                                      ?? bidi?.SelectorSet?.Text?.Value
                                      ?? bidi?.SelectorSet?.AriaLabel?.Value
                                      ?? bidi?.SelectorSet?.Name?.Value
                                      ?? interaction.VisibleText
                                      ?? interaction.TagName
                                      ?? "";
                        }

                        if (string.IsNullOrWhiteSpace(rawText) || rawText.Length < 3 || rawText.ToLower() == "input")
                        {
                            if (!string.IsNullOrWhiteSpace(interaction.ElementId))
                                rawText = interaction.ElementId.Replace("-", " ").Replace("_", " ");
                            else if (!string.IsNullOrWhiteSpace(interaction.InputType))
                                rawText = interaction.InputType;
                            else
                                rawText = interaction.TagName ?? "";
                        }

                        string locLowerCheck = intent.TargetLocator?.ToLower() ?? "";
                        if (locLowerCheck.Contains("body") || locLowerCheck.Contains("vazio") || string.IsNullOrEmpty(intent.TargetLocator))
                        {
                            if (!string.IsNullOrWhiteSpace(interaction.ElementId)) intent.TargetLocator = $"By.Id(\"{interaction.ElementId}\")";
                            else if (!string.IsNullOrWhiteSpace(bidi?.SelectorSet?.XpathAbsolute?.Value)) intent.TargetLocator = $"By.XPath(\"{bidi.SelectorSet.XpathAbsolute.Value.Replace("\"", "'")}\")";
                            else if (!string.IsNullOrWhiteSpace(interaction.VisibleText)) { string safeText = interaction.VisibleText.Replace("\"", "'").Replace("\n", " ").Trim(); intent.TargetLocator = $"By.XPath(\"//*[normalize-space(text())='{safeText}']\")"; }
                            else if (!string.IsNullOrWhiteSpace(interaction.Classes)) { string cleanClasses = string.Join(".", interaction.Classes.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)); intent.TargetLocator = $"By.CssSelector(\"{interaction.TagName}.{cleanClasses}\")"; }
                            else if (!string.IsNullOrWhiteSpace(interaction.TagName)) intent.TargetLocator = $"By.TagName(\"{interaction.TagName}\")";
                        }
                    }
                    else if (rawMain != null)
                    {
                        string pTitle = rawMain.ObservedContext?.PageTitle;
                        string sName = rawMain.StepName;

                        if (!string.IsNullOrWhiteSpace(pTitle))
                        {
                            rawText = pTitle;
                            if (rawText.Contains("-")) rawText = rawText.Split('-')[0];
                            if (rawText.Contains("|")) rawText = rawText.Split('|')[0];
                        }
                        else if (!string.IsNullOrWhiteSpace(sName) && !sName.ToLower().Contains("nova a"))
                            rawText = sName;
                        else
                            rawText = "carregamento atual";

                        if (rawText.Length > 25) rawText = rawText.Substring(0, 25).Trim();
                        role = "document";
                    }
                }

                if (!string.IsNullOrWhiteSpace(rawText))
                {
                    rawText = Regex.Replace(rawText, @"[^a-zA-Z0-9\sÀ-ÿ]", " ").Trim();
                    rawText = Regex.Replace(rawText, @"\s+", " ");
                    if (rawText.Length > 40) rawText = rawText.Substring(0, 40).Trim();
                }

                string humanName = SemanticNlpEngine.GenerateHumanReadable(rawText, role);
                string camelCaseName = SemanticNlpEngine.GenerateVariableName(rawText, role);

                bool isFemaleTarget = humanName.StartsWith(LanguageManager.GetString("NlpScreen")) ||
                                      humanName.StartsWith(LanguageManager.GetString("NlpPage")) ||
                                      humanName.StartsWith(LanguageManager.GetString("NlpList"));

                string locLower = intent.TargetLocator?.ToLower() ?? "";
                bool isGhostElement = locLower.Contains("body") || role == "document" ||
                                      string.IsNullOrEmpty(intent.TargetLocator) ||
                                      intent.TargetLocator.Contains("VAZIO");

                if (intent.Type == IntentType.Click && !string.IsNullOrWhiteSpace(role) && role != "document" && role != "body")
                {
                    isGhostElement = false;
                }

                if (isGhostElement)
                {
                    humanName = LanguageManager.GetString("NlpScreen") + rawText.Trim().ToLower();
                    camelCaseName = "tela" + Capitalize(SemanticNlpEngine.GenerateVariableName(rawText, "").Replace("el", ""));
                    isFemaleTarget = true;
                }

                if (string.IsNullOrWhiteSpace(camelCaseName) || camelCaseName.Length < 3) camelCaseName = "elTarget";

                string art = isFemaleTarget ? LanguageManager.GetString("GrammarArtF") : LanguageManager.GetString("GrammarArtM");
                string prepEm = isFemaleTarget ? LanguageManager.GetString("GrammarInF") : LanguageManager.GetString("GrammarInM");
                string prepDe = isFemaleTarget ? LanguageManager.GetString("GrammarOfF") : LanguageManager.GetString("GrammarOfM");
                string suffix = isFemaleTarget ? LanguageManager.GetString("GrammarSuffixF") : LanguageManager.GetString("GrammarSuffixM");

                string phaseContext = "";
                if (i == 0)
                {
                    phaseContext = kwGiven;
                }
                else
                {
                    if (isCanonical)
                    {
                        if (lastActionIndex != -1 && i > lastActionIndex) phaseContext = kwThen;
                        else phaseContext = kwWhen;
                    }
                    else
                    {
                        if (intent.Type == IntentType.WaitUrlChange || intent.Type == IntentType.AssertVisible || intent.Type == IntentType.AssertEnabled) phaseContext = kwThen;
                        else phaseContext = kwWhen;
                    }
                }

                string gherkinKeyword = (phaseContext == lastContext) ? kwAnd : phaseContext;
                lastContext = phaseContext;

                string stepDefAttribute = "";
                if (phaseContext == kwGiven) stepDefAttribute = LanguageManager.GetString("BddAttrGiven");
                else if (phaseContext == kwThen) stepDefAttribute = LanguageManager.GetString("BddAttrThen");
                else stepDefAttribute = LanguageManager.GetString("BddAttrWhen");

                string varName = "";
                var args = new object[] { art, humanName, varName, prepEm, prepDe, suffix, intent.Type.ToString() };

                var step = new StepInfo
                {
                    Keyword = gherkinKeyword,
                    StepDefAttribute = stepDefAttribute,
                    ErrorMsg = intent.FriendlyErrorMessage,
                    StepId = intent.StepId,
                    Diagnostics = intent.Diagnostics,
                    ActionCode = GenerateActionCode(intent, FormatLocator(intent.TargetLocator))
                };

                switch (intent.Type)
                {
                    case IntentType.Click:
                        step.GherkinText = isCanonical ? string.Format(LanguageManager.GetString("BddClickCanon"), args) : string.Format(LanguageManager.GetString("BddClickNarr"), args);
                        step.StepDefText = step.GherkinText;
                        break;
                    case IntentType.Hover:
                        step.GherkinText = isCanonical ? string.Format(LanguageManager.GetString("BddHoverCanon"), args) : string.Format(LanguageManager.GetString("BddHoverNarr"), args);
                        step.StepDefText = step.GherkinText;
                        break;
                    case IntentType.Blur:
                        step.GherkinText = isCanonical ? LanguageManager.GetString("BddBlurCanon") : LanguageManager.GetString("BddBlurNarr");
                        step.StepDefText = step.GherkinText;
                        break;
                    case IntentType.InputText:
                        string paramName = camelCaseName;
                        if (paramName.StartsWith("input", StringComparison.OrdinalIgnoreCase) && paramName.Length > 5)
                        {
                            paramName = paramName.Substring(5);
                            paramName = char.ToLower(paramName[0]) + paramName.Substring(1);
                        }
                        else if (paramName.StartsWith("campo", StringComparison.OrdinalIgnoreCase) && paramName.Length > 5)
                        {
                            paramName = paramName.Substring(5);
                            paramName = char.ToLower(paramName[0]) + paramName.Substring(1);
                        }

                        varName = EnsureUniqueVar(examples, paramName, intent.Value ?? "");
                        args[2] = varName;

                        step.GherkinText = isCanonical ? string.Format(LanguageManager.GetString("BddInputCanon"), args) : string.Format(LanguageManager.GetString("BddInputNarr"), args);
                        step.StepDefText = (isCanonical ? string.Format(LanguageManager.GetString("BddInputDefCanon"), args) : string.Format(LanguageManager.GetString("BddInputDefNarr"), args)) + "\"(.*)\"";
                        step.MethodParams = $"string {varName}";

                        string safeReplaceVal = intent.Value != null ? intent.Value.Replace("\"", "\\\"") : "";
                        step.ActionCode = step.ActionCode.Replace($"SendKeys(\"{safeReplaceVal}\")", $"SendKeys({varName})");
                        break;
                    case IntentType.KeyPress:
                        step.GherkinText = $"aciono a tecla {intent.Key.ToUpper()} no {humanName}";
                        step.StepDefText = step.GherkinText;
                        break;
                    case IntentType.AssertVisible:
                        step.GherkinText = isCanonical ? (phaseContext == kwThen ? string.Format(LanguageManager.GetString("BddAssertVisThenCanon"), args) : string.Format(LanguageManager.GetString("BddAssertVisWhenCanon"), args)) : string.Format(LanguageManager.GetString("BddAssertVisNarr"), args);
                        step.StepDefText = step.GherkinText;
                        break;
                    default:
                        step.GherkinText = string.Format(LanguageManager.GetString("BddDefaultIntent"), args);
                        step.StepDefText = step.GherkinText;
                        break;
                }

                if (i == 0 && intent.Type == IntentType.AssertVisible)
                {
                    step.GherkinText = $"estou na {humanName}";
                    step.StepDefText = step.GherkinText;
                }

                string baseGherkin = step.GherkinText;
                string baseStepDef = step.StepDefText;

                string genericTerm = LanguageManager.GetString("NlpGenericVar").ToLower();
                bool isGenericPage = string.IsNullOrWhiteSpace(currentPageName) ||
                                     currentPageName.ToLower().Contains("generic") ||
                                     currentPageName.ToLower().Contains(genericTerm);

                if (!gherkinTextCounts.ContainsKey(baseGherkin))
                {
                    gherkinTextCounts[baseGherkin] = 1;
                }
                else
                {
                    gherkinTextCounts[baseGherkin]++;
                    string screenTerm = LanguageManager.GetString("NlpScreen").Trim();
                    string onScreenText = $"{LanguageManager.GetString("GrammarInF")} {screenTerm} {currentPageName}".Trim();

                    string gherkinWithPage = $"{baseGherkin} {onScreenText}";
                    string stepDefWithPage = $"{baseStepDef} {onScreenText}";

                    if (!isGenericPage && !gherkinTextCounts.ContainsKey(gherkinWithPage))
                    {
                        gherkinTextCounts[gherkinWithPage] = 1;
                        step.GherkinText = gherkinWithPage;
                        step.StepDefText = stepDefWithPage;
                    }
                    else
                    {
                        string targetKey = isGenericPage ? baseGherkin : gherkinWithPage;
                        if (!gherkinTextCounts.ContainsKey(targetKey)) gherkinTextCounts[targetKey] = 1;
                        else gherkinTextCounts[targetKey]++;

                        int c = gherkinTextCounts[targetKey];
                        step.GherkinText = $"{(isGenericPage ? baseGherkin : gherkinWithPage)} {c}";
                        step.StepDefText = $"{(isGenericPage ? baseStepDef : stepDefWithPage)} {c}";
                    }
                }

                string methodVerb = intent.Type switch
                {
                    IntentType.NavigateToUrl => LanguageManager.GetString("MethodAccess"),
                    IntentType.Click => LanguageManager.GetString("MethodClick"),
                    IntentType.KeyPress => "AcionarTecla",
                    IntentType.InputText => LanguageManager.GetString("MethodFill"),
                    IntentType.WaitUrlChange => LanguageManager.GetString("MethodWait"),
                    IntentType.AssertVisible => LanguageManager.GetString("MethodVerifyVis"),
                    _ => "Executar"
                };

                string targetSuffix = intent.Type switch
                {
                    IntentType.NavigateToUrl => "Sistema",
                    IntentType.WaitUrlChange => "Pagina",
                    _ => Capitalize(camelCaseName)
                };

                string pageSuffix = isGenericPage ? "" : Capitalize(RemoveAccents(currentPageName).Replace(" ", ""));
                string dedupeSuffix = Regex.Match(step.GherkinText, @"\d+$").Success ? Regex.Match(step.GherkinText, @"\d+$").Value : "";

                step.MethodName = $"{methodVerb}{targetSuffix}{pageSuffix}{dedupeSuffix}";

                generatedSteps.Add(step);
            }

            sb.AppendLine("// =====================================================================");
            sb.AppendLine($"// 🥒 {LanguageManager.GetString("LblPattern").ToUpper()}: BDD ({(isCanonical ? LanguageManager.GetString("RdoCanonical").ToUpper() : LanguageManager.GetString("RdoNarrative").ToUpper())})");
            sb.AppendLine("// =====================================================================");
            sb.AppendLine($"/*\nFeature: {LanguageManager.GetString("BddFeatureTitle")}");

            string scenarioType = examples.Count > 0 ? LanguageManager.GetString("BddScenarioOutline") : LanguageManager.GetString("BddScenario");
            sb.AppendLine($"  {scenarioType}: {LanguageManager.GetString("BddMainScenario")}");

            foreach (var step in generatedSteps)
                sb.AppendLine($"    {step.Keyword} {step.GherkinText}");

            if (examples.Count > 0)
            {
                sb.AppendLine($"\n    {LanguageManager.GetString("BddExamples")}:");
                sb.AppendLine("      | " + string.Join(" | ", examples.Keys) + " |");
                sb.AppendLine("      | " + string.Join(" | ", examples.Values) + " |");
            }
            sb.AppendLine("*/\n");

            // 🚀 IMPORTANTE: Adicionado OpenQA.Selenium.Interactions
            sb.AppendLine("using System;\nusing TechTalk.SpecFlow;\nusing OpenQA.Selenium;\nusing OpenQA.Selenium.Chrome;\nusing OpenQA.Selenium.Support.UI;\nusing OpenQA.Selenium.Interactions;\nusing NUnit.Framework;\n");
            sb.AppendLine("namespace LiteAutomation.GeneratedTests.BDD\n{");
            sb.AppendLine($"    [Binding]");
            sb.AppendLine($"    public class {testClassName}Steps");
            sb.AppendLine($"    {{");
            sb.AppendLine($"        private IWebDriver driver;");
            sb.AppendLine($"        private WebDriverWait wait;");
            sb.AppendLine();

            sb.AppendLine("        [BeforeScenario]");
            sb.AppendLine("        public void Setup()");
            sb.AppendLine("        {");
            sb.AppendLine("            var options = new ChromeOptions();");
            sb.AppendLine("            options.AddExcludedArgument(\"enable-automation\");");
            sb.AppendLine("            options.AddAdditionalOption(\"useAutomationExtension\", false);");
            sb.AppendLine("            options.AddUserProfilePreference(\"credentials_enable_service\", false);");
            sb.AppendLine("            options.AddUserProfilePreference(\"profile.password_manager_enabled\", false);");
            sb.AppendLine("            options.AddArgument(\"--disable-blink-features=AutomationControlled\");");
            sb.AppendLine("            options.AddArgument(\"user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36\");");
            sb.AppendLine("");
            sb.AppendLine("            driver = new ChromeDriver(options);");
            sb.AppendLine("            driver.Manage().Window.Maximize();");
            sb.AppendLine("            wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));");

            var firstStepWithUrl = workspace.RawSteps?.FirstOrDefault(s => s.ObservedContext != null && !string.IsNullOrEmpty(s.ObservedContext.Url));
            if (firstStepWithUrl != null)
            {
                sb.AppendLine($"\n            driver.Navigate().GoToUrl(\"{firstStepWithUrl.ObservedContext.Url}\");");
            }

            sb.AppendLine("        }\n");

            sb.AppendLine("        [AfterScenario]");
            sb.AppendLine("        public void Teardown()");
            sb.AppendLine("        {");
            sb.AppendLine("            driver?.Dispose();");
            sb.AppendLine("            driver = null;");
            sb.AppendLine("        }\n");

            var uniqueStepDefs = new HashSet<string>();

            foreach (var step in generatedSteps)
            {
                string safeMethodName = step.MethodName;
                int safetyCounter = 1;
                while (uniqueStepDefs.Contains(safeMethodName))
                {
                    safetyCounter++;
                    safeMethodName = $"{step.MethodName}Alt{safetyCounter}";
                }
                uniqueStepDefs.Add(safeMethodName);

                sb.AppendLine($"        [{step.StepDefAttribute}(@\"{step.StepDefText}\")]");
                sb.AppendLine($"        public void {safeMethodName}({step.MethodParams})");
                sb.AppendLine($"        {{");

                sb.AppendLine($"            // {LanguageManager.GetString("LogStep")} {step.StepId}");
                if (config.IncludeReport && !string.IsNullOrWhiteSpace(step.Diagnostics))
                    sb.AppendLine($"            {step.Diagnostics}");

                if (!string.IsNullOrEmpty(step.ErrorMsg))
                {
                    sb.AppendLine($"            try\n            {{");
                    foreach (var line in step.ActionCode.Split('\n'))
                        if (!string.IsNullOrWhiteSpace(line)) sb.AppendLine($"                {line.TrimEnd()}");

                    sb.AppendLine($"            }}\n            catch (WebDriverTimeoutException)\n            {{");
                    sb.AppendLine($"                Assert.Fail(\"{step.ErrorMsg}\");\n            }}");
                }
                else if (!string.IsNullOrEmpty(step.ActionCode))
                {
                    foreach (var line in step.ActionCode.Split('\n'))
                        if (!string.IsNullOrWhiteSpace(line)) sb.AppendLine($"            {line.TrimEnd()}");
                }

                sb.AppendLine($"        }}");
                sb.AppendLine();
            }

            sb.AppendLine($"    }}");
            sb.AppendLine($"}}");

            return sb.ToString();
        }

        private string EnsureUniqueVar(Dictionary<string, string> examples, string baseName, string value)
        {
            string varName = string.IsNullOrEmpty(baseName) ? "valor" : baseName;
            int counter = 1;
            while (examples.ContainsKey(varName) && examples[varName] != value)
            {
                counter++;
                varName = baseName + counter;
            }
            examples[varName] = value;
            return varName;
        }

        // 🚀 AÇÕES NATIVAS: Uso ostensivo de OpenQA.Selenium.Interactions.Actions
        private string GenerateActionCode(AutomationIntent intent, string loc)
        {
            switch (intent.Type)
            {
                case IntentType.Click:
                    return $"var el = wait.Until(d => d.FindElement({loc}));\ntry\n{{\n    new Actions(driver).MoveToElement(el).Click().Perform();\n}}\ncatch\n{{\n    ((IJavaScriptExecutor)driver).ExecuteScript(\"arguments[0].click();\", el);\n}}";
                case IntentType.Hover:
                    return $"var el = wait.Until(d => d.FindElement({loc}));\nnew Actions(driver).MoveToElement(el).Perform();";
                case IntentType.Blur:
                    return $"new Actions(driver).MoveByOffset(0, 0).Click().Perform();"; // Clique nativo fora do foco (coordenada 0,0)
                case IntentType.InputText:
                    string safeInput = intent.Value != null ? intent.Value.Replace("\"", "\\\"") : "";
                    return $"var el = wait.Until(d => d.FindElement({loc}));\nnew Actions(driver).MoveToElement(el).Perform();\nel.Clear();\nel.SendKeys(\"{safeInput}\");";
                case IntentType.KeyPress:
                    return $"var el = wait.Until(d => d.FindElement({loc}));\nnew Actions(driver).MoveToElement(el).Perform();\nel.SendKeys({MapKey(intent.Key)});";
                case IntentType.ScrollTo:
                    return $"var el = wait.Until(d => d.FindElement({loc}));\nnew Actions(driver).MoveToElement(el).Perform();";
                case IntentType.AssertVisible:
                    return $"Assert.IsTrue(wait.Until(d => d.FindElement({loc})).Displayed);";
                case IntentType.AssertEnabled:
                    return $"Assert.IsTrue(wait.Until(d => d.FindElement({loc})).Enabled);";
                default:
                    return "";
            }
        }

        private string FormatLocator(string loc)
        {
            if (string.IsNullOrEmpty(loc) || loc.Contains("VAZIO") || loc.Contains("NÃO ENCONTRADO") || loc.Contains("AMBÍGUO")) return "By.TagName(\"body\")";

            // CLEAN SYNTAX: Remove escapes visuais
            loc = loc.Replace("\\\"", "'").Replace("\"", "'");

            if (loc.StartsWith("By.") || loc.StartsWith("Page.")) return loc;
            if (loc.StartsWith("xpath=") || loc.StartsWith("/") || loc.StartsWith("(")) return $"By.XPath(\"{loc.Replace("xpath=", "")}\")";
            if (loc.StartsWith("css=")) return $"By.CssSelector(\"{loc.Replace("css=", "")}\")";
            return $"By.CssSelector(\"{loc}\")";
        }

        private string MapKey(string key) => key?.ToLower().Replace("keypress_", "") switch { "enter" => "Keys.Enter", "tab" => "Keys.Tab", "escape" => "Keys.Escape", "space" => "Keys.Space", _ => $"\"{key}\"" };

        private string Capitalize(string text) { if (string.IsNullOrEmpty(text)) return text; return char.ToUpper(text[0]) + text.Substring(1); }

        private string RemoveAccents(string text)
        {
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in normalizedString) { if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(c); }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}