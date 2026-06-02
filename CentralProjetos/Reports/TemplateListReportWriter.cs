using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using DrawingCollector.Core;
using DrawingCollector.Core.Models;
using DrawingCollector.Logging;

namespace DrawingCollector.Reports
{
    /// <summary>
    /// Gera os arquivos finais usando os templates reais da empresa.
    /// </summary>
    /// <remarks>
    /// Saídas geradas:
    /// 1) Lista geral: usa o template LEPXXXX-PXXXX-C0X-CH-R00.xlsx
    ///    e preenche todas as colunas A:N, no mesmo estilo da lista padrão.
    ///
    /// 2) Lista do fornecedor: usa o template LEP2XXXX-LISTA GERAL-FORNECEDOR-CH-R00.xlsx
    ///    e preenche somente as colunas:
    ///    ITEM | REFERÊNCIA | REV. | DATA REV. | DESCRIÇÃO.
    ///
    /// 3) Lista de barramento: usa o template LEPXXXX-PXXXX-C0X-BA-R00.xlsx,
    ///    preenche a aba DADOS e mantém a aba COMPRA BARRAMENTO com as fórmulas.
    ///
    /// Observação importante do template de barramento:
    /// - A aba DADOS usa a coluna N como comprimento numérico;
    /// - A coluna O = N + 15;
    /// - A coluna P = O * QTDE.;
    /// - A aba COMPRA BARRAMENTO soma a coluna P por material/largura/espessura.
    /// </remarks>
    public class TemplateListReportWriter
    {
        private readonly ILogger _logger;

        private const string GeneralListSheet = "Lista geral";
        private const string SupplierDefaultSheet = "Plan1";
        private const string BusbarDataSheet = "DADOS";
        private const string BusbarPurchaseSheet = "COMPRA BARRAMENTO";

