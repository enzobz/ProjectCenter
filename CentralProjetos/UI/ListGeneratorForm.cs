using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DrawingCollector.Core;
using DrawingCollector.Core.Models;
using DrawingCollector.Logging;
using DrawingCollector.Reports;
using DrawingCollector.UI.Controls;
using DrawingCollector.UI.Extensions;
using DrawingCollector.UI.Theme;

namespace DrawingCollector.UI
{
    /// <summary>
    /// Módulo dedicado à geração das listas do projeto.
    /// Esta tela usa uma entrada própria de LISTA GERAL, não o painel genérico de códigos
    /// usado pelo coletor. Isso evita opções sem sentido como coluna de código, coluna REV etc.
    /// </summary>
    public class ListGeneratorForm : Form
    {
        private const int StepCardMinWidth = 260;

        private RadioButton rbPaste = null!;
        private RadioButton rbExcel = null!;
        private Panel pasteArea = null!;
        private Panel excelArea = null!;
        private TextBox txtPaste = null!;
        private TextBox txtExcelPath = null!;
        private ComboBox cmbSheets = null!;
        private TextBox txtOutput   = null!;
        private TextBox txtBaseName = null!;   // código do projeto (ex: LEP240928-P03-QG100-C01)
        private TextBox txtRevision = null!;   // revisão (ex: R00, R01)
        private RichTextBox rtbLog = null!;
        private Label lblPasteCount = null!;
        private Label lblStatus = null!;
        private Label lblOutputPreview = null!; // preview em tempo real dos nomes gerados
        private RoundedButton btnGenerate = null!;
        private RoundedButton btnOpenOutput = null!;
        private ILogger logger = null!;
        private readonly ToolTip pathToolTip = new() { ShowAlways = true, AutoPopDelay = 15000, InitialDelay = 150, ReshowDelay = 100 };

        private string lastOutputFolder = string.Empty;

        public ListGeneratorForm()
        {
            Text = "Gerar Listas";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1080, 740);
            MinimumSize = new Size(880, 620);
            Font = new Font("Segoe UI", 10f);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.FromArgb(244, 247, 252);

            string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.ico");
            Icon = File.Exists(icoPath) ? new Icon(icoPath) : SystemIcons.Application;

            BuildLayout();
            logger = new RichTextBoxLogger(rtbLog);
            UpdateInputMode();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                pathToolTip.Dispose();

