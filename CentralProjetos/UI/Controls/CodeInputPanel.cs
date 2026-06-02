using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DrawingCollector.Core;
using DrawingCollector.Logging;
using DrawingCollector.UI.Theme;

namespace DrawingCollector.UI.Controls
{
    /// <summary>
    /// Painel de entrada de códigos com 3 modos: colar, .txt e .xlsx.
    /// </summary>
    /// <remarks>
    /// Este controle encapsula TODA a lógica de "como o usuário fornece os códigos".
    /// O MainForm só precisa chamar GetCodes() para obter o array de strings,
    /// sem saber qual aba está ativa.
    ///
    /// Encapsulamento: o controle esconde a complexidade interna e expõe
    /// uma interface simples. Como um controle remoto: você aperta "ligar"
    /// sem saber o que acontece dentro da TV.
    /// </remarks>
    public class CodeInputPanel : UserControl
    {
        // ── Controles internos ───────────────────────────────────────────
        private TabControl    tabControl     = null!;
        private TabPage       tabPaste       = null!;
        private TabPage       tabTxt         = null!;
        private TabPage       tabXlsx        = null!;

        // Aba Colar
        private TextBox       txtPaste       = null!;
        private Label         lblPasteCount  = null!;

        // Aba .txt
        private TextBox       txtTxtPath     = null!;

        // Aba .xlsx
        private TextBox       txtXlsxPath    = null!;
        private ComboBox      cmbSheets      = null!;
        private ComboBox      cmbColumn      = null!;
        private ComboBox      cmbRevColumn   = null!;
        private CheckBox      chkHeader      = null!;

        // ── Logger (opcional — só usado ao carregar abas do Excel) ───────
        private ILogger? _logger;

        // ════════════════════════════════════════════════════════════════
        public CodeInputPanel()
        {
            Dock        = DockStyle.Fill;
            AutoSize    = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            BuildLayout();
        }

        /// <summary>
        /// Injeta o logger (chamado pelo MainForm após construção).
        /// </summary>
        public void SetLogger(ILogger logger) => _logger = logger;

        // ════════════════════════════════════════════════════════════════
        //   LAYOUT
        // ════════════════════════════════════════════════════════════════
        private void BuildLayout()
        {
            tabControl               = new TabControl();
            tabControl.Dock          = DockStyle.Fill;
            tabControl.Font          = new Font("Segoe UI", 9.5f);
            tabControl.Padding       = new Point(12, 4);

            tabPaste = new TabPage("📋  Colar");
            tabTxt   = new TabPage("📄  Arquivo .txt");
            tabXlsx  = new TabPage("📊  Excel .xlsx");

            BuildPasteTab();
            BuildTxtTab();
            BuildXlsxTab();

            tabControl.TabPages.Add(tabPaste);
            tabControl.TabPages.Add(tabTxt);
            tabControl.TabPages.Add(tabXlsx);

            Controls.Add(tabControl);

            // Altura mínima do controle inteiro
            MinimumSize = new Size(0, 120);
        }

        // ── Aba Colar ────────────────────────────────────────────────────
        private void BuildPasteTab()
        {
            var panel = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                Padding     = new Padding(4),
            };
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // textarea
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // rodapé

            // Área de texto multilinha onde o usuário cola os códigos
            txtPaste                  = new TextBox();
            txtPaste.Multiline        = true;
            txtPaste.ScrollBars       = ScrollBars.Vertical;
            txtPaste.Dock             = DockStyle.Fill;
            txtPaste.Font             = new Font("Consolas", 9.5f);
            txtPaste.PlaceholderText  = "Cole aqui os códigos (um por linha):\n\nABC123X0\nXYZ456X1\nLV-789_LV";
            txtPaste.TextChanged     += (_, __) => UpdatePasteCount();
            panel.Controls.Add(txtPaste, 0, 0);

            // Rodapé com contador e botão Limpar
            var footer = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize      = true,
                Padding       = new Padding(0, 2, 0, 0),
            };

            lblPasteCount = new Label
            {
                AutoSize  = true,
                ForeColor = Palette.Success,
                Font      = new Font("Segoe UI", 9f),
                Text      = "0 códigos",
            };

