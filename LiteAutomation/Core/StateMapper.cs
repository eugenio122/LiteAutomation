using System;
using System.Collections.Generic;
using System.Linq;
using LiteAutomation.DTOs;

namespace LiteAutomation.Core
{
    /// <summary>
    /// Representa um "Estado" ou "Fronteira de Página" único dentro da jornada de automação.
    /// Agrupa todos os passos e interações que ocorreram na mesma tela.
    /// </summary>
    public class PageState
    {
        public string StateId { get; set; } = Guid.NewGuid().ToString("N");
        public string BaseUrl { get; set; } = string.Empty;
        public string PageTitle { get; set; } = string.Empty;

        /// <summary>
        /// Todos os passos (snapshots) que ocorreram dentro deste mesmo estado/tela.
        /// </summary>
        public List<MainStepDto> Steps { get; set; } = new List<MainStepDto>();

        /// <summary>
        /// Atalho para resgatar todas as interações humanas que aconteceram nesta tela.
        /// Útil para verificar unicidade de seletores localmente (O(N) segmentado).
        /// </summary>
        public List<InteractionDto> LocalInteractions => Steps
            .Where(s => s.InteractionTrail != null)
            .SelectMany(s => s.InteractionTrail)
            .ToList();

        /// <summary>
        /// Agrega a árvore de elementos visíveis capturada no primeiro passo deste estado,
        /// servindo como base de conhecimento do DOM para validação de ambiguidade.
        /// </summary>
        public List<VisibleElementDto> BaseVisibleElements => Steps
            .FirstOrDefault(s => s.ObservedContext?.VisibleElements != null && s.ObservedContext.VisibleElements.Any())
            ?.ObservedContext?.VisibleElements ?? new List<VisibleElementDto>();
    }

    /// <summary>
    /// O Motor responsável por transformar uma lista linear de passos (MainStepDto)
    /// em uma Máquina de Estados baseada no contexto do navegador (URLs/Endpoints).
    /// </summary>
    public class StateMapper
    {
        /// <summary>
        /// Varre os passos brutos do JSON e os agrupa em Estados (PageStates).
        /// </summary>
        /// <param name="rawSteps">Lista bruta extraída do JSON.</param>
        /// <returns>Uma lista de Estados contendo as interações agrupadas.</returns>
        public List<PageState> MapStates(List<MainStepDto> rawSteps)
        {
            var states = new List<PageState>();
            if (rawSteps == null || rawSteps.Count == 0)
                return states;

            PageState currentState = null;

            foreach (var step in rawSteps)
            {
                if (!step.IsActive || step.PendingConfirmation)
                    continue;

                string currentUrl = step.ObservedContext?.Url ?? string.Empty;
                string currentTitle = step.ObservedContext?.PageTitle ?? string.Empty;

                // Identifica se houve uma mudança de página/estado
                bool isNewState = currentState == null ||
                                  (!string.IsNullOrEmpty(currentUrl) && !AreUrlsEquivalent(currentState.BaseUrl, currentUrl));

                if (isNewState)
                {
                    currentState = new PageState
                    {
                        BaseUrl = currentUrl,
                        PageTitle = currentTitle
                    };
                    states.Add(currentState);
                }

                // Vincula o passo ao estado atual
                currentState.Steps.Add(step);
            }

            return states;
        }

        /// <summary>
        /// Compara duas URLs para determinar se elas representam a mesma "Fronteira de Página".
        /// Ignora Query Strings (?id=1) e Fragmentos (#secao) para evitar falsos positivos de transição de tela.
        /// </summary>
        private bool AreUrlsEquivalent(string url1, string url2)
        {
            if (string.IsNullOrWhiteSpace(url1) || string.IsNullOrWhiteSpace(url2))
                return false;

            try
            {
                // Garante que a URI seja válida para o parser (fallback para localhost se for caminho relativo)
                var uri1 = new Uri(url1.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? url1 : $"http://localhost/{url1.TrimStart('/')}");
                var uri2 = new Uri(url2.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? url2 : $"http://localhost/{url2.TrimStart('/')}");

                // Compara apenas o domínio e o caminho base (Endpoint puro)
                string basePath1 = uri1.GetLeftPart(UriPartial.Path);
                string basePath2 = uri2.GetLeftPart(UriPartial.Path);

                return basePath1.Equals(basePath2, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                // Em caso de falha de parser, utiliza o fallback de string exata
                return url1.Equals(url2, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}