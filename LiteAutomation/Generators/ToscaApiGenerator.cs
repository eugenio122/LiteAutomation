using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using LiteAutomation.Core;
using LiteAutomation.DTOs;
using LiteAutomation.Enums;
using LiteAutomation.Interfaces;

namespace LiteAutomation.Generators
{
    public class ToscaApiGenerator : ICodeGenerator
    {
        private static List<Assembly> _loadedAssemblies = new List<Assembly>();
        private static string? _toscaDirectory;

        public string GenerateCode(WorkspaceState workspace, GeneratorConfig config, string testClassName = "GeneratedTest")
        {
            var summary = new System.Text.StringBuilder();
            summary.AppendLine("<!-- DIAGNÓSTICO DO MOTOR TOSCA API -->");
            summary.AppendLine($"<!-- Cenário: {testClassName} preparado para injeção via DLLs TCAPI. -->");
            return summary.ToString();
        }

        public static bool InitializeToscaEngine(string? customPath = null)
        {
            if (_loadedAssemblies.Count > 0) return true;

            try
            {
                string[] criticalDlls = { "TCAPIObjects.dll", "TCAPI.dll", "Tricentis.TCAPIHelper.dll", "TCAPINativeConnector.dll" };

                var possibleDirs = new List<string>
                {
                    @"C:\Program Files (x86)\TRICENTIS\Tosca Testsuite\ToscaCommander"
                };

                string? tricentisHome = Environment.GetEnvironmentVariable("TRICENTIS_HOME");
                if (!string.IsNullOrEmpty(tricentisHome))
                {
                    possibleDirs.Add(Path.Combine(tricentisHome, "ToscaCommander"));
                    possibleDirs.Add(tricentisHome);

                    if (tricentisHome.EndsWith("Settings", StringComparison.OrdinalIgnoreCase))
                    {
                        var parent = Directory.GetParent(tricentisHome)?.FullName;
                        if (parent != null) possibleDirs.Add(Path.Combine(parent, "ToscaCommander"));
                    }
                }

                string? baseDir = possibleDirs.FirstOrDefault(d => Directory.Exists(d));

                if (string.IsNullOrEmpty(baseDir)) return false;

                _toscaDirectory = baseDir;
                Directory.SetCurrentDirectory(baseDir);

                foreach (var dllName in criticalDlls)
                {
                    string fullPath = Path.Combine(baseDir, dllName);
                    if (File.Exists(fullPath))
                    {
                        try
                        {
                            var asm = Assembly.LoadFrom(fullPath);
                            _loadedAssemblies.Add(asm);
                        }
                        catch { }
                    }
                }

                return _loadedAssemblies.Count > 0;
            }
            catch { return false; }
        }

        public static void ExportToTsu(WorkspaceState workspace, string filePath)
        {
            if (!InitializeToscaEngine())
                throw new FileNotFoundException("Não foi possível inicializar o motor do Tosca.");

            if (workspace.RawSteps != null)
            {
                PerformApiExport(workspace.RawSteps, filePath);
            }
        }

        private static void PerformApiExport(List<MainStepDto> steps, string filePath)
        {
            try
            {
                Type? tcApiType = null;

                foreach (var asm in _loadedAssemblies)
                {
                    tcApiType = asm.GetType("Tricentis.TCAPIObjects.TCAPI")
                                ?? asm.GetType("TCAPIObjects.TCAPI")
                                ?? asm.GetTypes().FirstOrDefault(t => t.Name == "TCAPI");

                    if (tcApiType != null) break;
                }

                if (tcApiType == null)
                    throw new Exception("A classe 'TCAPI' não foi encontrada nas DLLs carregadas.");

                MethodInfo? createInstanceMethod = tcApiType.GetMethod("CreateInstance", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);

                if (createInstanceMethod == null)
                    throw new Exception($"Método 'CreateInstance()' sem parâmetros não encontrado.");

                dynamic tcApi = createInstanceMethod.Invoke(null, null);

                string tempPath = Path.Combine(Path.GetTempPath(), $"Lite_{Guid.NewGuid().ToString().Substring(0, 8)}.tws");
                dynamic workspace = tcApi.CreateWorkspace(tempPath, "Admin", "");

                try
                {
                    dynamic project = workspace.GetProject();
                    dynamic testCaseFolder = project.GetOrCreateTestCasesFolder();
                    dynamic testCase = testCaseFolder.CreateTestCase(Path.GetFileNameWithoutExtension(filePath));

                    foreach (var mainStep in steps)
                    {
                        // 🚀 NOVO: Lê diretamente da trilha de interação!
                        if (mainStep.InteractionTrail == null) continue;

                        foreach (var interaction in mainStep.InteractionTrail)
                        {
                            testCase.CreateTestStep($"{interaction.InteractionType}");
                        }
                    }

                    var objectsToExport = new List<dynamic> { testCase };
                    workspace.ExportSubset(filePath, objectsToExport.ToArray());
                }
                finally
                {
                    workspace.Close();
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
            }
            catch (TargetInvocationException tex)
            {
                throw new Exception($"Erro de Automação Tosca: {tex.InnerException?.Message ?? tex.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Falha na Exportação API: {ex.Message}");
            }
        }
    }
}