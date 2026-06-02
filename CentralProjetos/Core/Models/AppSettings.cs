namespace DrawingCollector.Core.Models
{
    /// <summary>
    /// Configurações do programa salvas entre sessões.
    /// Salvo em: %APPDATA%\DrawingCollector\config.json
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// Caminho do banco Excel de barramentos.
        /// </summary>
        public string BusbarDatabasePath { get; set; } = string.Empty;

        /// <summary>
        /// Caminho do banco Excel de revisões oficiais.
        /// </summary>
        public string RevisionDatabasePath { get; set; } = string.Empty;
    }
}
