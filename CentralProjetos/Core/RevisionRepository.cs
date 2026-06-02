using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using DrawingCollector.Logging;

namespace DrawingCollector.Core
{
    /// <summary>
    /// Lê o Excel banco de revisões oficiais e responde consultas.
    /// </summary>
    /// <remarks>
    /// Estrutura do Excel banco:
    ///   Coluna A = Codigo  | Coluna B = Revisao (oficial/atual)
    ///   Linha 1 = cabeçalho (pulado)
    ///
    /// Mesma arquitetura do BusbarRepository:
    ///   1. LoadFromFile(path) — lê o Excel uma vez
    ///   2. TryGetOfficialRevision(code) — consulta O(1)
    ///
    /// Compara a revisão pedida na lista de entrada com a revisão oficial.
    /// Se divergirem, o código entra no relatório de divergências.
    /// </remarks>
    public class RevisionRepository
    {
        private readonly ILogger _logger;

        // Codigo (maiúsculas) → Revisao oficial
        private Dictionary<string, string> _revisions =
            new(StringComparer.OrdinalIgnoreCase);

        public int  Count    => _revisions.Count;
        public bool IsLoaded { get; private set; }

        public RevisionRepository(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Lê o Excel banco de revisões e monta o dicionário em memória.
        /// </summary>
        public bool LoadFromFile(string? filePath)
        {
            IsLoaded   = false;
            _revisions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(filePath))
            {
                _logger.Info("Banco de revisões não informado — validação de revisão desativada.");
                return false;
            }

            if (!File.Exists(filePath))
            {
                _logger.Err($"Banco de revisões não encontrado: {filePath}");
                return false;
            }

            try
            {
                using var wb = new XLWorkbook(filePath);
                var ws = wb.Worksheets.First();

                int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
                int loaded  = 0;

                for (int row = 2; row <= lastRow; row++)
                {
                    string codigo = ws.Cell(row, 1).GetString().Trim().ToUpperInvariant();
                    string rev    = ws.Cell(row, 2).GetString().Trim();

                    if (string.IsNullOrEmpty(codigo)) continue;

                    // Em caso de duplicata, mantém o primeiro (e avisa só se conflitar)
                    if (_revisions.TryGetValue(codigo, out var existing))
                    {
                        if (!string.Equals(existing, rev, StringComparison.OrdinalIgnoreCase))
                            _logger.Warn($"[Revisões] Código duplicado com revisões diferentes na linha {row}: " +
                                         $"{codigo} ({existing} vs {rev}). Mantendo o primeiro.");
                        continue;
                    }

                    _revisions[codigo] = rev;
                    loaded++;
                }

                IsLoaded = true;
                _logger.Ok($"Banco de revisões carregado: {loaded} registros ← {Path.GetFileName(filePath)}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Err($"Erro ao ler banco de revisões: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Consulta a revisão oficial de um código.
        /// </summary>
        /// <returns>True se o código existe no banco.</returns>
        public bool TryGetOfficialRevision(string code, out string officialRevision)
        {
            string key = (code ?? "").Trim().ToUpperInvariant();
            if (_revisions.TryGetValue(key, out var rev))
            {
                officialRevision = rev;
                return true;
            }
            officialRevision = string.Empty;
            return false;
        }

        /// <summary>
        /// Gera um Excel modelo do banco de revisões.
        /// </summary>
        public static void GenerateTemplate(string outputPath)
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Revisoes");

            ws.Cell(1, 1).Value = "Codigo";
            ws.Cell(1, 2).Value = "Revisao";
            var header = ws.Range("A1:B1");
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.LightGreen;

            ws.Cell(2, 1).Value = "51127009X0";
            ws.Cell(2, 2).Value = "F";
            ws.Cell(3, 1).Value = "51132170X0";
            ws.Cell(3, 2).Value = "C";

            ws.Cell(1, 3).Value = "← Código do desenho";
            ws.Cell(2, 3).Value = "← Revisão oficial/atual (compara com a lista de entrada)";
            ws.Column(1).AdjustToContents();
            ws.Column(2).AdjustToContents();
            ws.Column(3).AdjustToContents();
            ws.Column(3).Style.Font.FontColor = XLColor.Gray;

            wb.SaveAs(outputPath);
        }
    }
}
