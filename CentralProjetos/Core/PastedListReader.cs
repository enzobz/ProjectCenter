using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DrawingCollector.Core.Models;
using DrawingCollector.Logging;

namespace DrawingCollector.Core
{
    /// <summary>
    /// Lê uma lista colada/copiadada do Solid Edge, Excel ou outro sistema em texto.
    /// </summary>
    /// <remarks>
    /// Uso principal:
    /// - copiar uma tabela;
    /// - colar em um .txt, ou colar direto no terminal no modo --colar;
    /// - transformar cada linha em GeneralListItem.
    ///
    /// O leitor aceita:
    /// - texto separado por TAB (mais comum ao copiar de Excel/Solid Edge);
    /// - texto separado por ponto-e-vírgula;
    /// - com cabeçalho ou sem cabeçalho.
    ///
    /// Se houver cabeçalho, as colunas são localizadas pelo nome:
    /// ITEM, QTDE., REFERÊNCIA, REV., DATA REV., DESCRIÇÃO, OBS., MATERIAL,
    /// LARGURA, COMPRIMENTO, ESPESSURA, PESO UNI, PESO TOTAL, PINTURA.
    ///
    /// Se não houver cabeçalho, assume a ordem padrão A:N do template.
    /// </remarks>
    public class PastedListReader
    {
        private readonly ILogger _logger;

        /// <summary>Linhas acima do cabeçalho da tabela, normalmente o cabeçalho do projeto.</summary>
        public List<string[]> ProjectHeaderRows { get; private set; } = new();

        public PastedListReader(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Lê uma lista a partir de um arquivo .txt.
        /// </summary>
        public List<GeneralListItem> LoadFromTextFile(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                _logger.Err("Texto colado: caminho do arquivo não informado.");
                return new List<GeneralListItem>();
            }

            if (!File.Exists(filePath))
            {
                _logger.Err($"Texto colado: arquivo não encontrado: {filePath}");
                return new List<GeneralListItem>();
            }

            try
            {
                string text = File.ReadAllText(filePath, Encoding.UTF8);
                return LoadFromText(text, Path.GetFileName(filePath));
            }
            catch (Exception ex)
            {
                _logger.Err($"Erro ao ler arquivo de texto: {ex.Message}");
                return new List<GeneralListItem>();
            }
        }

        /// <summary>
        /// Lê uma lista a partir do texto colado.
        /// </summary>
        public List<GeneralListItem> LoadFromText(string? text, string origem = "texto colado")
        {
            var result = new List<GeneralListItem>();
            ProjectHeaderRows = new List<string[]>();

            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.Warn("Texto colado vazio.");
                return result;
            }

            var lines = text
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n')
                .Select(l => l.TrimEnd())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            if (lines.Count == 0)
            {
                _logger.Warn("Texto colado sem linhas úteis.");
                return result;
            }

            var rows = lines.Select(SplitLine).ToList();
            int headerIndex = FindHeaderRow(rows);

            Dictionary<string, int> map;
            int firstDataIndex;

            if (headerIndex >= 0)
            {
                map = BuildHeaderMap(rows[headerIndex]);
                firstDataIndex = headerIndex + 1;

                // Preserva as linhas acima do cabeçalho.
                // Ex.: as duas linhas de título/cor do projeto quando a pessoa
                // copia a lista inteira do Excel/Solid Edge para o terminal.
                if (headerIndex > 0)
                    ProjectHeaderRows = rows.Take(headerIndex).ToList();
            }
            else
            {
                map = BuildDefaultMap();
                firstDataIndex = 0;
                _logger.Warn("Nenhum cabeçalho encontrado no texto. Usando a ordem padrão das colunas A:N.");
            }

