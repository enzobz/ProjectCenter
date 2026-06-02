using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using DrawingCollector.Core;
using DrawingCollector.Core.Models;
using DrawingCollector.Logging;

namespace DrawingCollector.Reports
{
    /// <summary>
    /// Gera os arquivos Excel de saída a partir do resultado da separação.
    /// </summary>
    /// <remarks>
    /// Segue o mesmo padrão do ExcelReporter: usa ClosedXML (sem Excel
    /// instalado), recebe ILogger, trata exceções com mensagem clara.
    ///
    /// Gera UM arquivo com várias abas:
    ///   - "Lista Geral"        → todos os itens
    ///   - "Fornecedor"         → itens para fabricar fora
    ///   - "Barramento"         → itens de cobre/alumínio
    ///   - "Compra Barramento"  → consolidação de metros (calculada, não fórmula)
    ///
    /// Por que valores calculados em vez de fórmulas SUMIFS?
    /// A planilha original usava SUMIFS para o usuário não recalcular à mão.
    /// Como agora o programa JÁ faz a soma, gravamos o número final direto —
    /// é mais simples, não quebra se a aba DADOS mudar de lugar, e abre
    /// rápido em qualquer máquina. Se você preferir manter as fórmulas vivas,
    /// dá para trocar depois (é uma decisão de produto, não técnica).
    /// </remarks>
    public class ListReportWriter
    {
        private readonly ILogger _logger;

        // Reaproveita as cores da identidade visual do aplicativo.
        // Convertidas de System.Drawing.Color para XLColor do ClosedXML.
        private static readonly XLColor HeaderBg =
            XLColor.FromArgb(33, 56, 110);   // Palette.Primary (azul escuro)
        private static readonly XLColor HeaderFg = XLColor.White;
        private static readonly XLColor TitleBg =
            XLColor.FromArgb(224, 134, 53);  // Palette.Accent (laranja)

        // Títulos das colunas da lista geral, na ordem do template.
        private static readonly string[] ListHeaders =
        {
            "ITEM", "QTDE.", "REFERÊNCIA", "REV.", "DATA REV.", "DESCRIÇÃO",
            "OBS.", "MATERIAL", "LARGURA", "COMPRIMENTO", "ESPESSURA",
            "PESO UNI", "PESO TOTAL", "PINTURA",
        };

