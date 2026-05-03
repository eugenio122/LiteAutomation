using System.Collections.Generic;
using System.Text;
using LiteAutomation.Core;
using LiteAutomation.DTOs;
using LiteAutomation.Enums;
using LiteAutomation.Interfaces;

namespace LiteAutomation.Generators.CSharp
{
    public class SeleniumLinearCSharpGenerator : ICodeGenerator
    {
        public string GenerateCode(WorkspaceState workspace, GeneratorConfig config, string testClassName = "GeneratedTest")
        {
            var sb = new StringBuilder();
            WriteHeader(sb, testClassName, config.GlobalTimeoutMs);

            var intents = workspace.IntentCache;

            foreach (var intent in intents)
            {
                if (intent.IsNewStepHeader)
                {
                    sb.AppendLine($"            // ---------------------------------------------------------");
                    sb.AppendLine($"            // {intent.StepDescription.ToUpper()}");
                    sb.AppendLine($"            // ---------------------------------------------------------");
                    continue;
                }

                if (!string.IsNullOrEmpty(intent.Diagnostics))
                    sb.AppendLine($"            // {intent.Diagnostics}");

                string loc = FormatLocator(intent.TargetLocator);
                string id = intent.StepId.Replace(".", "_");
                string actionCode = "";

                switch (intent.Type)
                {
                    case IntentType.NavigateToUrl:
                        actionCode = $"driver.Navigate().GoToUrl(\"{intent.Value}\");";
                        break;
                    case IntentType.WaitUrlChange:
                        actionCode = $"wait.Until(d => d.Url.Contains(\"{intent.Value}\"));";
                        break;
                    case IntentType.Click:
                        actionCode = $"var el_{id} = wait.Until(d => d.FindElement({loc}));\n((IJavaScriptExecutor)driver).ExecuteScript(\"arguments[0].scrollIntoView({{block: 'center'}});\", el_{id});\ntry {{ el_{id}.Click(); }} catch {{ ((IJavaScriptExecutor)driver).ExecuteScript(\"arguments[0].click();\", el_{id}); }}";
                        break;
                    case IntentType.InputText:
                        string safeVal = intent.Value.Replace("\"", "\\\"").Replace("\n", "\\n");
                        actionCode = $"var txt_{id} = wait.Until(d => d.FindElement({loc}));\ntxt_{id}.Clear(); txt_{id}.SendKeys(\"{safeVal}\");";
                        break;
                    case IntentType.KeyPress:
                        actionCode = $"wait.Until(d => d.FindElement({loc})).SendKeys({MapKey(intent.Key)});";
                        break;
                    case IntentType.ScrollTo:
                        actionCode = $"var scrl_{id} = wait.Until(d => d.FindElement({loc}));\n((IJavaScriptExecutor)driver).ExecuteScript(\"arguments[0].scrollIntoView({{block: 'center'}});\", scrl_{id});";
                        break;
                    case IntentType.AssertVisible:
                        actionCode = $"Assert.IsTrue(wait.Until(d => d.FindElement({loc})).Displayed);";
                        break;
                    case IntentType.AssertEnabled:
                        actionCode = $"wait.Until(d => d.FindElement({loc}).Enabled);";
                        break;
                }

                if (!string.IsNullOrEmpty(actionCode))
                {
                    // 🛡️ TRATAMENTO DE ERROS HUMANIZADO
                    if (!string.IsNullOrEmpty(intent.FriendlyErrorMessage))
                    {
                        sb.AppendLine($"            try");
                        sb.AppendLine($"            {{");
                        foreach (var line in actionCode.Split('\n')) { sb.AppendLine($"                {line}"); }
                        sb.AppendLine($"            }}");
                        sb.AppendLine($"            catch (WebDriverTimeoutException)");
                        sb.AppendLine($"            {{");
                        sb.AppendLine($"                Assert.Fail(\"{intent.FriendlyErrorMessage}\");");
                        sb.AppendLine($"            }}");
                    }
                    else
                    {
                        foreach (var line in actionCode.Split('\n')) { sb.AppendLine($"            {line}"); }
                    }
                    sb.AppendLine();
                }
            }

            WriteFooter(sb);
            return sb.ToString();
        }

        private string FormatLocator(string loc)
        {
            if (string.IsNullOrEmpty(loc)) return "By.XPath(\"/* VAZIO */\")";
            if (loc.StartsWith("By.") || loc.StartsWith("Page.")) return loc;
            if (loc.StartsWith("/") || loc.StartsWith("(")) return $"By.XPath(\"{loc}\")";
            return $"By.CssSelector(\"{loc}\")";
        }

        private string MapKey(string key) => key.ToLower() switch { "enter" => "Keys.Enter", "tab" => "Keys.Tab", "escape" => "Keys.Escape", "space" => "Keys.Space", _ => $"\"{key}\"" };

        private void WriteHeader(StringBuilder sb, string className, int timeout)
        {
            sb.AppendLine("using System;\nusing OpenQA.Selenium;\nusing OpenQA.Selenium.Chrome;\nusing OpenQA.Selenium.Support.UI;\nusing NUnit.Framework;");
            sb.AppendLine($"namespace LiteAutomation.GeneratedTests {{\n    public class {className} {{\n        private IWebDriver driver;\n        private WebDriverWait wait;");
            sb.AppendLine("        [SetUp] public void Setup() { var options = new ChromeOptions(); options.AddArgument(\"--disable-blink-features=AutomationControlled\"); driver = new ChromeDriver(options); driver.Manage().Window.Maximize(); wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10)); }");
            sb.AppendLine("        [TearDown] public void Teardown() { driver?.Dispose(); }");
            sb.AppendLine($"        [Test]\n        [Timeout({timeout})]\n        public void ExecuteScenario() {{");
        }
        private void WriteFooter(StringBuilder sb) { sb.AppendLine("        }\n    }\n}"); }
    }
}