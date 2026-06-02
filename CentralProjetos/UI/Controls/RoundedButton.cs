using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using DrawingCollector.UI.Theme;

namespace DrawingCollector.UI.Controls
{
    /// <summary>
    /// Botão com cantos arredondados, hover e estado pressionado.
    /// Herda de Button do Windows Forms e sobrescreve a renderização (OnPaint)
    /// para desenhar com gráficos vetoriais e antialiasing.
    /// </summary>
    public class RoundedButton : Button
    {
        // ── Propriedades de aparência ────────────────────────────────────
        public int   CornerRadius { get; set; } = 10;
        public Color FillColor    { get; set; } = Palette.Accent;
        public Color BorderColor  { get; set; } = Palette.AccentBorder;
        public Color HoverFill    { get; set; } = Palette.AccentHover;
        public Color PressedFill  { get; set; } = Palette.AccentPressed;

        // ── Estado interno (não exposto) ─────────────────────────────────
        // Os underlines no início (_hover, _down) marcam que são campos privados.
        // É uma convenção C# muito comum.
        private bool _hover;
        private bool _down;

        public RoundedButton()
        {
            // FlatStyle.Flat remove a aparência padrão do Windows pra podermos pintar do zero
            FlatStyle                 = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;

            ForeColor      = Color.White;
            BackColor      = Color.Transparent;
            Padding        = new Padding(12, 6, 12, 6);
            AutoSize       = true;
            AutoSizeMode   = AutoSizeMode.GrowAndShrink;
            MinimumSize    = new Size(96, 36);
            Font           = new Font("Segoe UI Semibold", 9.5f);
            DoubleBuffered = true;
            Cursor         = Cursors.Hand;

            // Eventos de mouse atualizam o estado e disparam um repaint via Invalidate()
            MouseEnter += (_, __) => { _hover = true;                  Invalidate(); };
            MouseLeave += (_, __) => { _hover = false; _down = false;  Invalidate(); };
            MouseDown  += (_, __) => { _down  = true;                  Invalidate(); };
            MouseUp    += (_, __) => { _down  = false;                 Invalidate(); };
        }

        // Quando o botão é redimensionado, recalculamos a região arredondada
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            using var path = Rounded(new Rectangle(0, 0, Width - 1, Height - 1), CornerRadius);
            Region = new Region(path);
        }

        // Pinta o fundo do botão acompanhando a cor do controle pai
        // (importante para o botão "se misturar" com o painel onde ele está)
        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            using var br = new SolidBrush(Parent?.BackColor ?? BackColor);
            pevent.Graphics.FillRectangle(br, ClientRectangle);
        }

        // Calcula o tamanho ideal baseado no texto + padding
        public override Size GetPreferredSize(Size proposed)
        {
            var sz = TextRenderer.MeasureText(Text ?? string.Empty, Font);
            return new Size(
                Math.Max(sz.Width  + Padding.Horizontal + 4, MinimumSize.Width),
                Math.Max(sz.Height + Padding.Vertical   + 4, MinimumSize.Height));
        }

        // Pintura principal do botão (chamada toda vez que precisa redesenhar)
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode   = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = Rounded(rect, CornerRadius);

            // Escolhe a cor de fundo conforme o estado:
            // pressionado > hover > normal
            var fillCol = _down ? PressedFill : (_hover ? HoverFill : FillColor);

            using (var fill = new SolidBrush(fillCol))
            using (var pen  = new Pen(BorderColor))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen,  path);
            }

            // Desenha o texto centralizado
            TextRenderer.DrawText(g, Text, Font,
                new Rectangle(
                    Padding.Left,
                    Padding.Top,
                    Width  - Padding.Horizontal,
                    Height - Padding.Vertical),
                ForeColor,
                TextFormatFlags.HorizontalCenter
                    | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.EndEllipsis);
        }

        /// <summary>
        /// Constrói um caminho gráfico com cantos arredondados.
        /// Usado tanto pelo Region quanto pelo OnPaint.
        /// </summary>
        private static GraphicsPath Rounded(Rectangle r, int radius)
        {
            var gp = new GraphicsPath();
            int d  = radius * 2;
            gp.AddArc(r.X,         r.Y,          d, d, 180, 90);  // canto superior esquerdo
            gp.AddArc(r.Right - d, r.Y,          d, d, 270, 90);  // canto superior direito
            gp.AddArc(r.Right - d, r.Bottom - d, d, d,   0, 90);  // canto inferior direito
            gp.AddArc(r.X,         r.Bottom - d, d, d,  90, 90);  // canto inferior esquerdo
            gp.CloseFigure();
            return gp;
        }
    }
}
