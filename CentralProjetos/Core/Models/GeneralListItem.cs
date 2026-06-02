namespace DrawingCollector.Core.Models
{
    /// <summary>
    /// Uma linha da Lista Geral exportada do Solid Edge.
    /// </summary>
    /// <remarks>
    /// As colunas seguem o template do cliente (linha 3 do .xlsx é o cabeçalho):
    ///   ITEM | QTDE. | REFERÊNCIA | REV. | DATA REV. | DESCRIÇÃO | OBS. |
    ///   MATERIAL | LARGURA | COMPRIMENTO | ESPESSURA | PESO UNI | PESO TOTAL | PINTURA
    ///
    /// 'record' porque é só dado, sem comportamento. Os campos são strings
    /// porque preservamos exatamente o que veio do Excel (incluindo formatos
    /// como "100,00 mm" e "0,32 kg") — não convertemos para número aqui para
    /// não perder informação na re-escrita.
    ///
    /// Larg/Comp/Espessura também ficam guardados em forma numérica
    /// (LargMm, CompMm, EspMm) para o cálculo de metros de barramento.
    /// Quando o valor não é numérico, fica 0.
    /// </remarks>
    public record GeneralListItem(
        string Item,
        string Qtde,
        string Referencia,
        string Rev,
        string DataRev,
        string Descricao,
        string Obs,
        string Material,
        string Largura,
        string Comprimento,
        string Espessura,
        string PesoUni,
        string PesoTotal,
        string Pintura,

        // Versões numéricas para cálculo (em mm e unidades)
        double QtdeNum,
        double LargMm,
        double CompMm,
        double EspMm
    );
}
