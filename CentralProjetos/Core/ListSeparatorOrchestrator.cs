using System;
using System.IO;
using DrawingCollector.Core;
using DrawingCollector.Core.Models;
using DrawingCollector.Logging;
using DrawingCollector.Reports;

namespace DrawingCollector.Core
{
    /// <summary>
    /// Orquestra o fluxo completo de separação de listas — do .xlsx de entrada
    /// até o .xlsx de saída com as abas separadas.
    /// </summary>
    /// <remarks>
    /// Esta classe é o "maestro": ela não faz o trabalho pesado, só coordena
    /// as outras classes na ordem certa. É o equivalente, para a separação de
    /// listas, ao que o DrawingCollectorService é para a cópia de arquivos.
    ///
    /// FLUXO:
    ///   1. GeneralListReader   → lê a lista geral do Solid Edge
    ///   2. ListSeparatorService → classifica + calcula metros
    ///   3. ListReportWriter    → grava o .xlsx de saída
    ///
    /// Como chamar a partir da UI (exemplo dentro de um clique de botão):
    ///
    ///   var orquestrador = new ListSeparatorOrchestrator(_logger);
    ///   bool ok = orquestrador.Run(
    ///       inputPath:  txtListaGeral.Text,
    ///       outputPath: Path.Combine(txtOut.Text, "listas_separadas.xlsx"),
    ///       sheetName:  "Lista geral");
    ///
    ///   if (ok) MessageBox.Show("Listas geradas com sucesso!");
    /// </remarks>
    public class ListSeparatorOrchestrator
    {
        private readonly ILogger _logger;
        private readonly MaterialRules _rules;

        public ListSeparatorOrchestrator(ILogger logger, MaterialRules? rules = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _rules  = rules ?? new MaterialRules();
        }

        /// <summary>
        /// Executa o fluxo completo: ler → separar → gravar.
        /// </summary>
        /// <param name="inputPath">Caminho do .xlsx exportado do Solid Edge.</param>
        /// <param name="outputPath">Caminho do .xlsx de saída a gerar.</param>
        /// <param name="sheetName">Aba da lista geral (padrão "Lista geral").</param>
        /// <param name="headerRow">Linha de títulos das colunas (padrão 3).</param>
        /// <returns>True se gerou o arquivo de saída com sucesso.</returns>
        public bool Run(
            string inputPath,
            string outputPath,
            string sheetName = "Lista geral",
            int headerRow = 3)
        {
            // 1. Ler
            var reader = new GeneralListReader(_logger);
            var itens = reader.LoadFromFile(inputPath, sheetName, headerRow);

            if (itens.Count == 0)
            {
                _logger.Warn("Nenhum item lido — separação abortada.");
                return false;
            }

            // 2. Separar e calcular metros
            var separator = new ListSeparatorService(_logger, _rules);
            var result = separator.Separate(itens);

            // 3. Gravar saída
            var writer = new ListReportWriter(_logger);
            return writer.Write(result, outputPath);
        }

        /// <summary>
        /// Devolve só os códigos (referências) de uma categoria, para alimentar
        /// o coletor de arquivos do DrawingCollector.
        /// </summary>
        /// <remarks>
        /// É a "ponte" entre os dois módulos: depois de separar, você pega os
        /// códigos da lista de Fornecedor (ou Barramento) e manda para o
        /// DrawingCollectorService copiar os .pdf/.dwg/.dxf/.stp.
        /// </remarks>
        public string[] GetReferencesFor(
            string inputPath,
            MaterialRules.Categoria categoria,
            string sheetName = "Lista geral",
            int headerRow = 3)
        {
            var reader = new GeneralListReader(_logger);
            var itens = reader.LoadFromFile(inputPath, sheetName, headerRow);

            var separator = new ListSeparatorService(_logger, _rules);
            var result = separator.Separate(itens);

            var lista = categoria switch
            {
                MaterialRules.Categoria.Barramento => result.Barramento,
                MaterialRules.Categoria.Fornecedor => result.Fornecedor,
                _                                  => result.Outros,
            };

            // Pega as referências não-vazias, sem duplicatas
            var refs = new System.Collections.Generic.List<string>();
            var seen = new System.Collections.Generic.HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var i in lista)
            {
                string r = i.Referencia.Trim();
                if (r.Length > 0 && seen.Add(r))
                    refs.Add(r);
            }

            return refs.ToArray();
        }
    }
}
