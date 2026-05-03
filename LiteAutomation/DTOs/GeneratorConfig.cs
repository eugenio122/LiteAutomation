using LiteAutomation.Enums;

namespace LiteAutomation.DTOs
{
    /// <summary>
    /// Representa a combinação exata selecionada pelo usuário na Matriz de 5 Eixos.
    /// </summary>
    public class GeneratorConfig
    {
        public AutomationPlatform Platform { get; set; }
        public AutomationStrategy Strategy { get; set; }
        public DesignPattern Pattern { get; set; }
        public TestFramework Framework { get; set; }
        public ScriptLanguage Language { get; set; }


        // 🚀 NOVOS PARÂMETROS DA INTERFACE
        public bool IncludeReport { get; set; }
        public BddStyle BddStyle { get; set; }

        // O Cofre de Decisões do Usuário (StepId -> Código do Locator escolhido)
        public Dictionary<string, string> LocatorOverrides { get; set; } = new Dictionary<string, string>();

        // Timeout global configurável pelo usuário no painel
        public int GlobalTimeoutMs { get; set; } = 30000;
    }
}