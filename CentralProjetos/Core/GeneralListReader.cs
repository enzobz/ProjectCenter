using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using DrawingCollector.Core.Models;
using DrawingCollector.Logging;

namespace DrawingCollector.Core
{
    /// <summary>
    /// Lê a Lista Geral exportada do Solid Edge (.xlsx) e devolve os itens
    /// como uma lista de objetos prontos para classificar.
    /// </summary>
    /// <remarks>
    /// Segue o mesmo padrão do CodeReader/BusbarRepository:
    ///   - recebe um ILogger no construtor
    ///   - LoadFromFile abre o Excel uma vez e devolve os dados em memória
    ///   - exceções específicas com mensagem clara no log
    ///
    /// O template do cliente tem 2 linhas de cabeçalho de projeto antes
    /// da linha de títulos das colunas. Por padrão:
    ///   Linha 1-2 → cabeçalho do projeto (ignorado)
    ///   Linha 3   → títulos das colunas (ITEM, QTDE, ...)
    ///   Linha 4+  → dados
    ///
    /// HeaderRow é configurável caso o layout mude.
    /// </remarks>
    public class GeneralListReader
    {
        private readonly ILogger _logger;

        // Índices das colunas (1-based, como o ClosedXML usa).
        // Baseados no template: A=ITEM(1) ... N=PINTURA(14).
        private const int ColItem        = 1;
        private const int ColQtde        = 2;
        private const int ColReferencia  = 3;
        private const int ColRev         = 4;
        private const int ColDataRev     = 5;
        private const int ColDescricao   = 6;
        private const int ColObs         = 7;
        private const int ColMaterial    = 8;
        private const int ColLargura     = 9;
        private const int ColComprimento = 10;
        private const int ColEspessura   = 11;
        private const int ColPesoUni     = 12;
        private const int ColPesoTotal   = 13;
        private const int ColPintura     = 14;

        public GeneralListReader(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Lê a aba indicada e devolve todos os itens com dados.
        /// </summary>
        /// <param name="filePath">Caminho do .xlsx da lista geral.</param>
        /// <param name="sheetName">Aba a ler. Se null/vazio, usa a primeira.</param>
        /// <param name="headerRow">Linha do cabeçalho das colunas (padrão 3).</param>
        /// <returns>Lista de itens; vazia em caso de erro.</returns>
        public List<GeneralListItem> LoadFromFile(
            string? filePath,
            string? sheetName = "Lista geral",
            int headerRow = 3)
        {
            var result = new List<GeneralListItem>();

            if (string.IsNullOrWhiteSpace(filePath))
            {
                _logger.Err("Lista geral: caminho do arquivo não informado.");
                return result;
            }

            if (!File.Exists(filePath))
            {
                _logger.Err($"Lista geral: arquivo não encontrado: {filePath}");
                return result;
            }

            try
            {
                using var wb = new XLWorkbook(filePath);

                IXLWorksheet ws;
                if (!string.IsNullOrWhiteSpace(sheetName))
                {
                    if (!wb.TryGetWorksheet(sheetName, out ws!))
                    {
                        _logger.Err($"Lista geral: aba '{sheetName}' não encontrada. " +
                                    $"Abas disponíveis: {string.Join(", ", wb.Worksheets.Select(w => w.Name))}");
                        return result;
                    }
                }
                else
                {
                    ws = wb.Worksheets.First();
                }

                int firstDataRow = headerRow + 1;
                int lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;

                if (lastRow < firstDataRow)
                {
                    _logger.Warn("Lista geral: nenhuma linha de dados abaixo do cabeçalho.");
                    return result;
                }

                for (int row = firstDataRow; row <= lastRow; row++)
                {
                    string item     = Cell(ws, row, ColItem);
                    string material = Cell(ws, row, ColMaterial);

                    // Pula linhas totalmente vazias (sem item E sem material)
                    if (item.Length == 0 && material.Length == 0)
                        continue;

                    string largura     = Cell(ws, row, ColLargura);
                    string comprimento = Cell(ws, row, ColComprimento);
                    string espessura   = Cell(ws, row, ColEspessura);
                    string qtde        = Cell(ws, row, ColQtde);

                    result.Add(new GeneralListItem(
                        Item:        item,
                        Qtde:        qtde,
                        Referencia:  Cell(ws, row, ColReferencia),
                        Rev:         Cell(ws, row, ColRev),
                        DataRev:     Cell(ws, row, ColDataRev),
                        Descricao:   Cell(ws, row, ColDescricao),
                        Obs:         Cell(ws, row, ColObs),
                        Material:    material,
                        Largura:     largura,
                        Comprimento: comprimento,
                        Espessura:   espessura,
                        PesoUni:     Cell(ws, row, ColPesoUni),
                        PesoTotal:   Cell(ws, row, ColPesoTotal),
                        Pintura:     Cell(ws, row, ColPintura),

                        QtdeNum: ParseNumber(qtde),
                        LargMm:  ParseNumber(largura),
                        CompMm:  ParseNumber(comprimento),
                        EspMm:   ParseNumber(espessura)
                    ));
                }

                _logger.Ok($"Lista geral carregada: {result.Count} itens " +
                           $"← {Path.GetFileName(filePath)} (aba '{ws.Name}')");
                return result;
            }
            catch (Exception ex)
            {
                _logger.Err($"Erro ao ler lista geral: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Lista as abas de um .xlsx (para popular um ComboBox na UI).
        /// </summary>
        public string[] GetSheetNames(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return Array.Empty<string>();

            try
            {
                using var wb = new XLWorkbook(filePath);
                return wb.Worksheets.Select(w => w.Name).ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        // ── Helpers privados ─────────────────────────────────────────────

        /// <summary>
        /// Lê uma célula como texto já com Trim aplicado.
        /// Se a célula for uma data, formata como dd/MM/yyyy (sem a hora).
        /// </summary>
        /// <remarks>
        /// Por que tratar data aqui?
        /// O Solid Edge exporta a coluna DATA REV. como DateTime, então o
        /// GetString() devolveria "03/05/2021 00:00:00" com a hora pendurada.
        /// Detectamos o tipo de dado da célula e, se for data, pegamos só a
        /// parte da data. Para todo o resto, comportamento normal (texto).
        /// </remarks>
        private static string Cell(IXLWorksheet ws, int row, int col)
        {
            var cell = ws.Cell(row, col);

            // Se o conteúdo é uma data, formata só a data (sem hora)
            if (cell.DataType == XLDataType.DateTime &&
                cell.TryGetValue(out DateTime dt))
            {
                return dt.ToString("dd/MM/yyyy");
            }

            return cell.GetString().Trim();
        }

        /// <summary>
        /// Extrai um número de textos como "100,00 mm", "0,32 kg", "30".
        /// Aceita vírgula OU ponto como separador decimal.
        /// Retorna 0 se não houver número.
        /// </summary>
        /// <remarks>
        /// Estratégia: varre o texto pegando o primeiro trecho numérico
        /// (dígitos, vírgula, ponto), troca vírgula por ponto e converte
        /// usando InvariantCulture — assim funciona independentemente da
        /// configuração regional da máquina.
        /// </remarks>
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
                    // Acabou o trecho numérico (ex: chegou no espaço antes de "mm")
                    break;
                }
            }

            if (chars.Count == 0) return 0;

            string num = new string(chars.ToArray());
            return double.TryParse(num, NumberStyles.Any,
                                   CultureInfo.InvariantCulture, out double v)
                   ? v : 0;
        }
    }
}
