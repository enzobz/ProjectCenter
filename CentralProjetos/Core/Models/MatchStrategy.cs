namespace DrawingCollector.Core.Models
{
    /// <summary>
    /// Estratégia de correspondência entre código e nome de arquivo.
    /// </summary>
    /// <remarks>
    /// O projeto usa códigos no padrão Schneider que terminam em
    /// "X" seguido de letra/dígito indicando a revisão. Ex: ABC123X0 (revisão 0),
    /// ABC123X1 (revisão 1). Códigos terminados em "_LV" são internos e não
    /// seguem essa regra.
    ///
    /// Cada extensão de arquivo pode usar uma estratégia diferente:
    /// - PDFs/DWGs: o nome pode ser X0 ou X3 (qualquer revisão) — Schneider
    /// - STPs/DXFs: tem que bater exatamente o código pedido — ExactOnly
    /// </remarks>
    public enum MatchStrategy
    {
        /// <summary>Só aceita o nome-base idêntico ao código.</summary>
        ExactOnly,

        /// <summary>Normaliza para X0 e aceita qualquer arquivo do mesmo grupo de revisão.</summary>
        SchneiderX0Only,

        /// <summary>Tenta exato primeiro; se não achar, cai para Schneider.</summary>
        ExactThenSchneider,
    }
}
