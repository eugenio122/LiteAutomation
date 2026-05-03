using System;
using System.Windows.Forms;
using LiteAutomation.UI;
using LiteTools.Interfaces;

namespace LiteAutomation
{
    public class LiteAutomationPlugin : ILitePlugin
    {
        public string Name => "LiteAutomation Data Projector";
        public string Version => "1.0.0";

        private ILiteHostContext? _hostContext;
        private IEventBus? _eventBus;
        private string _language = "pt-PT";
        private LiteAutomationSettingsUI? _settingsUI;

        // 🚀 O Segredo: Guardar o estado do tema da Nave-Mãe! Assume-se Escuro como padrão moderno.
        private bool _isCurrentDark = true;

        public void Initialize(ILiteHostContext hostContext, IEventBus eventBus, string currentLanguage)
        {
            _hostContext = hostContext;
            _eventBus = eventBus;
            _language = currentLanguage;

            // Tenta descobrir o tema inicial, caso a Nave-Mãe o forneça nos metadados
            if (_hostContext.TryGetSessionMetadata<bool>("IsDarkMode", out bool isDark))
            {
                _isCurrentDark = isDark;
            }

            // Fica à escuta de mudanças futuras
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
                _settingsUI = new LiteAutomationSettingsUI(_language);

                // 🚀 INJEÇÃO IMEDIATA: Aplica o tema guardado no momento em que a UI nasce!
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