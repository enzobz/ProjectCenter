using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using DrawingCollector.UI.Theme;

namespace DrawingCollector.UI.Controls
{
    /// <summary>
    /// Painel decorativo no topo do formulário com gradiente de 3 cores
    /// (azul escuro → azul médio → laranja) e um título centralizado.
    /// </summary>
    public class GradientHeader : Panel
    {
        public string Title        { get; set; } = "DC";
        public int    CornerRadius { get; set; } = 18;
        public Color  StartColor   { get; set; } = Palette.Primary;
        public Color  MidColor     { get; set; } = Palette.PrimaryMid;
        public Color  EndColor     { get; set; } = Palette.Accent;
        public Font   TitleFont    { get; set; } = new Font("Segoe UI Black", 28f, FontStyle.Bold);

        public GradientHeader()
        {
            // DoubleBuffered = true elimina o piscar (flicker) durante o redraw
            DoubleBuffered = true;
            Height         = 80;
            Padding        = new Padding(24, 16, 24, 16);
            BackColor      = Color.White;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;

            // Configura o motor gráfico para máxima qualidade
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.PixelOffsetMode   = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // Limpa o fundo com a cor do pai (pra parecer integrado ao formulário)
            try   { g.Clear(Parent?.BackColor ?? BackColor); }
            catch { g.Clear(BackColor); }

            // Desenha o retângulo arredondado com gradiente
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path  = Rounded(rect, CornerRadius))
            using (var brush = new LinearGradientBrush(rect, StartColor, EndColor, 0f))
            {
                // ColorBlend permite controlar exatamente onde cada cor aparece
                // no gradiente. Aqui: StartColor em 0%, MidColor em 48%, EndColor em 100%.
                brush.InterpolationColors = new ColorBlend(3)
                {
                    Colors    = new[] { StartColor, MidColor, EndColor },
                    Positions = new[] { 0f, 0.48f, 1f },
                };
                g.FillPath(brush, path);
            }

            // Desenha o título em branco com ClearType (TextRenderer > DrawString em DPI alto)
            var titleRect = new Rectangle(24, 0, Width - 48, Height);
            TextRenderer.DrawText(
                g,
                Title ?? "",
                TitleFont,
                titleRect,
                Color.White,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate(); // força redesenho ao redimensionar
        }

        /// <summary>
        /// Constrói um caminho gráfico com cantos arredondados.
        /// </summary>
        private static GraphicsPath Rounded(Rectangle r, int radius)
        {
            var gp = new GraphicsPath();
            int d  = radius * 2;
            gp.AddArc(r.X,         r.Y,          d, d, 180, 90);
            gp.AddArc(r.Right - d, r.Y,          d, d, 270, 90);
            gp.AddArc(r.Right - d, r.Bottom - d, d, d,   0, 90);
            gp.AddArc(r.X,         r.Bottom - d, d, d,  90, 90);
            gp.CloseFigure();
            return gp;
        }
    }
}
