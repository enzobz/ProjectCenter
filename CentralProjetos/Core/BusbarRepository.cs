using System;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;
using DrawingCollector.Core.Models;
using DrawingCollector.Logging;

namespace DrawingCollector.Core
{
    /// <summary>
    /// Lê o arquivo Excel banco de barramentos e responde consultas sobre TemDobra.
    /// </summary>
    /// <remarks>
    /// Por que "Repository"?
    /// É um padrão de design que representa "um lugar onde você busca dados".
    /// Quem usa esta classe não precisa saber que os dados estão num Excel —
    /// poderia ser SQLite, JSON, API — a interface seria a mesma.
    /// Se um dia você quiser migrar para outro formato de banco, só reescreve
    /// esta classe; o resto do programa não muda nada.
    ///
    /// CICLO DE VIDA:
    ///   1. LoadFromFile(path) — lê o Excel uma vez, monta o dicionário em memória
    ///   2. TryGetEntry(code)  — consultas O(1) usando o dicionário
    ///
    /// O dicionário é carregado uma vez só antes da execução começar,
    /// então não há I/O durante o loop de busca.
    /// </remarks>
    public class BusbarRepository
    {
        private readonly ILogger _logger;

        // Dicionário: CodigoX0 (maiúsculas) → BusbarEntry
        // Carregado uma vez em LoadFromFile, depois só leitura.
        private Dictionary<string, BusbarEntry> _entries =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Quantos registros foram carregados do banco.</summary>
        public int Count => _entries.Count;

        /// <summary>True se o banco foi carregado com sucesso.</summary>
        public bool IsLoaded { get; private set; }

        public BusbarRepository(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // ════════════════════════════════════════════════════════════════
        //   CARREGAMENTO
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Lê o Excel banco e constrói o dicionário em memória.
        /// Deve ser chamado uma vez antes de iniciar o processamento.
        /// </summary>
        /// <param name="filePath">Caminho do arquivo .xlsx banco.</param>
        /// <returns>True se carregou com sucesso, false se falhou.</returns>
        public bool LoadFromFile(string? filePath)
        {
            IsLoaded = false;
            _entries = new Dictionary<string, BusbarEntry>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(filePath))
            {
                _logger.Err("Modo Barramento: caminho do banco não informado.");
                return false;
            }

            if (!File.Exists(filePath))
            {
                _logger.Err($"Modo Barramento: banco não encontrado: {filePath}");
                return false;
            }

            try
            {
                using var wb = new XLWorkbook(filePath);
                var ws = wb.Worksheets.First();

                // Detecta onde os dados começam:
                // Linha 1 sempre é o cabeçalho (Codigo | TemDobra)
                // Dados começam na linha 2
                int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
                int loaded = 0, skipped = 0;

                for (int row = 2; row <= lastRow; row++)
                {
                    string rawCodigo  = ws.Cell(row, 1).GetString().Trim();
                    string rawDobra   = ws.Cell(row, 2).GetString().Trim().ToUpperInvariant();

                    if (string.IsNullOrEmpty(rawCodigo)) continue;

                    // Normaliza o código para X0 — aceita qualquer revisão no banco
                    // Ex: operador digitou "ABC123XD" → vira "ABC123X0"
                    string codigoX0 = CodeMatcher.NormalizeSchneiderToX0(
                                          rawCodigo.ToUpperInvariant());

                    // Interpreta TemDobra: aceita Sim/S/YES/Y/1/TRUE como true
                    bool temDobra = rawDobra is "SIM" or "S" or "YES" or "Y" or "1" or "TRUE";

                    if (_entries.ContainsKey(codigoX0))
                    {
                        // Só avisa se o TemDobra conflitar com o já registrado.
                        // É normal o banco ter XB, XC, XD do mesmo código raiz —
                        // todos normalizam para X0 e costumam ter o mesmo TemDobra.
                        // Conflito (ex: XB=Sim e XC=Não) é um erro real na planilha.
                        if (_entries[codigoX0].TemDobra != temDobra)
                        {
                            _logger.Warn(
                                $"[Banco] Conflito na linha {row}: {rawCodigo} " +
                                $"(TemDobra={temDobra}) contradiz entrada anterior " +
                                $"(TemDobra={_entries[codigoX0].TemDobra}). " +
                                $"Mantendo o primeiro valor.");
                        }
                        skipped++;
                        continue;
                    }

                    _entries[codigoX0] = new BusbarEntry(codigoX0, temDobra);
                    loaded++;
                }

                IsLoaded = true;
                _logger.Ok($"Banco barramento carregado: {loaded} registros " +
                           (skipped > 0 ? $"({skipped} duplicados ignorados)" : "") +
                           $" ← {Path.GetFileName(filePath)}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Err($"Erro ao ler banco barramento: {ex.Message}");
                return false;
            }
        }

        // ════════════════════════════════════════════════════════════════
        //   CONSULTA
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Consulta se um código está no banco e qual é o seu TemDobra.
        /// </summary>
        /// <param name="code">Código a consultar (qualquer revisão — será normalizado).</param>
        /// <param name="entry">A entrada encontrada, ou null se não estiver no banco.</param>
        /// <returns>True se o código foi encontrado no banco.</returns>
        public bool TryGetEntry(string code, out BusbarEntry? entry)
        {
            // Normaliza antes de buscar — garante que "ABC123XD" acha "ABC123X0"
            string x0 = CodeMatcher.NormalizeSchneiderToX0(
                             (code ?? "").Trim().ToUpperInvariant());

            return _entries.TryGetValue(x0, out entry);
        }

        // ════════════════════════════════════════════════════════════════
        //   GERAÇÃO DO TEMPLATE
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gera um arquivo Excel modelo vazio com o cabeçalho correto.
        /// Útil para o usuário que ainda não tem o banco criado.
        /// </summary>
        public static void GenerateTemplate(string outputPath)
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Barramentos");

            // Cabeçalho
            ws.Cell(1, 1).Value = "Codigo";
            ws.Cell(1, 2).Value = "TemDobra";

            // Estilo do cabeçalho
            var header = ws.Range("A1:B1");
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.LightBlue;

            // Exemplos para guiar o usuário
            ws.Cell(2, 1).Value = "ABC123X0";
            ws.Cell(2, 2).Value = "Sim";
            ws.Cell(3, 1).Value = "XYZ456X0";
            ws.Cell(3, 2).Value = "Não";

            // Largura automática
            ws.Column(1).AdjustToContents();
            ws.Column(2).AdjustToContents();

            // Instrução na coluna C
            ws.Cell(1, 3).Value = "← Código do barramento (qualquer revisão: X0, XA, XD...)";
            ws.Cell(2, 3).Value = "← 'Sim' = tem dobra (exige .dxf + .stp)";
            ws.Cell(3, 3).Value = "← 'Não' = sem dobra (só .dxf)";
            ws.Column(3).AdjustToContents();
            ws.Column(3).Style.Font.FontColor = XLColor.Gray;

            wb.SaveAs(outputPath);
        }
    }
}
