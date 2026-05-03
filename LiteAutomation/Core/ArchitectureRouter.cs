using System;
using System.Collections.Generic;
using System.Linq;
using LiteAutomation.Enums;

namespace LiteAutomation.Core
{
    /// <summary>
    /// Pega a fita linear do DeltaAnalyzer e a "fatia" em Páginas (POM) ou Passos de Negócio (BDD).
    /// Esta classe não escreve código de linguagem, apenas organiza a lógica matemática.
    /// </summary>
    public class ArchitectureRouter
    {
        // =====================================================================
        // CONSTRUTOR POM (Fatia a fita baseada em URLs)
        // =====================================================================
        public List<PomPageModel> BuildPomStructure(List<AutomationIntent> linearIntents)
        {
            var pages = new List<PomPageModel>();
            PomPageModel currentPage = null;
            int pageCounter = 1;

            foreach (var intent in linearIntents)
            {
                // Se for uma navegação, criamos uma nova "Página" no nosso modelo mental
                if (intent.Type == IntentType.NavigateToUrl || intent.Type == IntentType.WaitUrlChange)
                {
                    string safeName = ExtractPageNameFromUrl(intent.Value);
                    if (string.IsNullOrEmpty(safeName)) safeName = $"Page{pageCounter}";

                    currentPage = new PomPageModel
                    {
                        ClassName = $"{safeName}Page",
                        UrlIdentifier = intent.Value
                    };
                    pages.Add(currentPage);
                    pageCounter++;
                }

                // Se houver interações e não tivermos página (ex: Single Page App sem mudança de URL formal inicial), forçamos uma
                if (currentPage == null && intent.Type != IntentType.Unknown)
                {
                    currentPage = new PomPageModel { ClassName = "MainPage", UrlIdentifier = "app" };
                    pages.Add(currentPage);
                }

                // Adicionamos as ações à página atual
                if (currentPage != null && intent.Type != IntentType.Unknown && !intent.IsNewStepHeader)
                {
                    currentPage.PageActions.Add(intent);

                    // Mapeia o locator para o dicionário do POM (evitando locators duplicados na mesma tela)
                    if (!string.IsNullOrEmpty(intent.TargetLocator) && !currentPage.MappedLocators.ContainsKey(intent.TargetLocator))
                    {
                        string locName = $"element_{intent.StepId.Replace(".", "_")}";
                        currentPage.MappedLocators.Add(intent.TargetLocator, locName);
                    }
                }
            }

            return pages;
        }

        // =====================================================================
        // CONSTRUTOR BDD (Fatia a fita baseada nas Âncoras do SDET)
        // =====================================================================
        public BddScenarioModel BuildBddStructure(List<AutomationIntent> linearIntents)
        {
            var scenario = new BddScenarioModel();
            BddStepModel currentStep = null;

            // Passo padrão de configuração inicial (Given)
            scenario.Steps.Add(new BddStepModel
            {
                Keyword = "Given",
                TextDescription = "que eu acesso o sistema"
            });

            foreach (var intent in linearIntents)
            {
                if (intent.Type == IntentType.NavigateToUrl)
                {
                    scenario.Steps[0].InternalIntents.Add(intent);
                    continue;
                }

                // Toda vez que o painel tiver um MainStep (Âncora Visual), vira um 'When' ou 'Then' no BDD
                if (intent.IsNewStepHeader)
                {
                    // Se a descrição contiver palavras como "valido", "verifico", "erro", vira Then (Assert)
                    string keyword = intent.StepDescription.ToLower().Contains("valida") || intent.StepDescription.ToLower().Contains("verifica")
                                     ? "Then" : "When";

                    currentStep = new BddStepModel
                    {
                        Keyword = keyword,
                        TextDescription = $"eu realizo a acao de {intent.StepDescription.ToLower()}"
                    };
                    scenario.Steps.Add(currentStep);
                }
                else if (currentStep != null)
                {
                    currentStep.InternalIntents.Add(intent);
                }
            }

            // Remove passos BDD que ficaram vazios (sem intents internos)
            scenario.Steps.RemoveAll(s => s.InternalIntents.Count == 0);

            return scenario;
        }

        // =====================================================================
        // Utilitários Internos
        // =====================================================================
        private string ExtractPageNameFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            try
            {
                var uri = new Uri(url.StartsWith("http") ? url : "http://dummy.com" + url);
                string lastSegment = uri.Segments.LastOrDefault()?.Replace("/", "").Split('?')[0];

                if (string.IsNullOrEmpty(lastSegment) || lastSegment == "index.html")
                    return "Home";

                // Capitaliza a primeira letra (ex: sacola -> Sacola)
                return char.ToUpper(lastSegment[0]) + lastSegment.Substring(1).Replace("-", "").Replace("_", "");
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}