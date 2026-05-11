using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using LiteAutomation.Core;
using LiteAutomation.DTOs;
using LiteAutomation.Enums;
using LiteAutomation.Interfaces;

namespace LiteAutomation.Generators
{
    public class ToscaTemplateGenerator : ICodeGenerator
    {
        private string? _templatePath;

        public ToscaTemplateGenerator(string? templatePath)
        {
            _templatePath = templatePath;
        }

        public string GenerateCode(WorkspaceState workspace, GeneratorConfig config, string testClassName = "GeneratedTest")
        {
            return "<!-- SUCESSO: Template .tsu simulado pronto para exportação! -->\r\n" +
                   "<!-- Clique em Exportar para injetar as interações no Tricentis Tosca. -->";
        }

        public void ExportInjectedTsu(WorkspaceState workspace, string outputPath)
        {
            if (workspace.RawSteps == null) return;
            var steps = workspace.RawSteps;

            var testCaseItems = new JsonArray();
            var entities = new JsonArray();

            foreach (var mainStep in steps)
            {
                var cleanTrail = DeltaAnalyzer.GetCleanInteractionTrail(mainStep.InteractionTrail);

                if (mainStep.IsEvidenceOnly || cleanTrail == null || cleanTrail.Count == 0) continue;

                foreach (var interaction in cleanTrail)
                {
                    string stepId = Guid.NewGuid().ToString("D").ToLower();
                    string actionName = interaction.InteractionType?.Replace("_Action", "") ?? "Acao";

                    // 🚀 ACESSO DIRETO O(1)
                    var uia = interaction.Uia?.ElementData;

                    string roleName = string.IsNullOrEmpty(uia?.Semantic?.AccessibleName?.Value)
                        ? (uia?.Semantic?.Role?.Value ?? interaction.TagName ?? "Elemento")
                        : uia.Semantic.AccessibleName.Value;

                    string stepName = $"[LiteJson] {actionName} - {roleName}";

                    testCaseItems.Add(stepId);

                    var newStep = new JsonObject
                    {
                        ["ObjectClass"] = "XTestStep",
                        ["Surrogate"] = stepId,
                        ["Attributes"] = new JsonObject { ["Name"] = stepName }
                    };
                    entities.Add(newStep);
                }
            }

            File.WriteAllText(outputPath, "Tsu Fictício com " + testCaseItems.Count + " passos gerados.");
        }
    }
}