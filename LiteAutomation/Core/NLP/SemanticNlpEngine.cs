using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using LiteTools.Core.Languages; // 🚀 Injeção do LanguageManager

namespace LiteAutomation.Core.NLP
{
    public static class SemanticNlpEngine
    {
        /// <summary>
        /// Gera o nome de variável seguro para código (ex: btnEntrarSemSenha).
        /// </summary>
        public static string GenerateVariableName(string rawText, string role)
        {
            var (cleanedWords, semanticPrefix) = ProcessPipeline(rawText, role);

            // Usa o fallback traduzido (ex: btnGenerico, btnGeneric)
            if (cleanedWords.Length == 0) return $"{semanticPrefix}{LanguageManager.GetString("NlpGenericVar")}";

            string camelCaseName = string.Join("", cleanedWords.Select(Capitalize));
            return $"{semanticPrefix}{camelCaseName}";
        }

        /// <summary>
        /// Gera o texto fluido para o Gherkin (ex: botão entrar sem senha).
        /// </summary>
        public static string GenerateHumanReadable(string rawText, string role)
        {
            var (cleanedWords, semanticPrefix) = ProcessPipeline(rawText, role);
            string humanPrefix = GetHumanPrefix(semanticPrefix);

            // Usa o fallback traduzido (ex: botão genérico, generic button)
            if (cleanedWords.Length == 0) return $"{humanPrefix}{LanguageManager.GetString("NlpGenericHuman")}";

            string cleanText = string.Join(" ", cleanedWords);
            return $"{humanPrefix}{cleanText}";
        }

        // =====================================================================
        // O PIPELINE DE 4 PASSOS
        // =====================================================================
        private static (string[] words, string prefix) ProcessPipeline(string rawText, string role)
        {
            if (string.IsNullOrWhiteSpace(rawText)) rawText = "";

            // Passo 1: Limpeza (ToLower, Sem Acentos, Regex de Pontuação)
            string normalized = RemoveDiacritics(rawText.ToLower());
            normalized = Regex.Replace(normalized, @"[^a-z0-9\s]", " "); // Troca símbolos por espaço
            normalized = Regex.Replace(normalized, @"\s+", " ").Trim(); // Remove múltiplos espaços

            // 🚀 Passo 2: Filtro de Stop-Words (Agora consome o idioma global do LanguageManager)
            var stopWords = StopWordsManager.GetStopWords(LanguageManager.CurrentLanguage);
            var allWords = normalized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var filteredWords = allWords.Where(w => !stopWords.Contains(w)).ToArray();

            // Fallback de segurança (Se a frase inteira for só Stop-Words, usamos a original limpa)
            if (filteredWords.Length == 0 && allWords.Length > 0)
                filteredWords = allWords;

            // Passo 3: Injeção de Prefixo Semântico
            string safeRole = (role ?? "").ToLower();
            string semanticPrefix = "el";

            if (safeRole.Contains("button") || safeRole.Contains("botão") || safeRole.Contains("botón") || safeRole.Contains("bouton")) semanticPrefix = "btn";
            else if (safeRole.Contains("textbox") || safeRole.Contains("input") || safeRole.Contains("texto") || safeRole.Contains("field")) semanticPrefix = "input";
            else if (safeRole.Contains("link") || safeRole == "a" || safeRole.Contains("enlace") || safeRole.Contains("lien")) semanticPrefix = "link";
            else if (safeRole.Contains("combobox") || safeRole.Contains("select") || safeRole.Contains("lista") || safeRole.Contains("dropdown")) semanticPrefix = "combo";

            return (filteredWords, semanticPrefix);
        }

        // =====================================================================
        // UTILITÁRIOS
        // =====================================================================
        private static string GetHumanPrefix(string codePrefix)
        {
            // 🚀 Busca do Dicionário Multi-idioma
            return codePrefix switch
            {
                "btn" => LanguageManager.GetString("NlpBtn"),
                "input" => LanguageManager.GetString("NlpInput"),
                "link" => LanguageManager.GetString("NlpLink"),
                "combo" => LanguageManager.GetString("NlpCombo"),
                _ => LanguageManager.GetString("NlpElement")
            };
        }

        private static string RemoveDiacritics(string text)
        {
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark) sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private static string Capitalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return char.ToUpper(text[0]) + text.Substring(1);
        }
    }
}