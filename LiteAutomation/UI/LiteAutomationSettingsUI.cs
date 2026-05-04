using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using FastColoredTextBoxNS;
using LiteAutomation.Core;
using LiteAutomation.DTOs;
using LiteAutomation.Enums;
using LiteAutomation.Diagnostics;
using LiteTools.Core.Languages;

namespace LiteAutomation.UI
{
    public partial class LiteAutomationSettingsUI : UserControl
    {
        private FastColoredTextBox codeEditor;
        private DataGridView gridLocators;
        private Button btnImportJson;
        private Button btnExport;
        private Button btnApplyFilter;

        private Label lblCurrentFile;
        private Button btnClearFile;
        private SplitContainer splitContainer;
        private Button btnPinPanel;
        private Button btnApplyLocators;

        private ComboBox cmbPlatform, cmbStrategy, cmbPattern, cmbFramework, cmbLanguage;

        private CheckBox chkReport;
        private Panel pnlBddStyle;
        private RadioButton rdoNarrative;
        private RadioButton rdoCanonical;

        private WorkspaceState _workspace;
        private GeneratorConfig _currentConfig;
        private string _currentLanguage;
        private bool _isDarkMode = false;
        private bool _isProcessed = false;
        private bool _isUpdatingRules = false;

        private class UIPreferences
        {
            public int SplitterDistance { get; set; } = 850;
            public bool IsPinned { get; set; } = false;
            // 🚀 Propriedade que vai para o JSON
            public string Language { get; set; } = "pt-BR";
        }

        private UIPreferences _uiPrefs;

        public LiteAutomationSettingsUI(string hostLanguage)
        {
            _currentConfig = new GeneratorConfig();
            _workspace = new WorkspaceState();

            LoadUIPreferences();

            // 🚀 LÓGICA DE SINCRONIZAÇÃO BLINDADA
            // Se o idioma enviado pela Nave-Mãe for diferente do que temos no JSON, atualizamos o JSON!
            if (!string.IsNullOrEmpty(hostLanguage) && _uiPrefs.Language != hostLanguage)
            {
                _uiPrefs.Language = hostLanguage;
                SaveUIPreferences();
            }

            // Define o idioma atual com base na preferência consolidada
            _currentLanguage = _uiPrefs.Language ?? "pt-BR";
            LanguageManager.CurrentLanguage = _currentLanguage;

            InitializeComponent();
            InitializeMatrix();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            this.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (_uiPrefs.SplitterDistance > 0)
                        splitContainer.SplitterDistance = Math.Min(_uiPrefs.SplitterDistance, splitContainer.Width - 50);
                    splitContainer.IsSplitterFixed = _uiPrefs.IsPinned;
                    UpdatePinButtonVisuals();
                }
                catch { }
            }));
        }

        private string GetConfigPath() => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LiteAutomationConfig.json");

        private void LoadUIPreferences()
        {
            try
            {
                string path = GetConfigPath();
                _uiPrefs = File.Exists(path) ? JsonSerializer.Deserialize<UIPreferences>(File.ReadAllText(path)) ?? new UIPreferences() : new UIPreferences();
            }
            catch { _uiPrefs = new UIPreferences(); }
        }

        private void SaveUIPreferences()
        {
            try { File.WriteAllText(GetConfigPath(), JsonSerializer.Serialize(_uiPrefs)); } catch { }
        }
    }
}