using System;
using System.Collections.Generic;
using System.Text;

namespace LiteAutomation.Core
{
    // =========================================================================
    // MODELOS PARA BDD (GHERKIN)
    // =========================================================================


    /// <summary>
    /// Representa o cenário completo, contendo o arquivo .feature e a lista de passos.
    /// </summary>
    public class BddScenarioModel
    {
        public string FeatureTitle { get; set; } = "Cenário Gerado Automaticamente";
        public List<BddStepModel> Steps { get; set; } = new List<BddStepModel>();
    }
}
