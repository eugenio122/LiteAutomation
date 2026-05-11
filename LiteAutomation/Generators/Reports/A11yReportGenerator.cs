using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LiteAutomation.Core;
using LiteAutomation.DTOs;
using LiteAutomation.Enums;
using LiteAutomation.Interfaces;

namespace LiteAutomation.Generators.Reports
{
    public class A11yReportGenerator : ICodeGenerator
    {
        private class ElementAuditResult
        {
            public bool IsCritical { get; set; }
            public bool IsWarning { get; set; }
            public bool IsHealthy { get; set; }
            public string StatusLabel { get; set; } = "";
            public string BestLocator { get; set; } = "";
            public List<string> Flags { get; set; } = new List<string>();
            public string DiagnosticsDetails { get; set; } = "";
            public int MaxScore { get; set; }
        }

        public string GenerateCode(WorkspaceState workspace, GeneratorConfig config, string testClassName = "Relatorio_Auditoria")
        {
            var sb = new StringBuilder();

            int globalInteracted = 0;
            int globalVisibleScanned = 0;
            int criticalInteractions = 0;
            int warningInteractions = 0;
            int healthyInteractions = 0;

            var reportBody = new StringBuilder();
            var steps = workspace.RawSteps;

            foreach (var mainStep in steps)
            {
                if (mainStep.IsEvidenceOnly) continue;

                int stepIdx = mainStep.StepIndex ?? 0;
                string stepName = string.IsNullOrWhiteSpace(mainStep.StepName) ? $"Ação da Tela" : mainStep.StepName;

                reportBody.AppendLine($"## 📍 Passo {stepIdx}: {stepName}");
                reportBody.AppendLine();

                if (mainStep.ObservedContext?.VisibleElements != null && mainStep.ObservedContext.VisibleElements.Count > 0)
                {
                    var visibleResults = new List<ElementAuditResult>();

                    void ScanTree(List<VisibleElementDto> nodes)
                    {
                        foreach (var node in nodes)
                        {
                            if (node.CapturedData != null)
                            {
                                visibleResults.Add(EvaluateElement(node.CapturedData));
                                globalVisibleScanned++;
                            }
                            if (node.Children != null && node.Children.Count > 0)
                                ScanTree(node.Children);
                        }
                    }

                    ScanTree(mainStep.ObservedContext.VisibleElements);

                    int totalVisible = visibleResults.Count;
                    int screenCritical = visibleResults.Count(r => r.IsCritical);
                    int screenWarnings = visibleResults.Count(r => r.IsWarning);
                    int screenHealthy = visibleResults.Count(r => r.IsHealthy);

                    reportBody.AppendLine($"### 🌐 Contexto da Tela (Background Scan)");
                    reportBody.AppendLine($"- **Elementos varridos na árvore DOM:** `{totalVisible}`");
                    reportBody.AppendLine($"- 🟢 Saudáveis: `{screenHealthy}` | 🟡 Alertas: `{screenWarnings}` | 🔴 Críticos (Sem A11Y): `{screenCritical}`");

                    if (screenCritical > 0)
                    {
                        reportBody.AppendLine();
                        reportBody.AppendLine($"**Top Ofensores de Acessibilidade nesta tela:**");
                        var worstOffenders = visibleResults.Where(r => r.IsCritical).Take(5);
                        foreach (var offender in worstOffenders)
                        {
                            reportBody.AppendLine($"- 🔴 Alvo cego/ambíguo: `{offender.BestLocator}` (Score: {offender.MaxScore})");
                        }
                        if (screenCritical > 5) reportBody.AppendLine($"- *... e mais {screenCritical - 5} elementos críticos ocultados.*");
                    }
                    reportBody.AppendLine();
                }

                var cleanTrail = DeltaAnalyzer.GetCleanInteractionTrail(mainStep.InteractionTrail);

                if (cleanTrail != null && cleanTrail.Count > 0)
                {
                    reportBody.AppendLine($"### 🖱️ Interações Diretas no Fluxo");
                    int interactionIndex = 1;

                    foreach (var interaction in cleanTrail)
                    {
                        globalInteracted++;
                        string displayStep = $"{stepIdx}.{interactionIndex}";

                        // 🚀 COMPATIBILIDADE ESTRUTURAL MANTIDA!
                        CapturedDataDto interactionData = null;
                        if (interaction.WebDriverBiDi != null || interaction.Uia != null || interaction.AxTree != null)
                        {
                            interactionData = new CapturedDataDto
                            {
                                WebDriverBiDi = interaction.WebDriverBiDi,
                                Uia = interaction.Uia,
                                AxTree = interaction.AxTree
                            };
                        }
                        var audit = EvaluateElement(interactionData);

                        if (audit.IsCritical) criticalInteractions++;
                        else if (audit.IsWarning) warningInteractions++;
                        else healthyInteractions++;

                        reportBody.AppendLine($"#### Ação [{displayStep}]: `{interaction.InteractionType}`");
                        reportBody.AppendLine($"**Status:** {audit.StatusLabel}");
                        reportBody.AppendLine($"**Melhor Seletor:** `{audit.BestLocator}`");

                        if (audit.Flags.Any())
                            reportBody.AppendLine($"**Quality Flags:** `{string.Join("`, `", audit.Flags)}`");
                        else
                            reportBody.AppendLine($"**Quality Flags:** *Nenhuma flag detetada.*");

                        reportBody.AppendLine();
                        reportBody.AppendLine($"**Diagnóstico do Engenheiro:**");
                        reportBody.AppendLine(audit.DiagnosticsDetails);
                        reportBody.AppendLine("---");

                        interactionIndex++;
                    }
                }
                else
                {
                    reportBody.AppendLine("*Nenhuma interação humana registada neste passo.*");
                    reportBody.AppendLine("---");
                }
            }

            int healthScore = globalInteracted == 0 ? 100 : (int)(((double)healthyInteractions / globalInteracted) * 100);

            sb.AppendLine($"# 📊 Relatório de Auditoria de Acessibilidade e Resiliência DOM");
            sb.AppendLine($"> **Gerado pelo Motor:** LiteAutomation (State-Driven Engine)");
            sb.AppendLine($"> **Data da Análise:** {DateTime.Now.ToString("dd/MM/yyyy HH:mm")}");
            sb.AppendLine();
            sb.AppendLine($"## 🩸 Hemograma do Front-End (Resumo Executivo)");
            sb.AppendLine($"O LiteAutomation auditou ativamente a jornada do utilizador e mapeou em background a árvore de elementos em volta.");
            sb.AppendLine();
            sb.AppendLine($"- **Total de Nós DOM Escaneados (Background):** `{globalVisibleScanned}`");
            sb.AppendLine($"- **Total de Interações Físicas da Automação:** `{globalInteracted}`");
            sb.AppendLine($"- **Índice de Saúde do Fluxo de Automação:** `{healthScore}%`");
            sb.AppendLine();
            sb.AppendLine($"### Distribuição das Interações do Robô:");
            sb.AppendLine($"- 🟢 **Saudáveis (Padrão Ouro / Acessíveis):** {healthyInteractions}");
            sb.AppendLine($"- 🟡 **Alertas (XPath Frágil / CSS Genérico):** {warningInteractions}");
            sb.AppendLine($"- 🔴 **Críticos (Ambiguidade / Falha A11Y Severa):** {criticalInteractions}");
            sb.AppendLine();
            sb.AppendLine("*(Nota para a Engenharia: Interações classificadas como Críticas reprovam o pipeline por alto risco de quebra).*");
            sb.AppendLine();
            sb.AppendLine($"---");
            sb.AppendLine();
            sb.AppendLine($"## 🔍 Inspeção Detalhada por Passo");
            sb.AppendLine();
            sb.Append(reportBody.ToString());

            return sb.ToString();
        }

