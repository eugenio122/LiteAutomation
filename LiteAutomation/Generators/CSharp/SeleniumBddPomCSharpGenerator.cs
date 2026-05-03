using System.Collections.Generic;
using System.Text;
using LiteAutomation.Core;
using LiteAutomation.DTOs;
using LiteAutomation.Enums;
using LiteAutomation.Interfaces;

namespace LiteAutomation.Generators.CSharp
{
    public class SeleniumBddPomCSharpGenerator : ICodeGenerator
    {
        public string GenerateCode(WorkspaceState workspace, GeneratorConfig config, string testClassName = "GeneratedTest")
        {
            var sb = new StringBuilder();
            var analyzer = new DeltaAnalyzer();
            var router = new ArchitectureRouter();

            var intents = workspace.IntentCache;
            var pages = router.BuildPomStructure(intents);
            var scenario = router.BuildBddStructure(intents);

            sb.AppendLine("// =====================================================================");
            sb.AppendLine("// 👑 ARQUITETURA SUPREMA: BDD + PAGE OBJECT MODEL");
            sb.AppendLine("// =====================================================================");

            // 1. GHERKIN FEATURE
            sb.AppendLine("/*\nFeature: Automação Gerada pelo LiteAutomation");
            sb.AppendLine($"  Scenario: {scenario.FeatureTitle}");
            foreach (var step in scenario.Steps) sb.AppendLine($"    {step.Keyword} {step.TextDescription}");
            sb.AppendLine("*/\n");

            sb.AppendLine("using System;\nusing TechTalk.SpecFlow;\nusing OpenQA.Selenium;\nusing OpenQA.Selenium.Chrome;\nusing OpenQA.Selenium.Support.UI;\nusing NUnit.Framework;\n");
            sb.AppendLine("namespace LiteAutomation.GeneratedTests.BDDPOM\n{");

            // 2. GERAR AS CLASSES DE PÁGINA (POM)
            foreach (var page in pages)
            {
                sb.AppendLine($"    public class {page.ClassName}");
                sb.AppendLine($"    {{");
                sb.AppendLine($"        private IWebDriver driver; private WebDriverWait wait;");
                foreach (var loc in page.MappedLocators) sb.AppendLine($"        private By {loc.Value} = {FormatLocator(loc.Key)};");
                sb.AppendLine($"        public {page.ClassName}(IWebDriver driver, WebDriverWait wait) {{ this.driver = driver; this.wait = wait; }}");

                foreach (var action in page.PageActions)
                {
                    sb.AppendLine($"        public void ExecutarAcao_{action.StepId.Replace(".", "_")}()");
                    sb.AppendLine($"        {{");
                    string locVar = page.MappedLocators.ContainsKey(action.TargetLocator) ? page.MappedLocators[action.TargetLocator] : FormatLocator(action.TargetLocator);
                    string code = GenerateActionCode(action, locVar);

                    if (!string.IsNullOrEmpty(action.FriendlyErrorMessage))
                    {
                        sb.AppendLine($"            try {{ {code.Replace("\n", " ")} }}");
                        sb.AppendLine($"            catch (WebDriverTimeoutException) {{ Assert.Fail(\"{action.FriendlyErrorMessage}\"); }}");
                    }
                    else
                    {
                        foreach (var line in code.Split('\n')) { if (!string.IsNullOrWhiteSpace(line)) sb.AppendLine($"            {line}"); }
                    }
                    sb.AppendLine($"        }}");
                }
                sb.AppendLine($"    }}\n");
            }

            // 3. GERAR O STEP DEFINITION (Chamando o POM)
            sb.AppendLine($"    [Binding]");
            sb.AppendLine($"    public class {testClassName}Steps");
            sb.AppendLine($"    {{");
            sb.AppendLine($"        private IWebDriver driver; private WebDriverWait wait;");

            // Instanciar Páginas no Step Definition
            foreach (var page in pages) sb.AppendLine($"        private {page.ClassName} {page.ClassName.ToLower()};");

            sb.AppendLine();
            sb.AppendLine($"        [BeforeScenario] public void Setup() {{ ");
            sb.AppendLine($"            var options = new ChromeOptions(); options.AddArgument(\"--disable-blink-features=AutomationControlled\"); driver = new ChromeDriver(options); driver.Manage().Window.Maximize(); wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));");
            foreach (var page in pages) sb.AppendLine($"            {page.ClassName.ToLower()} = new {page.ClassName}(driver, wait);");
            sb.AppendLine($"        }}");
            sb.AppendLine($"        [AfterScenario] public void Teardown() {{ driver?.Dispose(); }}");
            sb.AppendLine();

            foreach (var step in scenario.Steps)
            {
                sb.AppendLine($"        [{step.Keyword}(@\"{step.TextDescription}\")]");
                sb.AppendLine($"        public void {step.Keyword}{step.TextDescription.Replace(" ", "").Replace("ç", "c").Replace("ã", "a")}()");
                sb.AppendLine($"        {{");

                foreach (var intent in step.InternalIntents)
                {
                    if (intent.Type == IntentType.NavigateToUrl)
                        sb.AppendLine($"            driver.Navigate().GoToUrl(\"{intent.Value}\");");
                    else if (intent.Type == IntentType.WaitUrlChange)
                        sb.AppendLine($"            wait.Until(d => d.Url.Contains(\"{intent.Value}\"));");
                    else if (intent.Type != IntentType.Unknown && !intent.IsNewStepHeader)
                    {
                        foreach (var page in pages)
                        {
                            if (page.PageActions.Contains(intent))
                            {
                                sb.AppendLine($"            {page.ClassName.ToLower()}.ExecutarAcao_{intent.StepId.Replace(".", "_")}();");
                                break;
                            }
                        }
                    }
                }
                sb.AppendLine($"        }}");
                sb.AppendLine();
            }

            sb.AppendLine($"    }}");
            sb.AppendLine($"}}");

            return sb.ToString();
        }

