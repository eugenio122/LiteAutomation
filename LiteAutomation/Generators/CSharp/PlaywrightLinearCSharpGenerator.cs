using System.Collections.Generic;
using System.Text;
using LiteAutomation.Core;
using LiteAutomation.DTOs;
using LiteAutomation.Enums;
using LiteAutomation.Interfaces;

namespace LiteAutomation.Generators.CSharp
{
    public class PlaywrightLinearCSharpGenerator : ICodeGenerator
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
                string actionCode = "";

                switch (intent.Type)
                {
                    case IntentType.NavigateToUrl: actionCode = $"await Page.GotoAsync(\"{intent.Value}\");"; break;
                    case IntentType.WaitUrlChange: actionCode = $"await Page.WaitForURLAsync(\"**{intent.Value}\");"; break;
                    case IntentType.Click: actionCode = $"await {loc}.ClickAsync();"; break;
                    case IntentType.InputText:
                        string safeVal = intent.Value.Replace("\"", "\\\"").Replace("\n", "\\n");
                        actionCode = $"await {loc}.FillAsync(\"{safeVal}\");";
                        break;
                    case IntentType.KeyPress:
                        string pwKey = char.ToUpper(intent.Key[0]) + intent.Key.Substring(1).ToLower();
                        actionCode = $"await {loc}.PressAsync(\"{pwKey}\");";
                        break;
                    case IntentType.ScrollTo: actionCode = $"await {loc}.ScrollIntoViewIfNeededAsync();"; break;
                    case IntentType.AssertVisible: actionCode = $"await Expect({loc}).ToBeVisibleAsync();"; break;
                    case IntentType.AssertEnabled: actionCode = $"await Expect({loc}).ToBeEnabledAsync();"; break;
                }

                if (!string.IsNullOrEmpty(actionCode))
                {
                    // 🛡️ TRATAMENTO DE ERROS HUMANIZADO (Playwright usa System.TimeoutException)
                    if (!string.IsNullOrEmpty(intent.FriendlyErrorMessage))
                    {
                        sb.AppendLine($"            try");
                        sb.AppendLine($"            {{");
                        foreach (var line in actionCode.Split('\n')) { sb.AppendLine($"                {line}"); }
                        sb.AppendLine($"            }}");
                        sb.AppendLine($"            catch (System.TimeoutException)");
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
            if (string.IsNullOrEmpty(loc)) return "Page.Locator(\"/* VAZIO */\")";
            if (loc.StartsWith("Page.") || loc.StartsWith("By.")) return loc;
            if (loc.StartsWith("/") || loc.StartsWith("(")) return $"Page.Locator(\"xpath={loc}\")";
            return $"Page.Locator(\"css={loc}\")";
        }

        private void WriteHeader(StringBuilder sb, string className, int timeout)
        {
            sb.AppendLine("using Microsoft.Playwright;\nusing Microsoft.Playwright.NUnit;\nusing NUnit.Framework;\nusing System.Threading.Tasks;");
            sb.AppendLine($"namespace LiteAutomation.GeneratedTests {{\n    [Parallelizable(ParallelScope.Self)]\n    [TestFixture]\n    public class {className} : PageTest {{");
            sb.AppendLine($"        [Test]\n        [Timeout({timeout})]\n        public async Task ExecuteTestAsync() {{");
        }
        private void WriteFooter(StringBuilder sb) { sb.AppendLine("        }\n    }\n}"); }
    }
}