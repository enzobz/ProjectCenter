using System;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;
using DrawingCollector.Logging;

namespace DrawingCollector.Reports
{
    /// <summary>
    /// Uma divergência de revisão entre a lista e o banco oficial.
    /// </summary>
    public record RevisionDivergence(
        string Code,
        string ListRevision,      // revisão pedida na lista de entrada
        string OfficialRevision,  // revisão oficial do banco
        string Situation);        // descrição da situação

    /// <summary>
    /// Gera relatório Excel das divergências de revisão.
    /// </summary>
    public class DivergenceReporter
    {
        private readonly ILogger _logger;

        public DivergenceReporter(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Export(IReadOnlyList<RevisionDivergence> divergences, string outputDir)
        {
            if (divergences == null || divergences.Count == 0)
            {
                _logger.Ok("Nenhuma divergência de revisão encontrada.");
                return;
            }

            try
            {
                using var wb = new XLWorkbook();
                var ws = wb.AddWorksheet("Divergencias");

                // Cabeçalho
                ws.Cell(1, 1).Value = "Código";
                ws.Cell(1, 2).Value = "Revisão na Lista";
                ws.Cell(1, 3).Value = "Revisão Oficial";
                ws.Cell(1, 4).Value = "Situação";

                var header = ws.Range("A1:D1");
                header.Style.Font.Bold = true;
                header.Style.Fill.BackgroundColor = XLColor.LightSalmon;

                // Dados
                for (int i = 0; i < divergences.Count; i++)
                {
                    var d = divergences[i];
                    int row = i + 2;
                    ws.Cell(row, 1).Value = d.Code;
                    ws.Cell(row, 2).Value = d.ListRevision;
                    ws.Cell(row, 3).Value = d.OfficialRevision;
                    ws.Cell(row, 4).Value = d.Situation;
                }

                ws.Columns(1, 4).AdjustToContents();

                string path = Path.Combine(outputDir,
                    "divergencias_revisao_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx");
                wb.SaveAs(path);

                _logger.Warn($"{divergences.Count} divergência(s) de revisão encontrada(s). " +
                             $"Relatório: {Path.GetFileName(path)}");
            }
            catch (Exception ex)
            {
                _logger.Err($"Erro ao gerar relatório de divergências: {ex.Message}");
            }
        }
    }
}
