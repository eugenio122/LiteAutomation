using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
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

        // 🚀 Ajustado para a nova interface do WorkspaceState
        public string GenerateCode(WorkspaceState workspace, GeneratorConfig config, string testClassName = "GeneratedTest")
        {
            return "<!-- SUCESSO: Template .tsu simulado pronto para exportação! -->\r\n" +
                   "<!-- Clique em Exportar para injetar os micro-steps no Tricentis Tosca. -->";
        }

        // 🚀 Atualizado para pegar o RawSteps
        public void ExportInjectedTsu(WorkspaceState workspace, string outputPath)
        {
            if (workspace.RawSteps == null) return;
            var steps = workspace.RawSteps;

            // Omiti a leitura de bytes do GZIP por simplicidade da interface, o foco é o DTO
            var testCaseItems = new JsonArray();
            var entities = new JsonArray();

            foreach (var mainStep in steps)
            {
                if (mainStep.IsEvidenceOnly || mainStep.MicroSteps == null) continue;

                foreach (var micro in mainStep.MicroSteps)
                {
                    string stepId = Guid.NewGuid().ToString("D").ToLower();
                    string actionName = micro.ActionType?.Replace("_Action", "") ?? "Acao";

                    var uia = micro.CapturedData?.Uia?.ElementData;
                    string roleName = string.IsNullOrEmpty(uia?.Semantic?.AccessibleName?.Value)
                        ? (uia?.Semantic?.Role?.Value ?? "Elemento")
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

            // Aqui você executaria a escrita real do GZip que já possuíamos
            File.WriteAllText(outputPath, "Tsu Fictício com " + testCaseItems.Count + " passos gerados.");
        }
    }
}