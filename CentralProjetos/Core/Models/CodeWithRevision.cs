namespace DrawingCollector.Core.Models
{
    /// <summary>
    /// Um código da lista de entrada, com sua revisão pedida (se houver).
    /// </summary>
    /// <remarks>
    /// Quando a lista vem de Excel com coluna REV, cada código carrega também
    /// a revisão que a lista está pedindo. Isso permite comparar com a revisão
    /// oficial do banco de revisões e detectar divergências.
    ///
    /// Revision pode ser vazia (modos Colar/.txt, ou Excel sem coluna REV).
    /// </remarks>
    public record CodeWithRevision(string Code, string Revision)
    {
        /// <summary>True se há uma revisão informada para comparar.</summary>
        public bool HasRevision => !string.IsNullOrWhiteSpace(Revision);
    }
}
