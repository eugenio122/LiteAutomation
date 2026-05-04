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
    public class SeleniumBddPomJavaGenerator : ICodeGenerator
    {
        private class PageDef
        {
            public string PageName { get; set; }
            public string ClassName => $"{PageName}Page";
            public string ActionClassName => $"{PageName}Actions";
            public Dictionary<string, LocatorDef> Locators { get; set; } = new Dictionary<string, LocatorDef>();
            public List<ActionDef> Actions { get; set; } = new List<ActionDef>();
        }

        private class LocatorDef
        {
            public string RawLocator { get; set; }
            public string StepId { get; set; }
        }

        private class ActionDef
        {
            public string MethodName { get; set; }
            public string MethodParams { get; set; }
            public string CodeBody { get; set; }
            public string ErrorMsg { get; set; }
            public string StepId { get; set; }
            public string Diagnostics { get; set; }
        }

        private class StepDef
        {
            public string Keyword { get; set; }
            public string GherkinText { get; set; }
            public string StepDefAttribute { get; set; }
            public string StepDefText { get; set; }
            public string MethodName { get; set; }
            public string MethodParams { get; set; }
            public string ActionCallCode { get; set; }
            public string Diagnostics { get; set; }
            public string StepId { get; set; }
        }

        public string GenerateCode(WorkspaceState workspace, GeneratorConfig config, string testClassName = "GeneratedTest")
        {
            var sb = new StringBuilder();
            var examples = new Dictionary<string, string>();

            var analyzer = new DeltaAnalyzer();
            var validIntents = analyzer.Analyze(workspace.RawSteps, config)
                                       .Where(i => !i.IsNewStepHeader && i.Type != IntentType.Unknown)
                                       .ToList();

            int lastActionIndex = validIntents.FindLastIndex(i =>
                i.Type == IntentType.Click || i.Type == IntentType.InputText ||
                i.Type == IntentType.KeyPress || i.Type == IntentType.Hover ||
                i.Type == IntentType.Blur || i.Type == IntentType.ScrollTo ||
                i.Type == IntentType.NavigateToUrl);

            bool isCanonical = config.BddStyle == BddStyle.Canonical;
            string lastContext = "";

            string kwGiven = LanguageManager.GetString("BddKwGiven");
            string kwWhen = LanguageManager.GetString("BddKwWhen");
            string kwThen = LanguageManager.GetString("BddKwThen");
            string kwAnd = LanguageManager.GetString("BddKwAnd");

            var gherkinTextCounts = new Dictionary<string, int>();
            string currentPageName = LanguageManager.GetString("PageNameHome");

            var pageDictionary = new Dictionary<string, PageDef>();
            var stepDefinitions = new List<StepDef>();

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

                string safePageKey = string.IsNullOrWhiteSpace(currentPageName) ? "Generica" : Capitalize(RemoveAccents(currentPageName).Replace(" ", ""));
                if (!pageDictionary.ContainsKey(safePageKey))
                {
                    pageDictionary[safePageKey] = new PageDef { PageName = safePageKey };
                }
                var activePage = pageDictionary[safePageKey];

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
                string stepDefAttribute = phaseContext == kwGiven ? LanguageManager.GetString("BddAttrGiven") : (phaseContext == kwThen ? LanguageManager.GetString("BddAttrThen") : LanguageManager.GetString("BddAttrWhen"));

                bool requiresLocator = intent.Type != IntentType.NavigateToUrl &&
                                       intent.Type != IntentType.WaitUrlChange &&
                                       intent.Type != IntentType.Blur;

                string locatorPropName = "";
                if (requiresLocator)
                {
                    string findByLoc = FormatJavaFindBy(intent.TargetLocator);
                    locatorPropName = EnsureUniqueLocatorProp(activePage.Locators, camelCaseName, findByLoc, intent.StepId);
                }

                string varName = "";
                var args = new object[] { art, humanName, varName, prepEm, prepDe, suffix, intent.Type.ToString() };

                var step = new StepDef
                {
                    Keyword = gherkinKeyword,
                    StepDefAttribute = stepDefAttribute,
                    StepId = intent.StepId,
                    Diagnostics = intent.Diagnostics
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
                        break;
                    case IntentType.WaitUrlChange:
                        step.GherkinText = isCanonical ? (phaseContext == kwThen ? LanguageManager.GetString("BddWaitThenCanon") : LanguageManager.GetString("BddWaitWhenCanon")) : LanguageManager.GetString("BddWaitNarr");
                        step.StepDefText = step.GherkinText;
                        break;
                    case IntentType.AssertVisible:
                        step.GherkinText = isCanonical ? (phaseContext == kwThen ? string.Format(LanguageManager.GetString("BddAssertVisThenCanon"), args) : string.Format(LanguageManager.GetString("BddAssertVisWhenCanon"), args)) : string.Format(LanguageManager.GetString("BddAssertVisNarr"), args);
                        step.StepDefText = step.GherkinText;
                        break;
                    case IntentType.AssertEnabled:
                        step.GherkinText = isCanonical ? (phaseContext == kwThen ? string.Format(LanguageManager.GetString("BddAssertEnThenCanon"), args) : string.Format(LanguageManager.GetString("BddAssertEnWhenCanon"), args)) : string.Format(LanguageManager.GetString("BddAssertEnNarr"), args);
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
                bool isGenericPage = string.IsNullOrWhiteSpace(currentPageName) || currentPageName.ToLower().Contains("generic") || currentPageName.ToLower().Contains(genericTerm);

                if (!gherkinTextCounts.ContainsKey(baseGherkin)) gherkinTextCounts[baseGherkin] = 1;
                else
                {
                    gherkinTextCounts[baseGherkin]++;
                    string onScreenText = $"{LanguageManager.GetString("GrammarInF")} {LanguageManager.GetString("NlpScreen").Trim()} {currentPageName}".Trim();
                    string gherkinWithPage = $"{baseGherkin} {onScreenText}";

                    if (!isGenericPage && !gherkinTextCounts.ContainsKey(gherkinWithPage))
                    {
                        gherkinTextCounts[gherkinWithPage] = 1;
                        step.GherkinText = gherkinWithPage; step.StepDefText = $"{baseStepDef} {onScreenText}";
                    }
                    else
                    {
                        string targetKey = isGenericPage ? baseGherkin : gherkinWithPage;
                        if (!gherkinTextCounts.ContainsKey(targetKey)) gherkinTextCounts[targetKey] = 1; else gherkinTextCounts[targetKey]++;
                        step.GherkinText = $"{(isGenericPage ? baseGherkin : gherkinWithPage)} {gherkinTextCounts[targetKey]}";
                        step.StepDefText = $"{(isGenericPage ? baseStepDef : $"{baseStepDef} {onScreenText}")} {gherkinTextCounts[targetKey]}";
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
                    _ => locatorPropName
                };

                string dedupeSuffix = Regex.Match(step.GherkinText, @"\d+$").Success ? Regex.Match(step.GherkinText, @"\d+$").Value : "";
                string pageSuffix = isGenericPage ? "" : Capitalize(RemoveAccents(currentPageName).Replace(" ", ""));

                string baseActionName = $"{methodVerb}{targetSuffix}{dedupeSuffix}";
                string safeActionName = baseActionName;
                int actCounter = 1;
                while (activePage.Actions.Any(a => a.MethodName == safeActionName))
                {
                    actCounter++; safeActionName = $"{baseActionName}Alt{actCounter}";
                }

                var newAction = new ActionDef
                {
                    MethodName = safeActionName,
                    MethodParams = step.MethodParams,
                    ErrorMsg = intent.FriendlyErrorMessage,
                    StepId = intent.StepId,
                    Diagnostics = intent.Diagnostics
                };
                newAction.CodeBody = GenerateJavaPomActionCode(intent, locatorPropName, varName);
                activePage.Actions.Add(newAction);

                step.MethodName = $"{methodVerb}{targetSuffix}{pageSuffix}{dedupeSuffix}";
                string actionCallArgs = string.IsNullOrEmpty(varName) ? "" : varName;
                string actionInstanceName = $"{Char.ToLower(activePage.ActionClassName[0]) + activePage.ActionClassName.Substring(1)}";
                step.ActionCallCode = $"{actionInstanceName}.{newAction.MethodName}({actionCallArgs});";

                stepDefinitions.Add(step);
            }

            sb.AppendLine("// =====================================================================");
            sb.AppendLine($"// 👑 {LanguageManager.GetString("LblPattern").ToUpper()}: BDD POM JAVA (4 LAYERS)");
            sb.AppendLine("// =====================================================================\n");

            // 1. FEATURE FILE
            sb.AppendLine($"// =====================================================================");
            sb.AppendLine($"// 📁 {LanguageManager.GetString("PomFeatureFile")}");
            sb.AppendLine($"// =====================================================================");
            sb.AppendLine($"/*\nFeature: {LanguageManager.GetString("BddFeatureTitle")}");
            string scenarioType = examples.Count > 0 ? LanguageManager.GetString("BddScenarioOutline") : LanguageManager.GetString("BddScenario");
            sb.AppendLine($"  {scenarioType}: {LanguageManager.GetString("BddMainScenario")}");
            foreach (var step in stepDefinitions) sb.AppendLine($"    {step.Keyword} {step.GherkinText}");
            if (examples.Count > 0)
            {
                sb.AppendLine($"\n    {LanguageManager.GetString("BddExamples")}:");
                sb.AppendLine("      | " + string.Join(" | ", examples.Keys) + " |");
                sb.AppendLine("      | " + string.Join(" | ", examples.Values) + " |");
            }
            sb.AppendLine("*/\n");

            // 2. PAGE OBJECTS (JAVA com @FindBy)
            sb.AppendLine($"// =====================================================================");
            sb.AppendLine($"// 📁 {LanguageManager.GetString("PomPageClass")}");
            sb.AppendLine($"// =====================================================================");
            sb.AppendLine("package liteautomation.generatedtests.pages;\n");
            sb.AppendLine("import org.openqa.selenium.WebElement;");
            sb.AppendLine("import org.openqa.selenium.support.FindBy;");
            sb.AppendLine("import org.openqa.selenium.support.PageFactory;");
            sb.AppendLine("import org.openqa.selenium.WebDriver;\n");

            foreach (var page in pageDictionary.Values)
            {
                sb.AppendLine($"public class {page.ClassName} {{");
                sb.AppendLine($"    public {page.ClassName}(WebDriver driver) {{");
                sb.AppendLine($"        PageFactory.initElements(driver, this);");
                sb.AppendLine($"    }}\n");

                foreach (var loc in page.Locators)
                {
                    sb.AppendLine($"    // {LanguageManager.GetString("LogStep")} {loc.Value.StepId}");
                    sb.AppendLine($"    {loc.Value.RawLocator}");
                    string fieldName = Char.ToLower(loc.Key[0]) + loc.Key.Substring(1);
                    sb.AppendLine($"    public WebElement {fieldName};\n");
                }
                sb.AppendLine($"}}\n");
            }

            // 3. ACTIONS
            sb.AppendLine($"// =====================================================================");
            sb.AppendLine($"// 📁 {LanguageManager.GetString("PomActionClass")}");
            sb.AppendLine($"// =====================================================================");
            sb.AppendLine("package liteautomation.generatedtests.actions;\n");
            sb.AppendLine("import liteautomation.generatedtests.pages.*;");
            sb.AppendLine("import org.openqa.selenium.*;");
            sb.AppendLine("import org.openqa.selenium.support.ui.WebDriverWait;");
            sb.AppendLine("import org.openqa.selenium.support.ui.ExpectedConditions;");
            sb.AppendLine("import org.junit.Assert;\n");

            foreach (var page in pageDictionary.Values)
            {
                sb.AppendLine($"public class {page.ActionClassName} {{");
                sb.AppendLine($"    private WebDriver driver;");
                sb.AppendLine($"    private WebDriverWait wait;");
                sb.AppendLine($"    private {page.ClassName} page;\n");
                sb.AppendLine($"    public {page.ActionClassName}(WebDriver driver, WebDriverWait wait) {{");
                sb.AppendLine($"        this.driver = driver;");
                sb.AppendLine($"        this.wait = wait;");
                sb.AppendLine($"        this.page = new {page.ClassName}(driver);");
                sb.AppendLine($"    }}\n");

                foreach (var action in page.Actions)
                {
                    sb.AppendLine($"    // {LanguageManager.GetString("LogStep")} {action.StepId}");
                    if (config.IncludeReport && !string.IsNullOrWhiteSpace(action.Diagnostics))
                    {
                        var diag = action.Diagnostics.Trim();
                        if (!diag.StartsWith("//")) diag = "// " + diag;
                        sb.AppendLine($"    {diag}");
                    }

                    sb.AppendLine($"    public void {Char.ToLower(action.MethodName[0]) + action.MethodName.Substring(1)}({action.MethodParams}) {{");
                    if (!string.IsNullOrEmpty(action.ErrorMsg))
                    {
                        sb.AppendLine($"        try {{");
                        foreach (var line in action.CodeBody.Split('\n')) if (!string.IsNullOrWhiteSpace(line)) sb.AppendLine($"            {line.TrimEnd()}");
                        sb.AppendLine($"        }} catch (Exception e) {{");
                        sb.AppendLine($"            Assert.fail(\"{action.ErrorMsg}\");");
                        sb.AppendLine($"        }}");
                    }
                    else
                    {
                        foreach (var line in action.CodeBody.Split('\n')) if (!string.IsNullOrWhiteSpace(line)) sb.AppendLine($"        {line.TrimEnd()}");
                    }
                    sb.AppendLine($"    }}\n");
                }
                sb.AppendLine($"}}\n");
            }

            // 4. STEPS DEFINITIONS
            sb.AppendLine($"// =====================================================================");
            sb.AppendLine($"// 📁 {LanguageManager.GetString("PomStepClass")}");
            sb.AppendLine($"// =====================================================================");
            sb.AppendLine("package liteautomation.generatedtests.steps;\n");
            sb.AppendLine("import liteautomation.generatedtests.actions.*;");
            sb.AppendLine("import io.cucumber.java.pt.*;");
            sb.AppendLine("import io.cucumber.java.Before;");
            sb.AppendLine("import io.cucumber.java.After;");
            sb.AppendLine("import org.openqa.selenium.chrome.ChromeDriver;");
            sb.AppendLine("import org.openqa.selenium.chrome.ChromeOptions;");
            sb.AppendLine("import org.openqa.selenium.WebDriver;");
            sb.AppendLine("import org.openqa.selenium.support.ui.WebDriverWait;");
            sb.AppendLine("import java.time.Duration;\n");

            sb.AppendLine($"public class {testClassName}Steps {{");
            sb.AppendLine($"    private WebDriver driver;");
            sb.AppendLine($"    private WebDriverWait wait;");
            foreach (var page in pageDictionary.Values)
            {
                string actionVar = $"{Char.ToLower(page.ActionClassName[0]) + page.ActionClassName.Substring(1)}";
                sb.AppendLine($"    private {page.ActionClassName} {actionVar};");
            }

            sb.AppendLine($"\n    @Before");
            sb.AppendLine($"    public void setup() {{");
            sb.AppendLine($"        ChromeOptions options = new ChromeOptions();");
            sb.AppendLine($"        options.addArguments(\"--disable-blink-features=AutomationControlled\");");
            sb.AppendLine($"        driver = new ChromeDriver(options);");
            sb.AppendLine($"        driver.manage().window().maximize();");
            sb.AppendLine($"        wait = new WebDriverWait(driver, Duration.ofSeconds(10));\n");
            foreach (var page in pageDictionary.Values)
            {
                string actionVar = $"{Char.ToLower(page.ActionClassName[0]) + page.ActionClassName.Substring(1)}";
                sb.AppendLine($"        {actionVar} = new {page.ActionClassName}(driver, wait);");
            }
            sb.AppendLine($"    }}\n");

            sb.AppendLine($"    @After");
            sb.AppendLine($"    public void teardown() {{");
            sb.AppendLine($"        if (driver != null) driver.quit();");
            sb.AppendLine($"    }}\n");

            var uniqueStepDefs = new HashSet<string>();
            foreach (var step in stepDefinitions)
            {
                string safeMethodName = step.MethodName;
                int safetyCounter = 1;
                while (uniqueStepDefs.Contains(safeMethodName)) { safetyCounter++; safeMethodName = $"{step.MethodName}Alt{safetyCounter}"; }
                uniqueStepDefs.Add(safeMethodName);

                string javaAnnotation = Capitalize(step.StepDefAttribute);
                sb.AppendLine($"    @{javaAnnotation}(\"{step.StepDefText}\")");
                sb.AppendLine($"    public void {Char.ToLower(safeMethodName[0]) + safeMethodName.Substring(1)}({step.MethodParams}) {{");
                sb.AppendLine($"        // {LanguageManager.GetString("LogStep")} {step.StepId}");
                if (config.IncludeReport && !string.IsNullOrWhiteSpace(step.Diagnostics))
                {
                    var diag = step.Diagnostics.Trim();
                    if (!diag.StartsWith("//")) diag = "// " + diag;
                    sb.AppendLine($"        {diag}");
                }

                sb.AppendLine($"        {step.ActionCallCode}");
                sb.AppendLine($"    }}\n");
            }
            sb.AppendLine($"}}");

            return sb.ToString();
        }

        private string EnsureUniqueVar(Dictionary<string, string> examples, string baseName, string value)
        {
            string varName = string.IsNullOrEmpty(baseName) ? "valor" : baseName;
            int counter = 1;
            while (examples.ContainsKey(varName) && examples[varName] != value) { counter++; varName = baseName + counter; }
            examples[varName] = value; return varName;
        }

        private string EnsureUniqueLocatorProp(Dictionary<string, LocatorDef> locators, string baseName, string rawLocator, string stepId)
        {
            string propName = Capitalize(baseName);
            if (string.IsNullOrEmpty(propName)) propName = "Elemento";
            int counter = 1; string currentName = propName;

            while (locators.ContainsKey(currentName) && locators[currentName].RawLocator != rawLocator)
            {
                counter++;
                currentName = propName + counter;
            }

            if (!locators.ContainsKey(currentName)) locators[currentName] = new LocatorDef { RawLocator = rawLocator, StepId = stepId };
            else if (!locators[currentName].StepId.Split(new[] { ", " }, StringSplitOptions.None).Contains(stepId)) locators[currentName].StepId += $", {stepId}";

            return currentName;
        }

        private string FormatJavaFindBy(string loc)
        {
            if (string.IsNullOrEmpty(loc) || loc.Contains("VAZIO") || loc.Contains("NÃO ENCONTRADO") || loc.Contains("AMBÍGUO"))
                return "@FindBy(tagName = \"body\")";

            if (loc.StartsWith("/") || loc.StartsWith("(")) return $"@FindBy(xpath = \"{loc}\")";

            return $"@FindBy(css = \"{loc}\")";
        }

        // 🚀 AQUI ESTÁ O FIX: {{block: 'center'}}
        private string GenerateJavaPomActionCode(AutomationIntent intent, string propName, string varName)
        {
            string javaPropName = string.IsNullOrEmpty(propName) ? "" : Char.ToLower(propName[0]) + propName.Substring(1);
            string loc = string.IsNullOrEmpty(javaPropName) ? "" : $"page.{javaPropName}";

            switch (intent.Type)
            {
                case IntentType.NavigateToUrl: return $"driver.get(\"{intent.Value}\");";
                case IntentType.WaitUrlChange: return $"wait.until(ExpectedConditions.urlContains(\"{intent.Value}\"));";
                case IntentType.Click: return $"wait.until(ExpectedConditions.visibilityOf({loc}));\n((JavascriptExecutor)driver).executeScript(\"arguments[0].scrollIntoView({{block: 'center'}});\", {loc});\nwait.until(ExpectedConditions.elementToBeClickable({loc}));\ntry {{\n    {loc}.click();\n}} catch (Exception e) {{\n    ((JavascriptExecutor)driver).executeScript(\"arguments[0].click();\", {loc});\n}}";
                case IntentType.Hover: return $"wait.until(ExpectedConditions.visibilityOf({loc}));\nnew org.openqa.selenium.interactions.Actions(driver).moveToElement({loc}).perform();";
                case IntentType.Blur: return $"driver.findElement(By.tagName(\"body\")).click();";
                case IntentType.InputText: return $"wait.until(ExpectedConditions.visibilityOf({loc}));\n{loc}.clear();\n{loc}.sendKeys({varName});";
                case IntentType.KeyPress: return $"wait.until(ExpectedConditions.visibilityOf({loc})).sendKeys({MapJavaKey(intent.Key)});";
                case IntentType.ScrollTo: return $"wait.until(ExpectedConditions.visibilityOf({loc}));\n((JavascriptExecutor)driver).executeScript(\"arguments[0].scrollIntoView({{block: 'center'}});\", {loc});";
                case IntentType.AssertVisible: return $"Assert.assertTrue(wait.until(ExpectedConditions.visibilityOf({loc})).isDisplayed());";
                case IntentType.AssertEnabled: return $"Assert.assertTrue(wait.until(ExpectedConditions.visibilityOf({loc})).isEnabled());";
                default: return "";
            }
        }

        private string MapJavaKey(string key) => key.ToLower() switch { "enter" => "Keys.ENTER", "tab" => "Keys.TAB", "escape" => "Keys.ESCAPE", "space" => "Keys.SPACE", _ => $"\"{key}\"" };
        private string Capitalize(string text) { if (string.IsNullOrEmpty(text)) return text; return char.ToUpper(text[0]) + text.Substring(1); }
        private string RemoveAccents(string text) { var n = text.Normalize(NormalizationForm.FormD); var sb = new StringBuilder(); foreach (var c in n) { if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(c); } return sb.ToString().Normalize(NormalizationForm.FormC); }
    }
}