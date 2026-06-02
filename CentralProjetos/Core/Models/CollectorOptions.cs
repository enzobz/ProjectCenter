using System.Collections.Generic;
using DrawingCollector.Core;

namespace DrawingCollector.Core.Models
{
    /// <summary>
    /// Agrupa todas as opções de uma execução do coletor.
    /// </summary>
    /// <remarks>
    /// Em vez de passar 8+ parâmetros para o método DoWorkMulti, agrupamos
    /// tudo em um objeto. Vantagens:
    /// 1. Não precisa lembrar a ordem dos parâmetros
    /// 2. Fácil adicionar novas opções sem quebrar chamadas antigas
    /// 3. Pode validar tudo em um lugar só
    ///
    /// Os 'init' nas propriedades significam que só podem ser definidas
    /// na criação do objeto (imutável depois). Isso evita bugs como
    /// "alguém mudou GroupByCode no meio da execução".
    /// </remarks>
    public class CollectorOptions
    {
        /// <summary>Pastas raiz do servidor onde os arquivos serão procurados.</summary>
        public string[] ServerRoots { get; init; } = System.Array.Empty<string>();

        /// <summary>Pasta onde os arquivos encontrados serão copiados.</summary>
        public string OutputDirectory { get; init; } = string.Empty;

        /// <summary>Extensões a procurar (ex: ".pdf", ".dwg"), sem espaços.</summary>
        public string[] Extensions { get; init; } = System.Array.Empty<string>();

        /// <summary>Lista de códigos a buscar.</summary>
        public string[] Codes { get; init; } = System.Array.Empty<string>();

        /// <summary>Se true, só lista os encontrados no grid; não copia nada.</summary>
        public bool PreviewOnly { get; init; }

        /// <summary>Se true, cria uma subpasta por código no destino.</summary>
        public bool GroupByCode { get; init; }

        /// <summary>Se true, exige que o nome-base seja idêntico ao código (ignora regras Schneider).</summary>
        public bool ExactMode { get; init; }

        /// <summary>
        /// Regras de equivalência entre extensões para o relatório de faltantes.
        ///
        /// Exemplo: ExtensionEquivalence(foundExt: ".dwg", dispensingExt: ".pdf")
        /// → Se o .dwg foi encontrado para um código, o .pdf não entra como faltante.
        ///
        /// Deixado como lista para suportar múltiplos pares no futuro
        /// (ex: .stp encontrado → dispensa .dxf).
        /// </summary>
        public IReadOnlyList<ExtensionEquivalence> ExtensionEquivalences { get; init; }
            = System.Array.Empty<ExtensionEquivalence>();

        /// <summary>
        /// Se true, o Modo Barramento está ativo.
        /// Nesse modo, o programa consulta o BusbarRepository antes de processar
        /// cada código para saber se TemDobra = Sim ou Não.
        ///
        /// Regra:
        ///   TemDobra = Sim → busca .dxf + .stp normalmente
        ///   TemDobra = Não → dispensa o .stp (não reclama de faltante)
        ///   Não no banco   → avisa no log e processa normalmente
        /// </summary>
        public bool BusbarMode { get; init; }

        /// <summary>
        /// Repositório de barramentos já carregado (só usado quando BusbarMode = true).
        /// Null significa que o banco não foi carregado ou BusbarMode está desligado.
        /// </summary>
        public BusbarRepository? BusbarRepository { get; init; }

        /// <summary>
        /// Códigos com suas revisões pedidas (da lista de entrada).
        /// Quando presente e há RevisionRepository, valida divergências.
        /// </summary>
        public IReadOnlyList<CodeWithRevision> CodesWithRevision { get; init; }
            = System.Array.Empty<CodeWithRevision>();

        /// <summary>
        /// Banco de revisões oficiais já carregado.
        /// Null = validação de revisão desativada.
        /// </summary>
        public RevisionRepository? RevisionRepository { get; init; }
    }
}
