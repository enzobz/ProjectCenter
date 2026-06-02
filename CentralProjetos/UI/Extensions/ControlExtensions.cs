using System.Reflection;
using System.Windows.Forms;

namespace DrawingCollector.UI.Extensions
{
    /// <summary>
    /// Métodos auxiliares para controles WinForms.
    /// </summary>
    /// <remarks>
    /// A propriedade DoubleBuffered de Control é protegida (não pública), então
    /// para ativá-la em qualquer controle precisamos usar reflection — que é a
    /// capacidade de inspecionar/modificar membros não-públicos em tempo de execução.
    ///
    /// DoubleBuffered = true reduz o "piscar" (flicker) ao redimensionar/redesenhar.
    /// </remarks>
    internal static class ControlExtensions
    {
        /// <summary>
        /// Ativa (ou desativa) o double buffering em qualquer controle.
        /// Uso: meuPainel.SetDoubleBuffered();
        /// </summary>
        public static void SetDoubleBuffered(this Control c, bool value = true)
        {
            typeof(Control)
                .GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(c, value);
        }
    }
}