        private ElementAuditResult EvaluateElement(CapturedDataDto? capturedData)
        {
            var result = new ElementAuditResult();
            if (capturedData == null)
            {
                result.IsCritical = true;
                result.StatusLabel = "🔴 Crítico (Dados Não Capturados / Somente Evento)";
                result.DiagnosticsDetails = "- 🚨 Elemento Ghost: O robô interagiu em algo que não gerou dados estruturais (ex: canvas, svg não mapeado).";
                return result;
            }

            var uia = capturedData.Uia;
            var bidi = capturedData.WebDriverBiDi;
            var bidiData = bidi?.ElementData;

            if (bidi?.QualityFlags != null) result.Flags.AddRange(bidi.QualityFlags);
            if (uia?.QualityFlags != null) result.Flags.AddRange(uia.QualityFlags);

            int maxValidScore = 0;
            int lowestNegativeScore = 0;
            bool hasAmbiguidade = result.Flags.Contains("WARNING_AMBIGUOUS_LOCATOR");

            if (bidiData?.SelectorSet != null)
            {
                var locs = new List<LocatorData> {
                    bidiData.SelectorSet.CustomAttribute, bidiData.SelectorSet.Id, bidiData.SelectorSet.AriaLabel,
                    bidiData.SelectorSet.Name, bidiData.SelectorSet.Placeholder, bidiData.SelectorSet.Alt,
                    bidiData.SelectorSet.Text, bidiData.SelectorSet.Title, bidiData.SelectorSet.Css,
                    bidiData.SelectorSet.XpathRelative, bidiData.SelectorSet.XpathAbsolute
                };

                var negativeLocs = locs.Where(l => l != null && l.Confidence < 0).ToList();
                if (negativeLocs.Any())
                {
                    hasAmbiguidade = true;
                    lowestNegativeScore = negativeLocs.Min(l => l.Confidence);
                }

                var validLocs = locs.Where(l => l != null && l.Confidence >= 0).OrderByDescending(l => l.Confidence).ToList();
                if (validLocs.Any())
                {
                    maxValidScore = validLocs.First().Confidence;
                    result.BestLocator = $"{validLocs.First().Value} (Score: {maxValidScore})";
                    result.MaxScore = maxValidScore;
                }
                else
                {
                    result.BestLocator = "*Sem locators válidos*";
                }
            }

            bool hasA11yGap = result.Flags.Contains("A11Y_GAP_WARNING") || (!result.Flags.Contains("A11Y_NAME_PRESENT") && !result.Flags.Contains("LOCATOR_P1_SEMANTIC") && maxValidScore < 85);
            bool isBrittle = result.Flags.Contains("WARNING_BRITTLE_LOCATOR") || maxValidScore <= 60;
            bool isGolden = result.Flags.Contains("LOCATOR_PO_GOLDEN") || result.Flags.Contains("A11Y_AUTOMATION_ID_PRESENT") || maxValidScore >= 90;

            if (hasAmbiguidade || hasA11yGap)
            {
                result.IsCritical = true;
                result.StatusLabel = "🔴 Crítico (Rejeitado)";
            }
            else if (isBrittle)
            {
                result.IsWarning = true;
                result.StatusLabel = "🟡 Aceitável (Com Alertas)";
            }
            else
            {
                result.IsHealthy = true;
                result.StatusLabel = "🟢 Saudável (Aprovado)";
            }

            var diag = new StringBuilder();
            if (isGolden && !hasAmbiguidade)
                diag.AppendLine("- ✅ **Padrão Ouro Web (PO):** O elemento possui identificadores estruturais robustos e únicos (ex: `data-testid` ou `id`). A automação será indestrutível.");

            if (hasAmbiguidade)
                diag.AppendLine($"- 🚨 **Ambiguidade Crítica (Strict Mode):** Foi detectado um seletor duplicado no DOM (Score Negativo atingiu {lowestNegativeScore}). Risco altíssimo de a automação clicar no elemento errado. Exija a correção do frontend!");

            if (hasA11yGap)
                diag.AppendLine("- ♿ **Falha de Acessibilidade (A11Y Gap):** O elemento é cego para leitores de tela. Não possui `aria-label`, `alt` ou texto interno descritivo.");

            if (isBrittle && !hasAmbiguidade && !hasA11yGap)
                diag.AppendLine("- ⚠️ **Débito Técnico Estrutural:** A interação depende exclusivamente de Classes CSS genéricas ou XPath Relativo. É altamente provável que quebre na próxima atualização visual.");

            if (!isGolden && !hasAmbiguidade && !hasA11yGap && !isBrittle)
                diag.AppendLine("- ℹ️ **Informativo (P1/P2):** Elemento funcional. Utiliza atributos semânticos ou visuais saudáveis (ex: `name`, `placeholder`, `text`).");

            result.DiagnosticsDetails = diag.ToString().TrimEnd();
            return result;
        }
    }
}