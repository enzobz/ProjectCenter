using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using DrawingCollector.UI.Theme;

namespace DrawingCollector.UI.Controls
{
    /// <summary>
    /// Card visual usado no dashboard principal da Central de Projetos.
    /// Ele funciona como um botão grande, com ícone, título, descrição e badge.
    /// </summary>
    public class ModuleCard : Panel
    {
        public string CardIcon { get; set; } = "•";
        public string CardTitle { get; set; } = "Módulo";
        public string CardSubtitle { get; set; } = "Descrição do módulo";
        public string CardBadge { get; set; } = string.Empty;
        public Color AccentColor { get; set; } = Palette.Accent;
        public int CornerRadius { get; set; } = 20;

        private bool _hover;
        private bool _down;

        public ModuleCard()
        {
            DoubleBuffered = true;
            Cursor = Cursors.Hand;
            MinimumSize = new Size(260, 150);
            // Sem Width fixo — o FlowLayoutPanel decide a largura.
            // O card se adapta de 260px (mínimo) até o espaço disponível.
            Margin = new Padding(12);
            Padding = new Padding(22);
            BackColor = Color.Transparent;

            MouseEnter += (_, __) => { _hover = true; Invalidate(); };
            MouseLeave += (_, __) => { _hover = false; _down = false; Invalidate(); };
            MouseDown += (_, __) => { _down = true; Invalidate(); };
            MouseUp += (_, __) => { _down = false; Invalidate(); };
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using var br = new SolidBrush(Parent?.BackColor ?? Palette.LightBackground);
            e.Graphics.FillRectangle(br, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            int offset = _down ? 2 : (_hover ? -1 : 0);
            var cardRect = new Rectangle(rect.X + 1, rect.Y + 1 + offset, rect.Width - 2, rect.Height - 3);

            // Sombra sutil
            if (!_down)
            {
                using var shadowPath = Rounded(new Rectangle(cardRect.X + 2, cardRect.Y + 4, cardRect.Width, cardRect.Height), CornerRadius);
                using var shadow = new SolidBrush(Color.FromArgb(_hover ? 34 : 20, 0, 0, 0));
                g.FillPath(shadow, shadowPath);
            }

            using var path = Rounded(cardRect, CornerRadius);
            using var fill = new SolidBrush(_hover ? Color.FromArgb(252, 252, 252) : Color.White);
            using var border = new Pen(_hover ? AccentColor : Color.FromArgb(226, 230, 238), _hover ? 2f : 1f);
            g.FillPath(fill, path);
            g.DrawPath(border, path);

            // Acento visual apenas no círculo e no badge.
            // A barra lateral foi removida para evitar vazamento de cor fora do card arredondado.

            // Círculo do ícone
            var iconCircle = new Rectangle(cardRect.X + 26, cardRect.Y + 26, 52, 52);
            using (var iconBg = new SolidBrush(Color.FromArgb(26, AccentColor)))
                g.FillEllipse(iconBg, iconCircle);

            using (var iconFont = new Font("Segoe UI Semibold", 20f, FontStyle.Bold))
            {
                TextRenderer.DrawText(
                    g,
                    CardIcon,
                    iconFont,
                    iconCircle,
                    AccentColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            var titleRect = new Rectangle(cardRect.X + 95, cardRect.Y + 28, cardRect.Width - 125, 32);
            using (var titleFont = new Font("Segoe UI Semibold", 13.5f))
            {
                TextRenderer.DrawText(g, CardTitle, titleFont, titleRect,
                    Color.FromArgb(30, 36, 52),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            var subtitleRect = new Rectangle(cardRect.X + 95, cardRect.Y + 64, cardRect.Width - 120, 58);
            using (var subFont = new Font("Segoe UI", 9.5f))
            {
                TextRenderer.DrawText(g, CardSubtitle, subFont, subtitleRect,
                    Color.FromArgb(92, 98, 112),
                    TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
            }

            if (!string.IsNullOrWhiteSpace(CardBadge))
            {
                using var badgeFont = new Font("Segoe UI Semibold", 8.5f);
                var badgeSize = TextRenderer.MeasureText(CardBadge, badgeFont);
                int badgeW = badgeSize.Width + 20;
                int badgeH = 24;
                // Garante que o badge não sai pela esquerda nem pela borda do card
                int badgeX = Math.Max(cardRect.X + 8, cardRect.Right - badgeW - 16);
                int badgeY = cardRect.Bottom - badgeH - 10;
                var badgeRect = new Rectangle(badgeX, badgeY, badgeW, badgeH);

                using var badgePath = Rounded(badgeRect, 12);
                using var badgeBg = new SolidBrush(Color.FromArgb(18, AccentColor));
                using var badgePen = new Pen(Color.FromArgb(90, AccentColor));
                g.FillPath(badgeBg, badgePath);
                g.DrawPath(badgePen, badgePath);
                TextRenderer.DrawText(g, CardBadge, badgeFont, badgeRect, AccentColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        private static GraphicsPath Rounded(Rectangle r, int radius)
        {
            var gp = new GraphicsPath();
            int d = radius * 2;
            gp.AddArc(r.X, r.Y, d, d, 180, 90);
            gp.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            gp.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            gp.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            gp.CloseFigure();
            return gp;
        }

        private static GraphicsPath RoundedLeft(Rectangle r, int radius)
        {
            var gp = new GraphicsPath();
            int d = radius * 2;
            gp.AddArc(r.X, r.Y, d, d, 180, 90);
            gp.AddLine(r.Right, r.Y, r.Right, r.Bottom);
            gp.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            gp.CloseFigure();
            return gp;
        }
    }
}
