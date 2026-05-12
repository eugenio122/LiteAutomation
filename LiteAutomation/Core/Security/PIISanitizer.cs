using System.Text.RegularExpressions;

namespace LiteAutomation.Core.Security
{
    /// <summary>
    /// Motor de sanitização de PII (Personally Identifiable Information) para conformidade com a LGPD.
    /// Atua como um mecanismo de Double Check (Defesa em Profundidade) no backend C#.
    /// A sanitização primária já ocorre na fonte durante a gravação do JSON (via injeção JS), 
    /// garantindo que dados sensíveis não sejam processados ou exibidos no SDET Panel caso a primeira camada falhe.
    /// </summary>
    public static class PIISanitizer
    {
        // Padrões Regex focados no contexto do Brasil e formatos internacionais básicos

        // CNPJ: 00.000.000/0000-00 ou apenas números
        private static readonly Regex CnpjRegex = new Regex(@"\b\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2}\b", RegexOptions.Compiled);

        // CPF: 000.000.000-00 ou apenas números
        private static readonly Regex CpfRegex = new Regex(@"\b\d{3}\.?\d{3}\.?\d{3}-?\d{2}\b", RegexOptions.Compiled);

        // E-mail padrão
        private static readonly Regex EmailRegex = new Regex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b", RegexOptions.Compiled);

        // Cartão de Crédito (16 dígitos com ou sem espacios/hífens)
        private static readonly Regex CreditCardRegex = new Regex(@"\b(?:\d{4}[-\s]?){3}\d{4}\b", RegexOptions.Compiled);

        // Telefone Celular/Fixo Brasil: +55 (11) 90000-0000, 11900000000, etc.
        private static readonly Regex PhoneRegex = new Regex(@"\b(?:\+?55\s?)?(?:\(?\d{2}\)?\s?)?\d{4,5}[-\s]?\d{4}\b", RegexOptions.Compiled);

        /// <summary>
        /// Aplica todas as regras de mascaramento em uma string de entrada para ocultar informações sensíveis como Double Check.
        /// </summary>
        /// <param name="input">A string coletada pelo UIA ou BiDi.</param>
        /// <returns>A string limpa com as tags de ocultação (ex: [CPF OCULTO]).</returns>
        public static string Sanitize(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input ?? string.Empty;

            string sanitized = input;

            // A ordem de aplicação importa. 
            // CNPJ é processado antes do CPF para evitar que os primeiros 11 dígitos de um CNPJ sem formatação sejam capturados como CPF.
            sanitized = CnpjRegex.Replace(sanitized, "[CNPJ OCULTO]");
            sanitized = CpfRegex.Replace(sanitized, "[CPF OCULTO]");

            // Cartão de crédito processado antes de telefone para evitar colisão de números longos
            sanitized = CreditCardRegex.Replace(sanitized, "[CARTÃO OCULTO]");
            sanitized = PhoneRegex.Replace(sanitized, "[TELEFONE OCULTO]");

            sanitized = EmailRegex.Replace(sanitized, "[EMAIL OCULTO]");

            return sanitized;
        }
    }
}