using System;
using LiteAutomation.DTOs;
using LiteAutomation.Enums;
using LiteAutomation.Interfaces;

using LiteAutomation.Generators.CSharp;
using LiteAutomation.Generators.Java;
using LiteAutomation.Generators.JavaScript;
using LiteAutomation.Generators.Reports;

namespace LiteAutomation.Factories
{
    public static class CodeGeneratorFactory
    {
        public static ICodeGenerator Create(GeneratorConfig config)
        {
            // =========================================================
            // 🛡️ REGRAS GLOBAIS E BLOQUEIOS
            // =========================================================
            if (config.Strategy == AutomationStrategy.Accessibility_Audit)
                return new A11yReportGenerator();

            if (config.Platform == AutomationPlatform.Desktop_SAP)
                throw new NotImplementedException($"Desktop SAP não suportado no momento.");

            // =========================================================
            // 🌐 ROTEAMENTO WEB
            // =========================================================
            if (config.Platform == AutomationPlatform.Web)
            {
                // -----------------------------------------------------
                // 🟡 TRADUTORES PARA CYPRESS (JS)
                // -----------------------------------------------------
                if (config.Framework == TestFramework.Cypress)
                {
                    if (config.Pattern == DesignPattern.Linear) return new CypressLinearJsGenerator();

                    // TODO (Treino C): Implementar BDD e POM para Cypress
                    // if (config.Pattern == DesignPattern.Page_Object_Model) return new CypressPomJsGenerator();
                    // if (config.Pattern == DesignPattern.BDD_Gherkin) return new CypressBddJsGenerator();
                    // if (config.Pattern == DesignPattern.BDD_POM_Hybrid) return new CypressBddPomJsGenerator();

                    throw new NotImplementedException($"A arquitetura {config.Pattern} ainda não foi implementada para Cypress.");
                }

                // -----------------------------------------------------
                // 🔴 TRADUTORES PARA SELENIUM (JAVA)
                // -----------------------------------------------------
                if (config.Framework == TestFramework.Selenium && config.Language == ScriptLanguage.Java)
                {
                    if (config.Pattern == DesignPattern.Linear) return new SeleniumLinearJavaGenerator();

                    // TODO (Treino B): Implementar as arquiteturas para Java (Faremos depois)
                    // if (config.Pattern == DesignPattern.Page_Object_Model) return new SeleniumPomJavaGenerator();
                    // if (config.Pattern == DesignPattern.BDD_Gherkin) return new SeleniumBddJavaGenerator();
                    // if (config.Pattern == DesignPattern.BDD_POM_Hybrid) return new SeleniumBddPomJavaGenerator();

                    throw new NotImplementedException($"A arquitetura {config.Pattern} ainda não foi implementada para Java.");
                }

                // -----------------------------------------------------
                // 🟣 TRADUTORES PARA SELENIUM (C#) - CONCLUÍDO (Treino A)
                // -----------------------------------------------------
                if (config.Framework == TestFramework.Selenium && config.Language == ScriptLanguage.CSharp)
                {
                    if (config.Pattern == DesignPattern.Linear) return new SeleniumLinearCSharpGenerator();
                    if (config.Pattern == DesignPattern.Page_Object_Model) return new SeleniumPomCSharpGenerator();
                    if (config.Pattern == DesignPattern.BDD_Gherkin) return new SeleniumBddCSharpGenerator();
                    if (config.Pattern == DesignPattern.BDD_POM_Hybrid) return new SeleniumBddPomCSharpGenerator();
                }

                // -----------------------------------------------------
                // 🟢 TRADUTORES PARA PLAYWRIGHT (C#)
                // -----------------------------------------------------
                if (config.Framework == TestFramework.Playwright)
                {
                    if (config.Pattern == DesignPattern.Linear) return new PlaywrightLinearCSharpGenerator();
                    throw new NotImplementedException("Arquitetura não implementada para Playwright.");
                }
            }

            throw new NotSupportedException("Combinação de eixos inválida ou não suportada.");
        }
    }
}