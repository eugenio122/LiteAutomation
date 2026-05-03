using System.Drawing;
using FastColoredTextBoxNS;
using LiteAutomation.Enums;

namespace LiteAutomation.UI
{
    /// <summary>
    /// Centraliza todas as regras de sintaxe, margens e cores do FastColoredTextBox.
    /// Simulando o Visual Studio 2022 com um parser Customizado e Blindado.
    /// </summary>
    public static class EditorConfigurator
    {
        // =====================================================================
        // PALETA DE CORES (VISUAL STUDIO 2022 DARK)
        // =====================================================================
        private static readonly TextStyle DarkKeywordStyle = new TextStyle(new SolidBrush(Color.FromArgb(86, 156, 214)), null, FontStyle.Regular); // Azul
        private static readonly TextStyle DarkStringStyle = new TextStyle(new SolidBrush(Color.FromArgb(214, 157, 133)), null, FontStyle.Regular); // Laranja
        private static readonly TextStyle DarkCommentStyle = new TextStyle(new SolidBrush(Color.FromArgb(87, 166, 74)), null, FontStyle.Regular); // Verde
        private static readonly TextStyle DarkMethodStyle = new TextStyle(new SolidBrush(Color.FromArgb(220, 220, 170)), null, FontStyle.Regular); // Amarelo Pálido
        private static readonly TextStyle DarkClassAndAttrStyle = new TextStyle(new SolidBrush(Color.FromArgb(78, 201, 176)), null, FontStyle.Regular); // Verde Água

        // =====================================================================
        // PALETA DE CORES (VISUAL STUDIO LIGHT)
        // =====================================================================
        private static readonly TextStyle LightKeywordStyle = new TextStyle(Brushes.Blue, null, FontStyle.Regular);
        private static readonly TextStyle LightStringStyle = new TextStyle(new SolidBrush(Color.FromArgb(163, 21, 21)), null, FontStyle.Regular);
        private static readonly TextStyle LightCommentStyle = new TextStyle(new SolidBrush(Color.FromArgb(0, 128, 0)), null, FontStyle.Regular);
        private static readonly TextStyle LightMethodStyle = new TextStyle(new SolidBrush(Color.FromArgb(116, 83, 31)), null, FontStyle.Regular);
        private static readonly TextStyle LightClassAndAttrStyle = new TextStyle(new SolidBrush(Color.FromArgb(43, 145, 175)), null, FontStyle.Regular);

        public static void SetupBaseEditor(FastColoredTextBox editor)
        {
            editor.WordWrap = false;
            editor.ShowLineNumbers = true;
            editor.Font = new Font("Consolas", 10f);
            editor.ReadOnly = true;

            // Margem minimalista sem blocos coloridos
            editor.IndentBackColor = Color.Transparent;
        }

        public static void ApplyLanguageTheme(FastColoredTextBox editor, ScriptLanguage language, DesignPattern pattern, bool isDark)
        {
            // 1. Cores da Base do Editor
            editor.BackColor = isDark ? Color.FromArgb(30, 30, 30) : Color.White;
            editor.ForeColor = isDark ? Color.FromArgb(212, 212, 212) : Color.Black;
            editor.IndentBackColor = isDark ? Color.FromArgb(45, 45, 48) : Color.FromArgb(240, 240, 240);
            editor.LineNumberColor = isDark ? Color.FromArgb(43, 145, 175) : Color.Teal;

            // 2. DESLIGAR O MOTOR NATIVO (Que quebrava ao ler o "/*" do XPath do Selenium)
            editor.Language = Language.Custom;

            // 3. Limpar toda a sujeira visual antes de pintar
            editor.Range.ClearStyle(StyleIndex.All);

            // 4. Instanciar Paletas ativas
            var keywordStyle = isDark ? DarkKeywordStyle : LightKeywordStyle;
            var stringStyle = isDark ? DarkStringStyle : LightStringStyle;
            var commentStyle = isDark ? DarkCommentStyle : LightCommentStyle;
            var methodStyle = isDark ? DarkMethodStyle : LightMethodStyle;
            var classAttrStyle = isDark ? DarkClassAndAttrStyle : LightClassAndAttrStyle;

            // =================================================================
            // 5. PARSER CUSTOMIZADO (A ordem de pintura é vital!)
            // =================================================================

            // A) Comentários de Bloco (O nosso Gherkin Feature no topo)
            // Exigimos que comece no início da linha para não bugar com os //* do XPath
            editor.Range.SetStyle(commentStyle, @"^\s*/\*[\s\S]*?\*/", System.Text.RegularExpressions.RegexOptions.Multiline);

            // B) Comentários de Linha (//)
            // Exigimos início de linha para não pintar as URLs "https://" acidentalmente
            editor.Range.SetStyle(commentStyle, @"^\s*//.*$", System.Text.RegularExpressions.RegexOptions.Multiline);

            // C) Atributos do SpecFlow e NUnit (ex: [Binding], [Dado(...)])
            // Exigimos início de linha para não pintar o "[0]" de "arguments[0]" do JS Executor
            editor.Range.SetStyle(classAttrStyle, @"^\s*\[.*?\]", System.Text.RegularExpressions.RegexOptions.Multiline);

            // D) Palavras Reservadas (C#, Playwright, Selenium, NUnit)
            string keywords = @"\b(abstract|as|base|bool|break|byte|case|catch|char|checked|class|const|continue|decimal|default|delegate|do|double|else|enum|event|explicit|extern|false|finally|fixed|float|for|foreach|goto|if|implicit|in|int|interface|internal|is|lock|long|namespace|new|null|object|operator|out|override|params|private|protected|public|readonly|ref|return|sbyte|sealed|short|sizeof|stackalloc|static|string|struct|switch|this|throw|true|try|typeof|uint|ulong|unchecked|unsafe|ushort|using|virtual|void|volatile|while|await|async|var|Task|IPage|IWebDriver|IJavaScriptExecutor|WebDriverWait|Exception|By|Locator|Page|Assert)\b";
            editor.Range.SetStyle(keywordStyle, keywords);

            // E) Nomes de Classes
            editor.Range.SetStyle(classAttrStyle, @"(?<=class\s+)\w+");

            // F) Invocações e Assinaturas de Métodos
            editor.Range.SetStyle(methodStyle, @"\b(?!if|catch|for|while|switch|return|new)\w+(?=\s*\()");

            // G) Strings ("..." e @"...") - RODA POR ÚLTIMO
            // Ao rodar por último, garantimos que as strings sobresscrevam qualquer cor 
            // que tenha vazado acidentalmente para dentro das aspas!
            editor.Range.SetStyle(stringStyle, @"@""([^""]|"""")*""|""(\\.|[^\\""])*""");

            // Força a tela a se redesenhar com a nova beleza
            editor.Invalidate();
        }
    }
}