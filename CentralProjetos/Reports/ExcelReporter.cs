using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using DrawingCollector.Logging;

namespace DrawingCollector.Reports
{
    /// <summary>
    /// Gera relatório Excel de códigos não encontrados, agrupados por extensão.
    /// </summary>
    /// <remarks>
    /// MELHORIA vs. versão anterior:
    /// Antes usava COM Interop (Excel.Application via registro do Windows),
    /// que exigia Microsoft Excel instalado, abria um processo invisível e
    /// podia deixar EXCEL.EXE travado em memória se desse erro.
    ///
    /// Agora usa ClosedXML — biblioteca NuGet que lê/escreve .xlsx
    /// sem precisar de Excel instalado. É mais rápida, mais segura e o
    /// código fica muito mais simples.
    ///
    /// Para adicionar ao projeto: dotnet add package ClosedXML
    /// </remarks>
    public class ExcelReporter
    {
        private readonly ILogger _logger;

        public ExcelReporter(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gera um .xlsx com uma aba por extensão, listando os códigos faltantes.
        /// </summary>
        public void ExportMissingByExt(
            Dictionary<string, List<string>> missingByExt,
            string outputDir)
        {
            // Só gera o arquivo se houver pelo menos um faltante
            if (missingByExt == null || !missingByExt.Values.Any(v => v?.Count > 0))
            {
                _logger.Info("Nenhum faltante por extensão para exportar.");
                return;
            }

            try
            {
                using var wb = new XLWorkbook();

                foreach (var kv in missingByExt.Where(k => k.Value?.Count > 0))
                {
                    string sheetName = SanitizeSheetName(kv.Key);
                    var ws = wb.AddWorksheet(sheetName);

                    // Cabeçalho
                    ws.Cell(1, 1).Value = $"Código faltante em {kv.Key}";
                    ws.Cell(1, 1).Style.Font.Bold = true;
                    ws.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.LightYellow;

                    // Dados
                    for (int i = 0; i < kv.Value.Count; i++)
                        ws.Cell(i + 2, 1).Value = kv.Value[i];

                    ws.Column(1).AdjustToContents();
                }

                string path = Path.Combine(outputDir,
                    "faltantes_por_extensao_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx");

                wb.SaveAs(path);
                _logger.Ok("Relatório Excel (faltantes por extensão) gerado: " + path);
            }
            catch (Exception ex)
            {
                _logger.Err("Erro ao exportar Excel: " + ex.Message);
            }
        }

        /// <summary>
        /// Sanitiza o nome da aba do Excel — Excel tem várias restrições de caracteres.
        /// Nomes de aba não podem ter: / \ ? * [ ] :
        /// Máximo de 31 caracteres. Não pode ser vazio.
        /// </summary>
        private static string SanitizeSheetName(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "semext";

            // Remove caracteres proibidos pelo Excel em nomes de aba
            var invalid = new[] { '/', '\\', '?', '*', '[', ']', ':' };
            string name = raw;
            foreach (char c in invalid)
                name = name.Replace(c, '-');

            // Remove aspas simples no início/fim (proibido pelo Excel)
            name = name.Trim('\'');

            // Trunca em 31 caracteres
            if (name.Length > 31) name = name.Substring(0, 31);

            // Se ficou vazio após tudo, usa fallback
            return string.IsNullOrWhiteSpace(name) ? "semext" : name;
        }
    }
}
