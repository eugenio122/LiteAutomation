using System;
using System.Drawing;
using System.Windows.Forms;
using FastColoredTextBoxNS;
using LiteAutomation.Enums;
using LiteTools.Core.Languages;

namespace LiteAutomation.UI
{
    public partial class LiteAutomationSettingsUI
    {
        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.Padding = new Padding(10);
            this.BackColor = Color.FromArgb(240, 240, 240);

            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // TOPO
            var topPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            btnImportJson = new Button { Text = LanguageManager.GetString("BtnImportJson"), Width = 220, Height = 40, FlatStyle = FlatStyle.Flat };
            btnImportJson.Click += BtnImportJson_Click;

            lblCurrentFile = new Label { Text = "", AutoSize = true, Margin = new Padding(15, 12, 0, 0), Font = new Font("Segoe UI", 9, FontStyle.Italic), Visible = false };
            btnClearFile = new Button { Text = "❌", Width = 30, Height = 30, Margin = new Padding(5, 5, 0, 0), FlatStyle = FlatStyle.Flat, ForeColor = Color.Red, Visible = false, Cursor = Cursors.Hand };
            btnClearFile.FlatAppearance.BorderSize = 0;
            btnClearFile.Click += BtnClearFile_Click;

            topPanel.Controls.Add(btnImportJson);
            topPanel.Controls.Add(lblCurrentFile);
            topPanel.Controls.Add(btnClearFile);

            // CENTRO (Eixos e Controles Extras)
            var matrixPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Padding = new Padding(0, 5, 0, 10) };
            cmbPlatform = CreateCombo(); cmbStrategy = CreateCombo(); cmbPattern = CreateCombo(); cmbFramework = CreateCombo(); cmbLanguage = CreateCombo();

            // Checkbox Report
            chkReport = new CheckBox { Text = LanguageManager.GetString("ChkReport"), AutoSize = true, Margin = new Padding(10, 25, 0, 0), Font = new Font("Segoe UI", 8, FontStyle.Bold) };

            // PAINEL BDD
            pnlBddStyle = new Panel { Width = 160, Height = 60, Margin = new Padding(10, 5, 0, 0), Visible = false };
            var lblBdd = new Label { Text = LanguageManager.GetString("LblBddStyle"), AutoSize = true, Location = new Point(0, 0), Font = new Font("Segoe UI", 8, FontStyle.Bold) };
            rdoCanonical = new RadioButton { Text = LanguageManager.GetString("RdoCanonical"), AutoSize = true, Location = new Point(0, 20), Checked = true, Font = new Font("Segoe UI", 8) };
            rdoNarrative = new RadioButton { Text = LanguageManager.GetString("RdoNarrative"), AutoSize = true, Location = new Point(80, 20), Font = new Font("Segoe UI", 8) };
            pnlBddStyle.Controls.Add(lblBdd); pnlBddStyle.Controls.Add(rdoCanonical); pnlBddStyle.Controls.Add(rdoNarrative);

            btnApplyFilter = new Button { Text = LanguageManager.GetString("BtnApplyFilter"), Width = 140, Height = 45, BackColor = Color.FromArgb(40, 167, 69), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Margin = new Padding(10, 18, 0, 0) };
            btnApplyFilter.Click += BtnApplyFilter_Click;

            btnExport = new Button { Text = LanguageManager.GetString("BtnExport"), Width = 180, Height = 45, BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Enabled = false, Margin = new Padding(10, 18, 0, 0) };
            btnExport.Click += BtnExport_Click;

            matrixPanel.Controls.Add(CreateLabeledControl(LanguageManager.GetString("LblPlatform"), cmbPlatform));
            matrixPanel.Controls.Add(CreateLabeledControl(LanguageManager.GetString("LblStrategy"), cmbStrategy));
            matrixPanel.Controls.Add(CreateLabeledControl(LanguageManager.GetString("LblPattern"), cmbPattern));
            matrixPanel.Controls.Add(CreateLabeledControl(LanguageManager.GetString("LblFramework"), cmbFramework));
            matrixPanel.Controls.Add(CreateLabeledControl(LanguageManager.GetString("LblLanguage"), cmbLanguage));
            matrixPanel.Controls.Add(chkReport);
            matrixPanel.Controls.Add(pnlBddStyle);
            matrixPanel.Controls.Add(btnApplyFilter);
            matrixPanel.Controls.Add(btnExport);

