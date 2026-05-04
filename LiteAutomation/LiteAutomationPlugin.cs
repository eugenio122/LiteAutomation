using System;
using System.Windows.Forms;
using LiteAutomation.UI;
using LiteTools.Interfaces;
using LiteTools.Core.Languages;

namespace LiteAutomation
{
    public class LiteAutomationPlugin : ILitePlugin
    {
        public string Name => "LiteAutomation";
        public string Version => "1.0.0";

        private ILiteHostContext? _hostContext;
        private IEventBus? _eventBus;
        private LiteAutomationSettingsUI? _settingsUI;

        private string _language = "pt-BR";
        private bool _isCurrentDark = true;

        public void Initialize(ILiteHostContext hostContext, IEventBus eventBus, string currentLanguage)
        {
            _hostContext = hostContext;
            _eventBus = eventBus;

            // 🚀 Guarda o idioma recebido pela Nave-Mãe
            _language = string.IsNullOrEmpty(currentLanguage) ? "pt-BR" : currentLanguage;

            // Sincroniza globalmente
            LanguageManager.CurrentLanguage = _language;

            if (_hostContext.TryGetSessionMetadata<bool>("IsDarkMode", out bool isDark))
            {
                _isCurrentDark = isDark;
            }

            _eventBus.Subscribe<ThemeChangedEvent>(OnThemeChanged);
        }

        private void OnThemeChanged(ThemeChangedEvent e)
        {
            _isCurrentDark = e.IsDarkMode;
            if (_settingsUI != null && !_settingsUI.IsDisposed)
            {
                _settingsUI.Invoke(new Action(() => _settingsUI.ApplyTheme(_isCurrentDark)));
            }
        }

        public UserControl GetSettingsUI()
        {
            if (_settingsUI == null || _settingsUI.IsDisposed)
            {
                // 🚀 PASSAGEM EXPLÍCITA: Avisamos a UI exatamente qual é o idioma da vez
                _settingsUI = new LiteAutomationSettingsUI(_language);
                _settingsUI.ApplyTheme(_isCurrentDark);
            }
            return _settingsUI;
        }

        public void Shutdown()
        {
            if (_settingsUI != null)
            {
                _settingsUI.Dispose();
                _settingsUI = null;
            }
        }
    }
}