using System;
using System.Collections.Generic;
using System.Text;

namespace LiteAutomation.Enums
{
    /// <summary>
    /// O Idioma Universal do LiteAutomation. 
    /// Representa os verbos fundamentais que qualquer framework (Selenium, Cypress, Playwright) deve saber traduzir.
    /// </summary>
    public enum IntentType
    {
        // Navegação e Controle
        NavigateToUrl,
        WaitUrlChange,

        // Ações de Usuário
        Click,
        InputText,
        KeyPress,
        ScrollTo,

        // 🚀 NOVO: Ações de Contexto e Mouse
        Hover,         // Passar o mouse sobre o elemento (MouseOver)
        Blur,          // Clicar fora / Remover o foco de um campo (Ghost Clicks)

        // Asserts e Validações (Delta Engine)
        AssertVisible,
        AssertEnabled,

        // Desconhecido ou Fallback
        Unknown
    }
}