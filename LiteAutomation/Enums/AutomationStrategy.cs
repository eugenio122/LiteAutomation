using System;
using System.Collections.Generic;
using System.Text;

namespace LiteAutomation.Enums
{
    public enum AutomationStrategy
    {
        Smart_Selector,       // Usa a heurística inteligente misturando Semântico e DOM
        Accessibility_Audit,  // Foco exclusivo em relatórios de Acessibilidade
        Pure_SAP_GUI,         // Mantido para o futuro legado
        SAP_GUI_UIA           // Mantido para o futuro legado
    }
}
