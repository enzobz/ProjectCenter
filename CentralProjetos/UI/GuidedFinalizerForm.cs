using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
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
    /// Finalização guiada automática.
    /// Lê a Lista Geral, gera os três Excels e usa os códigos separados para
    /// buscar/copiar automaticamente os arquivos técnicos.
    /// </summary>
    public class GuidedFinalizerForm : Form
    {
        private const int StepCardMinWidth = 200;
        private const int ResponsiveBreakpoint = 1100;

        private RadioButton rbPaste = null!;
        private RadioButton rbExcel = null!;
        private Panel pasteArea = null!;
        private Panel excelArea = null!;
        private TextBox txtPaste = null!;
        private TextBox txtExcelPath = null!;
        private ComboBox cmbSheets = null!;
        private TextBox txtProjectFolder   = null!;
        private TextBox txtProjectCode     = null!;
        private TextBox txtProjectRevision = null!;
        private Label   lblProjectPreview  = null!;
        private TextBox txtBusbarDbPath = null!;
        private TextBox txtRevisionDbPath = null!;
        private ListBox lstRoots = null!;
        private RichTextBox rtbLog = null!;
        private ProgressBar progress = null!;
        private Label lblStatus = null!;
        private Label lblPasteCount = null!;
        private RoundedButton btnRun = null!;
        private RoundedButton btnOpenProject = null!;
        private CancellationTokenSource? cts;
        private ILogger logger = null!;
        private readonly ToolTip pathToolTip = new() { ShowAlways = true, AutoPopDelay = 15000, InitialDelay = 150, ReshowDelay = 100 };
        private TableLayoutPanel mainContentLayout = null!;
        private Control mainInputCard = null!;
        private Control mainSideCards = null!;
        private int lastRootTooltipIndex = -1;
        private string lastRootTooltipText = string.Empty;
        private string lastProjectFolder = string.Empty;

        public GuidedFinalizerForm()
        {
            Text = "Finalização Guiada";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1120, 780);
            MinimumSize = new Size(940, 680);
            Font = new Font("Segoe UI", 10f);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.FromArgb(244, 247, 252);

            string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.ico");
            Icon = File.Exists(icoPath) ? new Icon(icoPath) : SystemIcons.Application;

            BuildLayout();
            logger = new RichTextBoxLogger(rtbLog);
            LoadSavedSettings();
            UpdateInputMode();
            UpdateResponsiveLayout();
            Resize += (_, __) => UpdateResponsiveLayout();
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
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 62f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 38f));
            Controls.Add(root);

            root.Controls.Add(new GradientHeader
            {
                Dock = DockStyle.Top,
                Height = 82,
                Title = "Finalização Guiada",
                CornerRadius = 22,
                Margin = new Padding(0, 0, 0, 14),
            }, 0, 0);

            root.Controls.Add(BuildFlowHint(), 0, 1);
            root.Controls.Add(BuildMainContent(), 0, 2);
            root.Controls.Add(BuildActionRow(), 0, 3);
            root.Controls.Add(BuildLogCard(), 0, 4);

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

            flow.Controls.Add(MakeStep("1", "Entrada", "Cole ou selecione a Lista Geral do Solid Edge."));
            flow.Controls.Add(MakeStep("2", "Pasta do projeto", "Escolha onde criar listas e arquivos finais."));
            flow.Controls.Add(MakeStep("3", "Bancos", "Informe barramentos e, opcionalmente, revisões para gerar relatórios."));
            flow.Controls.Add(MakeStep("4", "Pastas de origem", "Adicione as pastas do servidor onde estão PDF, DWG, DXF e STP."));
            flow.Controls.Add(MakeStep("5", "Automático", "Gera listas e coleta Fornecedor + Barramento sem colar códigos de novo."));
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

            panel.Controls.Add(new Label
            {
                AutoSize = true,
                Text = title,
                Font = new Font("Segoe UI Semibold", 10.2f),
                ForeColor = Color.FromArgb(30, 36, 52),
                Margin = new Padding(0, 0, 0, 2),
            }, 1, 0);

            panel.Controls.Add(new Label
            {
                AutoSize = true,
                Text = text,
                Font = new Font("Segoe UI", 8.6f),
                ForeColor = Color.FromArgb(90, 98, 116),
                MaximumSize = new Size(128, 0),
                Margin = new Padding(0),
            }, 1, 1);
            return panel;
        }

        private Control BuildMainContent()
        {
            mainContentLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent,
            };
            mainContentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62f));
            mainContentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
            mainContentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            mainInputCard = BuildInputCard();
            mainSideCards = BuildSideCards();

            mainContentLayout.Controls.Add(mainInputCard, 0, 0);
            mainContentLayout.Controls.Add(mainSideCards, 1, 0);
            return mainContentLayout;
        }

        private Control BuildInputCard()
        {
            var card = MakeCard("1. Lista Geral", "Use a lista completa do Solid Edge. O programa vai separar Fornecedor e Barramento sozinho.");
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

            var host = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            pasteArea = BuildPasteArea();
            excelArea = BuildExcelArea();
            host.Controls.Add(pasteArea);
            host.Controls.Add(excelArea);
            body.Controls.Add(host, 0, 1);
            return card;
        }

        private Panel BuildPasteArea()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

            txtPaste = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9.4f),
                PlaceholderText = "Cole aqui a LISTA COMPLETA do Solid Edge.\r\n\r\nDica: copie também as linhas superiores do projeto e a linha com ITEM | QTDE. | REFERÊNCIA | MATERIAL...",
            };
            txtPaste.TextChanged += (_, __) => UpdatePasteCount();
            panel.Controls.Add(txtPaste);

            var footer = new Panel { Dock = DockStyle.Bottom, AutoSize = true, BackColor = Color.White, Padding = new Padding(0, 7, 0, 0) };
            lblPasteCount = new Label
            {
                Dock = DockStyle.Left,
                AutoSize = false,
                Width = 330,
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
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Visible = false };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.White,
                Padding = new Padding(0, 12, 0, 0),
            };
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
                AllowDrop = true,
            };
            ConfigurePathTextBox(txtExcelPath);
            txtExcelPath.TextChanged += (_, __) => OnExcelPathChanged();
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

            layout.Controls.Add(new Label
            {
                Text = "Padrão esperado: linha 3 = cabeçalho, linha 4 em diante = itens.",
                AutoSize = true,
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(96, 104, 122),
                MaximumSize = new Size(520, 0),
                Margin = new Padding(0, 4, 0, 0),
            }, 0, 2);
            return panel;
        }

        private Control BuildSideCards()
        {
            var side = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.Transparent,
                Margin = new Padding(14, 0, 0, 0),
            };
            side.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            side.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            side.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            side.Controls.Add(BuildProjectFolderCard(), 0, 0);
            side.Controls.Add(BuildBusbarDatabaseCard(), 0, 1);
            side.Controls.Add(BuildRootsCard(), 0, 2);
            return side;
        }

        private Control BuildProjectFolderCard()
        {
            var card = MakeCard("2. Pasta do projeto", "A finalização criará subpastas para listas, fornecedor e barramento.");
            var body = (TableLayoutPanel)card.Tag!;

            // body: 3 linhas AutoSize (pasta, código+rev, preview)
            body.RowStyles.Clear();
            body.RowCount = 3;
            body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            body.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // ── Linha 1: pasta de destino ──────────────────────────────
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 0, 8),
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            txtProjectFolder = new TextBox
            {
                Dock = DockStyle.Fill,
                Height = 32,
                PlaceholderText = "Selecione a pasta do projeto...",
            };
            ConfigurePathTextBox(txtProjectFolder);
            row.Controls.Add(txtProjectFolder, 0, 0);

            var btnSelect = new RoundedButton
            {
                Text = "Selecionar",
                MinimumSize = new Size(110, 34),
                Height = 34,
                Margin = new Padding(8, 0, 0, 0),
            };
            btnSelect.Click += (_, __) => SelectProjectFolder();
            row.Controls.Add(btnSelect, 1, 0);
            body.Controls.Add(row, 0, 0);

            // ── Linha 2: código do projeto + revisão ───────────────────
            var codeRow = new TableLayoutPanel
            {
                Dock         = DockStyle.Top,
                ColumnCount  = 4,
                AutoSize     = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor    = Color.White,
                Margin       = new Padding(0, 0, 0, 6),
            };
            codeRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            codeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            codeRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            codeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72f));

            codeRow.Controls.Add(new Label
            {
                Text      = "Código:",
                AutoSize  = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Font      = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(60, 68, 82),
                Margin    = new Padding(0, 0, 8, 0),
                Padding   = new Padding(0, 6, 0, 0),
            }, 0, 0);

            txtProjectCode = new TextBox
            {
                Dock            = DockStyle.Fill,
                Height          = 32,
                PlaceholderText = "ex: LEP240928-P03-QG100-C01",
                Font            = new Font("Segoe UI", 9.5f),
                CharacterCasing = CharacterCasing.Upper,
            };
            // Sugere o código extraído do nome do arquivo Excel quando selecionado
            txtProjectCode.TextChanged += (_, __) => UpdateProjectPreview();
            codeRow.Controls.Add(txtProjectCode, 1, 0);

            codeRow.Controls.Add(new Label
            {
                Text      = "Rev.:",
                AutoSize  = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Font      = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(60, 68, 82),
                Margin    = new Padding(8, 0, 4, 0),
                Padding   = new Padding(0, 6, 0, 0),
            }, 2, 0);

            txtProjectRevision = new TextBox
            {
                Dock            = DockStyle.Fill,
                Height          = 32,
                Text            = "R00",
                Font            = new Font("Segoe UI", 9.5f),
                TextAlign       = HorizontalAlignment.Center,
                CharacterCasing = CharacterCasing.Upper,
                Margin          = new Padding(0, 0, 0, 0),
                MaxLength       = 5,
            };
            txtProjectRevision.TextChanged += (_, __) => UpdateProjectPreview();
            codeRow.Controls.Add(txtProjectRevision, 3, 0);
            body.Controls.Add(codeRow, 0, 1);

            // ── Linha 3: preview dos nomes ─────────────────────────────
            lblProjectPreview = new Label
            {
                Dock      = DockStyle.Top,
                AutoSize  = true,
                Text      = BuildProjectPreviewText("<CÓDIGO>", "R00"),
                Font      = new Font("Segoe UI", 8.8f),
                ForeColor = Color.FromArgb(96, 104, 122),
                Margin    = new Padding(0, 2, 0, 0),
            };
            body.Controls.Add(lblProjectPreview, 0, 2);

            return card;
        }


        private Control BuildBusbarDatabaseCard()
        {
            var card = MakeCard("3. Bancos de controle", "Barramento aplica TemDobra; revisões geram relatório de divergências.");
            var body = (TableLayoutPanel)card.Tag!;

            var stack = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.White,
            };
            stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            stack.Controls.Add(new Label
            {
                Text = "Banco de barramento (obrigatório)",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 9.4f),
                ForeColor = Color.FromArgb(30, 36, 52),
                Margin = new Padding(0, 0, 0, 4),
            }, 0, 0);

            var busbarRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 0, 6),
            };
            busbarRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            busbarRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            txtBusbarDbPath = new TextBox
            {
                Dock = DockStyle.Fill,
                Height = 32,
                PlaceholderText = "Excel: Codigo | TemDobra...",
            };
            ConfigurePathTextBox(txtBusbarDbPath);
            busbarRow.Controls.Add(txtBusbarDbPath, 0, 0);

            var btnSelectBusbar = new RoundedButton
            {
                Text = "Selecionar",
                MinimumSize = new Size(110, 34),
                Height = 34,
                Margin = new Padding(8, 0, 0, 0),
            };
            btnSelectBusbar.Click += (_, __) => SelectBusbarDatabase();
            busbarRow.Controls.Add(btnSelectBusbar, 1, 0);
            stack.Controls.Add(busbarRow, 0, 1);

            var busbarActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 12),
                BackColor = Color.White,
            };

            var btnBusbarTemplate = new RoundedButton
            {
                Text = "Gerar template barramento",
                MinimumSize = new Size(176, 30),
                Height = 30,
                FillColor = Color.FromArgb(72, 84, 116),
                HoverFill = Color.FromArgb(84, 98, 136),
                PressedFill = Color.FromArgb(56, 66, 92),
                BorderColor = Color.FromArgb(72, 84, 116),
            };
            btnBusbarTemplate.Click += (_, __) => GenerateBusbarTemplate();
            busbarActions.Controls.Add(btnBusbarTemplate);

            var busbarHint = new Label
            {
                AutoSize = true,
                Text = "Sim = exige .dxf + .stp | Não = dispensa .stp",
                ForeColor = Color.FromArgb(96, 104, 122),
                Font = new Font("Segoe UI", 8.5f),
                Margin = new Padding(8, 6, 0, 0),
            };
            busbarActions.Controls.Add(busbarHint);
            stack.Controls.Add(busbarActions, 0, 2);

            stack.Controls.Add(new Label
            {
                Text = "Banco de revisões (opcional)",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 9.4f),
                ForeColor = Color.FromArgb(30, 36, 52),
                Margin = new Padding(0, 0, 0, 4),
            }, 0, 3);

            var revisionRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.White,
            };
            revisionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            revisionRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            txtRevisionDbPath = new TextBox
            {
                Dock = DockStyle.Fill,
                Height = 32,
                PlaceholderText = "Excel: Codigo | Revisao...",
            };
            ConfigurePathTextBox(txtRevisionDbPath);
            revisionRow.Controls.Add(txtRevisionDbPath, 0, 0);

            var btnSelectRevision = new RoundedButton
            {
                Text = "Selecionar",
                MinimumSize = new Size(110, 34),
                Height = 34,
                Margin = new Padding(8, 0, 0, 0),
                FillColor = Color.FromArgb(72, 84, 116),
                HoverFill = Color.FromArgb(84, 98, 136),
                PressedFill = Color.FromArgb(56, 66, 92),
                BorderColor = Color.FromArgb(72, 84, 116),
            };
            btnSelectRevision.Click += (_, __) => SelectRevisionDatabase();
            revisionRow.Controls.Add(btnSelectRevision, 1, 0);
            stack.Controls.Add(revisionRow, 0, 4);

            var revisionActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 6, 0, 0),
                BackColor = Color.White,
            };
            var btnRevisionTemplate = new RoundedButton
            {
                Text = "Gerar template revisão",
                MinimumSize = new Size(158, 30),
                Height = 30,
                FillColor = Color.FromArgb(110, 118, 132),
                HoverFill = Color.FromArgb(124, 132, 148),
                PressedFill = Color.FromArgb(92, 100, 114),
                BorderColor = Color.FromArgb(110, 118, 132),
            };
            btnRevisionTemplate.Click += (_, __) => GenerateRevisionTemplate();
            revisionActions.Controls.Add(btnRevisionTemplate);

            var revisionHint = new Label
            {
                AutoSize = true,
                Text = "Compara REV. da lista com a revisão oficial.",
                ForeColor = Color.FromArgb(96, 104, 122),
                Font = new Font("Segoe UI", 8.5f),
                Margin = new Padding(8, 6, 0, 0),
            };
            revisionActions.Controls.Add(revisionHint);
            stack.Controls.Add(revisionActions, 0, 5);

            body.Controls.Add(stack, 0, 0);
            return card;
        }

        private Control BuildRootsCard()
        {
            var card = MakeCard("4. Pastas de origem dos arquivos", "Adicione as pastas do servidor onde o sistema deve procurar PDF, DWG, DXF e STP.");
            var body = (TableLayoutPanel)card.Tag!;

            lstRoots = new ListBox
            {
                Dock = DockStyle.Fill,
                IntegralHeight = false,
                Font = new Font("Segoe UI", 9.2f),
                HorizontalScrollbar = true,
            };
            lstRoots.MouseMove += (_, e) => UpdateRootsTooltip(lstRoots.IndexFromPoint(e.Location));
            body.Controls.Add(lstRoots, 0, 0);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 10, 0, 0),
                BackColor = Color.White,
            };

            var btnAdd = new RoundedButton
            {
                Text = "Adicionar Pasta",
                MinimumSize = new Size(138, 34),
            };
            btnAdd.Click += (_, __) => AddRootFolder();
            buttons.Controls.Add(btnAdd);

            var btnRemove = new RoundedButton
            {
                Text = "Remover",
                MinimumSize = new Size(96, 34),
                FillColor = Color.FromArgb(110, 118, 132),
                HoverFill = Color.FromArgb(124, 132, 148),
                PressedFill = Color.FromArgb(92, 100, 114),
                BorderColor = Color.FromArgb(110, 118, 132),
                Margin = new Padding(8, 0, 0, 0),
            };
            btnRemove.Click += (_, __) => RemoveRootFolder();
            buttons.Controls.Add(btnRemove);

            body.Controls.Add(buttons, 0, 1);
            return card;
        }

        private Control BuildActionRow()
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                RowCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 14, 0, 14),
                BackColor = Color.FromArgb(244, 247, 252),
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            var left = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 0, 0, 8),
            };
            lblStatus = new Label
            {
                Dock = DockStyle.Top,
                Text = "Pronto para finalizar: gerar listas e buscar arquivos automaticamente.",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(96, 104, 122),
                AutoSize = true,
            };
            progress = new ProgressBar { Dock = DockStyle.Top, Height = 8, Style = ProgressBarStyle.Continuous, Maximum = 100 };
            left.Controls.Add(lblStatus, 0, 0);
            left.Controls.Add(progress, 0, 1);
            row.Controls.Add(left, 0, 0);

            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = true,
            };

            btnRun = new RoundedButton
            {
                Text = "Finalizar Automaticamente",
                MinimumSize = new Size(220, 42),
                FillColor = Palette.Accent,
            };
            btnRun.Click += (_, __) => StartAutomaticFinalization();
            actions.Controls.Add(btnRun);

            btnOpenProject = new RoundedButton
            {
                Text = "Abrir Projeto",
                MinimumSize = new Size(132, 42),
                Enabled = false,
                FillColor = Color.FromArgb(72, 84, 116),
                HoverFill = Color.FromArgb(84, 98, 136),
                PressedFill = Color.FromArgb(56, 66, 92),
                BorderColor = Color.FromArgb(72, 84, 116),
                Margin = new Padding(8, 0, 0, 0),
            };
            btnOpenProject.Click += (_, __) => OpenProjectFolder();
            actions.Controls.Add(btnOpenProject);

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
            var card = MakeCard("Log da finalização", "Acompanhe a geração das listas, a indexação das pastas e a cópia dos arquivos.");
            var body = (TableLayoutPanel)card.Tag!;

            rtbLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Consolas", 9.2f),
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

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, BackColor = Color.White };
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

            // body: agora cresce conforme conteúdo. Inicialmente 1 linha AutoSize;
            // os Build*Card podem reconfigurar RowStyles conforme precisarem.
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
            lblStatus.Text = rbPaste.Checked
                ? "Modo colar: cole a lista completa do Solid Edge."
                : "Modo Excel: selecione a planilha da Lista Geral.";
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
                if (string.IsNullOrWhiteSpace(txtProjectFolder.Text))
                    txtProjectFolder.Text = Path.GetDirectoryName(dlg.FileName) ?? string.Empty;
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
        }

        private void UpdateProjectPreview()
        {
            if (lblProjectPreview == null) return;
            string code = SanitizeForFileName(txtProjectCode?.Text.Trim() ?? "");
            string rev  = SanitizeForFileName(txtProjectRevision?.Text.Trim() ?? "R00");
            lblProjectPreview.Text = BuildProjectPreviewText(
                string.IsNullOrWhiteSpace(code) ? "<CÓDIGO>" : code,
                string.IsNullOrWhiteSpace(rev)  ? "R00"      : rev);
        }

        private static string BuildProjectPreviewText(string code, string rev) =>
            $"Saída:\n" +
            $"  {code}-LISTA_GERAL-CH-{rev}.xlsx\n" +
            $"  {code}-FORNECEDOR-CH-{rev}.xlsx\n" +
            $"  {code}-BA-{rev}.xlsx";

        private static string SanitizeForFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        }

        private void SelectProjectFolder()
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "Selecione a pasta do projeto",
                UseDescriptionForTitle = true,
            };
            if (Directory.Exists(txtProjectFolder.Text.Trim()))
                dlg.SelectedPath = txtProjectFolder.Text.Trim();
            if (dlg.ShowDialog(this) == DialogResult.OK)
                txtProjectFolder.Text = dlg.SelectedPath;
        }

        private void AddRootFolder()
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "Selecione uma pasta de origem no servidor",
                UseDescriptionForTitle = true,
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            string picked = dlg.SelectedPath.Trim();
            foreach (var item in lstRoots.Items)
                if (string.Equals(item?.ToString(), picked, StringComparison.OrdinalIgnoreCase))
                    return;
            lstRoots.Items.Add(picked);
            UpdateRootsHorizontalExtent();
        }

        private void RemoveRootFolder()
        {
            int idx = lstRoots.SelectedIndex;
            if (idx >= 0) lstRoots.Items.RemoveAt(idx);
            UpdateRootsHorizontalExtent();
        }


        private void SelectBusbarDatabase()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Selecionar banco de barramento",
                Filter = "Excel (*.xlsx)|*.xlsx|Todos os arquivos (*.*)|*.*",
                CheckFileExists = true,
            };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                txtBusbarDbPath.Text = dlg.FileName;
                SaveBusbarSetting(dlg.FileName);
            }
        }

        private void GenerateBusbarTemplate()
        {
            using var dlg = new SaveFileDialog
            {
                Title = "Salvar template do banco de barramento",
                Filter = "Excel (*.xlsx)|*.xlsx",
                FileName = "banco_barramentos_template.xlsx",
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                BusbarRepository.GenerateTemplate(dlg.FileName);
                txtBusbarDbPath.Text = dlg.FileName;
                SaveBusbarSetting(dlg.FileName);
                logger.Ok("Template do banco de barramento gerado: " + dlg.FileName);
            }
            catch (Exception ex)
            {
                logger.Err("Erro ao gerar template do banco de barramento: " + ex.Message);
            }
        }


        private void SelectRevisionDatabase()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Selecionar banco de revisões",
                Filter = "Excel (*.xlsx)|*.xlsx|Todos os arquivos (*.*)|*.*",
                CheckFileExists = true,
            };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                txtRevisionDbPath.Text = dlg.FileName;
                SaveRevisionSetting(dlg.FileName);
            }
        }

        private void GenerateRevisionTemplate()
        {
            using var dlg = new SaveFileDialog
            {
                Title = "Salvar template do banco de revisões",
                Filter = "Excel (*.xlsx)|*.xlsx",
                FileName = "banco_revisoes_template.xlsx",
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                RevisionRepository.GenerateTemplate(dlg.FileName);
                txtRevisionDbPath.Text = dlg.FileName;
                SaveRevisionSetting(dlg.FileName);
                logger.Ok("Template do banco de revisões gerado: " + dlg.FileName);
            }
            catch (Exception ex)
            {
                logger.Err("Erro ao gerar template do banco de revisões: " + ex.Message);
            }
        }

        private void LoadSavedSettings()
        {
            try
            {
                var settings = SettingsManager.Load();
                if (!string.IsNullOrWhiteSpace(settings.BusbarDatabasePath))
                    txtBusbarDbPath.Text = settings.BusbarDatabasePath;
                if (!string.IsNullOrWhiteSpace(settings.RevisionDatabasePath))
                    txtRevisionDbPath.Text = settings.RevisionDatabasePath;
            }
            catch
            {
                // Configuração corrompida ou inacessível não deve impedir a abertura da tela.
            }
        }

        private static void SaveBusbarSetting(string path)
        {
            try
            {
                var settings = SettingsManager.Load();
                settings.BusbarDatabasePath = path;
                SettingsManager.Save(settings);
            }
            catch
            {
                // Não bloqueia a execução se não conseguir salvar preferência local.
            }
        }


        private static void SaveRevisionSetting(string path)
        {
            try
            {
                var settings = SettingsManager.Load();
                settings.RevisionDatabasePath = path;
                SettingsManager.Save(settings);
            }
            catch
            {
                // Não bloqueia a execução se não conseguir salvar preferência local.
            }
        }

        private void StartAutomaticFinalization()
        {
            if (cts != null)
            {
                MessageBox.Show(this, "Já existe uma finalização em execução.", "Aguarde", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string projectFolder = txtProjectFolder.Text.Trim();
            string[] roots = lstRoots.Items.Cast<string>()
                .Select(x => (x ?? "").Trim())
                .Where(x => x.Length > 0)
                .ToArray();
            string busbarDbPath = txtBusbarDbPath.Text.Trim();
            string revisionDbPath = txtRevisionDbPath.Text.Trim();

            if (string.IsNullOrWhiteSpace(projectFolder))
            {
                logger.Err("ERRO: selecione a pasta do projeto.");
                return;
            }
            if (roots.Length == 0)
            {
                logger.Err("ERRO: adicione pelo menos uma pasta de origem dos arquivos.");
                return;
            }
            if (string.IsNullOrWhiteSpace(busbarDbPath) || !File.Exists(busbarDbPath))
            {
                logger.Err("ERRO: selecione o banco de barramento (.xlsx) para aplicar a regra TemDobra.");
                return;
            }

            string pasted     = txtPaste.Text;
            string excelPath  = txtExcelPath.Text.Trim();
            string sheetName  = cmbSheets.SelectedItem?.ToString() ?? "Lista geral";
            bool   usePaste   = rbPaste.Checked;
            string userCode   = SanitizeForFileName(txtProjectCode?.Text.Trim() ?? "");
            string userRevision = SanitizeForFileName(txtProjectRevision?.Text.Trim() ?? "R00");
            if (string.IsNullOrWhiteSpace(userRevision)) userRevision = "R00";

            if (usePaste && string.IsNullOrWhiteSpace(pasted))
            {
                logger.Err("ERRO: cole a lista completa ou altere para modo Excel.");
                return;
            }
            if (!usePaste && !File.Exists(excelPath))
            {
                logger.Err("ERRO: selecione um Excel válido da Lista Geral.");
                return;
            }

            SaveBusbarSetting(busbarDbPath);
            if (!string.IsNullOrWhiteSpace(revisionDbPath))
                SaveRevisionSetting(revisionDbPath);

            cts = new CancellationTokenSource();
            var token = cts.Token;
            btnRun.Enabled = false;
            btnOpenProject.Enabled = false;
            progress.Value = 0;
            rtbLog.Clear();
            lblStatus.Text = "Finalizando projeto...";

            Task.Run(() =>
            {
                try
                {
                    RunAutomaticFinalization(usePaste, pasted, excelPath, sheetName, projectFolder, roots, busbarDbPath, revisionDbPath, userCode, userRevision, token);
                    SafeUi(() =>
                    {
                        lblStatus.Text = "Finalização concluída.";
                        btnOpenProject.Enabled = true;
                        progress.Value = 100;
                    });
                }
                catch (OperationCanceledException)
                {
                    logger.Warn("Finalização cancelada.");
                }
                catch (Exception ex)
                {
                    logger.Err("ERRO inesperado na finalização: " + ex.Message);
                }
                finally
                {
                    SafeUi(() => btnRun.Enabled = true);
                    cts?.Dispose();
                    cts = null;
                }
            }, token);
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

        private void UpdateRootsHorizontalExtent()
        {
            if (lstRoots == null) return;

            int maxWidth = 0;
            foreach (var item in lstRoots.Items)
            {
                string text = item?.ToString() ?? string.Empty;
                maxWidth = Math.Max(maxWidth, TextRenderer.MeasureText(text, lstRoots.Font).Width);
            }

            lstRoots.HorizontalExtent = maxWidth + 24;
        }

        private void UpdateRootsTooltip(int index)
        {
            if (lstRoots == null) return;
            if (index == lastRootTooltipIndex) return;

            string tooltipText = index >= 0 && index < lstRoots.Items.Count
                ? lstRoots.Items[index]?.ToString() ?? string.Empty
                : string.Empty;

            if (string.Equals(lastRootTooltipText, tooltipText, StringComparison.Ordinal))
            {
                lastRootTooltipIndex = index;
                return;
            }

            lastRootTooltipIndex = index;
            lastRootTooltipText = tooltipText;
            UpdateTooltip(lstRoots, tooltipText);
        }

        private void UpdateResponsiveLayout()
        {
            if (mainContentLayout == null || mainInputCard == null || mainSideCards == null)
                return;

            bool stacked = ClientSize.Width < ResponsiveBreakpoint;

            mainContentLayout.SuspendLayout();
            mainContentLayout.Controls.Clear();
            mainContentLayout.ColumnStyles.Clear();
            mainContentLayout.RowStyles.Clear();

            if (stacked)
            {
                mainContentLayout.ColumnCount = 1;
                mainContentLayout.RowCount = 2;
                mainContentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
                mainContentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 56f));
                mainContentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 44f));
                mainSideCards.Margin = new Padding(0);
                mainContentLayout.Controls.Add(mainInputCard, 0, 0);
                mainContentLayout.Controls.Add(mainSideCards, 0, 1);
            }
            else
            {
                mainContentLayout.ColumnCount = 2;
                mainContentLayout.RowCount = 1;
                mainContentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62f));
                mainContentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
                mainContentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                mainSideCards.Margin = new Padding(14, 0, 0, 0);
                mainContentLayout.Controls.Add(mainInputCard, 0, 0);
                mainContentLayout.Controls.Add(mainSideCards, 1, 0);
            }

            mainContentLayout.ResumeLayout(true);
        }

        private void OnPathTextChanged(object? sender, EventArgs e)
        {
            if (sender is TextBox textBox)
                UpdateTooltip(textBox, textBox.Text);
        }

        private void RunAutomaticFinalization(
            bool usePaste,
            string pasted,
            string excelPath,
            string sheetName,
            string projectFolder,
            string[] roots,
            string busbarDbPath,
            string revisionDbPath,
            string userCode,
            string userRevision,
            CancellationToken token)
        {
            Directory.CreateDirectory(projectFolder);
            string listsDir = Path.Combine(projectFolder, "01_LISTAS");
            string supplierFilesDir = Path.Combine(projectFolder, "02_FORNECEDOR_ARQUIVOS");
            string busbarFilesDir = Path.Combine(projectFolder, "03_BARRAMENTO_ARQUIVOS");
            string reportsDir = Path.Combine(projectFolder, "04_RELATORIOS");
            string supplierReportsDir = Path.Combine(reportsDir, "01_FORNECEDOR");
            string busbarReportsDir = Path.Combine(reportsDir, "02_BARRAMENTO");
            Directory.CreateDirectory(listsDir);
            Directory.CreateDirectory(supplierFilesDir);
            Directory.CreateDirectory(busbarFilesDir);
            Directory.CreateDirectory(reportsDir);
            Directory.CreateDirectory(supplierReportsDir);
            Directory.CreateDirectory(busbarReportsDir);
            lastProjectFolder = projectFolder;

            token.ThrowIfCancellationRequested();
            SetProgress(5);
            logger.Info("1/4 Lendo Lista Geral...");

            IReadOnlyList<string[]>? projectHeaderRows = null;
            string outputBaseName;
            List<GeneralListItem> items;
            if (usePaste)
            {
                var reader = new PastedListReader(logger);
                items = reader.LoadFromText(pasted, "texto colado na finalização guiada");
                projectHeaderRows = reader.ProjectHeaderRows;
                outputBaseName = string.IsNullOrWhiteSpace(userCode)
                    ? "lista_colada_" + DateTime.Now.ToString("yyyyMMdd_HHmmss")
                    : userCode;
            }
            else
            {
                items = new GeneralListReader(logger).LoadFromFile(excelPath, sheetName, headerRow: 3);
                outputBaseName = string.IsNullOrWhiteSpace(userCode)
                    ? Path.GetFileNameWithoutExtension(excelPath)
                    : userCode;
            }

            if (items.Count == 0)
            {
                logger.Warn("Nenhum item encontrado. Finalização abortada.");
                return;
            }

            token.ThrowIfCancellationRequested();
            SetProgress(18);
            logger.Info("2/4 Separando materiais e gerando listas...");
            var result = new ListSeparatorService(logger).Separate(items);

            string generalTemplate = FindModelTemplate("LEPXXXX-PXXXX-C0X-CH-R00.xlsx");
            string supplierTemplate = FindModelTemplate("LEP2XXXX-LISTA GERAL-FORNECEDOR-CH-R00.xlsx");
            string busbarTemplate = FindModelTemplate("LEPXXXX-PXXXX-C0X-BA-R00.xlsx");
            if (!File.Exists(generalTemplate) || !File.Exists(supplierTemplate) || !File.Exists(busbarTemplate))
            {
                logger.Err("ERRO: templates não encontrados. Verifique a pasta 'modelos' ao lado do executável.");
                return;
            }

            var outputs = new TemplateListReportWriter(logger).WriteFromTemplates(
                result,
                generalTemplate,
                supplierTemplate,
                busbarTemplate,
                listsDir,
                outputBaseName,
                projectHeaderRows,
                userRevision,
                projectTitle: userCode);

            if (!outputs.Success)
            {
                logger.Err("As listas não foram geradas corretamente. A coleta automática foi interrompida.");
                return;
            }

            string[] supplierCodes = ExtractReferences(result.Fornecedor);
            string[] busbarCodes = ExtractReferences(result.Barramento);
            logger.Info($"Códigos para coleta: Fornecedor {supplierCodes.Length}, Barramento {busbarCodes.Length}.");

            RevisionRepository? revisionRepo = null;
            if (!string.IsNullOrWhiteSpace(revisionDbPath))
            {
                revisionRepo = new RevisionRepository(logger);
                if (!revisionRepo.LoadFromFile(revisionDbPath))
                {
                    logger.Warn("Banco de revisões informado, mas não foi possível carregar. Relatório de divergências será ignorado.");
                    revisionRepo = null;
                }
            }
            else
            {
                logger.Info("Banco de revisões não informado — relatório de divergências de revisão desativado.");
            }

            token.ThrowIfCancellationRequested();
            SetProgress(35);
            logger.Info("3/4 Buscando arquivos do fornecedor (.pdf/.dwg)...");
            if (supplierCodes.Length > 0)
            {
                var supplierOptions = new CollectorOptions
                {
                    ServerRoots = roots,
                    OutputDirectory = supplierFilesDir,
                    Extensions = new[] { ".pdf", ".dwg" },
                    Codes = supplierCodes,
                    CodesWithRevision = ExtractCodesWithRevision(result.Fornecedor),
                    RevisionRepository = revisionRepo,
                    PreviewOnly = false,
                    GroupByCode = false,
                    ExactMode = false,
                    BusbarMode = false,
                    ExtensionEquivalences = new[]
                    {
                        new ExtensionEquivalence(".dwg", ".pdf"),
                        new ExtensionEquivalence(".pdf", ".dwg"),
                    },
                };
                RunCollectionStep(supplierOptions, supplierReportsDir, token, 35, 65);
            }
            else
            {
                logger.Warn("Nenhum código de fornecedor para coletar.");
            }

            token.ThrowIfCancellationRequested();
            SetProgress(68);
            logger.Info("4/4 Buscando arquivos de barramento (.dxf/.stp) com regra TemDobra...");
            if (busbarCodes.Length > 0)
            {
                var busbarRepo = new BusbarRepository(logger);
                if (!busbarRepo.LoadFromFile(busbarDbPath))
                {
                    logger.Err("Coleta de barramento interrompida: banco de barramento não carregou.");
                    return;
                }

                var busbarOptions = new CollectorOptions
                {
                    ServerRoots = roots,
                    OutputDirectory = busbarFilesDir,
                    Extensions = new[] { ".dxf", ".stp" },
                    Codes = busbarCodes,
                    CodesWithRevision = ExtractCodesWithRevision(result.Barramento),
                    RevisionRepository = revisionRepo,
                    PreviewOnly = false,
                    GroupByCode = false,
                    ExactMode = false,
                    BusbarMode = true,
                    BusbarRepository = busbarRepo,
                    ExtensionEquivalences = Array.Empty<ExtensionEquivalence>(),
                };
                RunCollectionStep(busbarOptions, busbarReportsDir, token, 68, 96);
            }
            else
            {
                logger.Warn("Nenhum código de barramento para coletar.");
            }

            logger.Ok("Finalização automática concluída.");
            logger.Info("Listas:      " + listsDir);
            logger.Info("Fornecedor:  " + supplierFilesDir);
            logger.Info("Barramento:  " + busbarFilesDir);
            logger.Info("Relatórios:  " + reportsDir);
            SetProgress(100);
        }

        private void RunCollectionStep(
            CollectorOptions options,
            string reportDir,
            CancellationToken token,
            int progressStart,
            int progressEnd)
        {
            var service = new DrawingCollectorService(logger);
            service.OnProgress = pct =>
            {
                int mapped = progressStart + (int)((progressEnd - progressStart) * (pct / 100.0));
                SetProgress(Math.Max(progressStart, Math.Min(progressEnd, mapped)));
            };

            var missing = service.Run(options, token);
            new ExcelReporter(logger).ExportMissingByExt(missing, reportDir);

            if (options.RevisionRepository != null)
            {
                new DivergenceReporter(logger).Export(service.RevisionDivergences, reportDir);
            }
        }

        private static string[] ExtractReferences(IEnumerable<GeneralListItem> items)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = new List<string>();
            foreach (var item in items)
            {
                string code = (item.Referencia ?? string.Empty).Trim();
                if (code.Length == 0) continue;
                if (seen.Add(code)) list.Add(code);
            }
            return list.ToArray();
        }


        private static CodeWithRevision[] ExtractCodesWithRevision(IEnumerable<GeneralListItem> items)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = new List<CodeWithRevision>();
            foreach (var item in items)
            {
                string code = (item.Referencia ?? string.Empty).Trim();
                if (code.Length == 0) continue;

                // Um código pode aparecer mais de uma vez na lista. Para o relatório de revisão,
                // mantém a primeira ocorrência encontrada, que normalmente representa a revisão
                // informada no desenho/lista do projeto.
                if (seen.Add(code))
                    list.Add(new CodeWithRevision(code, (item.Rev ?? string.Empty).Trim()));
            }
            return list.ToArray();
        }

        private void OpenProjectFolder()
        {
            string p = string.IsNullOrWhiteSpace(lastProjectFolder) ? txtProjectFolder.Text.Trim() : lastProjectFolder;
            if (Directory.Exists(p)) Process.Start("explorer.exe", p);
        }

        private void SetProgress(int value)
        {
            SafeUi(() => progress.Value = Math.Max(0, Math.Min(100, value)));
        }

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

        private static void OnDragEnter(object? sender, DragEventArgs e)
        {
            e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
                ? DragDropEffects.Copy
                : DragDropEffects.None;
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
