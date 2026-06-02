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
    /// Janela principal do aplicativo.
    /// </summary>
    /// <remarks>
    /// Responsabilidades desta classe (e só estas):
    ///   1. Construir e organizar os controles visuais (BuildLayout)
    ///   2. Conectar eventos de UI às ações (cliques, mudanças)
    ///   3. Delegar o trabalho pesado para as classes do Core e Reports
    ///   4. Atualizar a UI com os resultados (via Invoke quando necessário)
    ///
    /// O que ela NÃO faz (foi movido para outras classes):
    ///   - Lógica de matching (CodeMatcher)
    ///   - Varredura de arquivos (FileIndexer)
    ///   - Cópia/orquestração (DrawingCollectorService)
    ///   - Relatórios (ExcelReporter, ZipReporter)
    ///   - Tema (ThemeManager)
    ///   - Log (RichTextBoxLogger)
    /// </remarks>
    public class MainForm : Form
    {
        // ── Nomes das colunas do grid (constantes para evitar string mágica) ──
        private static class GridCol
        {
            public const string Selected  = "Sel";
            public const string Code      = "Codigo";
            public const string FileName  = "Arquivo";
            public const string Extension = "Ext";
            public const string MatchType = "Match";
            public const string FullPath  = "Caminho";
        }

        // ── Controles de UI ──────────────────────────────────────────────
        private CodeInputPanel codeInputPanel = null!;
        private TextBox    txtOut         = null!;
        private TextBox    txtExt         = null!;
        private TextBox    txtBusbarPath  = null!;
        private TextBox    txtRevisionPath = null!;
        private CheckBox   chkGroup       = null!;
        private CheckBox   chkPreviewOnly = null!;
        private CheckBox   chkDark        = null!;
        private ProgressBar progress      = null!;
        private RichTextBox rtbLog        = null!;

        private RoundedButton btnStart        = null!;
        private RoundedButton btnOpenDest     = null!;
        private RoundedButton btnZip          = null!;
        private RoundedButton btnAddRoot      = null!;
        private RoundedButton btnRemoveRoot   = null!;
        private RoundedButton btnCopySelected = null!;
        private RoundedButton btnClearGrid    = null!;
        private RoundedButton btnExactly      = null!;
        private RoundedButton btnSelExt       = null!;
        private RoundedButton btnBusbar       = null!;
        private RoundedButton btnGenerateLists= null!;

        private ListBox            lstRoots  = null!;
        private DataGridView       grid      = null!;
        private TableLayoutPanel   rootPanel = null!;
        private TableLayoutPanel   rootsRow  = null!;
        private ContextMenuStrip   menuExt   = new();

        // ── Estado ───────────────────────────────────────────────────────
        private bool _exactMode;
        private bool _busbarMode;
        private CancellationTokenSource? _cts;

        // ── Serviços (injetados ou criados aqui) ─────────────────────────
        private ILogger?      _logger;
        private ThemeManager? _themeManager;

        // ── Cores dos botões de estado ───────────────────────────────────
        private static readonly Color ExactOnColor   = Palette.Success;
        private static readonly Color ExactOffColor  = Palette.Accent;
        private static readonly Color BusbarOnColor  = Color.FromArgb(52, 120, 180); // azul
        private static readonly Color BusbarOffColor = Palette.Accent;

        // ════════════════════════════════════════════════════════════════
        //   CONSTRUTOR
        // ════════════════════════════════════════════════════════════════
        public MainForm()
        {
            Text          = "Módulo 2 - Coletar Arquivos";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize    = new Size(1100, 720);
            MinimumSize   = new Size(1000, 640);
            Font          = new Font("Segoe UI", 10f);
            AutoScaleMode = AutoScaleMode.Dpi;

            // Ícone da aplicação (ao lado do título e na barra de tarefas)
            string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.ico");
            Icon = File.Exists(icoPath) ? new Icon(icoPath) : SystemIcons.Application;

            BuildLayout();
            ImproveRendering();

            // Cria os serviços que dependem de controles já construídos
            _logger       = new RichTextBoxLogger(rtbLog);
            _themeManager = new ThemeManager(this, grid, rtbLog);
            codeInputPanel.SetLogger(_logger);

            // Carrega configurações salvas e restaura a UI
            // (feito APÓS BuildLayout para que os controles já existam)
            RestoreSettings(SettingsManager.Load());

            // Salva configurações ao fechar a janela
            FormClosing += (_, __) => SettingsManager.Save(CaptureSettings());
        }

        // ════════════════════════════════════════════════════════════════
        //   RENDERING
        // ════════════════════════════════════════════════════════════════
        private void ImproveRendering()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                    | ControlStyles.OptimizedDoubleBuffer
                    | ControlStyles.UserPaint,
                true);
            UpdateStyles();
            DoubleBuffered = true;

            // Ativa double buffer nos painéis (reduz flickering ao redimensionar)
            rootPanel.SetDoubleBuffered();
            rootsRow.SetDoubleBuffered();
            grid.SetDoubleBuffered();

            grid.EnableHeadersVisualStyles         = false;
            grid.RowHeadersVisible                 = false;
            grid.ColumnHeadersBorderStyle          = DataGridViewHeaderBorderStyle.None;
            grid.CellBorderStyle                   = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.AdvancedColumnHeadersBorderStyle.All = DataGridViewAdvancedCellBorderStyle.None;
        }

        // Sincroniza a altura de um botão com a de um TextBox de referência
        private void MatchButtonHeightTo(Control reference, Control target)
        {
            void Sync(object? s, EventArgs e)
            {
                int h = reference.Height;
                if (h <= 0) return;
                target.MinimumSize = new Size(Math.Max(target.MinimumSize.Width, 110), h);
                target.Height      = h;
            }
            Shown                 += (_, __) => Sync(null, EventArgs.Empty);
            reference.SizeChanged += Sync;
        }

        // ════════════════════════════════════════════════════════════════
        //   LAYOUT
        // ════════════════════════════════════════════════════════════════
        private void BuildLayout()
        {
            // Instancia todos os controles
            codeInputPanel = new CodeInputPanel();
            txtOut         = new TextBox();
            txtBusbarPath  = new TextBox();
            txtRevisionPath = new TextBox();
            txtExt         = new TextBox();
            chkGroup       = new CheckBox();
            chkPreviewOnly = new CheckBox();
            chkDark        = new CheckBox();
            btnStart       = new RoundedButton { Text = "Iniciar" };
            btnCopySelected= new RoundedButton { Text = "Copiar Selecionados" };
            btnClearGrid   = new RoundedButton { Text = "Limpar tabela" };
            btnZip         = new RoundedButton { Text = "Gerar ZIP" };
            btnOpenDest    = new RoundedButton { Text = "Abrir pasta destino" };
            btnExactly     = new RoundedButton { Text = "Modo: EXATO (OFF)" };
            btnSelExt      = new RoundedButton { Text = "Ext" };
            btnBusbar      = new RoundedButton { Text = "Barramento (OFF)" };
            btnGenerateLists = new RoundedButton { Text = "Gerar Listas" };
            btnAddRoot     = new RoundedButton { Text = "Adicionar pasta" };
            btnRemoveRoot  = new RoundedButton { Text = "Remover pasta" };
            lstRoots       = new ListBox();
            grid           = new DataGridView();
            rtbLog         = new RichTextBox();

            // Painel raiz que ocupa o formulário inteiro
            rootPanel = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                Padding     = new Padding(8),
            };
            rootPanel.RowStyles.Clear();
            rootPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // 0 header
            rootPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // 1 códigos
            rootPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // 2 pastas raiz
            rootPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // 3 destino
            rootPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // 4 opções
            rootPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // 5 barramento
            rootPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // 6 ações
            rootPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));   // 7 grid (expande)
            rootPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 160f));  // 8 log
            rootPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));   // 9 rodapé

            Controls.Clear();
            Controls.Add(rootPanel);

            BuildHeaderRow();
            BuildCodesRow();
            BuildRootsRow();
            BuildOutputRow();
            BuildOptionsRow();
            BuildBusbarRow();     // NOVO
            BuildActionsRow();
            BuildGridSection();
            BuildLogSection();
            BuildFooterRow();
        }

        private void BuildHeaderRow()
        {
            var header = new GradientHeader
            {
                Dock         = DockStyle.Top,
                Height       = 80,
                Margin       = new Padding(16, 16, 16, 8),
                Title        = "Coletar Arquivos",
                CornerRadius = 18,
            };
            rootPanel.Controls.Add(header, 0, 0);
        }

        private void BuildCodesRow()
        {
            // O CodeInputPanel já é um UserControl completo com as 3 abas.
            // A row 1 do rootPanel agora tem altura automática baseada no painel.
            // Precisamos de uma altura mínima razoável para as abas aparecerem bem.
            rootPanel.RowStyles[1] = new RowStyle(SizeType.Absolute, 130f);

            var wrapper = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                Padding     = new Padding(0, 2, 0, 4),
                Margin      = Padding.Empty,
            };
            wrapper.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            wrapper.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            wrapper.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            wrapper.Controls.Add(new Label
            {
                Text      = "Códigos:",
                AutoSize  = true,
                Font      = new Font("Segoe UI Semibold", 9.5f),
                Margin    = new Padding(0, 6, 8, 0),
            }, 0, 0);

            codeInputPanel.Dock = DockStyle.Fill;
            wrapper.Controls.Add(codeInputPanel, 1, 0);

            rootPanel.Controls.Add(wrapper, 0, 1);
        }


        private void BuildRootsRow()
        {
            rootsRow = new TableLayoutPanel
            {
                ColumnCount  = 2,
                Dock         = DockStyle.Top,
                AutoSize     = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding      = new Padding(0, 4, 0, 0),
            };
            rootsRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            rootsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            rootsRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            rootsRow.Controls.Add(new Label
            {
                Text     = "Pastas raiz do servidor:",
                AutoSize = true,
                Font     = new Font("Segoe UI Semibold", 9.5f),
            }, 0, 0);

            var rootsContainer = new TableLayoutPanel
            {
                ColumnCount = 2,
                Dock        = DockStyle.Fill,
                Margin      = new Padding(0),
            };
            rootsContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            rootsContainer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            rootsContainer.RowStyles.Clear();
            rootsContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            lstRoots.Dock           = DockStyle.Fill;
            lstRoots.Margin         = new Padding(0);
            lstRoots.IntegralHeight = false;
            lstRoots.MinimumSize    = new Size(0, 26 * 4); // ~4 linhas de altura
            rootsContainer.Controls.Add(lstRoots, 0, 0);

            var buttonsCol = new TableLayoutPanel
            {
                ColumnCount  = 1,
                RowCount     = 3,
                Dock         = DockStyle.Fill,
                AutoSize     = true,
                Margin       = new Padding(8, 0, 0, 0),
                Anchor       = AnchorStyles.Top | AnchorStyles.Right,
            };
            buttonsCol.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            buttonsCol.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            buttonsCol.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            btnAddRoot.Margin       = Padding.Empty;
            btnAddRoot.MinimumSize  = new Size(120, 26);
            btnAddRoot.AutoSize     = true;
            btnAddRoot.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            btnAddRoot.Anchor       = AnchorStyles.Top | AnchorStyles.Left;
            btnAddRoot.Click       += (_, __) => AddRoot();

            btnRemoveRoot.Margin       = Padding.Empty;
            btnRemoveRoot.MinimumSize  = new Size(120, 26);
            btnRemoveRoot.AutoSize     = true;
            btnRemoveRoot.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            btnRemoveRoot.Anchor       = AnchorStyles.Bottom | AnchorStyles.Left;
            btnRemoveRoot.Click       += (_, __) => RemoveSelectedRoot();

            buttonsCol.Controls.Add(btnAddRoot,    0, 0);
            buttonsCol.Controls.Add(btnRemoveRoot, 0, 2);
            rootsContainer.Controls.Add(buttonsCol, 1, 0);
            rootsRow.Controls.Add(rootsContainer, 1, 0);
            rootPanel.Controls.Add(rootsRow, 0, 2);
        }

        private void BuildOutputRow()
        {
            var outRow = new TableLayoutPanel
            {
                ColumnCount  = 2,
                Dock         = DockStyle.Top,
                AutoSize     = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
            };
            outRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            outRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            outRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            outRow.Controls.Add(new Label
            {
                Text     = "Pasta de destino:",
                AutoSize = true,
                Font     = new Font("Segoe UI Semibold", 9.5f),
            }, 0, 0);

            var btnSelectOut = new RoundedButton { Text = "Selecionar" };
            var outContainer = MakeTwoColumnContainer(
                left: txtOut,
                right: btnSelectOut,
                rightMargin: new Padding(8, 0, 0, 0),
                rightMinWidth: 110,
                onRightClick: (s, e) =>
                {
                    var p = PickFolderModern(this, txtOut.Text.Trim());
                    if (!string.IsNullOrEmpty(p)) txtOut.Text = p;
                });

            outRow.Controls.Add(outContainer, 1, 0);
            rootPanel.Controls.Add(outRow, 0, 3);
        }

        private void BuildOptionsRow()
        {
            var optRow = new TableLayoutPanel { ColumnCount = 6, Dock = DockStyle.Top,
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            optRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            optRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            optRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            optRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            optRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            optRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            optRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            optRow.Controls.Add(new Label
            {
                Text = "Extensões (separadas por vírgula):",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 9.5f),
            }, 0, 0);

            txtExt.Text = ".dwg,.pdf,.dft,.asm,.par,.psm,.dxf,.stp";
            txtExt.Dock = DockStyle.Fill;
            optRow.Controls.Add(txtExt, 1, 0);

            btnSelExt.Margin  = new Padding(8, 0, 8, 0);
            btnSelExt.Enabled = false;
            btnSelExt.Click  += (_, __) =>
            {
                BuildExtensionsMenu();
                menuExt.Show(btnSelExt, new Point(0, btnSelExt.Height));
            };
            optRow.Controls.Add(btnSelExt, 2, 0);

            chkGroup.Text    = "Criar subpasta por código";
            chkGroup.AutoSize = true;
            chkGroup.Margin  = new Padding(8, 0, 8, 0);
            optRow.Controls.Add(chkGroup, 3, 0);

            chkPreviewOnly.Text     = "Apenas pré-visualizar";
            chkPreviewOnly.AutoSize = true;
            chkPreviewOnly.Margin   = new Padding(8, 0, 8, 0);
            optRow.Controls.Add(chkPreviewOnly, 4, 0);

            chkDark.Text           = "Tema escuro";
            chkDark.AutoSize       = true;
            chkDark.CheckedChanged += (_, __) => ApplyTheme(chkDark.Checked);
            optRow.Controls.Add(chkDark, 5, 0);

            rootPanel.Controls.Add(optRow, 0, 4);
        }

        private void BuildBusbarRow()
        {
            var row = new TableLayoutPanel
            {
                ColumnCount  = 2,
                Dock         = DockStyle.Top,
                AutoSize     = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding      = new Padding(0, 2, 0, 2),
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            row.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            row.Controls.Add(new Label
            {
                Text     = "Banco barramento:",
                AutoSize = true,
                Font     = new Font("Segoe UI Semibold", 9.5f),
                Margin   = new Padding(0, 6, 8, 0),
            }, 0, 0);

            // Container: [txtBusbarPath] [Selecionar] [Gerar Template]
            var inner = new TableLayoutPanel { ColumnCount = 3, Dock = DockStyle.Fill,
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            inner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            inner.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            inner.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            inner.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            txtBusbarPath.Dock            = DockStyle.Fill;
            txtBusbarPath.PlaceholderText = "Selecione o Excel banco de barramentos (.xlsx)...";
            inner.Controls.Add(txtBusbarPath, 0, 0);

            var btnSelectBusbar = new RoundedButton
                { Text = "Selecionar", MinimumSize = new Size(100, 26), Margin = new Padding(6, 0, 0, 0) };
            btnSelectBusbar.Click += (_, __) =>
            {
                using var dlg = new OpenFileDialog
                {
                    Title  = "Selecionar banco de barramentos",
                    Filter = "Excel (*.xlsx)|*.xlsx",
                };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    txtBusbarPath.Text = dlg.FileName;
            };
            inner.Controls.Add(btnSelectBusbar, 1, 0);

            var btnTemplate = new RoundedButton
                { Text = "Gerar template", MinimumSize = new Size(110, 26), Margin = new Padding(6, 0, 0, 0) };
            btnTemplate.Click += (_, __) =>
            {
                using var dlg = new SaveFileDialog
                {
                    Title      = "Salvar template do banco de barramentos",
                    Filter     = "Excel (*.xlsx)|*.xlsx",
                    FileName   = "banco_barramentos_template.xlsx",
                };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        BusbarRepository.GenerateTemplate(dlg.FileName);
                        txtBusbarPath.Text = dlg.FileName;
                        _logger?.Ok("Template gerado: " + dlg.FileName);
                        Process.Start("explorer.exe", $"/select,\"{dlg.FileName}\"");
                    }
                    catch (Exception ex)
                    {
                        _logger?.Err("Erro ao gerar template: " + ex.Message);
                    }
                }
            };
            inner.Controls.Add(btnTemplate, 2, 0);

            row.Controls.Add(inner, 1, 0);
            rootPanel.Controls.Add(row, 0, 5);

            BuildRevisionRow();
        }

        private void BuildRevisionRow()
        {
            // Adiciona uma segunda linha visual logo abaixo do barramento,
            // dentro da mesma célula da grade (row 5), usando um painel empilhado.
            // Para simplicidade, inserimos na row 5 também via um FlowLayoutPanel
            // que cresce verticalmente. Mas como row 5 já está ocupada, usamos
            // a abordagem de embutir numa TableLayout de 2 linhas.

            // Recupera o painel da row 5 e o transforma em container de 2 linhas:
            // (mais simples: criar a linha de revisão como parte do mesmo 'row')
            // Aqui criamos um novo container e o colocamos na row 5 abaixo do existente.

            var revRow = new TableLayoutPanel
            {
                ColumnCount  = 2,
                Dock         = DockStyle.Top,
                AutoSize     = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding      = new Padding(0, 2, 0, 2),
            };
            revRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            revRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            revRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            revRow.Controls.Add(new Label
            {
                Text     = "Banco de revisões:",
                AutoSize = true,
                Font     = new Font("Segoe UI Semibold", 9.5f),
                Margin   = new Padding(0, 6, 8, 0),
            }, 0, 0);

            var inner = new TableLayoutPanel { ColumnCount = 3, Dock = DockStyle.Fill,
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            inner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            inner.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            inner.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            inner.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            txtRevisionPath.Dock            = DockStyle.Fill;
            txtRevisionPath.PlaceholderText = "Excel banco de revisões oficiais (.xlsx) — opcional...";
            inner.Controls.Add(txtRevisionPath, 0, 0);

            var btnSelectRev = new RoundedButton
                { Text = "Selecionar", MinimumSize = new Size(100, 26), Margin = new Padding(6, 0, 0, 0) };
            btnSelectRev.Click += (_, __) =>
            {
                using var dlg = new OpenFileDialog
                {
                    Title  = "Selecionar banco de revisões",
                    Filter = "Excel (*.xlsx)|*.xlsx",
                };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    txtRevisionPath.Text = dlg.FileName;
            };
            inner.Controls.Add(btnSelectRev, 1, 0);

            var btnRevTemplate = new RoundedButton
                { Text = "Gerar template", MinimumSize = new Size(110, 26), Margin = new Padding(6, 0, 0, 0) };
            btnRevTemplate.Click += (_, __) =>
            {
                using var dlg = new SaveFileDialog
                {
                    Title    = "Salvar template do banco de revisões",
                    Filter   = "Excel (*.xlsx)|*.xlsx",
                    FileName = "banco_revisoes_template.xlsx",
                };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        RevisionRepository.GenerateTemplate(dlg.FileName);
                        txtRevisionPath.Text = dlg.FileName;
                        _logger?.Ok("Template de revisões gerado: " + dlg.FileName);
                        Process.Start("explorer.exe", $"/select,\"{dlg.FileName}\"");
                    }
                    catch (Exception ex)
                    {
                        _logger?.Err("Erro ao gerar template: " + ex.Message);
                    }
                }
            };
            inner.Controls.Add(btnRevTemplate, 2, 0);

            revRow.Controls.Add(inner, 1, 0);

            // A row 5 já tem o barramento. Empilhamos a revisão logo após,
            // usando um Panel container. Como o TableLayoutPanel não permite
            // dois controles na mesma célula, reposicionamos ambos num painel.
            var existing = rootPanel.GetControlFromPosition(0, 5);
            if (existing != null)
            {
                rootPanel.Controls.Remove(existing);
                var stack = new TableLayoutPanel
                {
                    ColumnCount = 1, Dock = DockStyle.Fill,
                    AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                };
                stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                existing.Dock = DockStyle.Top;
                stack.Controls.Add(existing, 0, 0);
                stack.Controls.Add(revRow,   0, 1);
                rootPanel.Controls.Add(stack, 0, 5);
            }
        }

        private void BuildActionsRow()
        {
            var actionsPanel = new TableLayoutPanel
            {
                ColumnCount  = 1,
                Dock         = DockStyle.Top,
                AutoSize     = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
            };
            actionsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            actionsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var actionsButtons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents  = true,
                Dock          = DockStyle.Fill,
                AutoSize      = true,
                AutoSizeMode  = AutoSizeMode.GrowAndShrink,
            };

            btnStart.Click        += (_, __) => StartWork();
            btnCopySelected.Click += (_, __) => CopySelectedRows();
            btnClearGrid.Click    += (_, __) => { grid.Rows.Clear(); btnSelExt.Enabled = false; };
            btnZip.Click          += (_, __) => new ZipReporter(_logger!).GenerateZip(txtOut.Text.Trim());
            btnOpenDest.Click     += (_, __) =>
            {
                var p = txtOut.Text.Trim();
                if (Directory.Exists(p))
                    Process.Start("explorer.exe", p);
            };
            btnExactly.Click += (_, __) => ToggleExactMode();
            btnBusbar.Click  += (_, __) => ToggleBusbarMode();
            btnGenerateLists.Click += (_, __) => GenerateListsFromCurrentInput();
            btnZip.Enabled    = false;

            actionsButtons.Controls.AddRange(new Control[]
            {
                btnStart, btnCopySelected, btnClearGrid,
                btnZip, btnOpenDest, btnExactly, btnBusbar,
            });
            actionsPanel.Controls.Add(actionsButtons, 0, 0);

            progress = new ProgressBar
            {
                Dock   = DockStyle.Fill,
                Height = 16,
                Style  = ProgressBarStyle.Continuous,
                Margin = new Padding(0, 8, 0, 0),
            };
            actionsPanel.Controls.Add(progress, 0, 1);
            rootPanel.Controls.Add(actionsPanel, 0, 6);
        }

        private void BuildGridSection()
        {
            grid.ReadOnly              = false;
            grid.AllowUserToAddRows    = false;
            grid.AllowUserToDeleteRows = false;
            grid.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill;
            grid.MultiSelect           = true;
            grid.Dock                  = DockStyle.Fill;

            grid.Columns.Add(new DataGridViewCheckBoxColumn
                { Name = GridCol.Selected,  HeaderText = "Selecionar", Width = 70 });
            grid.Columns.Add(GridCol.Code,      "Código");
            grid.Columns.Add(GridCol.FileName,  "Arquivo");
            grid.Columns.Add(GridCol.Extension, "Ext");
            grid.Columns.Add(GridCol.MatchType, "Match");
            grid.Columns.Add(new DataGridViewTextBoxColumn
                { Name = GridCol.FullPath, HeaderText = "Caminho", ReadOnly = true });

            grid.Columns[GridCol.Code].ReadOnly      = true;
            grid.Columns[GridCol.FileName].ReadOnly  = true;
            grid.Columns[GridCol.Extension].ReadOnly = true;
            grid.Columns[GridCol.MatchType].ReadOnly = true;

            rootPanel.Controls.Add(grid, 0, 7);
        }

        private void BuildLogSection()
        {
            var logPanel = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                AutoSize    = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
            };
            logPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            logPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            rtbLog.ReadOnly   = true;
            rtbLog.Font       = new Font("Consolas", 9f);
            rtbLog.Dock       = DockStyle.Fill;
            rtbLog.ScrollBars = RichTextBoxScrollBars.Vertical;

            logPanel.Controls.Add(new Label
            {
                Text     = "Log / Resultado:",
                AutoSize = true,
                Font     = new Font("Segoe UI Semibold", 9.5f),
            }, 0, 0);
            logPanel.Controls.Add(rtbLog, 1, 0);
            rootPanel.Controls.Add(logPanel, 0, 8);
        }

        private void BuildFooterRow()
        {
            var footer = new Label
            {
                Text      = "v1.0  •  Desenvolvido por Enzo Zeferino",
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Font      = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(160, 160, 160),
                Padding   = new Padding(0, 0, 12, 0),
            };
            rootPanel.Controls.Add(footer, 0, 9);
        }

        // Helper: padrão [TextBox | Button] reutilizado em múltiplos campos
        private TableLayoutPanel MakeTwoColumnContainer(
            TextBox left,
            RoundedButton right,
            Padding rightMargin,
            int rightMinWidth,
            EventHandler onRightClick)
        {
            var container = new TableLayoutPanel
            {
                ColumnCount  = 2,
                Dock         = DockStyle.Fill,
                AutoSize     = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
            };
            container.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            container.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            container.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            left.Dock = DockStyle.Fill;
            container.Controls.Add(left, 0, 0);

            right.Dock        = DockStyle.Fill;
            right.Margin      = rightMargin;
            right.MinimumSize = new Size(rightMinWidth, left.PreferredHeight);
            right.Click      += onRightClick;
            container.Controls.Add(right, 1, 0);

            MatchButtonHeightTo(left, right);
            return container;
        }

        // ════════════════════════════════════════════════════════════════
        //   PERSISTÊNCIA DE CONFIGURAÇÕES
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Restaura as configurações salvas anteriormente.
        /// </summary>
        private void RestoreSettings(AppSettings s)
        {
            txtBusbarPath.Text   = s.BusbarDatabasePath;
            txtRevisionPath.Text = s.RevisionDatabasePath;
            ApplyTheme(dark: false);
        }

        /// <summary>
        /// Captura o estado atual para salvar.
        /// </summary>
        private AppSettings CaptureSettings() => new AppSettings
        {
            BusbarDatabasePath   = txtBusbarPath.Text.Trim(),
            RevisionDatabasePath = txtRevisionPath.Text.Trim(),
        };

        // ════════════════════════════════════════════════════════════════
        //   TEMA
        // ════════════════════════════════════════════════════════════════
        private void ApplyTheme(bool dark)
        {
            _themeManager?.Apply(dark);
            codeInputPanel.ApplyTheme(dark);
        }

        // ════════════════════════════════════════════════════════════════
        //   PASTAS RAIZ
        // ════════════════════════════════════════════════════════════════
        private void AddRoot()
        {
            string? picked = PickFolderModern(this, null);
            if (string.IsNullOrEmpty(picked)) return;

            // Evita duplicatas (comparação sem diferenciar maiúsculas/minúsculas)
            foreach (var item in lstRoots.Items)
                if (string.Equals(item?.ToString(), picked, StringComparison.OrdinalIgnoreCase))
                    return;

            lstRoots.Items.Add(picked);
        }

        private void RemoveSelectedRoot()
        {
            int idx = lstRoots.SelectedIndex;
            if (idx >= 0) lstRoots.Items.RemoveAt(idx);
        }

        // ════════════════════════════════════════════════════════════════
        //   INICIAR TRABALHO
        // ════════════════════════════════════════════════════════════════
        private void StartWork()
        {
            // Cancela execução anterior, se houver
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            btnStart.Enabled               = false;
            btnZip.Enabled                 = false;
            progress.Style                 = ProgressBarStyle.Marquee;
            progress.MarqueeAnimationSpeed = 20;
            rtbLog.Clear();

            if (!chkPreviewOnly.Checked) grid.Rows.Clear();

            // Valida campos antes de começar
            string   outDir = txtOut.Text.Trim();
            string[] roots  = lstRoots.Items.Cast<string>()
                                  .Select(r => (r ?? "").Trim())
                                  .Where(r => !string.IsNullOrEmpty(r))
                                  .ToArray();

            if (roots.Length == 0) { _logger!.Err("ERRO: Nenhuma pasta raiz informada."); ResetProgress(); return; }

            if (string.IsNullOrEmpty(outDir))
            {
                _logger!.Err("ERRO: Pasta de destino não informada.");
                ResetProgress();
                return;
            }

            if (!Directory.Exists(outDir))
            {
                try   { Directory.CreateDirectory(outDir); }
                catch { _logger!.Err("ERRO: Não foi possível criar a pasta de destino."); ResetProgress(); return; }
            }

            string[] exts = SplitExtensions(txtExt.Text);
            if (exts.Length == 0) { _logger!.Err("ERRO: Informe pelo menos uma extensão."); ResetProgress(); return; }

            // Lê os códigos do painel ativo (Colar / .txt / .xlsx)
            string[] codes = codeInputPanel.GetCodes(_logger!);
            if (codes.Length == 0) { _logger!.Err("ERRO: Nenhum código foi encontrado na entrada selecionada."); ResetProgress(); return; }

            // ── Modo Barramento: carrega o banco antes de lançar a thread ──
            // O banco é carregado aqui (thread UI) porque lê um campo de texto (txtBusbarPath).
            // O objeto já carregado é passado para o CollectorOptions e então para a thread worker.
            BusbarRepository? busbarRepo = null;
            if (_busbarMode)
            {
                busbarRepo = new BusbarRepository(_logger!);
                bool ok = busbarRepo.LoadFromFile(txtBusbarPath.Text.Trim());
                if (!ok)
                {
                    // Banco não carregou → não prossegue no modo barramento
                    ResetProgress();
                    return;
                }
            }

            // ── Banco de revisões (validação automática) ───────────────────
            // Se o caminho estiver preenchido, carrega o banco. A validação
            // acontece sempre que o banco existe E a lista tem coluna REV.
            RevisionRepository? revisionRepo = null;
            var codesWithRev = codeInputPanel.GetCodesWithRevision(_logger!);
            if (!string.IsNullOrWhiteSpace(txtRevisionPath.Text))
            {
                revisionRepo = new RevisionRepository(_logger!);
                revisionRepo.LoadFromFile(txtRevisionPath.Text.Trim());
                // Se falhar ao carregar, segue sem validação (não bloqueia)
                if (!revisionRepo.IsLoaded) revisionRepo = null;
            }

            // Captura o estado da UI ANTES de entrar na thread worker
            // (na thread worker não é seguro ler controles)
            var options = new CollectorOptions
            {
                ServerRoots      = roots,
                OutputDirectory  = outDir,
                Extensions       = exts,
                Codes            = codes,
                PreviewOnly      = chkPreviewOnly.Checked,
                GroupByCode      = chkGroup.Checked,
                ExactMode        = _exactMode,
                BusbarMode       = _busbarMode,
                BusbarRepository = busbarRepo,

                // Validação de revisão (só age se revisionRepo != null)
                CodesWithRevision  = codesWithRev,
                RevisionRepository = revisionRepo,

                // Regras de equivalência: achar .dwg dispensa o .pdf no relatório de faltantes.
                ExtensionEquivalences = new[]
                {
                    new ExtensionEquivalence(".dwg", ".pdf"),
                },
            };

            RunService(options, token);
        }

        private void RunService(CollectorOptions options, CancellationToken token)
        {
            var service = new DrawingCollectorService(_logger!);

            // Conecta os callbacks: o que fazer quando o service reportar algo
            service.OnMatchFound = ev => AddGridRow(
                ev.IsFirstSelected, ev.Code, ev.FileName,
                ev.Extension, ev.MatchType, ev.FullPath);

            service.OnProgress = pct => InvokeSetProgress(pct);

            Task.Run(() =>
            {
                try
                {
                    var missing = service.Run(options, token);

                    new ExcelReporter(_logger!).ExportMissingByExt(missing, options.OutputDirectory);

                    // Relatório de divergências de revisão (se houver validação ativa)
                    if (options.RevisionRepository != null)
                    {
                        new DivergenceReporter(_logger!)
                            .Export(service.RevisionDivergences, options.OutputDirectory);
                    }

                    if (!options.PreviewOnly)
                    {
                        SafeInvoke(() => btnZip.Enabled = true);
                        _logger!.Ok("Concluído. Veja os relatórios na pasta de destino.");
                    }
                    else
                    {
                        _logger!.Ok("Pré-visualização concluída. Revise e use 'Copiar Selecionados'.");
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger!.Info("Operação cancelada pelo usuário.");
                }
                catch (Exception ex)
                {
                    _logger!.Err("ERRO inesperado: " + ex.Message);
                }
                finally
                {
                    ResetProgress();
                }
            }, token);
        }


        // ════════════════════════════════════════════════════════════════
        //   GERAR LISTAS DO PROJETO
        // ════════════════════════════════════════════════════════════════
        private void GenerateListsFromCurrentInput()
        {
            if (_logger == null) return;

            string outDir = txtOut.Text.Trim();
            if (string.IsNullOrWhiteSpace(outDir))
            {
                _logger.Err("ERRO: informe a pasta de destino antes de gerar as listas.");
                return;
            }

            try
            {
                Directory.CreateDirectory(outDir);
            }
            catch (Exception ex)
            {
                _logger.Err("ERRO: não foi possível criar/acessar a pasta de destino: " + ex.Message);
                return;
            }

            try
            {
                var items = new List<GeneralListItem>();
                IReadOnlyList<string[]>? projectHeaderRows = null;
                string outputBaseName;

                // Usa a mesma entrada selecionada no painel de códigos:
                // 0 = Colar, 1 = .txt, 2 = Excel.
                if (codeInputPanel.ActiveMode == 0)
                {
                    var reader = new PastedListReader(_logger);
                    items = reader.LoadFromText(codeInputPanel.GetPastedText(), "texto colado na tela");
                    projectHeaderRows = reader.ProjectHeaderRows;
                    outputBaseName = "lista_colada_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                }
                else if (codeInputPanel.ActiveMode == 1)
                {
                    string txtPath = codeInputPanel.GetTxtPath();
                    if (!File.Exists(txtPath))
                    {
                        _logger.Err("ERRO: arquivo .txt não encontrado para gerar listas.");
                        return;
                    }

                    var reader = new PastedListReader(_logger);
                    items = reader.LoadFromTextFile(txtPath);
                    projectHeaderRows = reader.ProjectHeaderRows;
                    outputBaseName = Path.GetFileNameWithoutExtension(txtPath);
                }
                else if (codeInputPanel.ActiveMode == 2)
                {
                    string xlsxPath = codeInputPanel.GetXlsxPath();
                    if (!File.Exists(xlsxPath))
                    {
                        _logger.Err("ERRO: Excel não encontrado para gerar listas.");
                        return;
                    }

                    string sheet = codeInputPanel.GetSelectedSheetName();
                    if (string.IsNullOrWhiteSpace(sheet)) sheet = "Lista geral";

                    items = new GeneralListReader(_logger).LoadFromFile(xlsxPath, sheet, headerRow: 3);
                    outputBaseName = Path.GetFileNameWithoutExtension(xlsxPath);
                }
                else
                {
                    _logger.Err("ERRO: modo de entrada inválido.");
                    return;
                }

                if (items.Count == 0)
                {
                    _logger.Warn("Nenhum item encontrado para gerar as listas.");
                    return;
                }

                string generalTemplate  = FindModelTemplate("LEPXXXX-PXXXX-C0X-CH-R00.xlsx");
                string supplierTemplate = FindModelTemplate("LEP2XXXX-LISTA GERAL-FORNECEDOR-CH-R00.xlsx");
                string busbarTemplate   = FindModelTemplate("LEPXXXX-PXXXX-C0X-BA-R00.xlsx");

                if (!File.Exists(generalTemplate) || !File.Exists(supplierTemplate) || !File.Exists(busbarTemplate))
                {
                    _logger.Err("ERRO: templates não encontrados. Verifique a pasta 'modelos' ao lado do executável.");
                    _logger.Info("Template lista geral: " + generalTemplate);
                    _logger.Info("Template fornecedor:  " + supplierTemplate);
                    _logger.Info("Template barramento:  " + busbarTemplate);
                    return;
                }

                var result = new ListSeparatorService(_logger).Separate(items);

                _logger.Info($"Listas: geral {result.Todos.Count}, fornecedor {result.Fornecedor.Count}, barramento {result.Barramento.Count}, outros {result.Outros.Count}.");

                var output = new TemplateListReportWriter(_logger).WriteFromTemplates(
                    result,
                    generalTemplate,
                    supplierTemplate,
                    busbarTemplate,
                    outDir,
                    outputBaseName,
                    projectHeaderRows);

                if (output.Success)
                {
                    _logger.Ok("Listas geradas com sucesso.");
                    _logger.Info("Lista geral: " + output.GeneralPath);
                    _logger.Info("Fornecedor:  " + output.SupplierPath);
                    _logger.Info("Barramento:  " + output.BusbarPath);

                    try { Process.Start("explorer.exe", $"/select,\"{output.GeneralPath}\""); }
                    catch { /* não bloqueia se o Explorer falhar */ }
                }
                else
                {
                    _logger.Warn("A geração de listas terminou com erro. Confira as mensagens acima.");
                }
            }
            catch (Exception ex)
            {
                _logger.Err("ERRO ao gerar listas: " + ex.Message);
            }
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

            // Retorna o caminho principal esperado para facilitar o log.
            return candidates[0];
        }

        // ════════════════════════════════════════════════════════════════
        //   GRID
        // ════════════════════════════════════════════════════════════════
        private void AddGridRow(bool sel, string codigo, string arquivo,
                                string ext, string match, string caminho)
        {
            void Add()
            {
                grid.Rows.Add(sel, codigo, arquivo, ext, match, caminho);
                if (!btnSelExt.Enabled && grid.Rows.Count > 0)
                    btnSelExt.Enabled = true;
            }

            if (grid.InvokeRequired) grid.Invoke((Action)Add);
            else Add();
        }

        private void CopySelectedRows()
        {
            string outDir = txtOut.Text.Trim();
            if (!Directory.Exists(outDir)) { _logger!.Err("ERRO: pasta de destino inexistente."); return; }

            bool groupByCode = chkGroup.Checked;
            int copied = 0, selected = 0;

            foreach (DataGridViewRow row in grid.Rows)
            {
                bool sel = false;
                try { sel = Convert.ToBoolean(row.Cells[GridCol.Selected].Value ?? false); }
                catch (InvalidCastException) { /* linha com valor inválido; pula */ }

                if (!sel) continue;
                selected++;

                string code = Convert.ToString(row.Cells[GridCol.Code].Value ?? "")     ?? "";
                string src  = Convert.ToString(row.Cells[GridCol.FullPath].Value ?? "") ?? "";

                if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(src) || !File.Exists(src))
                {
                    _logger!.Warn($"Ignorando seleção inválida: {code} -> {src}");
                    continue;
                }

                string destFolder = groupByCode
                    ? Path.Combine(outDir, DrawingCollectorService.SanitizeFileName(code))
                    : outDir;
                Directory.CreateDirectory(destFolder);

                string dest = Path.Combine(destFolder, Path.GetFileName(src));
                try
                {
                    bool doCopy = !File.Exists(dest)
                               || new FileInfo(src).LastWriteTimeUtc > new FileInfo(dest).LastWriteTimeUtc;
                    if (doCopy) File.Copy(src, dest, true);
                    copied++;
                    _logger!.Ok($"Copiado (seleção): {Path.GetFileName(src)}");
                }
                catch (IOException ex)               { _logger!.Err($"Falha ao copiar: {ex.Message}"); }
                catch (UnauthorizedAccessException ex) { _logger!.Err($"Sem permissão: {ex.Message}"); }
            }

            _logger!.Info($"Seleções: {selected}  •  Copiados: {copied}");

            if (copied > 0) SafeInvoke(() => btnZip.Enabled = true);
        }

        // ════════════════════════════════════════════════════════════════
        //   FILTRO POR EXTENSÃO
        // ════════════════════════════════════════════════════════════════
        private void BuildExtensionsMenu()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataGridViewRow row in grid.Rows)
            {
                string ext = Convert.ToString(row.Cells[GridCol.Extension].Value ?? "") ?? "";
                if (!string.IsNullOrWhiteSpace(ext))
                    set.Add(ext.Trim().ToLowerInvariant());
            }

            menuExt.Items.Clear();
            foreach (var ext in set.OrderBy(x => x))
            {
                var item = new ToolStripMenuItem(ext);
                item.Click += (_, __) => SelectByExtension(ext);
                menuExt.Items.Add(item);
            }

            if (menuExt.Items.Count == 0)
                menuExt.Items.Add(new ToolStripMenuItem("(sem extensões)") { Enabled = false });
        }

        private void SelectByExtension(string extension)
        {
            string want = extension.Trim().StartsWith(".")
                ? extension.Trim().ToLowerInvariant()
                : "." + extension.Trim().ToLowerInvariant();

            foreach (DataGridViewRow row in grid.Rows)
            {
                string ext = Convert.ToString(row.Cells[GridCol.Extension].Value ?? "") ?? "";
                if (string.Equals(ext.ToLowerInvariant(), want, StringComparison.OrdinalIgnoreCase))
                    row.Cells[GridCol.Selected].Value = true;
            }
        }

        // ════════════════════════════════════════════════════════════════
        //   MODO EXATO
        // ════════════════════════════════════════════════════════════════
        private void ToggleExactMode()
        {
            _exactMode            = !_exactMode;
            btnExactly.Text       = _exactMode ? "Modo: EXATO (ON)" : "Modo: EXATO (OFF)";
            btnExactly.FillColor  = _exactMode ? ExactOnColor : ExactOffColor;
            btnExactly.Invalidate();
            _logger!.Info(_exactMode
                ? "Regra de correspondência: EXATA (nome-base idêntico)."
                : "Regra de correspondência: Schneider (X0 e variantes).");
        }

        private void ToggleBusbarMode()
        {
            _busbarMode           = !_busbarMode;
            btnBusbar.Text        = _busbarMode ? "Barramento (ON)" : "Barramento (OFF)";
            btnBusbar.FillColor   = _busbarMode ? BusbarOnColor : BusbarOffColor;
            btnBusbar.Invalidate();
            _logger!.Info(_busbarMode
                ? "Modo Barramento ATIVADO. Selecione o banco (.xlsx) acima."
                : "Modo Barramento DESATIVADO.");
        }

        // ════════════════════════════════════════════════════════════════
        //   HELPERS UTILITÁRIOS
        // ════════════════════════════════════════════════════════════════

        private static string[] SplitExtensions(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = new List<string>();
            foreach (var p in text.Split(new char[] { ',', ';', ' ' },
                                         StringSplitOptions.RemoveEmptyEntries))
            {
                string e = p.Trim();
                if (e.Length == 0) continue;
                if (!e.StartsWith(".")) e = "." + e;
                e = e.ToLowerInvariant();
                if (seen.Add(e)) list.Add(e);
            }
            return list.ToArray();
        }

        private void InvokeSetProgress(int value)
        {
            SafeInvoke(() =>
            {
                progress.Style = ProgressBarStyle.Continuous;
                progress.Value = Math.Max(0, Math.Min(100, value));
            });
        }

        private void ResetProgress()
        {
            SafeInvoke(() =>
            {
                btnStart.Enabled = true;
                progress.Style   = ProgressBarStyle.Continuous;
                progress.Value   = 0;
            });
        }

        /// <summary>
        /// Executa uma ação na thread da UI, independentemente de onde estamos.
        /// Substitui o padrão repetitivo if(InvokeRequired) Invoke(...) else ...
        /// </summary>
        private void SafeInvoke(Action action)
        {
            try
            {
                if (IsHandleCreated && InvokeRequired)
                    Invoke(action);
                else
                    action();
            }
            catch (ObjectDisposedException) { /* janela fechada; ignorar */ }
        }

        private static string? PickFolderModern(IWin32Window owner, string? initialDir)
        {
            using var dlg = new FolderBrowserDialog
            {
                Description            = "Selecione a pasta",
                UseDescriptionForTitle = true,
                ShowNewFolderButton    = true,
            };
            if (!string.IsNullOrEmpty(initialDir) && Directory.Exists(initialDir))
                dlg.SelectedPath = initialDir;
            return dlg.ShowDialog(owner) == DialogResult.OK ? dlg.SelectedPath : null;
        }
    }
}
