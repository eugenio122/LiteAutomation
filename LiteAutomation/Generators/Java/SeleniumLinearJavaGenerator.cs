using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LiteAutomation.Core;
using LiteAutomation.DTOs;
using LiteAutomation.Enums;
using LiteAutomation.Interfaces;

namespace LiteAutomation.Generators.Java
{
    public class SeleniumLinearJavaGenerator : ICodeGenerator
    {
        public string GenerateCode(WorkspaceState workspace, GeneratorConfig config, string testClassName = "GeneratedTest")
        {
            var sb = new StringBuilder();
            WriteHeader(sb, testClassName);

            // 🚀 1. INJEÇÃO DA NAVEGAÇÃO INICIAL (URL) - EQUIVALENTE AO C#
            var firstStepWithUrl = workspace.RawSteps?.FirstOrDefault(s => s.ObservedContext != null && !string.IsNullOrEmpty(s.ObservedContext.Url));
            if (firstStepWithUrl != null)
            {
                sb.AppendLine($"        // ---------------------------------------------------------");
                sb.AppendLine($"        // ACESSAR SISTEMA INICIAL");
                sb.AppendLine($"        // ---------------------------------------------------------");
                sb.AppendLine($"        driver.get(\"{firstStepWithUrl.ObservedContext.Url}\");");
                sb.AppendLine();
            }

            var analyzer = new DeltaAnalyzer();
            var validIntents = analyzer.Analyze(workspace.RawSteps, config)
                                       .Where(i => i.Type != IntentType.Unknown)
                                       .ToList();

            var cleanIntents = new List<AutomationIntent>();

            // 🚀 2. DEDUPLICAÇÃO INTELIGENTE (Mesma lógica do C#)
            for (int i = 0; i < validIntents.Count; i++)
            {
                var current = validIntents[i];
                if (current.Type == IntentType.Blur) continue;

                if (current.Type == IntentType.InputText)
                {
                    var lastAdded = cleanIntents.LastOrDefault();
                    if (lastAdded != null && lastAdded.Type == IntentType.InputText && current.TargetLocator == lastAdded.TargetLocator)
                    {
                        lastAdded.Value = current.Value;
                        continue;
                    }
                }

                if (current.Type == IntentType.Click)
                {
                    var nextAction = validIntents.Skip(i + 1).FirstOrDefault(x => x.Type != IntentType.Blur && x.Type != IntentType.Unknown);
                    if (nextAction != null && (nextAction.Type == IntentType.InputText || nextAction.Type == IntentType.KeyPress))
                    {
                        if (current.TargetLocator == nextAction.TargetLocator)
                            continue;
                    }
                }
                cleanIntents.Add(current);
            }

            // 🚀 3. GERAÇÃO DE CÓDIGO JAVA
            foreach (var intent in cleanIntents)
            {
                if (intent.IsNewStepHeader)
                {
                    sb.AppendLine($"        // ---------------------------------------------------------");
                    sb.AppendLine($"        // {(intent.StepDescription ?? "").ToUpper()}");
                    sb.AppendLine($"        // ---------------------------------------------------------");
                    continue;
                }

                if (!string.IsNullOrEmpty(intent.Diagnostics))
                    sb.AppendLine($"        // {intent.Diagnostics}");

                // 🚀 FALLBACK: Acesso direto às gavetas WebDriverBiDi (Fim do erro .XPath)
                var ids = intent.StepId?.Split('.');
                if (ids != null && ids.Length == 2 && int.TryParse(ids[0], out int mIdx) && int.TryParse(ids[1], out int micIdx))
                {
                    var rawMain = workspace.RawSteps?.FirstOrDefault(s => s.StepIndex == mIdx);
                    var interaction = micIdx > 0 ? rawMain?.InteractionTrail?.ElementAtOrDefault(micIdx - 1) : null;

                    if (interaction != null)
                    {
                        string locLowerCheck = intent.TargetLocator?.ToLower() ?? "";
                        if (string.IsNullOrEmpty(locLowerCheck) || locLowerCheck.Contains("body") || locLowerCheck.Contains("vazio") || locLowerCheck.Contains("ambígu"))
                        {
                            var bidi = interaction.WebDriverBiDi?.ElementData;
                            if (!string.IsNullOrWhiteSpace(interaction.ElementId)) intent.TargetLocator = $"By.id(\"{interaction.ElementId}\")";
                            else if (!string.IsNullOrWhiteSpace(bidi?.SelectorSet?.XpathAbsolute?.Value)) intent.TargetLocator = $"By.xpath(\"{bidi.SelectorSet.XpathAbsolute.Value}\")";
                            else if (!string.IsNullOrWhiteSpace(interaction.VisibleText)) { string safeText = interaction.VisibleText.Replace("\"", "\\\"").Replace("'", "\\'"); intent.TargetLocator = $"By.xpath(\"//*[normalize-space(text())='{safeText}']\")"; }
                            else if (!string.IsNullOrWhiteSpace(interaction.Classes)) { string cleanClasses = string.Join(".", interaction.Classes.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)); intent.TargetLocator = $"By.cssSelector(\"{interaction.TagName}.{cleanClasses}\")"; }
                            else if (!string.IsNullOrWhiteSpace(interaction.TagName)) intent.TargetLocator = $"By.tagName(\"{interaction.TagName}\")";
                        }
                    }
                }

                string loc = FormatLocator(intent.TargetLocator);
                string id = intent.StepId?.Replace(".", "_") ?? Guid.NewGuid().ToString("N").Substring(0, 6);
                string actionCode = "";

                switch (intent.Type)
                {
                    case IntentType.WaitUrlChange:
                        actionCode = $"wait.until(ExpectedConditions.urlContains(\"{intent.Value}\"));";
                        break;
                    case IntentType.Click:
                        actionCode = $"WebElement btn_{id} = wait.until(ExpectedConditions.presenceOfElementLocated({loc}));\n((JavascriptExecutor) driver).executeScript(\"arguments[0].scrollIntoView({{block: 'center'}});\", btn_{id});\ntry {{ wait.until(ExpectedConditions.elementToBeClickable(btn_{id})).click(); }} catch (Exception e) {{ ((JavascriptExecutor) driver).executeScript(\"arguments[0].click();\", btn_{id}); }}";
                        break;
                    case IntentType.InputText:
                        string safeVal = intent.Value?.Replace("\"", "\\\"").Replace("\n", "\\n") ?? "";
                        actionCode = $"WebElement txt_{id} = wait.until(ExpectedConditions.presenceOfElementLocated({loc}));\ntxt_{id}.clear(); txt_{id}.sendKeys(\"{safeVal}\");";
                        break;
                    case IntentType.KeyPress:
                        actionCode = $"wait.until(ExpectedConditions.presenceOfElementLocated({loc})).sendKeys({MapKey(intent.Key)});\ntry {{ Thread.sleep(500); }} catch (InterruptedException e) {{ e.printStackTrace(); }}";
                        break;
                    case IntentType.ScrollTo:
                        actionCode = $"WebElement scrl_{id} = wait.until(ExpectedConditions.presenceOfElementLocated({loc}));\n((JavascriptExecutor) driver).executeScript(\"arguments[0].scrollIntoView({{block: 'center'}});\", scrl_{id});";
                        break;
                    case IntentType.AssertVisible:
                        actionCode = $"assertTrue(wait.until(ExpectedConditions.visibilityOfElementLocated({loc})).isDisplayed());";
                        break;
                    case IntentType.AssertEnabled:
                        actionCode = $"assertTrue(wait.until(ExpectedConditions.elementToBeClickable({loc})).isEnabled());";
                        break;
                }

                if (!string.IsNullOrEmpty(actionCode))
                {
                    if (!string.IsNullOrEmpty(intent.FriendlyErrorMessage))
                    {
                        sb.AppendLine($"        try {{");
                        foreach (var line in actionCode.Split('\n')) { sb.AppendLine($"            {line}"); }
                        sb.AppendLine($"        }} catch (Exception e) {{");
                        sb.AppendLine($"            fail(\"{intent.FriendlyErrorMessage}\");");
                        sb.AppendLine($"        }}");
                    }
                    else
                    {
                        foreach (var line in actionCode.Split('\n')) { sb.AppendLine($"        {line}"); }
                    }
                    sb.AppendLine();
                }
            }

