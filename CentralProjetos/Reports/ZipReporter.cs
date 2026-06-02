using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using DrawingCollector.Logging;

namespace DrawingCollector.Reports
{
    /// <summary>
    /// Gera um arquivo ZIP com o conteúdo da pasta de destino,
    /// excluindo arquivos de log/temporários.
    /// </summary>
    public class ZipReporter
    {
        private static readonly HashSet<string> SkipExtensions =
            new(new[] { ".log", ".zip", ".txt" }, StringComparer.OrdinalIgnoreCase);

        private readonly ILogger _logger;

        public ZipReporter(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Cria um ZIP no diretório-pai do outputDir, com timestamp no nome.
        /// </summary>
        /// <remarks>
        /// MELHORIA vs. versão anterior:
        /// Antes copiávamos tudo pra uma pasta temp, depois zipávamos. Isso
        /// duplicava todos os arquivos em disco. Agora adicionamos direto
        /// no ZIP usando ZipArchive — mais rápido, menos disco.
        /// </remarks>
        public void GenerateZip(string outputDir)
        {
            try
            {
                if (!Directory.Exists(outputDir))
                {
                    _logger.Err("ERRO: pasta de destino inexistente.");
                    return;
                }

                string parent = Directory.GetParent(outputDir)!.FullName;
                string zipPath = Path.Combine(parent,
                    Path.GetFileName(outputDir) + "_coletados_"
                    + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".zip");

                if (File.Exists(zipPath)) File.Delete(zipPath);

                // ZipArchive permite adicionar arquivos diretamente, sem pasta temp
                using (var fs = new FileStream(zipPath, FileMode.CreateNew))
                using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    // EnumerateFiles é streaming (não carrega tudo na memória de uma vez)
                    foreach (string srcPath in Directory.EnumerateFiles(
                                 outputDir, "*", SearchOption.AllDirectories))
                    {
                        string ext = Path.GetExtension(srcPath).ToLowerInvariant();
                        if (SkipExtensions.Contains(ext)) continue;

                        // Caminho relativo dentro do ZIP
                        string entryName = Path.GetRelativePath(outputDir, srcPath)
                            .Replace(Path.DirectorySeparatorChar, '/');

                        zip.CreateEntryFromFile(srcPath, entryName, CompressionLevel.Optimal);
                    }
                }

                _logger.Ok("ZIP gerado: " + zipPath);
            }
            catch (Exception ex)
            {
                _logger.Err("Falha ao gerar ZIP: " + ex.Message);
            }
        }
    }
}