        public TemplateListReportWriter(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Resultado com os caminhos dos arquivos gerados.
        /// </summary>
        public record TemplateOutputResult(
            bool Success,
            string GeneralPath,
            string SupplierPath,
            string BusbarPath
        );

        /// <summary>
        /// Gera o Excel do fornecedor e o Excel de barramento usando templates.
        /// </summary>
        public TemplateOutputResult WriteFromTemplates(
            ListSeparatorService.SeparationResult result,
            string generalTemplatePath,
            string supplierTemplatePath,
            string busbarTemplatePath,
            string outputDirectory,
            string outputBaseName,
            IReadOnlyList<string[]>? generalTopRows = null,
            string revision = "R00",
            string projectTitle = "")
        {
            if (result == null)
            {
                _logger.Err("Geração por template: resultado nulo.");
                return new TemplateOutputResult(false, string.Empty, string.Empty, string.Empty);
            }

            if (!File.Exists(generalTemplatePath))
            {
                _logger.Err($"Template da lista geral não encontrado: {generalTemplatePath}");
                return new TemplateOutputResult(false, string.Empty, string.Empty, string.Empty);
            }

            if (!File.Exists(supplierTemplatePath))
            {
                _logger.Err($"Template do fornecedor não encontrado: {supplierTemplatePath}");
                return new TemplateOutputResult(false, string.Empty, string.Empty, string.Empty);
            }

            if (!File.Exists(busbarTemplatePath))
            {
                _logger.Err($"Template de barramento não encontrado: {busbarTemplatePath}");
                return new TemplateOutputResult(false, string.Empty, string.Empty, string.Empty);
            }

            if (string.IsNullOrWhiteSpace(outputDirectory))
                outputDirectory = Directory.GetCurrentDirectory();

            Directory.CreateDirectory(outputDirectory);

            string safeBase = MakeSafeFileName(outputBaseName);
            if (string.IsNullOrWhiteSpace(safeBase)) safeBase = "LISTAS";

            string safeRev = MakeSafeFileName(revision);
            if (string.IsNullOrWhiteSpace(safeRev)) safeRev = "R00";

            // Nomes no padrão do cliente: <CÓDIGO>-LISTA_GERAL-CH-<REV>.xlsx
            string generalOut  = Path.Combine(outputDirectory, $"{safeBase}-LISTA_GERAL-CH-{safeRev}.xlsx");
            string supplierOut = Path.Combine(outputDirectory, $"{safeBase}-FORNECEDOR-CH-{safeRev}.xlsx");
            string busbarOut   = Path.Combine(outputDirectory, $"{safeBase}-BA-{safeRev}.xlsx");

            bool generalOk  = WriteGeneral(result.Todos, generalTemplatePath, generalOut, generalTopRows, projectTitle, safeBase, safeRev);
            bool supplierOk = WriteSupplier(result.Fornecedor,  supplierTemplatePath, supplierOut);
            bool busbarOk   = WriteBusbar(result.Barramento,    busbarTemplatePath,   busbarOut);

            return new TemplateOutputResult(generalOk && supplierOk && busbarOk, generalOut, supplierOut, busbarOut);
        }


        // ── LISTA GERAL ─────────────────────────────────────────────────

        private bool WriteGeneral(
            List<GeneralListItem> todos,
            string templatePath,
            string outputPath,
            IReadOnlyList<string[]>? topRows = null,
            string projectTitle = "",
            string outputBaseName = "",
            string revision = "R00")
        {
            try
            {
                using var wb = new XLWorkbook(templatePath);

                if (!wb.TryGetWorksheet(GeneralListSheet, out var ws))
                {
                    _logger.Err($"Template da lista geral sem aba '{GeneralListSheet}'.");
                    return false;
                }

                const int headerRow = 3;
                const int firstDataRow = 4;
                const int lastCol = 14;

                int lastUsed = ws.LastRowUsed()?.RowNumber() ?? headerRow;
                int clearUntil = Math.Max(lastUsed, firstDataRow + todos.Count + 20);

                // Atualiza cabeçalho do projeto (F1 = título, F2 = cor do painel)
                ApplyTopRows(ws, topRows, rowsToWrite: 2, lastCol: lastCol,
                             projectTitle: projectTitle,
                             outputBaseName: outputBaseName, revision: revision);

                ws.Range(firstDataRow, 1, clearUntil, lastCol).Clear(XLClearOptions.Contents);

                int row = firstDataRow;
                foreach (var item in todos)
                {
                    CopyRowStyle(ws, firstDataRow, row, lastCol);

                    SetNumberOrText(ws.Cell(row, 1), item.Item, ParseNumber(item.Item));
                    SetNumberOrText(ws.Cell(row, 2), item.Qtde, item.QtdeNum);
                    ws.Cell(row, 3).Value = ToCellText(item.Referencia);
                    ws.Cell(row, 4).Value = ToCellText(item.Rev);
                    SetDateOrText(ws.Cell(row, 5), item.DataRev);
                    ws.Cell(row, 6).Value = ToCellText(item.Descricao);
                    ws.Cell(row, 7).Value = ToCellText(item.Obs);
                    ws.Cell(row, 8).Value = ToCellText(item.Material);
                    ws.Cell(row, 9).Value = ToCellText(item.Largura);
                    ws.Cell(row, 10).Value = ToCellText(item.Comprimento);
                    ws.Cell(row, 11).Value = ToCellText(item.Espessura);
                    ws.Cell(row, 12).Value = ToCellText(item.PesoUni);
                    ws.Cell(row, 13).Value = ToCellText(item.PesoTotal);
                    ws.Cell(row, 14).Value = ToCellText(item.Pintura);
                    row++;
                }

                ApplyTableBorders(ws, headerRow, 1, Math.Max(row - 1, headerRow), lastCol);
                ws.Columns(1, lastCol).AdjustToContents();
                LimitWidth(ws.Column(6), 70);
                LimitWidth(ws.Column(7), 55);

                SaveWorkbook(wb, outputPath);
                _logger.Ok($"Arquivo da lista geral gerado: {outputPath}");
                return true;
            }
            catch (IOException ex)
            {
                _logger.Err($"Erro ao gerar lista geral (arquivo aberto no Excel?): {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.Err($"Erro ao gerar lista geral: {ex.Message}");
                return false;
            }
        }

        // ── FORNECEDOR ──────────────────────────────────────────────────

        private bool WriteSupplier(
            List<GeneralListItem> fornecedor,
            string templatePath,
            string outputPath)
        {
            try
            {
                using var wb = new XLWorkbook(templatePath);
                var ws = GetWorksheetOrFirst(wb, SupplierDefaultSheet);

                const int headerRow = 1;
                const int firstDataRow = 2;
                const int lastCol = 5;

                int lastUsed = ws.LastRowUsed()?.RowNumber() ?? headerRow;
                int clearUntil = Math.Max(lastUsed, firstDataRow + fornecedor.Count + 20);

                ws.Range(firstDataRow, 1, clearUntil, lastCol).Clear(XLClearOptions.Contents);

                int row = firstDataRow;
                foreach (var item in fornecedor)
                {
                    CopyRowStyle(ws, firstDataRow, row, lastCol);

                    ws.Cell(row, 1).Value = ToCellText(item.Item);
                    ws.Cell(row, 2).Value = ToCellText(item.Referencia);
                    ws.Cell(row, 3).Value = ToCellText(item.Rev);
                    SetDateOrText(ws.Cell(row, 4), item.DataRev);
                    ws.Cell(row, 5).Value = ToCellText(item.Descricao);
                    row++;
                }

                ApplyTableBorders(ws, headerRow, 1, Math.Max(row - 1, headerRow), lastCol);
                ws.Columns(1, lastCol).AdjustToContents();
                LimitWidth(ws.Column(5), 70);

                SaveWorkbook(wb, outputPath);
                _logger.Ok($"Arquivo do fornecedor gerado: {outputPath}");
                return true;
            }
            catch (IOException ex)
            {
                _logger.Err($"Erro ao gerar fornecedor (arquivo aberto no Excel?): {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.Err($"Erro ao gerar fornecedor: {ex.Message}");
                return false;
            }
        }

        // ── BARRAMENTO ──────────────────────────────────────────────────

        private bool WriteBusbar(
            List<GeneralListItem> barramento,
            string templatePath,
            string outputPath)
        {
            try
            {
                using var wb = new XLWorkbook(templatePath);

                if (!wb.TryGetWorksheet(BusbarDataSheet, out var ws))
                {
                    _logger.Err($"Template de barramento sem aba '{BusbarDataSheet}'.");
                    return false;
                }

                const int headerRow = 1;
                const int firstDataRow = 2;
                const int dataLastCol = 14; // A:N. Mantemos O:P para fórmulas.
                const int formulaColO = 15;
                const int formulaColP = 16;

                int lastUsed = ws.LastRowUsed()?.RowNumber() ?? headerRow;
                int neededLastRow = Math.Max(firstDataRow + barramento.Count + 20, 95);
                int clearUntil = Math.Max(lastUsed, neededLastRow);

                // Limpa dados antigos de A:N, preservando as fórmulas O:P.
                ws.Range(firstDataRow, 1, clearUntil, dataLastCol).Clear(XLClearOptions.Contents);

                EnsureBusbarFormulas(ws, firstDataRow, clearUntil, formulaColO, formulaColP);

                int row = firstDataRow;
                foreach (var item in barramento)
                {
                    CopyRowStyle(ws, firstDataRow, row, 16);

                    ws.Cell(row, 1).Value = ToCellText(item.Item);
                    SetNumberOrText(ws.Cell(row, 2), item.Qtde, item.QtdeNum);
                    ws.Cell(row, 3).Value = ToCellText(item.Referencia);
                    ws.Cell(row, 4).Value = ToCellText(item.Rev);
                    SetDateOrText(ws.Cell(row, 5), item.DataRev);
                    ws.Cell(row, 6).Value = ToCellText(item.Descricao);
                    ws.Cell(row, 7).Value = ToCellText(item.Obs);
                    ws.Cell(row, 8).Value = ToCellText(item.Material);

                    // Importante: I/J/K ficam como texto com "mm", pois a aba
                    // COMPRA BARRAMENTO usa esses textos como critérios do SUMIFS.
                    ws.Cell(row, 9).Value = ToCellText(item.Largura);
                    ws.Cell(row, 10).Value = ToCellText(item.Comprimento);
                    ws.Cell(row, 11).Value = ToCellText(item.Espessura);

                    // L/M ficam em branco no template.
                    ws.Cell(row, 12).Clear(XLClearOptions.Contents);
                    ws.Cell(row, 13).Clear(XLClearOptions.Contents);

                    // Coluna N: comprimento numérico. A fórmula da coluna O é N+15.
                    // Se CompMm estiver 0, tentamos extrair de novo do texto.
                    double comp = item.CompMm > 0 ? item.CompMm : ParseNumber(item.Comprimento);
                    if (comp > 0)
                        ws.Cell(row, 14).Value = comp;
                    else
                        ws.Cell(row, 14).Value = ToCellText(item.Comprimento).Replace("mm", "", StringComparison.OrdinalIgnoreCase).Trim();

                    // Fórmulas por linha. Regravamos para garantir que a linha nova
                    // sempre usa a própria N/O/B corretas.
                    ws.Cell(row, formulaColO).FormulaA1 = $"N{row}+15";
                    ws.Cell(row, formulaColP).FormulaA1 = $"O{row}*B{row}";

                    row++;
                }

                // Corrige/normaliza as fórmulas da aba COMPRA BARRAMENTO do arquivo de saída.
                // Isso preserva o layout do template, mas evita erros caso alguma célula do
                // template antigo tenha referência incorreta.
                NormalizePurchaseFormulas(wb);

                ApplyTableBorders(ws, headerRow, 1, Math.Max(row - 1, headerRow), 16);
                ws.Columns(1, 16).AdjustToContents();
                LimitWidth(ws.Column(6), 70);
                LimitWidth(ws.Column(7), 55);

                SaveWorkbook(wb, outputPath);
                _logger.Ok($"Arquivo de barramento gerado: {outputPath}");
                return true;
            }
            catch (IOException ex)
            {
                _logger.Err($"Erro ao gerar barramento (arquivo aberto no Excel?): {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.Err($"Erro ao gerar barramento: {ex.Message}");
                return false;
            }
        }

        private static void EnsureBusbarFormulas(
            IXLWorksheet ws,
            int firstDataRow,
            int lastRow,
            int formulaColO,
            int formulaColP)
        {
            for (int row = firstDataRow; row <= lastRow; row++)
            {
                ws.Cell(row, formulaColO).FormulaA1 = $"N{row}+15";
                ws.Cell(row, formulaColP).FormulaA1 = $"O{row}*B{row}";
            }
        }

        private static void NormalizePurchaseFormulas(XLWorkbook wb)
        {
            if (!wb.TryGetWorksheet(BusbarPurchaseSheet, out var ws))
                return;

            // Blocos do template:
            // B/C/D, G/H/I, L/M/N, Q/R/S
            var blocks = new[]
            {
                new { BarraCol = "B", EspCol = "C", ResultCol = "D" },
                new { BarraCol = "G", EspCol = "H", ResultCol = "I" },
                new { BarraCol = "L", EspCol = "M", ResultCol = "N" },
                new { BarraCol = "Q", EspCol = "R", ResultCol = "S" },
            };

            foreach (var block in blocks)
            {
                // Alumínio: linhas 6 a 17, material em X5.
                for (int row = 6; row <= 17; row++)
                {
                    ws.Cell($"{block.ResultCol}{row}").FormulaA1 =
                        $"SUMIFS(DADOS!P:P,DADOS!I:I,'COMPRA BARRAMENTO'!{block.BarraCol}{row}," +
                        $"DADOS!K:K,'COMPRA BARRAMENTO'!{block.EspCol}{row}," +
                        $"DADOS!H:H,'COMPRA BARRAMENTO'!$X$5)/1000";
                }

                // Cobre: linhas 24 a 35, material em X6.
                for (int row = 24; row <= 35; row++)
                {
                    ws.Cell($"{block.ResultCol}{row}").FormulaA1 =
                        $"SUMIFS(DADOS!P:P,DADOS!I:I,'COMPRA BARRAMENTO'!{block.BarraCol}{row}," +
                        $"DADOS!K:K,'COMPRA BARRAMENTO'!{block.EspCol}{row}," +
                        $"DADOS!H:H,'COMPRA BARRAMENTO'!$X$6)/1000";
                }
            }
        }

        // ── Helpers gerais ──────────────────────────────────────────────

        /// <summary>
        /// Atualiza apenas as células variáveis do cabeçalho do projeto (L1 e L2)
        /// sem tocar nas células fixas do template (A1=OBS MATERIAL, H1=instrução impressão).
        /// </summary>
        /// <remarks>
        /// O template tem este layout nas linhas 1-2:
        ///   A1 (FIXO)    = "OBS : MATERIAL ELETRICO NÃO COMPRAR POR AQUI"
        ///   F1 (VARIÁVEL)= "C0X (PXXXX)_LISTA_GERAL - LEPXXXX XXXX"  → título do projeto
        ///   H1 (FIXO)    = "IMPRIMIR TODAS PLANILHAS EM A3..."
        ///   F2 (VARIÁVEL)= "COR DO PAINEL RAL-XXXX - ..."             → cor do painel
        ///
        /// Quando os dados vêm do EXCEL: topRows[0] e topRows[1] são as linhas lidas
        /// da aba "Lista geral" do arquivo de entrada, onde F é a coluna 6 (índice 5).
        ///
        /// Quando os dados vêm do COLAR: o Solid Edge cola as linhas de cabeçalho
        /// do projeto distribuídas em múltiplas colunas. O conteúdo relevante para
        /// F1 e F2 está espalhado — unimos tudo que não for vazio e colocamos em F.
        ///
        /// Em ambos os casos, A1 e H1 permanecem intactos (vêm do template).
        /// </remarks>
        private static void ApplyTopRows(
            IXLWorksheet ws,
            IReadOnlyList<string[]>? topRows,
            int rowsToWrite,
            int lastCol,
            string projectTitle = "",
            string outputBaseName = "",
            string revision = "R00")
        {
            const int ColF = 6; // coluna F (1-based)

            // ── F1: título do projeto ──────────────────────────────────
            // Prioridade: 1) projectTitle digitado pelo usuário
            //             2) cols[5] da linha 1 dos topRows (modo Excel)
            //             3) placeholder do template (não sobrescreve)
            string f1 = projectTitle.Trim();

            if (string.IsNullOrWhiteSpace(f1) && topRows != null && topRows.Count > 0)
            {
                var cols = topRows[0];
                f1 = cols.Length > 5 ? (cols[5] ?? "").Trim() : "";
            }

            if (!string.IsNullOrWhiteSpace(f1))
                ws.Cell(1, ColF).Value = f1;

            // ── F2: cor do painel ──────────────────────────────────────
            // Prioridade: 1) cols[5] da linha 2 dos topRows
            //             2) junção de todas as colunas da linha 2 (modo colar)
            //             3) placeholder do template (não sobrescreve)
            if (topRows != null && topRows.Count > 1)
            {
                var cols2 = topRows[1];
                string f2 = cols2.Length > 5 ? (cols2[5] ?? "").Trim() : "";

                if (string.IsNullOrWhiteSpace(f2))
                {
                    var parts = cols2.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim());
                    f2 = string.Join(" ", parts);
                }

                if (!string.IsNullOrWhiteSpace(f2))
                    ws.Cell(2, ColF).Value = f2;
            }
            // A1 e H1 NUNCA são tocados — permanecem com os valores fixos do template
        }

        private static IXLWorksheet GetWorksheetOrFirst(XLWorkbook wb, string preferredName)
        {
            if (wb.TryGetWorksheet(preferredName, out var ws))
                return ws;

            return wb.Worksheets.First();
        }

        private static void CopyRowStyle(IXLWorksheet ws, int sourceRow, int targetRow, int lastCol)
        {
            if (targetRow == sourceRow) return;

            for (int col = 1; col <= lastCol; col++)
                ws.Cell(targetRow, col).Style = ws.Cell(sourceRow, col).Style;
        }

        private static void ApplyTableBorders(IXLWorksheet ws, int firstRow, int firstCol, int lastRow, int lastCol)
        {
            if (lastRow < firstRow) return;

            var range = ws.Range(firstRow, firstCol, lastRow, lastCol);
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        private static void LimitWidth(IXLColumn column, double maxWidth)
        {
            if (column.Width > maxWidth)
                column.Width = maxWidth;
        }

        private static void SaveWorkbook(XLWorkbook wb, string outputPath)
        {
            string? dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            // Ajuda o Excel a recalcular as fórmulas quando abrir o arquivo.
            wb.CalculateMode = XLCalculateMode.Auto;
            wb.SaveAs(outputPath);
        }

        private static string ToCellText(string? value)
            => value?.Trim() ?? string.Empty;

        private static void SetNumberOrText(IXLCell cell, string rawText, double number)
        {
            if (number > 0)
                cell.Value = number;
            else
                cell.Value = ToCellText(rawText);
        }

        private static void SetDateOrText(IXLCell cell, string rawText)
        {
            string text = ToCellText(rawText);
            if (text.Length == 0)
            {
                cell.Clear(XLClearOptions.Contents);
                return;
            }

            string[] formats =
            {
                "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "MM/dd/yyyy", "M/d/yyyy"
            };

            if (DateTime.TryParseExact(text, formats,
                    CultureInfo.GetCultureInfo("pt-BR"),
                    DateTimeStyles.None, out DateTime dt) ||
                DateTime.TryParse(text, CultureInfo.GetCultureInfo("pt-BR"),
                    DateTimeStyles.None, out dt))
            {
                cell.Value = dt;
                cell.Style.DateFormat.Format = "dd/MM/yyyy";
            }
            else
            {
                cell.Value = text;
            }
        }

        private static double ParseNumber(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return 0;

            var chars = new List<char>();
            bool started = false;

            foreach (char c in raw.Trim())
            {
                if (char.IsDigit(c) || c == ',' || c == '.')
                {
                    chars.Add(c == ',' ? '.' : c);
                    started = true;
                }
                else if (started)
                {
                    break;
                }
            }

            if (chars.Count == 0) return 0;
            string num = new string(chars.ToArray());
            return double.TryParse(num, NumberStyles.Any,
                                   CultureInfo.InvariantCulture, out double v)
                   ? v : 0;
        }

        private static string MakeSafeFileName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;

            var invalid = Path.GetInvalidFileNameChars();
            var chars = name.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray();
            return new string(chars);
        }
    }
}
