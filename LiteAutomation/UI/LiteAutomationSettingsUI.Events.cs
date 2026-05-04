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
using LiteTools.Core.Languages;
using FastColoredTextBoxNS;

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
                    cmbStrategy.DataSource = new[] { AutomationStrategy.Smart_Selector, AutomationStrategy.Accessibility_Audit };
                    cmbPattern.DataSource = new[] { DesignPattern.Linear, DesignPattern.BDD_Gherkin, DesignPattern.BDD_POM_Hybrid };
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

            var currentPattern = cmbPattern.SelectedItem != null ? (DesignPattern)cmbPattern.SelectedItem : DesignPattern.Linear;
            pnlBddStyle.Visible = (currentPattern == DesignPattern.BDD_Gherkin || currentPattern == DesignPattern.BDD_POM_Hybrid);
            chkReport.Visible = !isAudit;

            _isUpdatingRules = false;
        }

        private async void BtnImportJson_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = LanguageManager.GetString("DialogFilterJson"), Title = LanguageManager.GetString("DialogTitleOpenJson") })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    codeEditor.Text = LanguageManager.GetString("MsgLoading");
                    btnImportJson.Enabled = false;
                    try
                    {
                        string jsonContent = File.ReadAllText(ofd.FileName);
                        await Task.Run(() => {
                            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                            _workspace.LoadJson(JsonSerializer.Deserialize<List<MainStepDto>>(jsonContent, opts), _currentConfig);
                        });

                        lblCurrentFile.Text = $"{LanguageManager.GetString("LblActiveFile")}{Path.GetFileName(ofd.FileName)}";
                        lblCurrentFile.Visible = true;
                        btnClearFile.Visible = true;
                        btnExport.Enabled = true;
                        _isProcessed = false;

                        codeEditor.ReadOnly = false;
                        codeEditor.Text = LanguageManager.GetString("MsgImportSuccess");
                        codeEditor.ReadOnly = true;
                    }
                    catch { codeEditor.Text = LanguageManager.GetString("MsgImportFailed"); }
                    finally { btnImportJson.Enabled = true; }
                }
            }
        }

        private void BtnApplyFilter_Click(object sender, EventArgs e)
        {
            if (!_workspace.HasData) return;
            _isProcessed = true;

            _currentConfig.IncludeReport = chkReport.Checked;
            _currentConfig.BddStyle = rdoCanonical.Checked ? BddStyle.Canonical : BddStyle.Narrative;

            PopulateDecisionGrid();
            AtualizarPreview();
        }

        // 🚀 O BOTÃO APLICAR AGORA ASSUME A RESPONSABILIDADE DO REBUILD
        private void BtnApplyLocators_Click(object sender, EventArgs e)
        {
            if (!_workspace.HasData || !_isProcessed) return;

            // Só quando o usuário aperta 'Aplicar' a gente recria as intenções matemáticas e desenha a tela
            _workspace.RebuildIntentCache(_currentConfig);
            AtualizarPreview();
        }

        private void BtnClearFile_Click(object sender, EventArgs e)
        {
            _workspace.Clear();
            gridLocators.Rows.Clear();
            _isProcessed = false;

            lblCurrentFile.Text = "";
            lblCurrentFile.Visible = false;
            btnClearFile.Visible = false;
            btnExport.Enabled = false;

            codeEditor.ReadOnly = false;
            codeEditor.Language = Language.Custom;
            codeEditor.Range.ClearStyle(StyleIndex.All);
            codeEditor.Text = LanguageManager.GetString("TxtCodeEditorInitial");
            codeEditor.ReadOnly = true;
        }

        private void BtnPinPanel_Click(object sender, EventArgs e) { splitContainer.IsSplitterFixed = !splitContainer.IsSplitterFixed; UpdatePinButtonVisuals(); }

        private void GridLocators_CurrentCellDirtyStateChanged(object sender, EventArgs e) { if (gridLocators.IsCurrentCellDirty) gridLocators.CommitEdit(DataGridViewDataErrorContexts.Commit); }

        // 🚀 O EVENTO DO GRID AGORA É SILENCIOSO
        private void GridLocators_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 2) return;
            string fullStepId = gridLocators.Rows[e.RowIndex].Cells[0].Value?.ToString();
            string newCodeSnippet = gridLocators.Rows[e.RowIndex].Cells[2].Value?.ToString();

            if (fullStepId != null && newCodeSnippet != null && _isProcessed)
            {
                // Apenas salva a intenção de mudança em memória (Batch update mode)
                _currentConfig.LocatorOverrides[fullStepId] = newCodeSnippet;
            }
        }

        private void BtnExport_Click(object sender, EventArgs e)
        {
            if (!_workspace.HasData || !_isProcessed) return;
            using (SaveFileDialog sfd = new SaveFileDialog { Filter = LanguageManager.GetString("DialogFilterCSharp"), Title = LanguageManager.GetString("DialogTitleSaveCode") })
                if (sfd.ShowDialog() == DialogResult.OK)
                    File.WriteAllText(sfd.FileName, CodeGeneratorFactory.Create(_currentConfig).GenerateCode(_workspace, _currentConfig, Path.GetFileNameWithoutExtension(sfd.FileName)));
        }
    }
}