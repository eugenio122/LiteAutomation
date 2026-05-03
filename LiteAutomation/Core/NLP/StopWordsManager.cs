using System.Collections.Generic;

namespace LiteAutomation.Core.NLP
{
    /// <summary>
    /// Gerenciador em memória das palavras vazias (Stop-Words) para o motor NLP.
    /// Utiliza HashSet para garantir performance O(1) na filtragem das strings.
    /// </summary>
    public static class StopWordsManager
    {
        private static readonly Dictionary<string, HashSet<string>> _stopWords = new()
        {
            ["pt-BR"] = new HashSet<string> {
                "o", "a", "os", "as", "um", "uma", "de", "do", "da", "para", "com", "por",
                "em", "no", "na", "clique", "informe", "digite", "insira", "aqui", "seu",
                "sua", "texto", "campo", "botão", "botao", "ou", "e"
            },
            ["en-US"] = new HashSet<string> {
                "the", "a", "an", "of", "to", "in", "on", "for", "with", "click",
                "enter", "type", "here", "your", "text", "field", "button", "or", "and"
            },
            ["es-ES"] = new HashSet<string> {
                "el", "la", "los", "las", "un", "una", "de", "del", "para", "con", "por",
                "en", "haga", "clic", "ingrese", "escriba", "aqui", "su", "texto", "campo", "boton", "o", "y"
            },
            ["fr-FR"] = new HashSet<string> {
                "le", "la", "les", "un", "une", "de", "du", "des", "pour", "avec", "par",
                "dans", "sur", "cliquez", "entrez", "tapez", "ici", "votre", "texte", "champ", "bouton", "ou", "et"
            },
            ["it-IT"] = new HashSet<string> {
                "il", "lo", "la", "i", "gli", "le", "un", "uno", "una", "di", "del", "della",
                "per", "con", "su", "in", "clicca", "inserisci", "digita", "qui", "tuo", "tua", "testo", "campo", "pulsante", "o", "e"
            },
            ["de-DE"] = new HashSet<string> {
                "der", "die", "das", "ein", "eine", "von", "zu", "für", "mit", "auf", "in",
                "klicken", "geben", "sie", "hier", "ihr", "ihre", "text", "feld", "taste", "oder", "und"
            }
        };

        public static HashSet<string> GetStopWords(string languageCode)
        {
            if (_stopWords.TryGetValue(languageCode, out var words))
                return words;

            return new HashSet<string>(); // Retorna vazio caso o idioma não esteja mapeado
        }
    }
}