            base.Dispose(disposing);
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                Padding = new Padding(24),
                BackColor = Color.FromArgb(244, 247, 252),
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 54f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 46f));
            Controls.Add(root);

            root.Controls.Add(new GradientHeader
            {
                Dock = DockStyle.Top,
                Height = 82,
                Title = "Gerar Listas",
                CornerRadius = 22,
                Margin = new Padding(0, 0, 0, 14),
            }, 0, 0);

            root.Controls.Add(BuildFlowHint(), 0, 1);
            root.Controls.Add(BuildInputCard(), 0, 2);
            root.Controls.Add(BuildOutputCard(), 0, 3);
            root.Controls.Add(BuildActionRow(), 0, 4);
            root.Controls.Add(BuildLogCard(), 0, 5);

            root.SetDoubleBuffered();
        }

        private Control BuildFlowHint()
        {
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 14),
                BackColor = Color.Transparent,
            };

            flow.Controls.Add(MakeStep("1", "Entrada", "Cole a lista completa ou selecione o Excel da Lista Geral."));
            flow.Controls.Add(MakeStep("2", "Pasta do projeto", "Escolha onde salvar Lista Geral, Fornecedor e Barramento."));
            flow.Controls.Add(MakeStep("3", "Gerar", "O sistema usa os templates reais e mantém as fórmulas."));
            return flow;
        }

        private Control MakeStep(string number, string title, string text)
        {
            var panel = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Color.White,
                Padding = new Padding(14, 10, 14, 10),
                Margin = new Padding(0, 0, 10, 10),
                MinimumSize = new Size(StepCardMinWidth, 0),
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            var badge = new CircleBadge(number, Palette.Primary)
            {
                Size = new Size(34, 34),
                Margin = new Padding(0, 2, 10, 0),
            };
            panel.Controls.Add(badge, 0, 0);
            panel.SetRowSpan(badge, 2);

            var lblTitle = new Label
            {
                AutoSize = true,
                Text = title,
                Font = new Font("Segoe UI Semibold", 10.5f),
                ForeColor = Color.FromArgb(30, 36, 52),
                Margin = new Padding(0, 0, 0, 2),
            };
            panel.Controls.Add(lblTitle, 1, 0);

            var lblText = new Label
            {
                AutoSize = true,
                Text = text,
                Font = new Font("Segoe UI", 8.9f),
                ForeColor = Color.FromArgb(90, 98, 116),
                MaximumSize = new Size(188, 0),
                Margin = new Padding(0),
            };
            panel.Controls.Add(lblText, 1, 1);
            return panel;
        }

        private Control BuildInputCard()
        {
            var card = MakeCard("1. Entrada da Lista Geral", "Esta etapa espera a lista completa do Solid Edge, com colunas como ITEM, QTDE., REFERÊNCIA, MATERIAL, LARGURA e COMPRIMENTO.");
            var body = (TableLayoutPanel)card.Tag!;

            var modes = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 0, 8),
            };

            rbPaste = new RadioButton
            {
                Text = "Colar lista do Solid Edge",
                Checked = true,
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 10f),
                ForeColor = Color.FromArgb(30, 36, 52),
                Margin = new Padding(0, 8, 28, 0),
            };
            rbPaste.CheckedChanged += (_, __) => UpdateInputMode();
            modes.Controls.Add(rbPaste);

            rbExcel = new RadioButton
            {
                Text = "Selecionar Excel da Lista Geral",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 10f),
                ForeColor = Color.FromArgb(30, 36, 52),
                Margin = new Padding(0, 8, 0, 0),
            };
            rbExcel.CheckedChanged += (_, __) => UpdateInputMode();
            modes.Controls.Add(rbExcel);

            body.Controls.Add(modes, 0, 0);

            var contentHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
            };

            pasteArea = BuildPasteArea();
            excelArea = BuildExcelArea();
            contentHost.Controls.Add(pasteArea);
            contentHost.Controls.Add(excelArea);

            body.Controls.Add(contentHost, 0, 1);
            return card;
        }

        private Panel BuildPasteArea()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
            };

            txtPaste = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9.6f),
                PlaceholderText = "Cole aqui a LISTA COMPLETA do Solid Edge.\r\n\r\nDica: copie também as linhas de cabeçalho do projeto e a linha com ITEM | QTDE. | REFERÊNCIA | MATERIAL...",
                Margin = new Padding(0),
            };
            txtPaste.TextChanged += (_, __) => UpdatePasteCount();
            panel.Controls.Add(txtPaste);

            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                BackColor = Color.White,
                Padding = new Padding(0, 7, 0, 0),
            };

            lblPasteCount = new Label
            {
                Dock = DockStyle.Left,
                AutoSize = false,
                Width = 340,
                Text = "Nenhuma linha colada",
                ForeColor = Palette.Warning,
                Font = new Font("Segoe UI", 9f),
            };
            footer.Controls.Add(lblPasteCount);

            var btnClear = new RoundedButton
            {
                Text = "Limpar",
                MinimumSize = new Size(84, 26),
                Height = 26,
                FillColor = Color.FromArgb(110, 118, 132),
                HoverFill = Color.FromArgb(124, 132, 148),
                PressedFill = Color.FromArgb(92, 100, 114),
                BorderColor = Color.FromArgb(110, 118, 132),
                Dock = DockStyle.Right,
            };
            btnClear.Click += (_, __) => txtPaste.Clear();
            footer.Controls.Add(btnClear);
            panel.Controls.Add(footer);

            return panel;
        }

        private Panel BuildExcelArea()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Visible = false,
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.White,
                Padding = new Padding(0, 10, 0, 0),
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.Controls.Add(layout);

            var fileRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 0, 12),
            };
            fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            txtExcelPath = new TextBox
            {
                Dock = DockStyle.Fill,
                PlaceholderText = "Selecione ou arraste aqui o Excel da Lista Geral (.xlsx)...",
            };
            ConfigurePathTextBox(txtExcelPath);
            txtExcelPath.TextChanged += (_, __) => OnExcelPathChanged();
            txtExcelPath.AllowDrop = true;
            txtExcelPath.DragEnter += OnDragEnter;
            txtExcelPath.DragDrop += (_, e) =>
            {
                if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                    txtExcelPath.Text = files[0];
            };
            fileRow.Controls.Add(txtExcelPath, 0, 0);

            var btnSelectExcel = new RoundedButton
            {
                Text = "Selecionar Excel",
                MinimumSize = new Size(150, 32),
                Margin = new Padding(10, 0, 0, 0),
            };
            btnSelectExcel.Click += (_, __) => SelectExcelFile();
            fileRow.Controls.Add(btnSelectExcel, 1, 0);
            layout.Controls.Add(fileRow, 0, 0);

            var sheetRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 0, 8),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
            };
            sheetRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            sheetRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            sheetRow.Controls.Add(new Label
            {
                Text = "Aba da lista:",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 9.7f),
                ForeColor = Color.FromArgb(30, 36, 52),
                Margin = new Padding(0, 6, 8, 0),
            }, 0, 0);

            cmbSheets = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Left,
                Width = 220,
                Margin = new Padding(0, 2, 0, 0),
            };
            sheetRow.Controls.Add(cmbSheets, 1, 0);
            layout.Controls.Add(sheetRow, 0, 1);

            var info = new Label
            {
                Text = "As colunas são detectadas pelo modelo padrão da Lista Geral.",
                AutoSize = true,
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(96, 104, 122),
                MaximumSize = new Size(520, 0),
                Margin = new Padding(0, 0, 0, 4),
            };
            layout.Controls.Add(info, 0, 2);

            var detail = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Text = "Não é necessário escolher coluna, REV. ou cabeçalho. Para Excel, o sistema lê a aba Lista geral no padrão do template: linha 3 = cabeçalho, linha 4 em diante = itens.",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(96, 104, 122),
                MaximumSize = new Size(700, 0),
                Margin = new Padding(0, 0, 0, 0),
            };
            layout.Controls.Add(detail, 0, 3);

            return panel;
        }

        private Control BuildOutputCard()
        {
            var card = MakeCard("2. Pasta e nome do projeto", "Defina a pasta de saída, o código do projeto e a revisão.");
            // Sem MinimumSize fixo — deixa o card crescer conforme o conteúdo (preview de 3 linhas)
            var body = (TableLayoutPanel)card.Tag!;

            // body precisa de 3 linhas AutoSize (pasta, código+rev, preview)
            body.RowStyles.Clear();
            body.RowCount = 3;
            body.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // pasta
            body.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // código + revisão
            body.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // preview

            // ── Linha 1: pasta de destino ──────────────────────────────
            var outputRow = new TableLayoutPanel
            {
                Dock        = DockStyle.Top,
                ColumnCount = 2,
                AutoSize    = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor   = Color.White,
                Margin      = new Padding(0, 0, 0, 8),
            };
            outputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            outputRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            txtOutput = new TextBox
            {
                Dock            = DockStyle.Fill,
                Height          = 32,
                PlaceholderText = "Selecione a pasta do projeto onde serão salvos os arquivos...",
            };
            ConfigurePathTextBox(txtOutput);
            outputRow.Controls.Add(txtOutput, 0, 0);

            var btnSelectOutput = new RoundedButton
            {
                Text        = "Selecionar Pasta",
                MinimumSize = new Size(150, 34),
                Height      = 34,
                Margin      = new Padding(10, 0, 0, 0),
            };
            btnSelectOutput.Click += (_, __) => SelectOutputFolder();
            outputRow.Controls.Add(btnSelectOutput, 1, 0);
            body.Controls.Add(outputRow, 0, 0);

            // ── Linha 2: código do projeto + revisão ───────────────────
            // Layout:  [Label "Código:"] [TextBox código — cresce] [Label "Rev.:"] [TextBox rev — fixo 60px]
            var codeRow = new TableLayoutPanel
            {
                Dock         = DockStyle.Top,
                ColumnCount  = 4,
                AutoSize     = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor    = Color.White,
                Margin       = new Padding(0, 0, 0, 6),
            };
            codeRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));          // label "Código:"
            codeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));     // txtBaseName (cresce)
            codeRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));          // label "Rev.:"
            codeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72f));     // txtRevision (fixo)

            codeRow.Controls.Add(MakeFieldLabel("Código:"), 0, 0);

            txtBaseName = new TextBox
            {
                Dock            = DockStyle.Fill,
                Height          = 32,
                PlaceholderText = "ex: LEP240928-P03-QG100-C01",
                Font            = new Font("Segoe UI", 9.5f),
                CharacterCasing = CharacterCasing.Upper,
            };
            txtBaseName.TextChanged += (_, __) => UpdateOutputPreview();
            codeRow.Controls.Add(txtBaseName, 1, 0);

            codeRow.Controls.Add(MakeFieldLabel("Rev.:"), 2, 0);

            txtRevision = new TextBox
            {
                Dock            = DockStyle.Fill,
                Height          = 32,
                Text            = "R00",
                Font            = new Font("Segoe UI", 9.5f),
                TextAlign       = HorizontalAlignment.Center,
                CharacterCasing = CharacterCasing.Upper,
                Margin          = new Padding(8, 0, 0, 0),
                MaxLength       = 5,
            };
            txtRevision.TextChanged += (_, __) => UpdateOutputPreview();
            codeRow.Controls.Add(txtRevision, 3, 0);

            body.Controls.Add(codeRow, 0, 1);

            // ── Linha 3: preview dos nomes gerados ─────────────────────
            lblOutputPreview = new Label
            {
                Dock      = DockStyle.Top,
                AutoSize  = true,
                Text      = BuildPreviewText("<CÓDIGO>", "R00"),
                Font      = new Font("Segoe UI", 8.8f),
                ForeColor = Color.FromArgb(96, 104, 122),
                Margin    = new Padding(0, 2, 0, 0),
            };
            body.Controls.Add(lblOutputPreview, 0, 2);

            return card;
        }

        /// <summary>Helper que cria um Label de rótulo de campo.</summary>
        private static Label MakeFieldLabel(string text) => new Label
        {
            Text      = text,
            AutoSize  = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Font      = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(60, 68, 82),
            Margin    = new Padding(0, 0, 8, 0),
            Padding   = new Padding(0, 6, 0, 0),
        };


        private Control BuildActionRow()
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                RowCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 0, 0, 14),
                BackColor = Color.FromArgb(244, 247, 252),
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            lblStatus = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Pronto para gerar as listas.",
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(96, 104, 122),
                Padding = new Padding(2, 0, 0, 0),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8),
            };
            row.Controls.Add(lblStatus, 0, 0);

            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = true,
                Margin = new Padding(0),
            };

            btnGenerate = new RoundedButton
            {
                Text = "Gerar Listas",
                MinimumSize = new Size(160, 42),
                FillColor = Palette.Accent,
            };
            btnGenerate.Click += (_, __) => GenerateLists();
            actions.Controls.Add(btnGenerate);

            btnOpenOutput = new RoundedButton
            {
                Text = "Abrir Saída",
                MinimumSize = new Size(132, 42),
                Enabled = false,
                FillColor = Color.FromArgb(72, 84, 116),
                HoverFill = Color.FromArgb(84, 98, 136),
                PressedFill = Color.FromArgb(56, 66, 92),
                BorderColor = Color.FromArgb(72, 84, 116),
                Margin = new Padding(8, 0, 0, 0),
            };
            btnOpenOutput.Click += (_, __) => OpenOutputFolder();
            actions.Controls.Add(btnOpenOutput);

            var btnBack = new RoundedButton
            {
                Text = "Voltar",
                MinimumSize = new Size(110, 42),
                FillColor = Color.FromArgb(110, 118, 132),
                HoverFill = Color.FromArgb(124, 132, 148),
                PressedFill = Color.FromArgb(92, 100, 114),
                BorderColor = Color.FromArgb(110, 118, 132),
                Margin = new Padding(8, 0, 0, 0),
            };
            btnBack.Click += (_, __) => Close();
            actions.Controls.Add(btnBack);

            row.Controls.Add(actions, 0, 1);
            return row;
        }

        private Control BuildLogCard()
        {
            var card = MakeCard("Log da geração", "Acompanhe aqui a leitura, separação por material e gravação dos templates.");
            var body = (TableLayoutPanel)card.Tag!;

            rtbLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Consolas", 9.5f),
                BackColor = Color.White,
                ForeColor = Color.FromArgb(35, 40, 50),
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical,
            };
            body.Controls.Add(rtbLog, 0, 0);
            return card;
        }

        private Control MakeCard(string title, string subtitle)
        {
            var outer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(16),
                Margin = new Padding(0, 0, 0, 14),
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                BackColor = Color.White,
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            layout.Controls.Add(new Label
            {
                Text = title,
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 10.8f),
                ForeColor = Color.FromArgb(30, 36, 52),
                Margin = new Padding(0, 0, 0, 2),
            }, 0, 0);

            layout.Controls.Add(new Label
            {
                Text = subtitle,
                AutoSize = true,
                Font = new Font("Segoe UI", 9.2f),
                ForeColor = Color.FromArgb(96, 104, 122),
                Margin = new Padding(0, 0, 0, 10),
            }, 0, 1);

            var body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                BackColor = Color.White,
            };
            body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.Controls.Add(body, 0, 2);

            outer.Controls.Add(layout);
            outer.Tag = body;
            return outer;
        }

        private void UpdateInputMode()
        {
            if (pasteArea == null || excelArea == null) return;
            pasteArea.Visible = rbPaste.Checked;
            excelArea.Visible = rbExcel.Checked;
            if (rbPaste.Checked)
                lblStatus.Text = "Modo colar: cole a lista completa do Solid Edge.";
            else
                lblStatus.Text = "Modo Excel: selecione a planilha da Lista Geral.";
        }

        private void ConfigurePathTextBox(TextBox textBox)
        {
            UpdateTooltip(textBox, textBox.Text);
            textBox.TextChanged -= OnPathTextChanged;
            textBox.TextChanged += OnPathTextChanged;
        }

        private void UpdateTooltip(Control control, string? text)
        {
            pathToolTip.SetToolTip(control, string.IsNullOrWhiteSpace(text) ? null : text.Trim());
        }

        private void OnPathTextChanged(object? sender, EventArgs e)
        {
            if (sender is TextBox textBox)
                UpdateTooltip(textBox, textBox.Text);
        }

        /// <summary>
        /// Atualiza o label de preview com os nomes exatos que serão gerados.
        /// </summary>
        private void UpdateOutputPreview()
        {
            if (lblOutputPreview == null) return;
            string code = SanitizeFileName(txtBaseName?.Text.Trim() ?? "");
            string rev  = SanitizeFileName(txtRevision?.Text.Trim() ?? "R00");
            lblOutputPreview.Text = BuildPreviewText(
                string.IsNullOrWhiteSpace(code) ? "<CÓDIGO>" : code,
                string.IsNullOrWhiteSpace(rev)  ? "R00"      : rev);
        }

        /// <summary>
        /// Monta o texto de preview com os três nomes de arquivo no padrão do cliente.
        /// </summary>
        private static string BuildPreviewText(string code, string rev) =>
            $"Saída:\n" +
            $"  {code}-LISTA_GERAL-CH-{rev}.xlsx\n" +
            $"  {code}-FORNECEDOR-CH-{rev}.xlsx\n" +
            $"  {code}-BA-{rev}.xlsx";

        /// <summary>Remove caracteres inválidos de nome de arquivo.</summary>
        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        }

        private void UpdatePasteCount()
        {
            int lines = txtPaste.Lines.Count(l => !string.IsNullOrWhiteSpace(l));
            lblPasteCount.Text = lines == 0 ? "Nenhuma linha colada" : $"{lines} linha{(lines == 1 ? "" : "s")} colada{(lines == 1 ? "" : "s")}";
            lblPasteCount.ForeColor = lines > 0 ? Palette.Success : Palette.Warning;
        }

        private void SelectExcelFile()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Selecionar Excel da Lista Geral",
                Filter = "Excel (*.xlsx)|*.xlsx|Todos os arquivos (*.*)|*.*",
            };

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                txtExcelPath.Text = dlg.FileName;
                if (string.IsNullOrWhiteSpace(txtOutput.Text))
                    txtOutput.Text = Path.GetDirectoryName(dlg.FileName) ?? string.Empty;
            }
        }

        private void OnExcelPathChanged()
        {
            cmbSheets.Items.Clear();
            string path = txtExcelPath.Text.Trim();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

            var reader = new GeneralListReader(logger ?? new NullLogger());
            string[] sheets = reader.GetSheetNames(path);
            foreach (string sheet in sheets)
                cmbSheets.Items.Add(sheet);

            if (cmbSheets.Items.Count > 0)
            {
                int idx = -1;
                for (int i = 0; i < cmbSheets.Items.Count; i++)
                {
                    if (string.Equals(cmbSheets.Items[i]?.ToString(), "Lista geral", StringComparison.OrdinalIgnoreCase))
                    {
                        idx = i;
                        break;
                    }
                }
                cmbSheets.SelectedIndex = idx >= 0 ? idx : 0;
            }

            // Sugere o código do projeto extraído do nome do arquivo
            // (só se o campo estiver vazio — não sobrescreve o que o usuário digitou)
            if (string.IsNullOrWhiteSpace(txtBaseName.Text))
            {
                txtBaseName.Text = ExtractProjectCode(Path.GetFileNameWithoutExtension(path));
                UpdateOutputPreview();
            }
        }

        /// <summary>
        /// Extrai o código do projeto do nome do arquivo, removendo sufixos conhecidos.
        /// Ex: "LEP240928-P03-QG100-C01-CH-R00" → "LEP240928-P03-QG100-C01"
        ///     "LEP240928-P03-QG100-C01-BA-R01" → "LEP240928-P03-QG100-C01"
        /// Se não reconhecer o padrão, devolve o nome inteiro.
        /// </summary>
        private static string ExtractProjectCode(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return string.Empty;

            // Remove sufixos conhecidos: -CH-RXX, -BA-RXX, -FORNECEDOR-CH-RXX, -LISTA_GERAL-CH-RXX
            var suffixes = new[]
            {
                @"-LISTA_GERAL-CH-R\d+",
                @"-FORNECEDOR-CH-R\d+",
                @"-CH-R\d+",
                @"-BA-R\d+",
            };

            string upper = fileName.ToUpperInvariant();
            foreach (var pattern in suffixes)
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    upper, pattern + "$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                    return fileName.Substring(0, match.Index);
            }

            return fileName; // não reconheceu o padrão, devolve o nome completo
        }

        private void SelectOutputFolder()
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "Selecione a pasta onde as listas serão salvas",
                UseDescriptionForTitle = true,
            };

            if (Directory.Exists(txtOutput.Text.Trim()))
                dlg.SelectedPath = txtOutput.Text.Trim();

            if (dlg.ShowDialog(this) == DialogResult.OK)
                txtOutput.Text = dlg.SelectedPath;
        }

        private void GenerateLists()
        {
            // ── Validações rápidas na UI thread (antes de lançar a Task) ──
            string outDir = txtOutput.Text.Trim();
            if (string.IsNullOrWhiteSpace(outDir))
            {
                logger.Err("ERRO: selecione a pasta do projeto/saída antes de gerar as listas.");
                return;
            }

            if (rbPaste.Checked && string.IsNullOrWhiteSpace(txtPaste.Text))
            {
                logger.Err("ERRO: cole a lista do Solid Edge antes de gerar.");
                return;
            }

            if (rbExcel.Checked && !File.Exists(txtExcelPath.Text.Trim()))
            {
                logger.Err("ERRO: selecione um arquivo Excel válido da Lista Geral.");
                return;
            }

            // Captura valores da UI antes de sair da UI thread
            bool isPaste        = rbPaste.Checked;
            string pasteText    = txtPaste.Text;
            string xlsxPath     = txtExcelPath.Text.Trim();
            string sheet        = cmbSheets.SelectedItem?.ToString() ?? "Lista geral";
            string userCode     = SanitizeFileName(txtBaseName.Text.Trim());
            string userRevision = SanitizeFileName(txtRevision.Text.Trim());
            if (string.IsNullOrWhiteSpace(userRevision)) userRevision = "R00";

            rtbLog.Clear();
            btnGenerate.Enabled   = false;
            btnOpenOutput.Enabled = false;
            lblStatus.Text        = "Gerando listas...";

            // ── Trabalho pesado em thread separada para não congelar a UI ──
            Task.Run(() =>
            {
                try
                {
                    IReadOnlyList<string[]>? projectHeaderRows = null;
                    string outputBaseName;
                    List<GeneralListItem> items;

                    string autoCode; // código de fallback se o campo estiver vazio

                    if (isPaste)
                    {
                        var reader = new PastedListReader(logger);
                        items = reader.LoadFromText(pasteText, "texto colado na tela");
                        projectHeaderRows = reader.ProjectHeaderRows;
                        autoCode = "LISTA_COLADA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(sheet)) sheet = "Lista geral";
                        items = new GeneralListReader(logger).LoadFromFile(xlsxPath, sheet, headerRow: 3);
                        // Extrai o código do projeto do nome do arquivo
                        // ex: "LEP240928-P03-QG100-C01-CH-R00" → "LEP240928-P03-QG100-C01"
                        autoCode = ExtractProjectCode(Path.GetFileNameWithoutExtension(xlsxPath));
                    }

                    // Usa o que o usuário digitou, ou o código extraído automaticamente
                    string code = string.IsNullOrWhiteSpace(userCode) ? autoCode : userCode;
                    string rev  = userRevision;
                    outputBaseName = code; // para o log

                    if (items.Count == 0)
                    {
                        logger.Warn("Nenhum item encontrado. Verifique se a lista contém o cabeçalho ITEM | QTDE. | REFERÊNCIA...");
                        return;
                    }

                    string generalTemplate  = FindModelTemplate("LEPXXXX-PXXXX-C0X-CH-R00.xlsx");
                    string supplierTemplate = FindModelTemplate("LEP2XXXX-LISTA GERAL-FORNECEDOR-CH-R00.xlsx");
                    string busbarTemplate   = FindModelTemplate("LEPXXXX-PXXXX-C0X-BA-R00.xlsx");

                    if (!File.Exists(generalTemplate) || !File.Exists(supplierTemplate) || !File.Exists(busbarTemplate))
                    {
                        logger.Err("ERRO: templates não encontrados. Verifique a pasta 'modelos' ao lado do executável.");
                        logger.Info("  Lista geral: " + generalTemplate);
                        logger.Info("  Fornecedor:  " + supplierTemplate);
                        logger.Info("  Barramento:  " + busbarTemplate);
                        return;
                    }

                    Directory.CreateDirectory(outDir);

                    var result = new ListSeparatorService(logger).Separate(items);
                    logger.Info($"Resumo: geral {result.Todos.Count} | fornecedor {result.Fornecedor.Count} | barramento {result.Barramento.Count} | outros {result.Outros.Count}.");

                    var output = new TemplateListReportWriter(logger).WriteFromTemplates(
                        result, generalTemplate, supplierTemplate, busbarTemplate,
                        outDir, code, projectHeaderRows, rev, projectTitle: code);

                    SafeUi(() =>
                    {
                        if (output.Success)
                        {
                            lastOutputFolder      = outDir;
                            btnOpenOutput.Enabled = true;
                            lblStatus.Text        = $"✓ {result.Todos.Count} itens  |  Fornecedor {result.Fornecedor.Count}  |  Barramento {result.Barramento.Count}";
                            logger.Ok("Listas geradas com sucesso.");
                            logger.Info("  Lista geral: " + output.GeneralPath);
                            logger.Info("  Fornecedor:  " + output.SupplierPath);
                            logger.Info("  Barramento:  " + output.BusbarPath);
                            try { Process.Start("explorer.exe", $"/select,\"{output.GeneralPath}\""); } catch { }
                        }
                        else
                        {
                            lblStatus.Text = "Geração terminou com erro — confira o log.";
                            logger.Warn("A geração terminou com erro. Confira o log acima.");
                        }
                    });
                }
                catch (Exception ex)
                {
                    logger.Err("ERRO ao gerar listas: " + ex.Message);
                    SafeUi(() => lblStatus.Text = "Erro ao gerar — confira o log.");
                }
                finally
                {
                    SafeUi(() => btnGenerate.Enabled = true);
                }
            });
        }

        /// <summary>Executa uma ação na UI thread de forma segura.</summary>
        private void SafeUi(Action action)
        {
            if (IsDisposed) return;
            try
            {
                if (InvokeRequired) BeginInvoke(action);
                else action();
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        private void OpenOutputFolder()
        {
            string p = string.IsNullOrWhiteSpace(lastOutputFolder) ? txtOutput.Text.Trim() : lastOutputFolder;
            if (Directory.Exists(p)) Process.Start("explorer.exe", p);
        }

        private static void OnDragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private static string FindModelTemplate(string fileName)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string current = Directory.GetCurrentDirectory();

            string[] candidates =
            {
                Path.Combine(baseDir, "modelos", fileName),
                Path.Combine(current, "modelos", fileName),
                Path.Combine(baseDir, fileName),
                Path.Combine(current, fileName),
            };

            foreach (string c in candidates)
                if (File.Exists(c)) return c;

            return candidates[0];
        }
    }
}
