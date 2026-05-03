using System;
using System.Collections.Generic;
using System.Text;

namespace LiteAutomation.Core
{
    // =========================================================================
    // MODELOS PARA BDD (GHERKIN)
    // =========================================================================

    /// <summary>
    /// Representa um passo do Gherkin (Given, When, Then).
    /// </summary>
    public class BddStepModel
    {
        public string Keyword { get; set; } = "When"; // Dado, Quando, Entao (Given, When, Then)
        public string TextDescription { get; set; } = string.Empty; // "eu clico no botão de login"

        /// <summary>
        /// O código que deve rodar dentro deste passo.
        /// </summary>
        public List<AutomationIntent> InternalIntents { get; set; } = new List<AutomationIntent>();
    }
}
