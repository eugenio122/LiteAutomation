using System;
using System.Collections.Generic;
using System.Text;

namespace LiteAutomation.Core
{
    // =========================================================================
    // MODELOS PARA PAGE OBJECT MODEL (POM)
    // =========================================================================

    /// <summary>
    /// Representa uma "Página" (Tela) isolada no modelo POM.
    /// Contém os seletores mapeados e as ações que podem ser feitas nela.
    /// </summary>
    public class PomPageModel
    {
        public string ClassName { get; set; } = string.Empty;
        public string UrlIdentifier { get; set; } = string.Empty;

        /// <summary>
        /// Lista de Locators únicos usados nesta página. 
        /// O Gerador vai transformar isso em variáveis no topo da classe (ex: By btnLogin = ...).
        /// </summary>
        public Dictionary<string, string> MappedLocators { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// As intenções de ação reais que o usuário executou nesta tela (Cliques, Textos, Asserts).
        /// O Gerador vai transformar isso em métodos (ex: public void PreencherFormulario() { ... }).
        /// </summary>
        public List<AutomationIntent> PageActions { get; set; } = new List<AutomationIntent>();
    }
}
