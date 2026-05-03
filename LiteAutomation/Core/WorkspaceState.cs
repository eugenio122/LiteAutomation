using System.Collections.Generic;
using LiteAutomation.DTOs;

namespace LiteAutomation.Core
{
    /// <summary>
    /// Gerencia o estado em memória (Cache) do projeto ativo, 
    /// evitando reprocessamento matemático desnecessário e travamentos na UI.
    /// </summary>
    public class WorkspaceState
    {
        // 1. A Camada Bruta (O JSON LIDO DO DISCO)
        public List<MainStepDto> RawSteps { get; private set; }

        // 2. O Coração (A Matemática já processada)
        public List<AutomationIntent> IntentCache { get; private set; }

        private DeltaAnalyzer _analyzer;

        public WorkspaceState()
        {
            _analyzer = new DeltaAnalyzer();
        }

        /// <summary>
        /// Carrega o JSON bruto e já gera o cache de intenções (AST).
        /// </summary>
        public void LoadJson(List<MainStepDto> steps, GeneratorConfig config)
        {
            RawSteps = steps;
            RebuildIntentCache(config);
        }

        /// <summary>
        /// Limpa o cache quando o usuário fecha o arquivo.
        /// </summary>
        public void Clear()
        {
            RawSteps = null;
            IntentCache = null;
        }

        /// <summary>
        /// Força a reconstrução do Cache Matemático. 
        /// Só deve ser chamado quando um Locator é alterado na UI.
        /// </summary>
        public void RebuildIntentCache(GeneratorConfig config)
        {
            if (RawSteps == null) return;
            IntentCache = _analyzer.Analyze(RawSteps, config);
        }

        public bool HasData => RawSteps != null && RawSteps.Count > 0;
    }
}