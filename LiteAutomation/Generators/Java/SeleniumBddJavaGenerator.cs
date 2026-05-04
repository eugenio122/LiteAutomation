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

namespace LiteAutomation.Generators.Java
{
    public class SeleniumBddJavaGenerator : ICodeGenerator
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
                                       .Where(i => !i.IsNewStepHeader && i.Type != IntentType.Unknown)
                                       .ToList();

            int lastActionIndex = validIntents.FindLastIndex(i =>
                i.Type == IntentType.Click || i.Type == IntentType.InputText ||
                i.Type == IntentType.KeyPress || i.Type == IntentType.Hover ||
                i.Type == IntentType.Blur || i.Type == IntentType.ScrollTo ||
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

                if (intent.Type == IntentType.NavigateToUrl || intent.Type == IntentType.WaitUrlChange)
                {
                    try
                    {
                        var uri = new Uri(intent.Value.StartsWith("http") ? intent.Value : "http://dummy" + intent.Value);
                        string seg = uri.Segments.LastOrDefault()?.Replace("/", "").Split('?')[0];
                        if (!string.IsNullOrEmpty(seg) && seg != "index.html")
                        {
                            string rawPageVar = SemanticNlpEngine.GenerateVariableName(seg, "");
                            if (rawPageVar.StartsWith("el")) rawPageVar = rawPageVar.Substring(2);
                            currentPageName = Capitalize(rawPageVar);
                        }
                    }
                    catch { }
                }

                if (ids.Length == 2 && int.TryParse(ids[0], out int mIdx) && int.TryParse(ids[1], out int micIdx))
                {
                    var rawMain = workspace.RawSteps?.FirstOrDefault(s => s.StepIndex == mIdx);
                    var rawMicro = rawMain?.MicroSteps?.ElementAtOrDefault(micIdx - 1);

                    if (rawMicro != null)
                    {
                        var bidi = rawMicro.CapturedData?.WebDriverBiDi?.ElementData;
                        var uia = rawMicro.CapturedData?.Uia?.ElementData;
                        rawText = uia?.Semantic?.AccessibleName?.Value ?? bidi?.SelectorSet?.Text?.Value ?? bidi?.SelectorSet?.AriaLabel?.Value ?? bidi?.SelectorSet?.Name?.Value ?? "";
                        role = bidi?.Semantic?.Role?.Value ?? uia?.Semantic?.Role?.Value ?? "";
                    }
                    else if (rawMain != null)
                    {
                        string pTitle = rawMain.ObservedContext?.PageTitle;
                        string sName = rawMain.StepName;
                        if (!string.IsNullOrWhiteSpace(pTitle)) { rawText = pTitle; if (rawText.Contains("-")) rawText = rawText.Split('-')[0]; if (rawText.Contains("|")) rawText = rawText.Split('|')[0]; }
                        else if (!string.IsNullOrWhiteSpace(sName) && !sName.ToLower().Contains("nova a")) rawText = sName;
                        else rawText = "carregamento atual";
                        if (rawText.Length > 25) rawText = rawText.Substring(0, 25).Trim();
                        role = "document";
                    }
                }

                string humanName = SemanticNlpEngine.GenerateHumanReadable(rawText, role);
                string camelCaseName = SemanticNlpEngine.GenerateVariableName(rawText, role);

                bool isFemaleTarget = humanName.StartsWith(LanguageManager.GetString("NlpScreen")) || humanName.StartsWith(LanguageManager.GetString("NlpPage")) || humanName.StartsWith(LanguageManager.GetString("NlpList"));

                string locLower = intent.TargetLocator?.ToLower() ?? "";
                bool isGhostElement = locLower.Contains("body") || role == "document" ||
                                      string.IsNullOrEmpty(intent.TargetLocator) ||
                                      intent.TargetLocator.Contains("VAZIO") ||
                                      intent.TargetLocator.Contains("NÃO ENCONTRADO") ||
                                      intent.TargetLocator.Contains("AMBÍGUO");

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
                if (isCanonical)
                {
                    if (i == 0 && intent.Type == IntentType.NavigateToUrl) phaseContext = kwGiven;
                    else if (lastActionIndex != -1 && i > lastActionIndex) phaseContext = kwThen;
                    else if (lastActionIndex == -1 && (intent.Type == IntentType.AssertVisible || intent.Type == IntentType.AssertEnabled || intent.Type == IntentType.WaitUrlChange)) phaseContext = kwThen;
                    else phaseContext = kwWhen;
                }
                else
                {
                    if (intent.Type == IntentType.NavigateToUrl) phaseContext = kwGiven;
                    else if (intent.Type == IntentType.WaitUrlChange || intent.Type == IntentType.AssertVisible || intent.Type == IntentType.AssertEnabled) phaseContext = kwThen;
                    else phaseContext = kwWhen;
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
                    ActionCode = GenerateJavaActionCode(intent, FormatJavaLocator(intent.TargetLocator), camelCaseName)
                };

                switch (intent.Type)
                {
                    case IntentType.NavigateToUrl:
                        step.GherkinText = isCanonical ? LanguageManager.GetString("BddAccessSysCanon") : LanguageManager.GetString("BddAccessSysNarr");
                        step.StepDefText = step.GherkinText;
                        break;
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
                        varName = EnsureUniqueVar(examples, camelCaseName, intent.Value ?? "");
                        args[2] = varName;

                        step.GherkinText = isCanonical ? string.Format(LanguageManager.GetString("BddInputCanon"), args) : string.Format(LanguageManager.GetString("BddInputNarr"), args);
                        step.StepDefText = (isCanonical ? string.Format(LanguageManager.GetString("BddInputDefCanon"), args) : string.Format(LanguageManager.GetString("BddInputDefNarr"), args)) + "\"{string}\"";
                        step.MethodParams = $"String {varName}";
                        step.ActionCode = step.ActionCode.Replace($"\"{intent.Value.Replace("\"", "\\\"")}\"", varName);
                        break;
                    case IntentType.WaitUrlChange:
                        if (isCanonical) step.GherkinText = phaseContext == kwThen ? LanguageManager.GetString("BddWaitThenCanon") : LanguageManager.GetString("BddWaitWhenCanon");
                        else step.GherkinText = LanguageManager.GetString("BddWaitNarr");
                        step.StepDefText = step.GherkinText;
                        break;
                    case IntentType.AssertVisible:
                        if (isCanonical) step.GherkinText = phaseContext == kwThen ? string.Format(LanguageManager.GetString("BddAssertVisThenCanon"), args) : string.Format(LanguageManager.GetString("BddAssertVisWhenCanon"), args);
                        else step.GherkinText = string.Format(LanguageManager.GetString("BddAssertVisNarr"), args);
                        step.StepDefText = step.GherkinText;
                        break;
                    case IntentType.AssertEnabled:
                        if (isCanonical) step.GherkinText = phaseContext == kwThen ? string.Format(LanguageManager.GetString("BddAssertEnThenCanon"), args) : string.Format(LanguageManager.GetString("BddAssertEnWhenCanon"), args);
                        else step.GherkinText = string.Format(LanguageManager.GetString("BddAssertEnNarr"), args);
                        step.StepDefText = step.GherkinText;
                        break;
                    default:
                        step.GherkinText = string.Format(LanguageManager.GetString("BddDefaultIntent"), args);
                        step.StepDefText = step.GherkinText;
                        break;
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
                    IntentType.Hover => LanguageManager.GetString("MethodHover"),
                    IntentType.Blur => LanguageManager.GetString("MethodBlur"),
                    IntentType.InputText => LanguageManager.GetString("MethodFill"),
                    IntentType.WaitUrlChange => LanguageManager.GetString("MethodWait"),
                    IntentType.AssertVisible => LanguageManager.GetString("MethodVerifyVis"),
                    IntentType.AssertEnabled => LanguageManager.GetString("MethodVerifyEn"),
                    _ => "Executar"
                };

                string targetSuffix = intent.Type switch
                {
                    IntentType.NavigateToUrl => "Sistema",
                    IntentType.Blur => "Menus",
                    IntentType.WaitUrlChange => "Pagina",
                    _ => Capitalize(camelCaseName)
                };

                string pageSuffix = isGenericPage ? "" : Capitalize(RemoveAccents(currentPageName).Replace(" ", ""));
                string dedupeSuffix = Regex.Match(step.GherkinText, @"\d+$").Success ? Regex.Match(step.GherkinText, @"\d+$").Value : "";

                step.MethodName = $"{methodVerb}{targetSuffix}{pageSuffix}{dedupeSuffix}";

                generatedSteps.Add(step);
            }

            sb.AppendLine("// =====================================================================");
            sb.AppendLine($"// 🥒 {LanguageManager.GetString("LblPattern").ToUpper()}: BDD JAVA ({(isCanonical ? LanguageManager.GetString("RdoCanonical").ToUpper() : LanguageManager.GetString("RdoNarrative").ToUpper())})");
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

            sb.AppendLine("package liteautomation.generatedtests.bdd;\n");
            sb.AppendLine("import io.cucumber.java.pt.*;");
            sb.AppendLine("import io.cucumber.java.Before;");
            sb.AppendLine("import io.cucumber.java.After;");
            sb.AppendLine("import org.openqa.selenium.*;");
            sb.AppendLine("import org.openqa.selenium.chrome.ChromeDriver;");
            sb.AppendLine("import org.openqa.selenium.chrome.ChromeOptions;");
            sb.AppendLine("import org.openqa.selenium.support.ui.WebDriverWait;");
            sb.AppendLine("import org.openqa.selenium.support.ui.ExpectedConditions;");
            sb.AppendLine("import org.junit.Assert;");
            sb.AppendLine("import java.time.Duration;\n");

            sb.AppendLine($"public class {testClassName}Steps {{");
            sb.AppendLine($"    private WebDriver driver;");
            sb.AppendLine($"    private WebDriverWait wait;");
            sb.AppendLine();
            sb.AppendLine($"    @Before");
            sb.AppendLine($"    public void setup() {{");
            sb.AppendLine($"        ChromeOptions options = new ChromeOptions();");
            sb.AppendLine($"        options.addArguments(\"--disable-blink-features=AutomationControlled\");");
            sb.AppendLine($"        driver = new ChromeDriver(options);");
            sb.AppendLine($"        driver.manage().window().maximize();");
            sb.AppendLine($"        wait = new WebDriverWait(driver, Duration.ofSeconds(10));");
            sb.AppendLine($"    }}\n");

            sb.AppendLine($"    @After");
            sb.AppendLine($"    public void teardown() {{");
            sb.AppendLine($"        if (driver != null) driver.quit();");
            sb.AppendLine($"    }}\n");

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

                string javaAnnotation = Capitalize(step.StepDefAttribute);
                sb.AppendLine($"    @{javaAnnotation}(\"{step.StepDefText}\")");
                sb.AppendLine($"    public void {Char.ToLower(safeMethodName[0]) + safeMethodName.Substring(1)}({step.MethodParams}) {{");

                sb.AppendLine($"        // {LanguageManager.GetString("LogStep")} {step.StepId}");
                if (config.IncludeReport && !string.IsNullOrWhiteSpace(step.Diagnostics))
                    sb.AppendLine($"        {step.Diagnostics}");

                if (!string.IsNullOrEmpty(step.ErrorMsg))
                {
                    sb.AppendLine($"        try {{");
                    foreach (var line in step.ActionCode.Split('\n'))
                        if (!string.IsNullOrWhiteSpace(line)) sb.AppendLine($"            {line.TrimEnd()}");

                    sb.AppendLine($"        }} catch (Exception e) {{");
                    sb.AppendLine($"            Assert.fail(\"{step.ErrorMsg}\");");
                    sb.AppendLine($"        }}");
                }
                else if (!string.IsNullOrEmpty(step.ActionCode))
                {
                    foreach (var line in step.ActionCode.Split('\n'))
                        if (!string.IsNullOrWhiteSpace(line)) sb.AppendLine($"        {line.TrimEnd()}");
                }

                sb.AppendLine($"    }}\n");
            }

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

        // 🚀 AQUI ESTÁ O FIX: {{block: 'center'}}
        private string GenerateJavaActionCode(AutomationIntent intent, string loc, string varName)
        {
            switch (intent.Type)
            {
                case IntentType.NavigateToUrl: return $"driver.get(\"{intent.Value}\");";
                case IntentType.WaitUrlChange: return $"wait.until(ExpectedConditions.urlContains(\"{intent.Value}\"));";
                case IntentType.Click: return $"WebElement {varName} = wait.until(ExpectedConditions.presenceOfElementLocated({loc}));\n((JavascriptExecutor)driver).executeScript(\"arguments[0].scrollIntoView({{block: 'center'}});\", {varName});\nwait.until(ExpectedConditions.elementToBeClickable({varName}));\ntry {{\n    {varName}.click();\n}} catch (Exception e) {{\n    ((JavascriptExecutor)driver).executeScript(\"arguments[0].click();\", {varName});\n}}";
                case IntentType.Hover: return $"WebElement {varName} = wait.until(ExpectedConditions.presenceOfElementLocated({loc}));\nnew org.openqa.selenium.interactions.Actions(driver).moveToElement({varName}).perform();";
                case IntentType.Blur: return $"driver.findElement(By.tagName(\"body\")).click();";
                case IntentType.InputText: return $"WebElement {varName} = wait.until(ExpectedConditions.presenceOfElementLocated({loc}));\n{varName}.clear();\n{varName}.sendKeys(\"{intent.Value.Replace("\"", "\\\"")}\");";
                case IntentType.KeyPress: return $"wait.until(ExpectedConditions.presenceOfElementLocated({loc})).sendKeys({MapJavaKey(intent.Key)});";
                case IntentType.ScrollTo: return $"WebElement {varName} = wait.until(ExpectedConditions.presenceOfElementLocated({loc}));\n((JavascriptExecutor)driver).executeScript(\"arguments[0].scrollIntoView({{block: 'center'}});\", {varName});";
                case IntentType.AssertVisible: return $"Assert.assertTrue(wait.until(ExpectedConditions.presenceOfElementLocated({loc})).isDisplayed());";
                case IntentType.AssertEnabled: return $"Assert.assertTrue(wait.until(ExpectedConditions.presenceOfElementLocated({loc})).isEnabled());";
                default: return "";
            }
        }

        private string FormatJavaLocator(string loc)
        {
            if (string.IsNullOrEmpty(loc) || loc.Contains("VAZIO") || loc.Contains("NÃO ENCONTRADO") || loc.Contains("AMBÍGUO"))
                return "By.tagName(\"body\")";
            if (loc.StartsWith("By.") || loc.StartsWith("Page.")) return loc;
            if (loc.StartsWith("/") || loc.StartsWith("(")) return $"By.xpath(\"{loc}\")";
            return $"By.cssSelector(\"{loc}\")";
        }

        private string MapJavaKey(string key) => key.ToLower() switch { "enter" => "Keys.ENTER", "tab" => "Keys.TAB", "escape" => "Keys.ESCAPE", "space" => "Keys.SPACE", _ => $"\"{key}\"" };
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