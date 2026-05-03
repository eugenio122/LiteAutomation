using System.Collections.Generic;
using System.Text;
using LiteAutomation.Core;
using LiteAutomation.DTOs;
using LiteAutomation.Enums;
using LiteAutomation.Interfaces;

namespace LiteAutomation.Generators.CSharp
{
    public class SeleniumPomCSharpGenerator : ICodeGenerator
    {
        public string GenerateCode(WorkspaceState workspace, GeneratorConfig config, string testClassName = "GeneratedTest")
        {
            var sb = new StringBuilder();

            
            var router = new ArchitectureRouter();

            var intents = workspace.IntentCache;
            var pages = router.BuildPomStructure(intents);

            sb.AppendLine("// =====================================================================");
            sb.AppendLine("// 🏗️ ARQUITETURA: PAGE OBJECT MODEL (POM)");
            sb.AppendLine("// =====================================================================");
            sb.AppendLine("using System;\nusing OpenQA.Selenium;\nusing OpenQA.Selenium.Chrome;\nusing OpenQA.Selenium.Support.UI;\nusing NUnit.Framework;\n");
            sb.AppendLine("namespace LiteAutomation.GeneratedTests.POM\n{");

            // 1. GERAR AS CLASSES DE PÁGINA (PAGES)
            foreach (var page in pages)
            {
                sb.AppendLine($"    // ---------------------------------------------------------");
                sb.AppendLine($"    // PÁGINA: {page.ClassName} (Alvo: {page.UrlIdentifier})");
                sb.AppendLine($"    // ---------------------------------------------------------");
                sb.AppendLine($"    public class {page.ClassName}");
                sb.AppendLine($"    {{");
                sb.AppendLine($"        private IWebDriver driver;");
                sb.AppendLine($"        private WebDriverWait wait;");
                sb.AppendLine();

                // Dicionário de Seletores (Variáveis da Classe)
                foreach (var loc in page.MappedLocators)
                {
                    sb.AppendLine($"        private By {loc.Value} = {FormatLocator(loc.Key)};");
                }
                sb.AppendLine();

                // Construtor
                sb.AppendLine($"        public {page.ClassName}(IWebDriver driver, WebDriverWait wait)");
                sb.AppendLine($"        {{");
                sb.AppendLine($"            this.driver = driver;");
                sb.AppendLine($"            this.wait = wait;");
                sb.AppendLine($"        }}");
                sb.AppendLine();

                // Métodos de Ação
                foreach (var action in page.PageActions)
                {
                    string methodName = $"ExecutarAcao_{action.StepId.Replace(".", "_")}";
                    sb.AppendLine($"        /// <summary>");
                    sb.AppendLine($"        /// Passo original: {action.StepId} - {action.Type}");
                    if (!string.IsNullOrEmpty(action.Diagnostics)) sb.AppendLine($"        /// {action.Diagnostics}");
                    sb.AppendLine($"        /// </summary>");
                    sb.AppendLine($"        public void {methodName}()");
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
                    sb.AppendLine();
                }
                sb.AppendLine($"    }}");
                sb.AppendLine();
            }

            // 2. GERAR A CLASSE DE TESTE (EXECUTOR)
            sb.AppendLine($"    // ---------------------------------------------------------");
            sb.AppendLine($"    // CLASSE DE TESTE PRINCIPAL");
            sb.AppendLine($"    // ---------------------------------------------------------");
            sb.AppendLine($"    public class {testClassName}");
            sb.AppendLine($"    {{");
            sb.AppendLine($"        private IWebDriver driver;");
            sb.AppendLine($"        private WebDriverWait wait;");
            sb.AppendLine();
            sb.AppendLine($"        [SetUp] public void Setup() {{ var options = new ChromeOptions(); options.AddArgument(\"--disable-blink-features=AutomationControlled\"); driver = new ChromeDriver(options); driver.Manage().Window.Maximize(); wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10)); }}");
            sb.AppendLine($"        [TearDown] public void Teardown() {{ driver?.Dispose(); }}");
            sb.AppendLine();
            sb.AppendLine($"        [Test]\n        [Timeout({config.GlobalTimeoutMs})]\n        public void ExecuteScenario()");
            sb.AppendLine($"        {{");

            foreach (var page in pages)
            {
                sb.AppendLine($"            var {page.ClassName.ToLower()} = new {page.ClassName}(driver, wait);");
            }
            sb.AppendLine();

            foreach (var intent in intents)
            {
                if (intent.Type == IntentType.NavigateToUrl)
                    sb.AppendLine($"            driver.Navigate().GoToUrl(\"{intent.Value}\");");
                else if (intent.Type == IntentType.WaitUrlChange)
                    sb.AppendLine($"            wait.Until(d => d.Url.Contains(\"{intent.Value}\"));");
                else if (intent.Type != IntentType.Unknown && !intent.IsNewStepHeader)
                {
                    // Descobre a qual página esta ação pertence para chamá-la
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