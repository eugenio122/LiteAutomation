namespace LiteAutomation.Enums
{
    /// <summary>
    /// Padrões de Arquitetura de Software Suportados pelo LiteAutomation.
    /// </summary>
    public enum DesignPattern
    {
        /// <summary>
        /// Script de cima a baixo (Simples e direto).
        /// </summary>
        Linear,

        /// <summary>
        /// Separa a lógica de testes da lógica de mapeamento de telas.
        /// </summary>
        Page_Object_Model,

        /// <summary>
        /// Escrita de testes orientada a comportamento (Given, When, Then).
        /// </summary>
        BDD_Gherkin,

        /// <summary>
        /// 👑 A Arquitetura Suprema: Cenários em Gherkin cujos passos chamam métodos de Páginas POM.
        /// </summary>
        BDD_POM_Hybrid,

        /// <summary>
        /// Arquitetura para o tosca, onde os testes são escritos em um formato modular e exportados para o Tosca.
        /// </summary>
        Modular
    }
}