            var btnClear = new RoundedButton
            {
                Text        = "Limpar",
                MinimumSize = new Size(72, 24),
                Font        = new Font("Segoe UI Semibold", 8.5f),
                Margin      = new Padding(8, 0, 0, 0),
            };
            btnClear.Click += (_, __) => { txtPaste.Clear(); lblPasteCount.Text = "0 códigos"; };

            footer.Controls.Add(lblPasteCount);
            footer.Controls.Add(btnClear);
            panel.Controls.Add(footer, 0, 1);

            tabPaste.Controls.Add(panel);
        }

        private void UpdatePasteCount()
        {
            int count = txtPaste.Lines
                .Count(l => !string.IsNullOrWhiteSpace(l));
            lblPasteCount.Text      = $"{count} código{(count == 1 ? "" : "s")}";
            lblPasteCount.ForeColor = count > 0 ? Palette.Success : Palette.Warning;
        }

        // ── Aba .txt ─────────────────────────────────────────────────────
        private void BuildTxtTab()
        {
            var panel = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                Padding     = new Padding(4, 8, 4, 4),
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            txtTxtPath             = new TextBox();
            txtTxtPath.Dock        = DockStyle.Fill;
            txtTxtPath.PlaceholderText = "Selecione ou arraste um arquivo .txt...";
            panel.Controls.Add(txtTxtPath, 0, 0);

            var btnSelect = new RoundedButton { Text = "Selecionar", MinimumSize = new Size(100, 28) };
            btnSelect.Margin = new Padding(6, 0, 0, 0);
            btnSelect.Click += (_, __) =>
            {
                using var dlg = new OpenFileDialog
                {
                    Title  = "Selecionar lista de códigos",
                    Filter = "Arquivos de texto (*.txt)|*.txt|Todos os arquivos (*.*)|*.*",
                };
                if (dlg.ShowDialog() == DialogResult.OK)
                    txtTxtPath.Text = dlg.FileName;
            };
            panel.Controls.Add(btnSelect, 1, 0);

            // Suporte a arrastar arquivo para o campo
            txtTxtPath.AllowDrop  = true;
            txtTxtPath.DragEnter += OnDragEnter;
            txtTxtPath.DragDrop  += (s, e) =>
            {
                if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                    txtTxtPath.Text = files[0];
            };

            tabTxt.Controls.Add(panel);
        }

        // ── Aba .xlsx ────────────────────────────────────────────────────
        private void BuildXlsxTab()
        {
            // Layout geral da aba: linha 0 = arquivo, linha 1 = opções
            var panel = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                Padding     = new Padding(4, 8, 4, 4),
            };
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // arquivo
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // opções

            // Linha 0: arquivo + botão selecionar
            var fileRow = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Fill,
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            fileRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            txtXlsxPath                 = new TextBox();
            txtXlsxPath.Dock            = DockStyle.Fill;
            txtXlsxPath.PlaceholderText = "Selecione ou arraste um arquivo .xlsx...";
            txtXlsxPath.TextChanged    += OnXlsxPathChanged;
            fileRow.Controls.Add(txtXlsxPath, 0, 0);

            var btnSelectXlsx = new RoundedButton { Text = "Selecionar", MinimumSize = new Size(100, 28) };
            btnSelectXlsx.Margin = new Padding(6, 0, 0, 0);
            btnSelectXlsx.Click += (_, __) =>
            {
                using var dlg = new OpenFileDialog
                {
                    Title  = "Selecionar planilha Excel",
                    Filter = "Excel (*.xlsx)|*.xlsx|Todos os arquivos (*.*)|*.*",
                };
                if (dlg.ShowDialog() == DialogResult.OK)
                    txtXlsxPath.Text = dlg.FileName;
            };
            fileRow.Controls.Add(btnSelectXlsx, 1, 0);

            // Suporte a arrastar arquivo
            txtXlsxPath.AllowDrop  = true;
            txtXlsxPath.DragEnter += OnDragEnter;
            txtXlsxPath.DragDrop  += (s, e) =>
            {
                if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                    txtXlsxPath.Text = files[0];
            };

            panel.Controls.Add(fileRow, 0, 0);

            // Linha 1: opções (coluna, aba, cabeçalho)
            var optsRow = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize      = true,
                AutoSizeMode  = AutoSizeMode.GrowAndShrink,
                Padding       = new Padding(0, 6, 0, 0),
                WrapContents  = false,
            };

            optsRow.Controls.Add(MakeLabel("Aba:"));
            cmbSheets            = new ComboBox();
            cmbSheets.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbSheets.Width      = 130;
            cmbSheets.Margin     = new Padding(0, 0, 10, 0);
            optsRow.Controls.Add(cmbSheets);

            optsRow.Controls.Add(MakeLabel("Coluna:"));
            cmbColumn               = new ComboBox();
            cmbColumn.DropDownStyle = ComboBoxStyle.DropDown; // permite digitar
            cmbColumn.Width         = 60;
            cmbColumn.Margin        = new Padding(0, 0, 10, 0);

            // Pré-popula A..Z como sugestões
            for (char c = 'A'; c <= 'Z'; c++)
                cmbColumn.Items.Add(c.ToString());
            cmbColumn.SelectedIndex = 0; // padrão: coluna A

            optsRow.Controls.Add(cmbColumn);

            // Coluna de revisão (opcional — para validação de revisão)
            optsRow.Controls.Add(MakeLabel("Col. REV:"));
            cmbRevColumn               = new ComboBox();
            cmbRevColumn.DropDownStyle = ComboBoxStyle.DropDown;
            cmbRevColumn.Width         = 60;
            cmbRevColumn.Margin        = new Padding(0, 0, 10, 0);
            cmbRevColumn.Items.Add(""); // vazio = sem validação de revisão
            for (char c = 'A'; c <= 'Z'; c++)
                cmbRevColumn.Items.Add(c.ToString());
            cmbRevColumn.SelectedIndex = 0; // padrão: vazio
            optsRow.Controls.Add(cmbRevColumn);

            chkHeader           = new CheckBox();
            chkHeader.Text      = "Tem cabeçalho (pular 1ª linha)";
            chkHeader.Checked   = true;
            chkHeader.AutoSize  = true;
            chkHeader.Margin    = new Padding(4, 2, 0, 0);
            optsRow.Controls.Add(chkHeader);

            panel.Controls.Add(optsRow, 0, 1);
            tabXlsx.Controls.Add(panel);
        }

        // Quando o caminho do xlsx muda, recarrega as abas disponíveis
        private void OnXlsxPathChanged(object? sender, EventArgs e)
        {
            cmbSheets.Items.Clear();
            string path = txtXlsxPath.Text.Trim();
            if (string.IsNullOrEmpty(path)) return;

            // Usa um logger nulo se não tiver sido injetado ainda
            var reader = new CodeReader(_logger ?? new NullLogger());
            string[] sheets = reader.GetSheetNames(path);

            foreach (string s in sheets)
                cmbSheets.Items.Add(s);

            if (cmbSheets.Items.Count > 0)
                cmbSheets.SelectedIndex = 0;
        }

        // ── Drag & Drop helper ───────────────────────────────────────────
        private static void OnDragEnter(object? sender, DragEventArgs e)
        {
            // Aceita só se for arquivo(s) sendo arrastado(s)
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        // ── Label helper ─────────────────────────────────────────────────
        private static Label MakeLabel(string text) => new Label
        {
            Text      = text,
            AutoSize  = true,
            Font      = new Font("Segoe UI Semibold", 9.5f),
            Margin    = new Padding(0, 4, 4, 0),
        };

        // ════════════════════════════════════════════════════════════════
        //   API PÚBLICA: o MainForm só precisa destas duas propriedades
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Índice da aba ativa (0=Colar, 1=.txt, 2=.xlsx).
        /// </summary>
        public int ActiveMode => tabControl.SelectedIndex;



        /// <summary>
        /// Texto bruto da aba Colar. Usado pelo gerador de listas, que precisa
        /// da tabela inteira e não apenas dos códigos.
        /// </summary>
        public string GetPastedText() => txtPaste.Text;

        /// <summary>
        /// Caminho bruto da aba .txt.
        /// </summary>
        public string GetTxtPath() => txtTxtPath.Text.Trim();

        /// <summary>
        /// Caminho bruto da aba Excel.
        /// </summary>
        public string GetXlsxPath() => txtXlsxPath.Text.Trim();

        /// <summary>
        /// Aba selecionada no Excel. Se nada estiver selecionado, retorna vazio.
        /// </summary>
        public string GetSelectedSheetName() => cmbSheets.SelectedItem?.ToString() ?? string.Empty;

        /// <summary>
        /// Retorna os códigos conforme a aba ativa, usando o CodeReader.
        /// </summary>
        public string[] GetCodes(ILogger logger)
        {
            var reader = new CodeReader(logger);

            return tabControl.SelectedIndex switch
            {
                0 => reader.FromText(txtPaste.Text),
                1 => reader.FromTxt(txtTxtPath.Text.Trim()),
                2 => reader.FromXlsx(
                         txtXlsxPath.Text.Trim(),
                         cmbSheets.SelectedItem?.ToString(),
                         cmbColumn.Text.Trim(),
                         chkHeader.Checked),
                _ => Array.Empty<string>(),
            };
        }

        /// <summary>
        /// Retorna códigos COM revisão. Só o modo Excel (aba 2) pode trazer
        /// revisão; os outros modos retornam revisão vazia.
        /// </summary>
        public Core.Models.CodeWithRevision[] GetCodesWithRevision(ILogger logger)
        {
            var reader = new CodeReader(logger);

            // Modo Excel com coluna REV configurada
            if (tabControl.SelectedIndex == 2)
            {
                return reader.FromXlsxWithRevision(
                    txtXlsxPath.Text.Trim(),
                    cmbSheets.SelectedItem?.ToString(),
                    cmbColumn.Text.Trim(),
                    cmbRevColumn.Text.Trim(),   // pode ser vazio
                    chkHeader.Checked);
            }

            // Outros modos: códigos sem revisão
            return GetCodes(logger)
                .Select(c => new Core.Models.CodeWithRevision(c, string.Empty))
                .ToArray();
        }

        // ════════════════════════════════════════════════════════════════
        //   TEMA ESCURO
        //   O ThemeManager aplica cores recursivamente, mas o TabControl
        //   tem quirks — precisamos de ajuda extra aqui.
        // ════════════════════════════════════════════════════════════════
        public void ApplyTheme(bool dark)
        {
            Color bg      = dark ? Palette.DarkBackground  : Palette.LightBackground;
            Color inputBg = dark ? Palette.DarkInputBg     : Palette.LightInputBg;
            Color fg      = dark ? Palette.DarkForeground  : Palette.LightForeground;

            tabControl.BackColor = bg;

            foreach (TabPage tab in tabControl.TabPages)
            {
                tab.BackColor = bg;
                ApplyThemeRecursive(tab, bg, inputBg, fg);
            }

            // RichTextBox / TextBox no modo escuro
            txtPaste.BackColor = inputBg;
            txtPaste.ForeColor = fg;
            txtTxtPath.BackColor  = inputBg;
            txtTxtPath.ForeColor  = fg;
            txtXlsxPath.BackColor = inputBg;
            txtXlsxPath.ForeColor = fg;
            cmbSheets.BackColor   = inputBg;
            cmbSheets.ForeColor   = fg;
            cmbColumn.BackColor   = inputBg;
            cmbColumn.ForeColor   = fg;
        }

        private static void ApplyThemeRecursive(Control c, Color bg, Color inputBg, Color fg)
        {
            if (c is TextBox || c is ComboBox || c is ListBox)
            {
                try { c.BackColor = inputBg; } catch { }
            }
            else if (c is RoundedButton)
            {
                c.ForeColor = fg; c.Invalidate(); return;
            }
            else
            {
                try { c.BackColor = bg; } catch { }
            }
            c.ForeColor = fg;
            foreach (Control child in c.Controls)
                ApplyThemeRecursive(child, bg, inputBg, fg);
        }
    }
}
