using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using DrawingCollector.Core.Models;
using DrawingCollector.Logging;

namespace DrawingCollector.Core
{
    /// <summary>
    /// Lê lista de códigos a partir de diferentes fontes.
    /// </summary>
    /// <remarks>
    /// Por que uma classe separada?
    /// O MainForm sabe ONDE o usuário digitou/selecionou os códigos.
    /// Mas a lógica de "como abrir um .xlsx e extrair uma coluna" não é
    /// responsabilidade da UI — pertence ao Core.
    ///
    /// Analogia: o garçom (MainForm) anota o pedido, mas é a cozinha
    /// (CodeReader) que sabe como preparar cada prato.
    /// </remarks>
    public class CodeReader
    {
        private readonly ILogger _logger;

        public CodeReader(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Lê códigos de texto colado diretamente (uma linha = um código).
        /// </summary>
        public string[] FromText(string? rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText))
                return Array.Empty<string>();

            return ParseLines(rawText
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
        }

        /// <summary>
        /// Lê códigos de um arquivo .txt (uma linha = um código).
        /// </summary>
        public string[] FromTxt(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                _logger.Err("ERRO: caminho do arquivo .txt não informado.");
                return Array.Empty<string>();
            }

            if (!File.Exists(filePath))
            {
                _logger.Err($"ERRO: arquivo não encontrado: {filePath}");
                return Array.Empty<string>();
            }

            try
            {
                return ParseLines(File.ReadAllLines(filePath));
            }
            catch (IOException ex)
            {
                _logger.Err($"ERRO ao ler .txt: {ex.Message}");
                return Array.Empty<string>();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.Err($"Sem permissão para ler: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Lê códigos de uma coluna específica de um arquivo .xlsx.
        /// </summary>
        /// <param name="filePath">Caminho do arquivo Excel.</param>
        /// <param name="sheetName">Nome da aba. Se null, usa a primeira aba.</param>
        /// <param name="columnLetter">Letra da coluna (ex: "A", "B", "C").</param>
        /// <param name="hasHeader">Se true, pula a primeira linha (cabeçalho).</param>
        public string[] FromXlsx(
            string? filePath,
            string? sheetName,
            string  columnLetter,
            bool    hasHeader)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                _logger.Err("ERRO: caminho do arquivo .xlsx não informado.");
                return Array.Empty<string>();
            }

            if (!File.Exists(filePath))
            {
                _logger.Err($"ERRO: arquivo não encontrado: {filePath}");
                return Array.Empty<string>();
            }

            try
            {
                using var wb = new XLWorkbook(filePath);

                // Seleciona a aba: pelo nome se informado, senão a primeira
                IXLWorksheet ws;
                if (!string.IsNullOrWhiteSpace(sheetName))
                {
                    if (!wb.TryGetWorksheet(sheetName, out ws!))
                    {
                        _logger.Err($"ERRO: aba '{sheetName}' não encontrada no arquivo.");
                        return Array.Empty<string>();
                    }
                }
                else
                {
                    ws = wb.Worksheets.First();
                }

                // Converte letra de coluna para número (A=1, B=2, C=3 ...)
                int colNum = LetterToColumnNumber(columnLetter);
                if (colNum < 1)
                {
                    _logger.Err($"ERRO: coluna inválida: '{columnLetter}'.");
                    return Array.Empty<string>();
                }

                // Determina a linha inicial (2 se tem cabeçalho, 1 se não tem)
                int startRow = hasHeader ? 2 : 1;
                int lastRow  = ws.LastRowUsed()?.RowNumber() ?? 0;

                if (lastRow < startRow)
                {
                    _logger.Warn("Planilha vazia ou sem dados abaixo do cabeçalho.");
                    return Array.Empty<string>();
                }

                var lines = new List<string>();
                for (int row = startRow; row <= lastRow; row++)
                {
                    string val = ws.Cell(row, colNum).GetString().Trim();
                    if (!string.IsNullOrEmpty(val))
                        lines.Add(val);
                }

                _logger.Info($"Lidos {lines.Count} códigos da coluna {columnLetter} ({ws.Name}).");
                return ParseLines(lines.ToArray());
            }
            catch (Exception ex)
            {
                _logger.Err($"ERRO ao ler .xlsx: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Lê códigos E revisões de um .xlsx (duas colunas).
        /// Usado quando há validação de revisão.
        /// </summary>
        /// <param name="revisionColumnLetter">
        /// Letra da coluna de revisão. Se vazia, a revisão fica em branco.
        /// </param>
        public CodeWithRevision[] FromXlsxWithRevision(
            string? filePath,
            string? sheetName,
            string  codeColumnLetter,
            string? revisionColumnLetter,
            bool    hasHeader)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                _logger.Err($"ERRO: arquivo não encontrado: {filePath}");
                return Array.Empty<CodeWithRevision>();
            }

            try
            {
                using var wb = new XLWorkbook(filePath);

                IXLWorksheet ws;
                if (!string.IsNullOrWhiteSpace(sheetName))
                {
                    if (!wb.TryGetWorksheet(sheetName, out ws!))
                    {
                        _logger.Err($"ERRO: aba '{sheetName}' não encontrada.");
                        return Array.Empty<CodeWithRevision>();
                    }
                }
                else ws = wb.Worksheets.First();

                int codeCol = LetterToColumnNumber(codeColumnLetter);
                int revCol  = string.IsNullOrWhiteSpace(revisionColumnLetter)
                              ? 0
                              : LetterToColumnNumber(revisionColumnLetter);

                if (codeCol < 1)
                {
                    _logger.Err($"ERRO: coluna de código inválida: '{codeColumnLetter}'.");
                    return Array.Empty<CodeWithRevision>();
                }

                int startRow = hasHeader ? 2 : 1;
                int lastRow  = ws.LastRowUsed()?.RowNumber() ?? 0;

                var result = new List<CodeWithRevision>();
                var seen   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int row = startRow; row <= lastRow; row++)
                {
                    string code = ws.Cell(row, codeCol).GetString().Trim();
                    if (string.IsNullOrEmpty(code)) continue;
                    if (!seen.Add(code)) continue; // remove duplicatas

                    string rev = revCol > 0
                        ? ws.Cell(row, revCol).GetString().Trim()
                        : string.Empty;

                    result.Add(new CodeWithRevision(code, rev));
                }

                _logger.Info($"Lidos {result.Count} códigos" +
                    (revCol > 0 ? $" + revisões (coluna {revisionColumnLetter})" : "") +
                    $" ({ws.Name}).");
                return result.ToArray();
            }
            catch (Exception ex)
            {
                _logger.Err($"ERRO ao ler .xlsx com revisão: {ex.Message}");
                return Array.Empty<CodeWithRevision>();
            }
        }

