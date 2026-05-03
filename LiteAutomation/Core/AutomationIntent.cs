using System;
using System.Collections.Generic;
using LiteAutomation.Enums;

namespace LiteAutomation.Core
{
    /// <summary>
    /// Representa uma ação ou validação isolada, já com toda a inteligência mastigada pelo DeltaAnalyzer.
    /// O Gerador final só precisa ler isso e escrever a string correspondente.
    /// </summary>
    public class AutomationIntent
    {
        /// <summary>
        /// O que o robô deve fazer? (Ex: Clicar, Esperar URL, Escrever Texto).
        /// </summary>
        public IntentType Type { get; set; }

        /// <summary>
        /// Onde o robô deve interagir? (A string do Seletor exato escolhido pelo SDET na grelha).
        /// Vazio para ações globais como navegar para URL.
        /// </summary>
        public string TargetLocator { get; set; } = string.Empty;

        /// <summary>
        /// O que deve ser injetado/validado? (Ex: O texto a ser digitado, ou a URL esperada).
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Qual foi a tecla pressionada? (Preenchido apenas quando IntentType == KeyPress).
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Comentários técnicos ou alertas gerados pelo motor (Dívidas Técnicas, avisos de ambiguidade).
        /// O Tradutor deve colocar isso como comentário acima da linha de código.
        /// </summary>
        public string Diagnostics { get; set; } = string.Empty;

        /// <summary>
        /// 🚀 A Mensagem Sutíl e Humana que será injetada nos blocos Try-Catch ou Asserts do framework alvo.
        /// </summary>
        public string FriendlyErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Referência para a UI (ex: "1.1"). Útil para mapeamento e logs.
        /// </summary>
        public string StepId { get; set; } = string.Empty;

        /// <summary>
        /// A descrição humana daquele passo (ex: "Passo 1.0: Nova Ação").
        /// </summary>
        public string StepDescription { get; set; } = string.Empty;

        /// <summary>
        /// Indica se este bloco representa a abertura de um novo MainStep (âncora visual),
        /// para que os geradores possam colocar um cabeçalho decorativo (ex: // --- PASSO 2 ---).
        /// </summary>
        public bool IsNewStepHeader { get; set; }
    }
}