using System;
using System.Drawing;
using System.Windows.Forms;
using DrawingCollector.UI.Controls;

namespace DrawingCollector.UI.Theme
{
    /// <summary>
    /// Aplica o tema claro ou escuro a um formulário e todos os seus controles.
    /// </summary>
    /// <remarks>
    /// MELHORIA vs. versão anterior:
    /// O método anterior usava um varredor genérico-recursivo que tentava colorir
    /// tudo indiscriminadamente, às vezes sobrescrevendo a pintura customizada
    /// dos RoundedButton com cor errada.
    ///
    /// Agora usamos o padrão "visitante tipado": cada tipo de controle recebe
    /// o tratamento correto que ele precisa, nada mais.
    /// </remarks>
    public class ThemeManager
    {
        private readonly Form _form;

        // Referências diretas para controles que têm tratamento especial
        private readonly DataGridView? _grid;
        private readonly RichTextBox?  _rtbLog;

        public ThemeManager(Form form, DataGridView? grid = null, RichTextBox? rtbLog = null)
        {
            _form   = form   ?? throw new ArgumentNullException(nameof(form));
            _grid   = grid;
            _rtbLog = rtbLog;
        }

        /// <summary>
        /// Aplica o tema ao formulário e todos os controles filhos.
        /// </summary>
        public void Apply(bool dark)
        {
            // Seleciona a paleta correta para o tema
            Color bg      = dark ? Palette.DarkBackground : Palette.LightBackground;
            Color panelBg = dark ? Palette.DarkPanelBg    : Palette.LightBackground;
            Color fg      = dark ? Palette.DarkForeground  : Palette.LightForeground;
            Color inputBg = dark ? Palette.DarkInputBg     : Palette.LightInputBg;
            Color border  = dark ? Palette.DarkBorder      : Palette.LightBorder;

            // SuspendLayout evita que o Windows redesenhe a tela a cada controle colorido
            // (sem isso daria um "flash" enquanto cada controle muda de cor)
            _form.SuspendLayout();
            _form.BackColor = bg;

            // Aplica recursivamente nos filhos
            ApplyRecursive(_form, bg, panelBg, fg, inputBg, border);

            // DataGridView tem muitas sub-propriedades de cor — trata separado
            if (_grid != null)
            {
                _grid.EnableHeadersVisualStyles          = false;
                _grid.BackgroundColor                    = bg;
                _grid.GridColor                          = border;
                _grid.ColumnHeadersDefaultCellStyle.BackColor = panelBg;
                _grid.ColumnHeadersDefaultCellStyle.ForeColor = fg;
                _grid.ColumnHeadersDefaultCellStyle.Font      =
                    new Font("Segoe UI Semibold", 9.5f);
                _grid.DefaultCellStyle.BackColor         = inputBg;
                _grid.DefaultCellStyle.ForeColor         = fg;
                _grid.DefaultCellStyle.Font              = new Font("Segoe UI", 9f);
                _grid.RowHeadersVisible                  = false;
            }

            // RichTextBox do log
            if (_rtbLog != null)
            {
                _rtbLog.BackColor = inputBg;
                _rtbLog.ForeColor = fg;
            }

            _form.ResumeLayout(true);
            _form.Invalidate(true);
        }

        private static void ApplyRecursive(Control ctrl,
            Color bg, Color panelBg, Color fg, Color inputBg, Color border)
        {
            // Aplica a cor correta conforme o TIPO do controle
            if (ctrl is TextBox || ctrl is ListBox)
            {
                TrySetBackColor(ctrl, inputBg);
            }
            else if (ctrl is Panel || ctrl is TableLayoutPanel || ctrl is FlowLayoutPanel)
            {
                TrySetBackColor(ctrl, panelBg);
            }
            else if (ctrl is RoundedButton rb)
            {
                // RoundedButton se pinta sozinho via OnPaint — só atualizamos o ForeColor
                // e forçamos repaint; NÃO tocamos no BackColor pra não quebrar o gradiente
                rb.ForeColor = fg;
                rb.Invalidate();
                return; // não processa filhos (botões não têm filhos)
            }
            else if (ctrl is Button b && b.FlatStyle == FlatStyle.Flat)
            {
                TrySetBackColor(b, panelBg);
                b.ForeColor = fg;
                b.FlatAppearance.BorderColor = border;
            }
            else
            {
                TrySetBackColor(ctrl, bg);
            }

            ctrl.ForeColor = fg;

            // Processa os filhos recursivamente
            foreach (Control child in ctrl.Controls)
                ApplyRecursive(child, bg, panelBg, fg, inputBg, border);
        }

        /// <summary>
        /// Define BackColor de forma segura — alguns controles lançam exceção
        /// se tentarmos setar uma cor que eles não suportam (ex: TabControl no XP).
        /// </summary>
        private static void TrySetBackColor(Control ctrl, Color color)
        {
            try { ctrl.BackColor = color; }
            catch (Exception) { /* controle não suporta esta cor; ignorar */ }
        }
    }
}