        private string GenerateActionCode(AutomationIntent intent, string loc)
        {
            switch (intent.Type)
            {
                case IntentType.Click: return $"var el = wait.Until(d => d.FindElement({loc}));\n((IJavaScriptExecutor)driver).ExecuteScript(\"arguments[0].scrollIntoView({{block: 'center'}});\", el);\ntry {{ el.Click(); }} catch {{ ((IJavaScriptExecutor)driver).ExecuteScript(\"arguments[0].click();\", el); }}";
                case IntentType.InputText: return $"var txt = wait.Until(d => d.FindElement({loc}));\ntxt.Clear(); txt.SendKeys(\"{intent.Value.Replace("\"", "\\\"")}\");";
                case IntentType.KeyPress: return $"wait.Until(d => d.FindElement({loc})).SendKeys({MapKey(intent.Key)});";
                case IntentType.ScrollTo: return $"var scrl = wait.Until(d => d.FindElement({loc}));\n((IJavaScriptExecutor)driver).ExecuteScript(\"arguments[0].scrollIntoView({{block: 'center'}});\", scrl);";
                case IntentType.AssertVisible: return $"Assert.IsTrue(wait.Until(d => d.FindElement({loc})).Displayed);";
                case IntentType.AssertEnabled: return $"wait.Until(d => d.FindElement({loc}).Enabled);";
                default: return "";
            }
        }
        private string FormatLocator(string loc) { if (string.IsNullOrEmpty(loc)) return "By.XPath(\"/* VAZIO */\")"; if (loc.StartsWith("By.") || loc.StartsWith("Page.")) return loc; if (loc.StartsWith("/") || loc.StartsWith("(")) return $"By.XPath(\"{loc}\")"; return $"By.CssSelector(\"{loc}\")"; }
        private string MapKey(string key) => key.ToLower() switch { "enter" => "Keys.Enter", "tab" => "Keys.Tab", "escape" => "Keys.Escape", "space" => "Keys.Space", _ => $"\"{key}\"" };
    }
}