            // BAIXO (Grelha e FCTB)
            splitContainer = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, FixedPanel = FixedPanel.Panel1 };
            splitContainer.SplitterMoved += (s, e) => { if (!splitContainer.IsSplitterFixed && this.Visible) { _uiPrefs.SplitterDistance = splitContainer.SplitterDistance; SaveUIPreferences(); } };

            gridLocators = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoGenerateColumns = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, EditMode = DataGridViewEditMode.EditOnEnter, EnableHeadersVisualStyles = false };
            gridLocators.Columns.Add(new DataGridViewTextBoxColumn { Name = "Step", HeaderText = LanguageManager.GetString("GridColStep"), Width = 50, ReadOnly = true });
            gridLocators.Columns.Add(new DataGridViewTextBoxColumn { Name = "Action", HeaderText = LanguageManager.GetString("GridColAction"), Width = 70, ReadOnly = true });
            gridLocators.Columns.Add(new DataGridViewComboBoxColumn { Name = "Locator", HeaderText = LanguageManager.GetString("GridColLocator"), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FlatStyle = FlatStyle.Flat });

            gridLocators.CellValueChanged += GridLocators_CellValueChanged;
            gridLocators.CurrentCellDirtyStateChanged += GridLocators_CurrentCellDirtyStateChanged;

            var leftHeaderPanel = new Panel { Dock = DockStyle.Top, Height = 30, Padding = new Padding(0, 0, 0, 5) };
            leftHeaderPanel.Controls.Add(new Label { Text = LanguageManager.GetString("LblSdetPanel"), Dock = DockStyle.Left, AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft });
            var rightButtonsPanel = new Panel { Dock = DockStyle.Right, Width = 180 };

            btnApplyLocators = new Button { Text = LanguageManager.GetString("BtnApplyLocators"), Width = 90, Left = 0, Top = 0, Height = 25, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White };
            btnApplyLocators.FlatAppearance.BorderSize = 0; btnApplyLocators.Click += BtnApplyLocators_Click;

            btnPinPanel = new Button { Text = LanguageManager.GetString("BtnPinPanel"), Width = 80, Left = 100, Top = 0, Height = 25, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnPinPanel.FlatAppearance.BorderSize = 1; btnPinPanel.Click += BtnPinPanel_Click;

            rightButtonsPanel.Controls.Add(btnApplyLocators); rightButtonsPanel.Controls.Add(btnPinPanel);
            leftHeaderPanel.Controls.Add(rightButtonsPanel);

            var panelEsquerda = new Panel { Dock = DockStyle.Fill };
            panelEsquerda.Controls.Add(gridLocators); panelEsquerda.Controls.Add(leftHeaderPanel);

            codeEditor = new FastColoredTextBox { Dock = DockStyle.Fill, ReadOnly = true, Text = LanguageManager.GetString("TxtCodeEditorInitial") };
            EditorConfigurator.SetupBaseEditor(codeEditor);

            var panelDireita = new Panel { Dock = DockStyle.Fill };
            panelDireita.Controls.Add(codeEditor);
            panelDireita.Controls.Add(new Label { Text = LanguageManager.GetString("LblCodePreview"), Dock = DockStyle.Top, Font = new Font("Segoe UI", 9, FontStyle.Bold), Padding = new Padding(0, 0, 0, 5) });

            splitContainer.Panel1.Controls.Add(panelEsquerda);
            splitContainer.Panel2.Controls.Add(panelDireita);

            mainLayout.Controls.Add(topPanel, 0, 0); mainLayout.Controls.Add(matrixPanel, 0, 1); mainLayout.Controls.Add(splitContainer, 0, 2);
            this.Controls.Add(mainLayout);
        }

        private ComboBox CreateCombo()
        {
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140, Height = 30, FlatStyle = FlatStyle.Flat };
            combo.SelectedIndexChanged += Axis_SelectedIndexChanged;
            return combo;
        }

        private Panel CreateLabeledControl(string labelText, Control control)
        {
            var panel = new Panel { Width = 150, Height = 60 };
            panel.Controls.Add(new Label { Text = labelText, AutoSize = true, Location = new Point(0, 0), Font = new Font("Segoe UI", 8, FontStyle.Bold) });
            control.Location = new Point(0, 20); panel.Controls.Add(control);
            return panel;
        }

        private void InitializeMatrix()
        {
            cmbPlatform.DataSource = Enum.GetValues(typeof(AutomationPlatform));
        }
    }
}