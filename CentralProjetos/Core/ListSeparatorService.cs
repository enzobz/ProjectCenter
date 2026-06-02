using System;
using System.Collections.Generic;
using System.Linq;
using DrawingCollector.Core.Models;
using DrawingCollector.Logging;

namespace DrawingCollector.Core
{
    /// <summary>
    /// Separa os itens da lista geral em categorias e calcula a consolidação
    /// de metros de barramento (equivalente à aba "COMPRA BARRAMENTO").
    /// </summary>
    /// <remarks>
    /// Esta é a versão C# da lógica que validamos em Python:
    ///
    ///   1. CLASSIFICAÇÃO — para cada item, MaterialRules.Classify diz se é
    ///      Barramento, Fornecedor ou Outros.
    ///
    ///   2. CONSOLIDAÇÃO DE METROS — replica o SUMIFS da planilha original:
    ///      agrupa os itens de barramento por (Largura × Espessura × Material),
    ///      soma o comprimento total e divide por 1000 → metros.
    ///
    ///      Fórmula original na planilha:
    ///        =SUMIFS(DADOS!P:P, DADOS!I:I, larg, DADOS!K:K, esp,
    ///                DADOS!H:H, material) / 1000
    ///
    ///      Onde P (TOTAL) = comprimento individual já multiplicado pela qtde.
    ///      Aqui calculamos TOTAL = CompMm × QtdeNum, somamos por grupo, /1000.
    ///
    /// A classe é "pura" no sentido de não tocar em arquivos nem na UI —
    /// recebe dados, devolve dados. Isso a torna fácil de testar (igual ao
    /// CodeMatcher).
    /// </remarks>
    public class ListSeparatorService
    {
        private readonly ILogger _logger;
        private readonly MaterialRules _rules;

        /// <summary>
        /// Margem de corte adicionada ao comprimento de CADA peça de barramento,
        /// em milímetros, antes de consolidar os metros.
        /// </summary>
        /// <remarks>
        /// A planilha original somava a coluna "Mais 15 corte" (= comprimento + 15)
        /// multiplicada pela quantidade. Esses 15mm representam a sobra de material
        /// para o corte de cada barra. Sem essa margem, o total de metros sai menor
        /// que o real e o planejador compraria barramento a menos.
        ///
        /// Validado contra a planilha do cliente: com +15mm por peça, os metros
        /// batem exatamente (ex: 100×5 = 19,04 m; 40×5 = 2,64 m).
        /// </remarks>
        public double MargemCorteMm { get; init; } = 15.0;

        public ListSeparatorService(ILogger logger, MaterialRules? rules = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _rules  = rules ?? new MaterialRules();
        }

        /// <summary>
        /// Resultado completo da separação.
        /// </summary>
        public class SeparationResult
        {
            /// <summary>Todos os itens, sem filtro (para a aba Lista Geral).</summary>
            public List<GeneralListItem> Todos { get; init; } = new();

            /// <summary>Itens classificados como barramento.</summary>
            public List<GeneralListItem> Barramento { get; init; } = new();

            /// <summary>Itens para o fornecedor externo.</summary>
            public List<GeneralListItem> Fornecedor { get; init; } = new();

            /// <summary>Itens que não se encaixam (comprados, core parts, etc.).</summary>
            public List<GeneralListItem> Outros { get; init; } = new();

            /// <summary>Consolidação de metros por perfil de barra.</summary>
            public List<BusbarPurchaseRow> CompraBarramento { get; init; } = new();
        }

        /// <summary>
        /// Uma linha da consolidação de compra de barramento.
        /// </summary>
        public record BusbarPurchaseRow(
            string Material,    // "Cobre ETP R250" ou "Alumínio IS 5082 - 63401-W"
            double LarguraMm,   // ex: 100
            double EspessuraMm, // ex: 5
            double Metros,      // soma dos comprimentos / 1000
            int    Pecas        // quantas linhas entraram nesse grupo
        );

        /// <summary>
        /// Processa a lista: classifica cada item e calcula a consolidação.
        /// </summary>
        public SeparationResult Separate(IEnumerable<GeneralListItem> itens)
        {
            var result = new SeparationResult();
            if (itens == null) return result;

            foreach (var item in itens)
            {
                result.Todos.Add(item);

                switch (_rules.Classify(item.Material))
                {
                    case MaterialRules.Categoria.Barramento:
                        result.Barramento.Add(item);
                        break;
                    case MaterialRules.Categoria.Fornecedor:
                        result.Fornecedor.Add(item);
                        break;
                    default:
                        result.Outros.Add(item);
                        break;
                }
            }

            // Consolida metros a partir dos itens de barramento
            var consolidado = ConsolidateBusbar(result.Barramento);
            result.CompraBarramento.AddRange(consolidado);

            _logger.Ok(
                $"Separação concluída: {result.Todos.Count} itens → " +
                $"Barramento {result.Barramento.Count}, " +
                $"Fornecedor {result.Fornecedor.Count}, " +
                $"Outros {result.Outros.Count}.");

            return result;
        }

        /// <summary>
        /// Agrupa os itens de barramento por (Material × Largura × Espessura)
        /// e soma os comprimentos totais em metros.
        /// </summary>
        /// <remarks>
        /// A chave do agrupamento usa os valores numéricos (LargMm, EspMm) e
        /// o material normalizado, para que "100,00 mm" e "100 mm" caiam no
        /// mesmo grupo. O TOTAL de cada item é (Comprimento + MargemCorteMm)
        /// × Qtde (em mm); a soma do grupo é dividida por 1000 para virar metros.
        /// </remarks>
        private IEnumerable<BusbarPurchaseRow> ConsolidateBusbar(
            IEnumerable<GeneralListItem> barramentoItens)
        {
            // Agrupa por material+largura+espessura
            var grupos = barramentoItens
                .GroupBy(i => new
                {
                    Material = i.Material.Trim(),
                    Larg     = i.LargMm,
                    Esp      = i.EspMm,
                });

            var rows = new List<BusbarPurchaseRow>();

            foreach (var g in grupos)
            {
                // TOTAL individual = (comprimento + margem de corte) × quantidade
                // A margem (15mm padrão) replica a coluna "Mais 15 corte" da
                // planilha original — sobra de material por peça.
                double somaMm = g.Sum(i =>
                    (i.CompMm + MargemCorteMm) * (i.QtdeNum > 0 ? i.QtdeNum : 1));
                double metros = somaMm / 1000.0;

                rows.Add(new BusbarPurchaseRow(
                    Material:    g.Key.Material,
                    LarguraMm:   g.Key.Larg,
                    EspessuraMm: g.Key.Esp,
                    Metros:      Math.Round(metros, 2),
                    Pecas:       g.Count()
                ));
            }

            // Ordena para leitura: material, depois espessura, depois largura
            return rows
                .OrderBy(r => r.Material)
                .ThenBy(r => r.EspessuraMm)
                .ThenBy(r => r.LarguraMm);
        }
    }
}
