using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LiteAutomation.Core;
using LiteAutomation.Core.NLP;
using LiteAutomation.DTOs;
using LiteAutomation.Enums;
using LiteAutomation.Interfaces;

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

            var validIntents = workspace.IntentCache.Where(i => !i.IsNewStepHeader && i.Type != IntentType.Unknown).ToList();

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

            for (int i = 0; i < validIntents.Count; i++)
            {
                var intent = validIntents[i];
                string rawText = "";
                string role = "";
                var ids = intent.StepId.Split('.');

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
                        // 🚀 INTELIGÊNCIA DE FALLBACK: Pega o título da página ao invés de "Nova Ação"
                        string pTitle = rawMain.ObservedContext?.PageTitle;
                        string sName = rawMain.StepName;

                        if (!string.IsNullOrWhiteSpace(pTitle))
                        {
                            rawText = pTitle;
                            if (rawText.Contains("-")) rawText = rawText.Split('-')[0];
                            if (rawText.Contains("|")) rawText = rawText.Split('|')[0];
                        }
                        else if (!string.IsNullOrWhiteSpace(sName) && !sName.ToLower().Contains("nova a"))
                        {
                            rawText = sName;
                        }
                        else
                        {
                            rawText = "carregamento atual";
                        }

                        if (rawText.Length > 25) rawText = rawText.Substring(0, 25).Trim();
                        role = "document";
                    }
                }

                string humanName = SemanticNlpEngine.GenerateHumanReadable(rawText, role, "pt-BR");
                string camelCaseName = SemanticNlpEngine.GenerateVariableName(rawText, role, "pt-BR");

                // 🚀 GRAMÁTICA DE PRONOMES: Se for a tela inteira (Body/Document), usa pronome feminino (A tela)
                if (intent.TargetLocator.Contains("body") || role == "document")
                {
                    humanName = "tela " + rawText.Trim().ToLower();
                    camelCaseName = "tela" + Capitalize(SemanticNlpEngine.GenerateVariableName(rawText, "", "pt-BR").Replace("el", ""));
                }

                if (string.IsNullOrWhiteSpace(camelCaseName) || camelCaseName.Length < 3) camelCaseName = "elTarget";

                string phaseContext = "";
                if (isCanonical)
                {
                    if (i == 0 && intent.Type == IntentType.NavigateToUrl) phaseContext = "Dado";
                    else if (lastActionIndex != -1 && i > lastActionIndex) phaseContext = "Então";
                    else if (lastActionIndex == -1 && (intent.Type == IntentType.AssertVisible || intent.Type == IntentType.AssertEnabled || intent.Type == IntentType.WaitUrlChange)) phaseContext = "Então";
                    else phaseContext = "Quando";
                }
                else
                {
                    if (intent.Type == IntentType.NavigateToUrl) phaseContext = "Dado";
                    else if (intent.Type == IntentType.WaitUrlChange || intent.Type == IntentType.AssertVisible || intent.Type == IntentType.AssertEnabled) phaseContext = "Então";
                    else phaseContext = "Quando";
                }

                string gherkinKeyword = (phaseContext == lastContext) ? "E" : phaseContext;
                lastContext = phaseContext;
                string stepDefAttribute = phaseContext == "Então" ? "Entao" : phaseContext;

                var step = new StepInfo
                {
                    Keyword = gherkinKeyword,
                    StepDefAttribute = stepDefAttribute,
                    ErrorMsg = intent.FriendlyErrorMessage,
                    StepId = intent.StepId,
                    Diagnostics = intent.Diagnostics,
                    ActionCode = GenerateActionCode(intent, FormatLocator(intent.TargetLocator), camelCaseName)
                };

                // LÓGICA DE GRAMÁTICA (O vs A)
                string art = humanName.StartsWith("tela ") || humanName.StartsWith("página ") || humanName.StartsWith("lista ") ? "a" : "o";

                switch (intent.Type)
                {
                    case IntentType.NavigateToUrl:
                        step.GherkinText = isCanonical ? "o acesso ao sistema principal" : "eu acesso o sistema na URL principal";
                        step.StepDefText = step.GherkinText;
                        step.MethodName = $"{stepDefAttribute}{Capitalize(camelCaseName)}SistemaAcessado";
                        break;
                    case IntentType.Click:
                        step.GherkinText = isCanonical ? $"aciono {art} {humanName}" : $"eu clico n{art} {humanName}";
                        step.StepDefText = step.GherkinText;
                        step.MethodName = $"{stepDefAttribute}Aciono{Capitalize(art)}{Capitalize(camelCaseName)}";
                        break;
                    case IntentType.Hover:
                        step.GherkinText = isCanonical ? $"foco n{art} {humanName}" : $"eu passo o mouse sobre {art} {humanName}";
                        step.StepDefText = step.GherkinText;
                        step.MethodName = $"{stepDefAttribute}FocoN{Capitalize(art)}{Capitalize(camelCaseName)}";
                        break;
                    case IntentType.Blur:
                        step.GherkinText = isCanonical ? $"fecho os menus suspensos" : $"eu clico fora para remover o foco e fechar menus";
                        step.StepDefText = step.GherkinText;
                        step.MethodName = $"{stepDefAttribute}FechoOsMenusSuspensos";
                        break;
                    case IntentType.InputText:
                        string varName = EnsureUniqueVar(examples, camelCaseName, intent.Value);
                        step.GherkinText = isCanonical ? $"informo {art} {humanName} com \"<{varName}>\"" : $"eu preencho {art} {humanName} com \"<{varName}>\"";
                        step.StepDefText = (isCanonical ? $"informo {art} {humanName} com " : $"eu preencho {art} {humanName} com ") + "\"\"(.*)\"\"";
                        step.MethodName = $"{stepDefAttribute}Informo{Capitalize(art)}{Capitalize(camelCaseName)}";
                        step.MethodParams = $"string {varName}";
                        step.ActionCode = step.ActionCode.Replace($"\"{intent.Value.Replace("\"", "\\\"")}\"", varName);
                        break;
                    case IntentType.WaitUrlChange:
                        if (isCanonical)
                        {
                            step.GherkinText = phaseContext == "Então" ? "a transição de página foi concluída" : "aguardo a transição de página";
                            step.MethodName = phaseContext == "Então" ? $"{stepDefAttribute}ATransicaoDePaginaFoiConcluida" : $"{stepDefAttribute}AguardoATransicaoDePagina";
                        }
                        else
                        {
                            step.GherkinText = "eu aguardo o carregamento da página";
                            step.MethodName = $"{stepDefAttribute}EuAguardoOCarregamentoDaPagina";
                        }
                        step.StepDefText = step.GherkinText;
                        break;
                    case IntentType.AssertVisible:
                        if (isCanonical)
                        {
                            step.GherkinText = phaseContext == "Então" ? $"{art} {humanName} é exibid{art} na tela" : $"verifico a exibição d{art} {humanName}";
                            step.MethodName = phaseContext == "Então" ? $"{stepDefAttribute}{Capitalize(art)}{Capitalize(camelCaseName)}EExibid{Capitalize(art)}NaTela" : $"{stepDefAttribute}VerificoAExibicaoD{Capitalize(art)}{Capitalize(camelCaseName)}";
                        }
                        else
                        {
                            step.GherkinText = $"{art} {humanName} deve estar visível";
                            step.MethodName = $"{stepDefAttribute}{Capitalize(art)}{Capitalize(camelCaseName)}DeveEstarVisivel";
                        }
                        step.StepDefText = step.GherkinText;
                        break;
                    case IntentType.AssertEnabled:
                        if (isCanonical)
                        {
                            step.GherkinText = phaseContext == "Então" ? $"{art} {humanName} fica habilitad{art}" : $"verifico a habilitação d{art} {humanName}";
                            step.MethodName = phaseContext == "Então" ? $"{stepDefAttribute}{Capitalize(art)}{Capitalize(camelCaseName)}FicaHabilitad{Capitalize(art)}" : $"{stepDefAttribute}VerificoAHabilitacaoD{Capitalize(art)}{Capitalize(camelCaseName)}";
                        }
                        else
                        {
                            step.GherkinText = $"{art} {humanName} deve estar habilitad{art}";
                            step.MethodName = $"{stepDefAttribute}{Capitalize(art)}{Capitalize(camelCaseName)}DeveEstarHabilitado";
                        }
                        step.StepDefText = step.GherkinText;
                        break;
                    default:
                        step.GherkinText = $"eu realizo a ação de {intent.Type} n{art} {humanName}";
                        step.StepDefText = step.GherkinText;
                        step.MethodName = $"{stepDefAttribute}EuRealizoAAcaoDe{intent.Type}";
                        break;
                }

                generatedSteps.Add(step);
            }

            // MONTAGEM DO ARQUIVO (Omitido as chaves de Namespace para brevidade, mas gerado completo)
            sb.AppendLine("// =====================================================================");
            sb.AppendLine($"// 🥒 ARQUITETURA: BDD ({(config.BddStyle == BddStyle.Canonical ? "CANÔNICO" : "NARRATIVO")})");
            sb.AppendLine("// =====================================================================");
            sb.AppendLine("/*\nFeature: Automação Gerada pelo LiteAutomation");

            string scenarioType = examples.Count > 0 ? "Esquema do Cenário" : "Cenário";
            sb.AppendLine($"  {scenarioType}: Cenário principal de execução");

            foreach (var step in generatedSteps)
                sb.AppendLine($"    {step.Keyword} {step.GherkinText}");

            if (examples.Count > 0)
            {
                sb.AppendLine("\n    Exemplos:");
                sb.AppendLine("      | " + string.Join(" | ", examples.Keys) + " |");
                sb.AppendLine("      | " + string.Join(" | ", examples.Values) + " |");
            }
            sb.AppendLine("*/\n");

            sb.AppendLine("using System;\nusing TechTalk.SpecFlow;\nusing OpenQA.Selenium;\nusing OpenQA.Selenium.Chrome;\nusing OpenQA.Selenium.Support.UI;\nusing NUnit.Framework;\n");
            sb.AppendLine("namespace LiteAutomation.GeneratedTests.BDD\n{");
            sb.AppendLine($"    [Binding]");
            sb.AppendLine($"    public class {testClassName}Steps");
            sb.AppendLine($"    {{");
            sb.AppendLine($"        private IWebDriver driver;");
            sb.AppendLine($"        private WebDriverWait wait;");
            sb.AppendLine();
            sb.AppendLine($"        [BeforeScenario]\n        public void Setup()\n        {{\n            var options = new ChromeOptions();\n            options.AddArgument(\"--disable-blink-features=AutomationControlled\");\n            driver = new ChromeDriver(options);\n            driver.Manage().Window.Maximize();\n            wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));\n        }}\n");
            sb.AppendLine($"        [AfterScenario]\n        public void Teardown()\n        {{\n            driver?.Dispose();\n        }}\n");

            var uniqueStepDefs = new HashSet<string>();

            foreach (var step in generatedSteps)
            {
                string safeMethodName = RemoveAccents(step.MethodName).Replace("\"", "");

                if (uniqueStepDefs.Contains(safeMethodName)) continue;
                uniqueStepDefs.Add(safeMethodName);

                sb.AppendLine($"        [{step.StepDefAttribute}(@\"{step.StepDefText}\")]");
                sb.AppendLine($"        public void {safeMethodName}({step.MethodParams})");
                sb.AppendLine($"        {{");

                sb.AppendLine($"            // Referência: Passo {step.StepId}");
                if (config.IncludeReport && !string.IsNullOrWhiteSpace(step.Diagnostics))
                    sb.AppendLine($"            // Report Visual: {step.Diagnostics}");

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

        private string GenerateActionCode(AutomationIntent intent, string loc, string varName)
        {
            switch (intent.Type)
            {
                case IntentType.NavigateToUrl: return $"driver.Navigate().GoToUrl(\"{intent.Value}\");";
                case IntentType.WaitUrlChange: return $"wait.Until(d => d.Url.Contains(\"{intent.Value}\"));";
                case IntentType.Click: return $"var {varName} = wait.Until(d => d.FindElement({loc}));\n((IJavaScriptExecutor)driver).ExecuteScript(\"arguments[0].scrollIntoView({{block: 'center'}});\", {varName});\ntry\n{{\n    {varName}.Click();\n}}\ncatch\n{{\n    ((IJavaScriptExecutor)driver).ExecuteScript(\"arguments[0].click();\", {varName});\n}}";
                case IntentType.Hover: return $"var {varName} = wait.Until(d => d.FindElement({loc}));\nnew OpenQA.Selenium.Interactions.Actions(driver).MoveToElement({varName}).Perform();";
                case IntentType.Blur: return $"driver.FindElement(By.TagName(\"body\")).Click();";
                case IntentType.InputText: return $"var {varName} = wait.Until(d => d.FindElement({loc}));\n{varName}.Clear();\n{varName}.SendKeys(\"{intent.Value.Replace("\"", "\\\"")}\");";
                case IntentType.KeyPress: return $"wait.Until(d => d.FindElement({loc})).SendKeys({MapKey(intent.Key)});";
                case IntentType.ScrollTo: return $"var {varName} = wait.Until(d => d.FindElement({loc}));\n((IJavaScriptExecutor)driver).ExecuteScript(\"arguments[0].scrollIntoView({{block: 'center'}});\", {varName});";
                case IntentType.AssertVisible: return $"Assert.IsTrue(wait.Until(d => d.FindElement({loc})).Displayed);";
                case IntentType.AssertEnabled: return $"wait.Until(d => d.FindElement({loc}).Enabled);";
                default: return "";
            }
        }

        private string FormatLocator(string loc) { if (string.IsNullOrEmpty(loc)) return "By.XPath(\"/* VAZIO */\")"; if (loc.StartsWith("By.") || loc.StartsWith("Page.")) return loc; if (loc.StartsWith("/") || loc.StartsWith("(")) return $"By.XPath(\"{loc}\")"; return $"By.CssSelector(\"{loc}\")"; }
        private string MapKey(string key) => key.ToLower() switch { "enter" => "Keys.Enter", "tab" => "Keys.Tab", "escape" => "Keys.Escape", "space" => "Keys.Space", _ => $"\"{key}\"" };
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