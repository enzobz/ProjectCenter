using System;
using System.Collections.Generic;
using System.Linq;

namespace DrawingCollector.Core.Models
{
    /// <summary>
    /// Regras de classificação de itens por material.
    /// Define quais materiais pertencem a cada categoria de saída.
    /// </summary>
    /// <remarks>
    /// Por que uma classe de regras em vez de listas soltas?
    /// O mesmo motivo do CollectorOptions: agrupar tudo em um lugar.
    /// Quando aparecer um material novo no projeto, você edita SÓ aqui —
    /// nenhuma outra parte do código precisa mudar.
    ///
    /// As listas vêm com valores-padrão (os materiais que mapeamos juntos),
    /// mas são 'init', então quem cria pode sobrescrever se quiser carregar
    /// de um arquivo de configuração no futuro.
    ///
    /// A comparação é case-insensitive e ignora espaços nas pontas —
    /// "Cobre ETP R250" e "cobre etp r250 " caem na mesma categoria.
    /// </remarks>
    public class MaterialRules
    {
        /// <summary>
        /// Materiais que identificam BARRAMENTO (fabricação interna de barras).
        /// </summary>
        public IReadOnlyList<string> Barramento { get; init; } = new[]
        {
            "Cobre ETP R250",
            "Alumínio IS 5082 - 63401-W",
        };

        /// <summary>
        /// Materiais que vão para a lista do FORNECEDOR EXTERNO
        /// (peças fabricadas fora — chaparia + perfis especiais).
        /// </summary>
        public IReadOnlyList<string> Fornecedor { get; init; } = new[]
        {
            "DX51D Z275-M-B-C",
            "DC01 ZE 25-25-A-PC",
            "Alumínio EN AW-5754 H111",
            "LEXAN F2000",
            "Aluminio",
            "Alumínio ASTM 1100 H14",
            "Latão CuZn36Pb3",
        };

        /// <summary>
        /// Categoria resultante da classificação de um item.
        /// </summary>
        public enum Categoria
        {
            /// <summary>Vai para a lista/aba de barramento (cobre/alumínio em barra).</summary>
            Barramento,

            /// <summary>Vai para a lista do fornecedor externo.</summary>
            Fornecedor,

            /// <summary>Não se encaixa em nenhuma das anteriores (comprado, core part, etc.).</summary>
            Outros,
        }

        // Conjuntos para busca O(1) — montados uma vez no construtor.
        // HashSet com comparador OrdinalIgnoreCase: "DX51D" == "dx51d".
        private readonly HashSet<string> _barramentoSet;
        private readonly HashSet<string> _fornecedorSet;

        public MaterialRules()
        {
            // Os 'init' acima já preencheram as listas; montamos os sets a partir delas.
            // Normalizamos cada entrada (trim) para casar com a normalização da consulta.
            _barramentoSet = new HashSet<string>(
                Barramento.Select(Normalize), StringComparer.OrdinalIgnoreCase);

            _fornecedorSet = new HashSet<string>(
                Fornecedor.Select(Normalize), StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Classifica um material em uma das três categorias.
        /// </summary>
        /// <param name="material">Texto da coluna MATERIAL (pode ter espaços/caixa variada).</param>
        public Categoria Classify(string? material)
        {
            string m = Normalize(material);
            if (m.Length == 0) return Categoria.Outros;

            if (_barramentoSet.Contains(m)) return Categoria.Barramento;
            if (_fornecedorSet.Contains(m)) return Categoria.Fornecedor;
            return Categoria.Outros;
        }

        /// <summary>
        /// Normaliza um material para comparação: remove espaços nas pontas
        /// e colapsa espaços internos múltiplos em um só.
        /// Ex: "Cobre  ETP   R250 " → "Cobre ETP R250".
        /// </summary>
        private static string Normalize(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            // Split por espaços e junta com um único espaço — remove espaços duplos
            return string.Join(" ",
                raw.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
