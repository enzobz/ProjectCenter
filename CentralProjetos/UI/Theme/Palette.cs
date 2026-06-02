using System.Drawing;

namespace DrawingCollector.UI.Theme
{
    /// <summary>
    /// Paleta de cores centralizada do aplicativo.
    /// Toda cor usada em qualquer lugar deve vir daqui — facilita rebranding
    /// e mantém consistência visual.
    /// </summary>
    /// <remarks>
    /// Por que constantes static readonly em vez de const?
    /// const só funciona para tipos primitivos (int, string, bool...).
    /// Color é um struct, então usamos static readonly: inicializado uma vez,
    /// nunca mais muda, compartilhado por toda a aplicação.
    /// </remarks>
    internal static class Palette
    {
        // ── Marca / Identidade visual ────────────────────────────────────
        public static readonly Color Primary       = Color.FromArgb(33, 56, 110);   // azul escuro
        public static readonly Color PrimaryMid    = Color.FromArgb(64, 72, 118);   // azul médio (gradiente)
        public static readonly Color Accent        = Color.FromArgb(224, 134, 53);  // laranja
        public static readonly Color AccentHover   = Color.FromArgb(233, 149, 78);
        public static readonly Color AccentPressed = Color.FromArgb(205, 120, 40);
        public static readonly Color AccentBorder  = Color.FromArgb(192, 105, 34);

        // ── Estados ──────────────────────────────────────────────────────
        public static readonly Color Success       = Color.SeaGreen;
        public static readonly Color Warning       = Color.Goldenrod;
        public static readonly Color Error         = Color.IndianRed;

        // ── Tema Claro ───────────────────────────────────────────────────
        public static readonly Color LightBackground = Color.White;
        public static readonly Color LightForeground = Color.FromArgb(32, 32, 32);
        public static readonly Color LightBorder     = Color.FromArgb(200, 200, 200);
        public static readonly Color LightInputBg    = Color.White;

        // ── Tema Escuro ──────────────────────────────────────────────────
        public static readonly Color DarkBackground  = Color.FromArgb(30,  30,  30);
        public static readonly Color DarkPanelBg     = Color.FromArgb(45,  45,  48);
        public static readonly Color DarkForeground  = Color.FromArgb(220, 220, 220);
        public static readonly Color DarkInputBg     = Color.FromArgb(37,  37,  38);
        public static readonly Color DarkBorder      = Color.FromArgb(80,  80,  80);
    }
}