            WriteFooter(sb);
            return sb.ToString();
        }

        private string FormatLocator(string loc)
        {
            if (string.IsNullOrEmpty(loc) || loc.Contains("VAZIO") || loc.Contains("NÃO ENCONTRADO") || loc.Contains("AMBÍGUO")) return "By.tagName(\"body\")";
            if (loc.StartsWith("By.") || loc.StartsWith("Page.")) return loc.Replace("By.CssSelector", "By.cssSelector").Replace("By.XPath", "By.xpath").Replace("By.Id", "By.id");
            if (loc.StartsWith("xpath=") || loc.StartsWith("/") || loc.StartsWith("(")) return $"By.xpath(\"{loc.Replace("xpath=", "")}\")";
            if (loc.StartsWith("css=")) return $"By.cssSelector(\"{loc.Replace("css=", "")}\")";
            return $"By.cssSelector(\"{loc}\")";
        }

        private string MapKey(string key) => key?.ToLower().Replace("keypress_", "") switch { "enter" => "Keys.ENTER", "tab" => "Keys.TAB", "escape" => "Keys.ESCAPE", "space" => "Keys.SPACE", _ => $"\"{key}\"" };

        private void WriteHeader(StringBuilder sb, string className)
        {
            sb.AppendLine("package liteautomation.generated;\n\nimport org.junit.jupiter.api.*;\nimport org.openqa.selenium.*;\nimport org.openqa.selenium.chrome.*;\nimport org.openqa.selenium.support.ui.*;\nimport java.time.Duration;\nimport java.util.Collections;\nimport java.util.HashMap;\nimport java.util.Map;\nimport static org.junit.jupiter.api.Assertions.assertTrue;\nimport static org.junit.jupiter.api.Assertions.fail;");
            sb.AppendLine($"\npublic class {className} {{\n    private WebDriver driver;\n    private WebDriverWait wait;");

            // 🚀 SETUP JAVA COM SEU ANTI-BOT EQUIVALENTE
            sb.AppendLine("\n    @BeforeEach");
            sb.AppendLine("    public void setUp() {");
            sb.AppendLine("        ChromeOptions options = new ChromeOptions();");
            sb.AppendLine("        options.setExperimentalOption(\"excludeSwitches\", Collections.singletonList(\"enable-automation\"));");
            sb.AppendLine("        options.setExperimentalOption(\"useAutomationExtension\", false);");
            sb.AppendLine("        Map<String, Object> prefs = new HashMap<String, Object>();");
            sb.AppendLine("        prefs.put(\"credentials_enable_service\", false);");
            sb.AppendLine("        prefs.put(\"profile.password_manager_enabled\", false);");
            sb.AppendLine("        options.setExperimentalOption(\"prefs\", prefs);");
            sb.AppendLine("        options.addArguments(\"--disable-blink-features=AutomationControlled\");");
            sb.AppendLine("        options.addArguments(\"user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36\");");
            sb.AppendLine("\n        driver = new ChromeDriver(options);");
            sb.AppendLine("        driver.manage().window().maximize();");
            sb.AppendLine("        wait = new WebDriverWait(driver, Duration.ofSeconds(10));");
            sb.AppendLine("    }");

            // 🚀 TEARDOWN JAVA EQUIVALENTE
            sb.AppendLine("\n    @AfterEach");
            sb.AppendLine("    public void tearDown() {");
            sb.AppendLine("        if (driver != null) {");
            sb.AppendLine("            driver.quit();");
            sb.AppendLine("            driver = null;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");

            sb.AppendLine("\n    @Test");
            sb.AppendLine("    public void executeScenario() {");
        }

        private void WriteFooter(StringBuilder sb) { sb.AppendLine("    }\n}"); }
    }
}