            for (int i = firstDataIndex; i < rows.Count; i++)
            {
                var cols = rows[i];

                string item = Get(cols, map, "ITEM");
                string referencia = Get(cols, map, "REFERENCIA");
                string material = Get(cols, map, "MATERIAL");

                // Pula linhas vazias ou linhas de observação/título.
                if (item.Length == 0 && referencia.Length == 0 && material.Length == 0)
                    continue;

                string qtde = Get(cols, map, "QTDE");
                string largura = Get(cols, map, "LARGURA");
                string comprimento = Get(cols, map, "COMPRIMENTO");
                string espessura = Get(cols, map, "ESPESSURA");

                result.Add(new GeneralListItem(
                    Item: item,
                    Qtde: qtde,
                    Referencia: referencia,
                    Rev: Get(cols, map, "REV"),
                    DataRev: Get(cols, map, "DATAREV"),
                    Descricao: Get(cols, map, "DESCRICAO"),
                    Obs: Get(cols, map, "OBS"),
                    Material: material,
                    Largura: largura,
                    Comprimento: comprimento,
                    Espessura: espessura,
                    PesoUni: Get(cols, map, "PESOUNI"),
                    PesoTotal: Get(cols, map, "PESOTOTAL"),
                    Pintura: Get(cols, map, "PINTURA"),

                    QtdeNum: ParseNumber(qtde),
                    LargMm: ParseNumber(largura),
                    CompMm: ParseNumber(comprimento),
                    EspMm: ParseNumber(espessura)
                ));
            }

            _logger.Ok($"Lista carregada do texto: {result.Count} itens ← {origem}");
            return result;
        }

        private static string[] SplitLine(string line)
        {
            if (line.Contains('\t'))
                return line.Split('\t').Select(c => c.Trim()).ToArray();

            if (line.Contains(';'))
                return line.Split(';').Select(c => c.Trim()).ToArray();

            // Último recurso: vírgula. Não é ideal para números brasileiros,
            // por isso só usamos se não tiver TAB nem ponto-e-vírgula.
            if (line.Contains(','))
                return line.Split(',').Select(c => c.Trim()).ToArray();

            return new[] { line.Trim() };
        }

        private static int FindHeaderRow(List<string[]> rows)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                var normalized = rows[i].Select(NormalizeHeader).ToArray();

                bool hasItem = normalized.Contains("ITEM");
                bool hasRef = normalized.Contains("REFERENCIA");
                bool hasMaterial = normalized.Contains("MATERIAL");

                if (hasItem && hasRef && hasMaterial)
                    return i;
            }

            return -1;
        }

        private static Dictionary<string, int> BuildHeaderMap(string[] header)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < header.Length; i++)
            {
                string key = NormalizeHeader(header[i]);
                if (key.Length == 0) continue;

                // Normaliza nomes equivalentes.
                key = key switch
                {
                    "QTDE" or "QTD" or "QUANTIDADE" => "QTDE",
                    "REFERENCIA" or "REFERÊNCIA" or "CODIGO" or "COD" => "REFERENCIA",
                    "REV" or "REVISAO" => "REV",
                    "DATAREV" or "DATAREVISAO" => "DATAREV",
                    "DESCRICAO" or "DESCRIÇÃO" => "DESCRICAO",
                    "OBS" or "OBSERVACAO" => "OBS",
                    "PESOUNI" or "PESOUNITARIO" => "PESOUNI",
                    "PESOTOTAL" => "PESOTOTAL",
                    _ => key
                };

                if (!map.ContainsKey(key))
                    map[key] = i;
            }

            return map;
        }

        private static Dictionary<string, int> BuildDefaultMap()
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["ITEM"] = 0,
                ["QTDE"] = 1,
                ["REFERENCIA"] = 2,
                ["REV"] = 3,
                ["DATAREV"] = 4,
                ["DESCRICAO"] = 5,
                ["OBS"] = 6,
                ["MATERIAL"] = 7,
                ["LARGURA"] = 8,
                ["COMPRIMENTO"] = 9,
                ["ESPESSURA"] = 10,
                ["PESOUNI"] = 11,
                ["PESOTOTAL"] = 12,
                ["PINTURA"] = 13,
            };
        }

        private static string Get(string[] cols, Dictionary<string, int> map, string key)
        {
            if (!map.TryGetValue(key, out int index)) return string.Empty;
            if (index < 0 || index >= cols.Length) return string.Empty;
            return cols[index].Trim();
        }

        private static string NormalizeHeader(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            string text = raw.Trim().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            foreach (char c in text)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category == UnicodeCategory.NonSpacingMark) continue;

                if (char.IsLetterOrDigit(c))
                    sb.Append(char.ToUpperInvariant(c));
            }

            return sb.ToString();
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
    }
}