        /// <summary>
        /// Lista os nomes das abas de um arquivo .xlsx.
        /// Usado para popular o ComboBox de abas na UI.
        /// </summary>
        public string[] GetSheetNames(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return Array.Empty<string>();

            try
            {
                using var wb = new XLWorkbook(filePath);
                return wb.Worksheets.Select(ws => ws.Name).ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        // ── Helpers privados ─────────────────────────────────────────────

        /// <summary>
        /// Normaliza e desduplicar uma coleção de linhas de texto.
        /// Remove espaços, linhas vazias e duplicatas (case-insensitive).
        /// </summary>
        private static string[] ParseLines(IEnumerable<string> lines)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();

            foreach (string raw in lines)
            {
                string code = raw.Trim();
                if (code.Length > 0 && seen.Add(code))
                    result.Add(code);
            }

            return result.ToArray();
        }

        /// <summary>
        /// Converte letra de coluna Excel para número (A=1, B=2, Z=26, AA=27...).
        /// </summary>
        private static int LetterToColumnNumber(string? letters)
        {
            if (string.IsNullOrWhiteSpace(letters)) return 0;

            string upper = letters.Trim().ToUpperInvariant();
            int result = 0;

            foreach (char c in upper)
            {
                if (c < 'A' || c > 'Z') return 0; // caractere inválido
                result = result * 26 + (c - 'A' + 1);
            }

            return result;
        }
    }
}
