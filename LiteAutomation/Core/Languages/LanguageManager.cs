using System.Collections.Generic;

namespace LiteTools.Core.Languages
{
    /// <summary>
    /// Gerenciador central de internacionalização (i18n) do Host.
    /// Mantém as traduções em memória para alta performance e fornece os 
    /// textos de interface baseados no idioma global escolhido pelo usuário.
    /// </summary>
    public static partial class LanguageManager
    {
        /// <summary>
        /// O idioma global atualmente ativo no ecossistema (ex: "pt-BR", "en-US").
        /// É definido durante a inicialização do Host (via HostSettings) e injetado nos plugins.
        /// </summary>
        public static string CurrentLanguage = "pt-BR";

        /// <summary>
        /// Dicionário em memória contendo todas as strings da UI do Host.
        /// Estrutura: Translations["idioma"]["ChaveDaString"]
        /// </summary>
        private static readonly Dictionary<string, Dictionary<string, string>> Translations = new();

        /// <summary>
        /// Construtor estático. Inicializa todos os dicionários dos arquivos parciais.
        /// </summary>
        static LanguageManager()
        {
            InitPortuguese();
            InitEnglish();
            InitSpanish();
            InitFrench();
            InitGerman();
            InitItalian();
        }

        /// <summary>
        /// Procura a tradução baseada na chave e no idioma atualmente configurado (CurrentLanguage).
        /// Caso a chave não seja encontrada, retorna o próprio nome da chave para facilitar o debug visual.
        /// </summary>
        /// <param name="key">A chave do texto (ex: "SaveAndApply")</param>
        /// <returns>O texto traduzido ou a chave original em caso de falha.</returns>
        public static string GetString(string key)
        {
            if (Translations.ContainsKey(CurrentLanguage) && Translations[CurrentLanguage].ContainsKey(key))
                return Translations[CurrentLanguage][key];

            return key; // Retorna a própria chave (Fallback visual)
        }
    }
}