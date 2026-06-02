namespace DrawingCollector.Core.Models
{
    /// <summary>
    /// Define que encontrar <see cref="FoundExt"/> dispensa a obrigatoriedade de
    /// <see cref="DispensingExt"/> no relatório de faltantes.
    /// </summary>
    /// <remarks>
    /// Exemplo de uso:
    ///   new ExtensionEquivalence(foundExt: ".dwg", dispensingExt: ".pdf")
    ///
    ///   Significa: "Se o .dwg foi encontrado, não reclama do .pdf."
    ///
    /// Por que um modelo separado em vez de um Dictionary?
    /// Um Dictionary&lt;string,string&gt; obrigaria a relação a ser 1-para-1.
    /// Com uma lista de ExtensionEquivalence, podemos no futuro ter:
    ///   - .dwg encontrado → dispensa .pdf
    ///   - .stp encontrado → dispensa .dxf
    ///   - .dwg encontrado → dispensa .dxf  (mesma chave, regra diferente)
    /// É um design mais flexível.
    ///
    /// 'record' gera automaticamente construtor, Equals e ToString.
    /// </remarks>
    public record ExtensionEquivalence(string FoundExt, string DispensingExt);
}