        public ListReportWriter(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Escreve o arquivo de saída com todas as abas.
        /// </summary>
        /// <param name="result">Resultado da separação.</param>
        /// <param name="outputPath">Caminho completo do .xlsx a gerar.</param>
        /// <returns>True se gerou com sucesso.</returns>
        public bool Write(ListSeparatorService.SeparationResult result, string outputPath)
        {
            if (result == null)
            {
                _logger.Err("Geração de saída: resultado nulo.");
                return false;
            }

            try
            {
                using var wb = new XLWorkbook();

                WriteListSheet(wb, "Lista Geral", result.Todos);
                WriteListSheet(wb, "Fornecedor",  result.Fornecedor);
                WriteListSheet(wb, "Barramento",  result.Barramento);
                WriteBusbarPurchaseSheet(wb, result.CompraBarramento);

                // Garante que a pasta de destino existe
                string? dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                wb.SaveAs(outputPath);
                _logger.Ok($"Arquivo de listas gerado: {outputPath}");
                return true;
            }
            catch (IOException ex)
            {
                _logger.Err($"Erro de E/S ao gerar saída (arquivo aberto no Excel?): {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.Err($"Erro ao gerar arquivo de saída: {ex.Message}");
                return false;
            }
        }

        // ── Aba de lista (Lista Geral / Fornecedor / Barramento) ─────────

        /// <summary>
        /// Escreve uma aba no formato da lista geral, com cabeçalho e dados.
        /// </summary>
        private void WriteListSheet(
            XLWorkbook wb, string sheetName, List<GeneralListItem> itens)
        {
            var ws = wb.AddWorksheet(SanitizeSheetName(sheetName));

            // Linha 1: título da aba (faixa laranja)
            ws.Cell(1, 1).Value = sheetName.ToUpperInvariant();
            var titleRange = ws.Range(1, 1, 1, ListHeaders.Length);
            titleRange.Merge();
            titleRange.Style.Fill.BackgroundColor = TitleBg;
            titleRange.Style.Font.FontColor = XLColor.White;
            titleRange.Style.Font.Bold = true;
            titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Linha 2: cabeçalho das colunas (faixa azul)
            for (int c = 0; c < ListHeaders.Length; c++)
            {
                var cell = ws.Cell(2, c + 1);
                cell.Value = ListHeaders[c];
                cell.Style.Fill.BackgroundColor = HeaderBg;
                cell.Style.Font.FontColor = HeaderFg;
                cell.Style.Font.Bold = true;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // Linhas 3+: dados
            int row = 3;
            foreach (var i in itens)
            {
                ws.Cell(row, 1).Value  = i.Item;
                ws.Cell(row, 2).Value  = i.Qtde;
                ws.Cell(row, 3).Value  = i.Referencia;
                ws.Cell(row, 4).Value  = i.Rev;
                ws.Cell(row, 5).Value  = i.DataRev;
                ws.Cell(row, 6).Value  = i.Descricao;
                ws.Cell(row, 7).Value  = i.Obs;
                ws.Cell(row, 8).Value  = i.Material;
                ws.Cell(row, 9).Value  = i.Largura;
                ws.Cell(row, 10).Value = i.Comprimento;
                ws.Cell(row, 11).Value = i.Espessura;
                ws.Cell(row, 12).Value = i.PesoUni;
                ws.Cell(row, 13).Value = i.PesoTotal;
                ws.Cell(row, 14).Value = i.Pintura;
                row++;
            }

            // Bordas em toda a tabela (cabeçalho + dados)
            if (itens.Count > 0)
            {
                var dataRange = ws.Range(2, 1, row - 1, ListHeaders.Length);
                dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            }

            // Congela a linha de cabeçalho e ajusta larguras
            ws.SheetView.FreezeRows(2);
            ws.Columns().AdjustToContents();

            // Limita largura de colunas muito longas (descrição)
            if (ws.Column(6).Width > 60) ws.Column(6).Width = 60;
        }

        // ── Aba de consolidação de compra de barramento ──────────────────

        /// <summary>
        /// Escreve a aba "Compra Barramento" com os metros por perfil.
        /// </summary>
        private void WriteBusbarPurchaseSheet(
            XLWorkbook wb, List<ListSeparatorService.BusbarPurchaseRow> rows)
        {
            var ws = wb.AddWorksheet("Compra Barramento");

            string[] headers = { "MATERIAL", "LARGURA (mm)", "ESPESSURA (mm)", "QNTD (Metros)", "Nº PEÇAS" };

            // Título
            ws.Cell(1, 1).Value = "COMPRA DE BARRAMENTO";
            var titleRange = ws.Range(1, 1, 1, headers.Length);
            titleRange.Merge();
            titleRange.Style.Fill.BackgroundColor = TitleBg;
            titleRange.Style.Font.FontColor = XLColor.White;
            titleRange.Style.Font.Bold = true;
            titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Cabeçalho
            for (int c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(2, c + 1);
                cell.Value = headers[c];
                cell.Style.Fill.BackgroundColor = HeaderBg;
                cell.Style.Font.FontColor = HeaderFg;
                cell.Style.Font.Bold = true;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // Dados
            int row = 3;
            foreach (var r in rows)
            {
                ws.Cell(row, 1).Value = r.Material;
                ws.Cell(row, 2).Value = r.LarguraMm;
                ws.Cell(row, 3).Value = r.EspessuraMm;

                var metrosCell = ws.Cell(row, 4);
                metrosCell.Value = r.Metros;

                // Formato de número com 2 casas decimais.
                // No CÓDIGO de formato do Excel, o "." é sempre o separador
                // decimal genérico — o Excel troca para vírgula automaticamente
                // conforme a região de quem abre (no Brasil, vira "2,64").
                // O "#,##0.00" adiciona separador de milhar também (1.234,56).
                metrosCell.Style.NumberFormat.Format = "#,##0.00";

                ws.Cell(row, 5).Value = r.Pecas;
                row++;
            }

            if (rows.Count > 0)
            {
                var dataRange = ws.Range(2, 1, row - 1, headers.Length);
                dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            }
            else
            {
                ws.Cell(3, 1).Value = "(nenhum barramento na lista)";
                ws.Cell(3, 1).Style.Font.Italic = true;
            }

            ws.SheetView.FreezeRows(2);
            ws.Columns().AdjustToContents();
        }

        // ── Helper (mesmo do ExcelReporter) ──────────────────────────────

        /// <summary>
        /// Sanitiza nome de aba: remove caracteres proibidos e trunca em 31.
        /// </summary>
        private static string SanitizeSheetName(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Aba";

            var invalid = new[] { '/', '\\', '?', '*', '[', ']', ':' };
            string name = raw;
            foreach (char c in invalid)
                name = name.Replace(c, '-');

            name = name.Trim('\'');
            if (name.Length > 31) name = name.Substring(0, 31);
            return string.IsNullOrWhiteSpace(name) ? "Aba" : name;
        }
    }
}
