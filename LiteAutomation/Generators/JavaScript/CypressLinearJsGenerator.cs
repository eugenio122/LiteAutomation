using System.Collections.Generic;
using System.Text;
using LiteAutomation.Core;
using LiteAutomation.DTOs;
using LiteAutomation.Enums;
using LiteAutomation.Interfaces;

namespace LiteAutomation.Generators.JavaScript
{
    public class CypressLinearJsGenerator : ICodeGenerator
    {
        public string GenerateCode(WorkspaceState workspace, GeneratorConfig config, string testClassName = "GeneratedTest")
        {
            var sb = new StringBuilder();
            sb.AppendLine($"/// <reference types=\"cypress\" />\n\ndescribe('Fluxo de Automação - {testClassName}', () => {{\n    it('Deve executar o cenário principal', () => {{");

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
                string actionCode = "";

                // 🛡️ Em JS (Cypress) não se usa try-catch. Nós injetamos a mensagem direto na Assertion!
                bool hasMsg = !string.IsNullOrEmpty(intent.FriendlyErrorMessage);
                string msgVal = hasMsg ? intent.FriendlyErrorMessage.Replace("'", "\\'") : "";
                string existCheck = hasMsg ? $".should('exist', '{msgVal}')" : "";

                switch (intent.Type)
                {
                    case IntentType.NavigateToUrl:
                        actionCode = $"cy.visit('{intent.Value}');";
                        break;
                    case IntentType.WaitUrlChange:
                        actionCode = hasMsg ? $"cy.url().should('include', '{intent.Value}', '{msgVal}');" : $"cy.url().should('include', '{intent.Value}');";
                        break;
                    case IntentType.Click:
                        actionCode = $"cy.get({loc}){existCheck}.scrollIntoView().click({{ force: true }});";
                        break;
                    case IntentType.InputText:
                        string safeVal = intent.Value.Replace("\"", "\\\"").Replace("\n", "\\n");
                        actionCode = $"cy.get({loc}){existCheck}.clear().type(\"{safeVal}\");";
                        break;
                    case IntentType.KeyPress:
                        actionCode = $"cy.get({loc}){existCheck}.type(\"{MapKey(intent.Key)}\");";
                        break;
                    case IntentType.ScrollTo:
                        actionCode = $"cy.get({loc}){existCheck}.scrollIntoView();";
                        break;
                    case IntentType.AssertVisible:
                        actionCode = hasMsg ? $"cy.get({loc}).should('be.visible', '{msgVal}');" : $"cy.get({loc}).should('be.visible');";
                        break;
                    case IntentType.AssertEnabled:
                        actionCode = hasMsg ? $"cy.get({loc}).should('be.enabled', '{msgVal}');" : $"cy.get({loc}).should('be.enabled');";
                        break;
                }

                if (!string.IsNullOrEmpty(actionCode))
                {
                    sb.AppendLine($"        {actionCode}");
                    sb.AppendLine();
                }
            }

            sb.AppendLine("    });\n});");
            return sb.ToString();
        }

        private string FormatLocator(string loc)
        {
            if (string.IsNullOrEmpty(loc)) return "\"/* VAZIO */\"";

            // Remove as marcações de C# que possam ter ficado no Override manual
            if (loc.StartsWith("By.XPath(\"")) loc = loc.Replace("By.XPath(\"", "").Replace("\")", "");
            else if (loc.StartsWith("By.CssSelector(\"")) loc = loc.Replace("By.CssSelector(\"", "").Replace("\")", "");

            if (loc.StartsWith("/") || loc.StartsWith("(")) return $"\"{loc}\""; // Nota: Cypress xpath plugin requer cy.xpath()
            return $"\"{loc}\"";
        }

        private string MapKey(string key) => key.ToLower() switch { "enter" => "{enter}", "tab" => "{tab}", "escape" => "{esc}", "space" => " ", _ => $"{{{key}}}" };
    }
}