using System.Drawing;
using System.Windows.Forms;
using FastColoredTextBoxNS;

namespace LiteAutomation.UI
{
    public partial class LiteAutomationSettingsUI
    {
        // =====================================================================
        // 🎨 TEMA E RENDERIZAÇÃO (RECURSIVIDADE)
        // =====================================================================
        public void ApplyTheme(bool isDark)
        {
            _isDarkMode = isDark;
            this.BackColor = isDark ? Color.FromArgb(45, 45, 48) : Color.FromArgb(240, 240, 240);
            this.ForeColor = isDark ? Color.White : Color.Black;

            ApplyThemeToControls(this.Controls, isDark);
            UpdatePinButtonVisuals();

            if (!_isProcessed && codeEditor != null)
            {
                EditorConfigurator.ApplyLanguageTheme(codeEditor, _currentConfig.Language, _currentConfig.Pattern, _isDarkMode);
            }

            if (_workspace.HasData && _isProcessed)
            {
                AtualizarPreview();
            }
        }

        private void ApplyThemeToControls(Control.ControlCollection controls, bool isDark)
        {
            Color backColor = isDark ? Color.FromArgb(45, 45, 48) : Color.FromArgb(240, 240, 240);
            Color foreColor = isDark ? Color.White : Color.Black;
            Color comboBack = isDark ? Color.FromArgb(60, 60, 60) : Color.White;
            Color editorBack = isDark ? Color.FromArgb(30, 30, 30) : Color.White;

            foreach (Control ctrl in controls)
            {
                if (ctrl is Label || ctrl is CheckBox || ctrl is RadioButton)
                {
                    ctrl.ForeColor = foreColor;
                    ctrl.BackColor = Color.Transparent;
                }
                else if (ctrl is ComboBox cb)
                {
                    cb.BackColor = comboBack;
                    cb.ForeColor = foreColor;
                    cb.FlatStyle = FlatStyle.Flat;
                }
                else if (ctrl is Button btn)
                {
                    if (btn == btnClearFile)
                    {
                        btn.BackColor = Color.Transparent;
                        btn.ForeColor = Color.Red;
                    }
                    else if (btn == btnPinPanel)
                    {
                        // Gerido pela UpdatePinButtonVisuals
                    }
                    else if (btn == btnImportJson)
                    {
                        btn.BackColor = isDark ? Color.FromArgb(60, 60, 60) : Color.LightGray;
                        btn.ForeColor = foreColor;
                    }
                }
                else if (ctrl is Panel || ctrl is FlowLayoutPanel || ctrl is TableLayoutPanel || ctrl is SplitContainer || ctrl is SplitterPanel)
                {
                    ctrl.BackColor = backColor;
                    ApplyThemeToControls(ctrl.Controls, isDark);
                }
                else if (ctrl is DataGridView dgv)
                {
                    dgv.BackgroundColor = backColor;
                    dgv.GridColor = isDark ? Color.FromArgb(60, 60, 60) : Color.LightGray;
                    dgv.DefaultCellStyle.BackColor = editorBack;
                    dgv.DefaultCellStyle.ForeColor = foreColor;
                    dgv.ColumnHeadersDefaultCellStyle.BackColor = isDark ? Color.FromArgb(60, 60, 60) : Color.LightGray;
                    dgv.ColumnHeadersDefaultCellStyle.ForeColor = foreColor;
                }
                // FastColoredTextBox tem renderizador próprio (atualizado via EditorConfigurator)
            }
        }

        private void UpdatePinButtonVisuals()
        {
            if (splitContainer == null || btnPinPanel == null) return;

            if (splitContainer.IsSplitterFixed)
            {
                btnPinPanel.Text = "🔓 Desfixar";
                btnPinPanel.BackColor = Color.FromArgb(40, 167, 69);
                btnPinPanel.ForeColor = Color.White;
            }
            else
            {
                btnPinPanel.Text = "📌 Fixar";
                btnPinPanel.BackColor = _isDarkMode ? Color.FromArgb(60, 60, 60) : Color.LightGray;
                btnPinPanel.ForeColor = _isDarkMode ? Color.White : Color.Black;
            }
        }

        private void GridLocators_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (e.Control is ComboBox cb)
            {
                cb.FlatStyle = FlatStyle.Flat;
                cb.BackColor = _isDarkMode ? Color.FromArgb(60, 60, 60) : Color.White;
                cb.ForeColor = _isDarkMode ? Color.White : Color.Black;
            }
        }
    }
}