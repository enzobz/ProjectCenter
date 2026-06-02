using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace DrawingCollector.UI.Controls
{
    /// <summary>
    /// Pequeno círculo colorido com número/texto, usado nos indicadores de passo.
    /// Substitui o Label quadrado nos MakeStep do ListGeneratorForm e GuidedFinalizerForm.
    /// </summary>
    /// <remarks>
    /// Por que um controle separado em vez de um Label com borda arredondada?
    /// O Label não suporta bordas arredondadas nativamente — precisaríamos de
    /// OnPaint de qualquer jeito. Encapsular em um controle próprio mantém
    /// os forms limpos e a lógica de desenho em um único lugar.
    ///
    /// Nota sobre fundo transparente:
    /// WinForms não aceita BackColor = Color.Transparent no construtor —
    /// lança ArgumentException. A solução correta é usar SetStyle com
    /// SupportsTransparentBackColor + UserPaint, e deixar o OnPaintBackground
    /// copiar o fundo do controle pai manualmente.
    /// </remarks>
    public class CircleBadge : Control
    {
        private readonly string _text;
        private readonly Color  _color;

        public CircleBadge(string text, Color color)
        {
            _text = text;
            _color = color;

            // Habilita suporte real a fundo transparente no WinForms.
            // Deve ser chamado ANTES de atribuir BackColor = Transparent.
            SetStyle(
                ControlStyles.SupportsTransparentBackColor |
                ControlStyles.UserPaint                    |
                ControlStyles.AllPaintingInWmPaint         |
                ControlStyles.OptimizedDoubleBuffer,
                true);

            BackColor  = Color.Transparent; // agora é aceito
            TabStop    = false;
            Size       = new Size(34, 34);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Copia o fundo do pai para o círculo "flutuar" sobre o painel
            // sem deixar retângulo branco ao redor.
            if (Parent != null)
            {
                using var br = new SolidBrush(Parent.BackColor);
                e.Graphics.FillRectangle(br, ClientRectangle);
            }
            // Se não há pai ainda, não pinta nada — evita artefato na inicialização.
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // Círculo com 1px de margem para não cortar o antialiasing
            var rect = new Rectangle(1, 1, Width - 2, Height - 2);
            using (var br = new SolidBrush(_color))
                g.FillEllipse(br, rect);

            // Número centralizado em branco
            using var font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold);
            TextRenderer.DrawText(
                g, _text, font, rect, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}
