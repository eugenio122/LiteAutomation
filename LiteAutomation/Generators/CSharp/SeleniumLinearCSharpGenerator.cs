using System;
using System.Collections.Generic;
using System.Linq;
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

            // 🚀 1. INJEÇÃO DA NAVEGAÇÃO INICIAL (URL)
            var firstStepWithUrl = workspace.RawSteps?.FirstOrDefault(s => s.ObservedContext != null && !string.IsNullOrEmpty(s.ObservedContext.Url));
            if (firstStepWithUrl != null)
            {
                sb.AppendLine($"            // ---------------------------------------------------------");
                sb.AppendLine($"            // ACESSAR SISTEMA INICIAL");
                sb.AppendLine($"            // ---------------------------------------------------------");
                sb.AppendLine($"            driver.Navigate().GoToUrl(\"{firstStepWithUrl.ObservedContext.Url}\");");
                sb.AppendLine();
            }

            var analyzer = new DeltaAnalyzer();
            var validIntents = analyzer.Analyze(workspace.RawSteps, config)
                                       .Where(i => i.Type != IntentType.Unknown)
                                       .ToList();

            // 🚀 2. GERAÇÃO DE CÓDIGO COM TRILHA LIMPA
            foreach (var intent in validIntents)
            {
                if (intent.IsNewStepHeader)
                {
                    sb.AppendLine($"            // ---------------------------------------------------------");
                    sb.AppendLine($"            // {(intent.StepDescription ?? "").ToUpper()}");
                    sb.AppendLine($"            // ---------------------------------------------------------");
                    continue;
                }

                if (!string.IsNullOrEmpty(intent.Diagnostics))
                    sb.AppendLine($"            // {intent.Diagnostics}");

                // 🚀 SUPER FALLBACK: Se o locator da intenção for cego, busca o BiDi direto do Json!
                var ids = intent.StepId?.Split('.');
                if (ids != null && ids.Length == 2 && int.TryParse(ids[0], out int mIdx) && int.TryParse(ids[1], out int micIdx))
                {
                    var rawMain = workspace.RawSteps?.FirstOrDefault(s => s.StepIndex == mIdx);
                    var cleanTrail = DeltaAnalyzer.GetCleanInteractionTrail(rawMain?.InteractionTrail);
                    var interaction = micIdx > 0 ? cleanTrail.ElementAtOrDefault(micIdx - 1) : null;

                    if (interaction != null)
                    {
                        string locLowerCheck = intent.TargetLocator?.ToLower() ?? "";
                        if (string.IsNullOrEmpty(locLowerCheck) || locLowerCheck.Contains("body") || locLowerCheck.Contains("vazio") || locLowerCheck.Contains("ambígu"))
                        {
                            var bidi = interaction.WebDriverBiDi?.ElementData;
                            if (!string.IsNullOrWhiteSpace(interaction.ElementId)) intent.TargetLocator = $"By.Id(\"{interaction.ElementId}\")";
                            else if (!string.IsNullOrWhiteSpace(bidi?.SelectorSet?.XpathAbsolute?.Value)) intent.TargetLocator = $"By.XPath(\"{bidi.SelectorSet.XpathAbsolute.Value}\")";
                            else if (!string.IsNullOrWhiteSpace(interaction.VisibleText)) { string safeText = interaction.VisibleText.Replace("\"", "\\\"").Replace("'", "\\'"); intent.TargetLocator = $"By.XPath(\"//*[normalize-space(text())='{safeText}']\")"; }
                            else if (!string.IsNullOrWhiteSpace(interaction.Classes)) { string cleanClasses = string.Join(".", interaction.Classes.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)); intent.TargetLocator = $"By.CssSelector(\"{interaction.TagName}.{cleanClasses}\")"; }
                            else if (!string.IsNullOrWhiteSpace(interaction.TagName)) intent.TargetLocator = $"By.TagName(\"{interaction.TagName}\")";
                        }
                    }
                }

                string loc = FormatLocator(intent.TargetLocator);
                string id = intent.StepId?.Replace(".", "_") ?? Guid.NewGuid().ToString("N").Substring(0, 6);
                string actionCode = "";

                switch (intent.Type)
                {
                    case IntentType.NavigateToUrl:
                        // Ignora se já inserimos lá em cima para evitar reload desnecessário
                        break;
                    case IntentType.WaitUrlChange:
                        actionCode = $"wait.Until(d => d.Url.Contains(\"{intent.Value}\"));";
                        break;
                    case IntentType.Click:
                        actionCode = $"var el_{id} = wait.Until(d => d.FindElement({loc}));\n((IJavaScriptExecutor)driver).ExecuteScript(\"arguments[0].scrollIntoView({{block: 'center'}});\", el_{id});\ntry\n{{\n    el_{id}.Click();\n}}\ncatch\n{{\n    ((IJavaScriptExecutor)driver).ExecuteScript(\"arguments[0].click();\", el_{id});\n}}";
                        break;
                    case IntentType.InputText:
                        string safeVal = intent.Value?.Replace("\"", "\\\"").Replace("\n", "\\n") ?? "";
                        actionCode = $"var el_{id} = wait.Until(d => d.FindElement({loc}));\nel_{id}.Clear();\nel_{id}.SendKeys(\"{safeVal}\");";
                        break;
                    case IntentType.KeyPress:
                        actionCode = $"var el_{id} = wait.Until(d => d.FindElement({loc}));\nel_{id}.SendKeys({MapKey(intent.Key)});\nSystem.Threading.Thread.Sleep(500);";
                        break;
                    case IntentType.ScrollTo:
                        actionCode = $"var el_{id} = wait.Until(d => d.FindElement({loc}));\n((IJavaScriptExecutor)driver).ExecuteScript(\"arguments[0].scrollIntoView({{block: 'center'}});\", el_{id});";
                        break;
                    case IntentType.AssertVisible:
                        actionCode = $"Assert.IsTrue(wait.Until(d => d.FindElement({loc})).Displayed);";
                        break;
                    case IntentType.AssertEnabled:
                        actionCode = $"Assert.IsTrue(wait.Until(d => d.FindElement({loc}).Enabled);";
                        break;
                }

                if (!string.IsNullOrEmpty(actionCode))
                {
                    // 🛡️ TRATAMENTO DE ERROS HUMANIZADO
                    if (!string.IsNullOrEmpty(intent.FriendlyErrorMessage))
                    {
                        sb.AppendLine($"            try\n            {{");
                        foreach (var line in actionCode.Split('\n')) { sb.AppendLine($"                {line.TrimEnd()}"); }
                        sb.AppendLine($"            }}\n            catch (WebDriverTimeoutException)\n            {{");
                        sb.AppendLine($"                Assert.Fail(\"{intent.FriendlyErrorMessage}\");\n            }}");
                    }
                    else
                    {
                        foreach (var line in actionCode.Split('\n')) { sb.AppendLine($"            {line.TrimEnd()}"); }
                    }
                    sb.AppendLine();
                }
            }

            WriteFooter(sb);
            return sb.ToString();
        }

        private string FormatLocator(string loc)
        {
            if (string.IsNullOrEmpty(loc) || loc.Contains("VAZIO") || loc.Contains("NÃO ENCONTRADO") || loc.Contains("AMBÍGUO")) return "By.TagName(\"body\")";
            if (loc.StartsWith("By.") || loc.StartsWith("Page.")) return loc;
            if (loc.StartsWith("xpath=") || loc.StartsWith("/") || loc.StartsWith("(")) return $"By.XPath(\"{loc.Replace("xpath=", "")}\")";
            if (loc.StartsWith("css=")) return $"By.CssSelector(\"{loc.Replace("css=", "")}\")";
            return $"By.CssSelector(\"{loc}\")";
        }

        private string MapKey(string key) => key?.ToLower().Replace("keypress_", "") switch { "enter" => "Keys.Enter", "tab" => "Keys.Tab", "escape" => "Keys.Escape", "space" => "Keys.Space", _ => $"\"{key}\"" };

        private void WriteHeader(StringBuilder sb, string className, int timeout)
        {
            sb.AppendLine("using System;\nusing OpenQA.Selenium;\nusing OpenQA.Selenium.Chrome;\nusing OpenQA.Selenium.Support.UI;\nusing NUnit.Framework;");
            sb.AppendLine($"namespace LiteAutomation.GeneratedTests\n{{\n    public class {className}\n    {{\n        private IWebDriver driver;\n        private WebDriverWait wait;\n");

            // 🚀 SEU SETUP ANTI-BOT EXATO
            sb.AppendLine("        [SetUp]\n        public void Setup()\n        {\n            var options = new ChromeOptions();");
            sb.AppendLine("            options.AddExcludedArgument(\"enable-automation\");");
            sb.AppendLine("            options.AddAdditionalOption(\"useAutomationExtension\", false);");
            sb.AppendLine("            options.AddUserProfilePreference(\"credentials_enable_service\", false);");
            sb.AppendLine("            options.AddUserProfilePreference(\"profile.password_manager_enabled\", false);");
            sb.AppendLine("            options.AddArgument(\"--disable-blink-features=AutomationControlled\");");
            sb.AppendLine("            options.AddArgument(\"user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36\");");
            sb.AppendLine("\n            driver = new ChromeDriver(options);");
            sb.AppendLine("            driver.Manage().Window.Maximize();");
            sb.AppendLine("            wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));\n        }\n");

            // 🚀 SEU TEARDOWN EXATO
            sb.AppendLine("        [TearDown]\n        public void Teardown()\n        {\n            driver?.Dispose();\n            driver = null;\n        }\n");
            sb.AppendLine($"        [Test]\n        [Timeout({timeout})]\n        public void ExecuteScenario()\n        {{");
        }

        private void WriteFooter(StringBuilder sb)
        {
            sb.AppendLine("        }\n    }\n}");
        }
    }
}