using LiteAutomation.Core;
using LiteAutomation.DTOs;
using LiteAutomation.Enums;
using System.Collections.Generic;

namespace LiteAutomation.Interfaces
{
    public interface ICodeGenerator
    {
        /// <summary>
        /// Gera o código fonte ou arquivo de automação com base no Fat Payload do LiteJson.
        /// </summary>
        /// <param name="workspace"> Recebe o estado completo da Workspace (Cache + RAW) e gera o código final.</param>
        /// <param name="config">A matriz de 5 eixos e os ajustes finos do painel modular.</param>
        /// <param name="testClassName">O nome base para a classe ou cenário gerado.</param>
        /// <returns>O código final em formato string.</returns>
        string GenerateCode(WorkspaceState workspace, GeneratorConfig config, string testClassName = "GeneratedTest");
    }
}