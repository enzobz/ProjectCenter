namespace DrawingCollector.Core.Models
{
    /// <summary>
    /// Representa um arquivo encontrado durante a indexação.
    /// </summary>
    /// <remarks>
    /// Por que guardar tanto o caminho quanto as chaves de lookup?
    ///
    /// Durante a busca precisamos de duas informações:
    ///   1. Onde o arquivo está de verdade (FullPath) — pra copiar
    ///   2. Por qual chave ele foi encontrado (ExactKey, X0Key) — pra decidir
    ///      qual estratégia de match usou (exato vs. X0 normalizado)
    ///
    /// 'record' gera automaticamente construtor, Equals, GetHashCode e ToString.
    /// É a escolha ideal para objetos que são só dados (sem comportamento).
    /// </remarks>
    public record IndexedFile(
        /// <summary>Caminho completo do arquivo no disco/servidor.</summary>
        string FullPath,

        /// <summary>
        /// Nome-base sem extensão, em maiúsculas.
        /// Ex: arquivo "51132814XD-03.dwg" → token "51132814XD"
        /// (cada token do nome vira uma entrada separada no índice exato)
        /// </summary>
        string ExactKey,

        /// <summary>
        /// Chave normalizada para X0, em maiúsculas.
        /// Ex: token "51132814XD" → X0Key "51132814X0"
        /// Tokens sem padrão Schneider (LV, sem X) mantêm o próprio valor.
        /// </summary>
        string X0Key,

        /// <summary>Extensão do arquivo em minúsculas, com ponto. Ex: ".dwg"</summary>
        string Extension
    );
}
