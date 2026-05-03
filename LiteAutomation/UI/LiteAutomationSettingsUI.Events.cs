using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using LiteAutomation.DTOs;
using LiteAutomation.Enums;
using LiteAutomation.Factories;
using LiteAutomation.Diagnostics;

namespace LiteAutomation.UI
{
    public partial class LiteAutomationSettingsUI
    {
        private void Axis_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isUpdatingRules) return;
            _isUpdatingRules = true;

            var platform = (AutomationPlatform)cmbPlatform.SelectedItem;

            if (sender == cmbPlatform)
            {
                if (platform == AutomationPlatform.Web)
                {
                    // 🚀 ESTRATÉGIAS ENXUTAS
                    cmbStrategy.DataSource = new[] { AutomationStrategy.Smart_Selector, AutomationStrategy.Accessibility_Audit };
                    cmbPattern.DataSource = new[] { DesignPattern.Linear, DesignPattern.Page_Object_Model, DesignPattern.BDD_Gherkin, DesignPattern.BDD_POM_Hybrid };
                    cmbFramework.DataSource = new[] { TestFramework.Playwright, TestFramework.Selenium, TestFramework.Cypress };
                }
                else
                {
                    cmbStrategy.DataSource = new[] { AutomationStrategy.Pure_SAP_GUI, AutomationStrategy.SAP_GUI_UIA, AutomationStrategy.Accessibility_Audit };
                    cmbPattern.DataSource = new[] { DesignPattern.Modular };
                    cmbFramework.DataSource = new[] { TestFramework.Tosca_Engine };
                    cmbLanguage.DataSource = new[] { ScriptLanguage.None_Raw_XML };
                }
            }

            if (sender == cmbFramework || sender == cmbPlatform)
            {
                if (cmbFramework.SelectedItem != null)
                {
                    var fw = (TestFramework)cmbFramework.SelectedItem;
                    if (fw == TestFramework.Cypress) cmbLanguage.DataSource = new[] { ScriptLanguage.JavaScript };
                    else if (fw == TestFramework.Selenium) cmbLanguage.DataSource = new[] { ScriptLanguage.CSharp, ScriptLanguage.Java };
                    else if (fw == TestFramework.Playwright) cmbLanguage.DataSource = new[] { ScriptLanguage.CSharp };
                }
            }

            bool isAudit = cmbStrategy.SelectedItem != null && (AutomationStrategy)cmbStrategy.SelectedItem == AutomationStrategy.Accessibility_Audit;
            cmbPattern.Enabled = !isAudit;
            cmbFramework.Enabled = !isAudit;
            cmbLanguage.Enabled = !isAudit;

            // 🚀 EXIBIR PAINEL DE BDD APENAS SE FOR BDD E OCULTAR REPORT NO AUDIT
            var currentPattern = cmbPattern.SelectedItem != null ? (DesignPattern)cmbPattern.SelectedItem : DesignPattern.Linear;
            pnlBddStyle.Visible = (currentPattern == DesignPattern.BDD_Gherkin || currentPattern == DesignPattern.BDD_POM_Hybrid);
            chkReport.Visible = !isAudit;

            _isUpdatingRules = false;
        }

        // Resto dos eventos continuam idênticos, vou repeti-los abreviados para focar no Apply
        private async void BtnImportJson_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "JSON Files|*.json", Title = "Abrir Fat Payload" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    codeEditor.Text = "⏳ Carregando...";
                    btnImportJson.Enabled = false;
                    try
                    {
                        string jsonContent = File.ReadAllText(ofd.FileName);
                        await Task.Run(() => {
                            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                            _workspace.LoadJson(JsonSerializer.Deserialize<List<MainStepDto>>(jsonContent, opts), _currentConfig);
                        });

                        lblCurrentFile.Text = $"Arquivo ativo: {Path.GetFileName(ofd.FileName)}";
                        lblCurrentFile.Visible = true; btnClearFile.Visible = true; btnExport.Enabled = true; _isProcessed = false;
                        codeEditor.Text = "✅ JSON importado com sucesso!\nAjuste os eixos e clique em 'Processar Filtros'.";
                    }
                    catch { codeEditor.Text = "Falha na importação do arquivo."; }
                    finally { btnImportJson.Enabled = true; }
                }
            }
        }

        private void BtnApplyFilter_Click(object sender, EventArgs e)
        {
            if (!_workspace.HasData) return;
            _isProcessed = true;

            // Popula as novas propriedades da Config!
            _currentConfig.IncludeReport = chkReport.Checked;
            _currentConfig.BddStyle = rdoCanonical.Checked ? BddStyle.Canonical : BddStyle.Narrative;

            PopulateDecisionGrid();
            AtualizarPreview();
        }

        private void BtnApplyLocators_Click(object sender, EventArgs e) { if (!_workspace.HasData || !_isProcessed) return; AtualizarPreview(); }
        private void BtnClearFile_Click(object sender, EventArgs e) { _workspace.Clear(); gridLocators.Rows.Clear(); _isProcessed = false; }
        private void BtnPinPanel_Click(object sender, EventArgs e) { splitContainer.IsSplitterFixed = !splitContainer.IsSplitterFixed; UpdatePinButtonVisuals(); }
        private void GridLocators_CurrentCellDirtyStateChanged(object sender, EventArgs e) { if (gridLocators.IsCurrentCellDirty) gridLocators.CommitEdit(DataGridViewDataErrorContexts.Commit); }
        private void GridLocators_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 2) return;
            string fullStepId = gridLocators.Rows[e.RowIndex].Cells[0].Value?.ToString();
            string newCodeSnippet = gridLocators.Rows[e.RowIndex].Cells[2].Value?.ToString();
            if (fullStepId != null && newCodeSnippet != null && _isProcessed)
            {
                _currentConfig.LocatorOverrides[fullStepId] = newCodeSnippet;
                gridLocators.Invalidate();
                _workspace.RebuildIntentCache(_currentConfig);
                AtualizarPreview();
            }
        }

        private void BtnExport_Click(object sender, EventArgs e)
        {
            if (!_workspace.HasData || !_isProcessed) return;
            using (SaveFileDialog sfd = new SaveFileDialog { Filter = "Código C#|*.cs|Todos os Arquivos|*.*", Title = "Salvar Código" })
                if (sfd.ShowDialog() == DialogResult.OK)
                    File.WriteAllText(sfd.FileName, CodeGeneratorFactory.Create(_currentConfig).GenerateCode(_workspace, _currentConfig, Path.GetFileNameWithoutExtension(sfd.FileName)));
        }
    }
}