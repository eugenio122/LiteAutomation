using System.Collections.Generic;
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

            var intents = workspace.IntentCache;

            foreach (var intent in intents)
            {
                if (intent.IsNewStepHeader)
                {
                    sb.AppendLine($"        // ---------------------------------------------------------");
                    sb.AppendLine($"        // {intent.StepDescription.ToUpper()}");
                    sb.AppendLine($"        // ---------------------------------------------------------");
                    continue;
                }

                if (!string.IsNullOrEmpty(intent.Diagnostics))
                    sb.AppendLine($"        // {intent.Diagnostics}");

                string loc = FormatLocator(intent.TargetLocator);
                string id = intent.StepId.Replace(".", "_");
                string actionCode = "";

                switch (intent.Type)
                {
                    case IntentType.NavigateToUrl: actionCode = $"driver.get(\"{intent.Value}\");"; break;
                    case IntentType.WaitUrlChange: actionCode = $"wait.until(ExpectedConditions.urlContains(\"{intent.Value}\"));"; break;
                    case IntentType.Click:
                        actionCode = $"WebElement btn_{id} = wait.until(ExpectedConditions.presenceOfElementLocated({loc}));\n((JavascriptExecutor) driver).executeScript(\"arguments[0].scrollIntoView({{block: 'center'}});\", btn_{id});\ntry {{ wait.until(ExpectedConditions.elementToBeClickable(btn_{id})).click(); }} catch (Exception e) {{ ((JavascriptExecutor) driver).executeScript(\"arguments[0].click();\", btn_{id}); }}";
                        break;
                    case IntentType.InputText:
                        string safeVal = intent.Value.Replace("\"", "\\\"").Replace("\n", "\\n");
                        actionCode = $"WebElement txt_{id} = wait.until(ExpectedConditions.presenceOfElementLocated({loc}));\ntxt_{id}.clear(); txt_{id}.sendKeys(\"{safeVal}\");";
                        break;
                    case IntentType.KeyPress: actionCode = $"wait.until(ExpectedConditions.presenceOfElementLocated({loc})).sendKeys({MapKey(intent.Key)});"; break;
                    case IntentType.ScrollTo: actionCode = $"WebElement scrl_{id} = wait.until(ExpectedConditions.presenceOfElementLocated({loc}));\n((JavascriptExecutor) driver).executeScript(\"arguments[0].scrollIntoView({{block: 'center'}});\", scrl_{id});"; break;
                    case IntentType.AssertVisible: actionCode = $"assertTrue(wait.until(ExpectedConditions.visibilityOfElementLocated({loc})).isDisplayed());"; break;
                    case IntentType.AssertEnabled: actionCode = $"wait.until(ExpectedConditions.elementToBeClickable({loc}));"; break;
                }

                if (!string.IsNullOrEmpty(actionCode))
                {
                    // 🛡️ TRATAMENTO DE ERROS HUMANIZADO (Java usa org.openqa.selenium.TimeoutException)
                    if (!string.IsNullOrEmpty(intent.FriendlyErrorMessage))
                    {
                        sb.AppendLine($"        try {{");
                        foreach (var line in actionCode.Split('\n')) { sb.AppendLine($"            {line}"); }
                        sb.AppendLine($"        }} catch (TimeoutException e) {{");
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
            if (string.IsNullOrEmpty(loc)) return "By.xpath(\"/* VAZIO */\")";
            if (loc.StartsWith("By.") || loc.StartsWith("Page.")) return loc.Replace("By.CssSelector", "By.cssSelector").Replace("By.XPath", "By.xpath");
            if (loc.StartsWith("/") || loc.StartsWith("(")) return $"By.xpath(\"{loc}\")";
            return $"By.cssSelector(\"{loc}\")";
        }

        private string MapKey(string key) => key.ToLower() switch { "enter" => "Keys.ENTER", "tab" => "Keys.TAB", "escape" => "Keys.ESCAPE", "space" => "Keys.SPACE", _ => $"\"{key}\"" };

        private void WriteHeader(StringBuilder sb, string className)
        {
            sb.AppendLine("package liteautomation.generated;\n\nimport org.junit.jupiter.api.*;\nimport org.openqa.selenium.*;\nimport org.openqa.selenium.chrome.*;\nimport org.openqa.selenium.support.ui.*;\nimport java.time.Duration;\nimport static org.junit.jupiter.api.Assertions.assertTrue;\nimport static org.junit.jupiter.api.Assertions.fail;");
            sb.AppendLine($"\npublic class {className} {{\n    private WebDriver driver;\n    private WebDriverWait wait;");
            sb.AppendLine("    @BeforeEach\n    public void setUp() { ChromeOptions options = new ChromeOptions(); options.addArguments(\"--disable-blink-features=AutomationControlled\"); driver = new ChromeDriver(options); driver.manage().window().maximize(); wait = new WebDriverWait(driver, Duration.ofSeconds(10)); }");
            sb.AppendLine("    @AfterEach\n    public void tearDown() { if (driver != null) driver.quit(); }");
            sb.AppendLine("    @Test\n    public void executeScenario() {");
        }
        private void WriteFooter(StringBuilder sb) { sb.AppendLine("    }\n}"); }
    }
}