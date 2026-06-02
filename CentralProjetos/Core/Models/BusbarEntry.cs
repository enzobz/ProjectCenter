namespace DrawingCollector.Core.Models
{
    /// <summary>
    /// Representa uma linha do banco Excel de barramentos.
    /// </summary>
    /// <remarks>
    /// Por que normalizar para X0 aqui?
    /// O operador pode digitar "ABC123XD" ou "ABC123X0" no banco — ambos
    /// se referem ao mesmo desenho pai. Normalizamos na leitura para que
    /// a busca sempre funcione independentemente do que foi digitado.
    ///
    /// 'record' porque é só dado: sem comportamento, sem estado mutável.
    /// </remarks>
    public record BusbarEntry(
        /// <summary>
        /// Código normalizado para X0 (chave de busca).
        /// Ex: "ABC123XD" e "ABC123X0" viram ambos "ABC123X0".
        /// </summary>
        string CodigoX0,

        /// <summary>
        /// true  = tem dobra → exige .dxf + .stp
        /// false = sem dobra → exige só .dxf (.stp é dispensado)
        /// </summary>
        bool TemDobra
    );
}
