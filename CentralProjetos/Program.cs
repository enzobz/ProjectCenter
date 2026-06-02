using System;
using System.Windows.Forms;
using DrawingCollector.UI;

namespace DrawingCollector
{
    /// <summary>
    /// Ponto de entrada da aplicação.
    /// Responsabilidade: configurar o ambiente Windows Forms e abrir a janela principal.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            // Suporte a monitores de alta resolução (4K, etc.)
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

            // Habilita os estilos visuais modernos do Windows (botões com gradiente, etc.)
            Application.EnableVisualStyles();

            // Renderização de texto usando GDI+ (compatibilidade com .NET antigo)
            Application.SetCompatibleTextRenderingDefault(false);

            // Abre a janela principal e mantém o programa rodando até ela ser fechada
            Application.Run(new MainMenuForm());
        }
    }
}
