namespace DrawingCollector.Logging
{
    /// <summary>
    /// Contrato para um sistema de log.
    /// </summary>
    /// <remarks>
    /// Uma interface define O QUE algo faz, sem dizer COMO faz.
    /// Diferentes implementações podem cumprir o mesmo contrato:
    /// - RichTextBoxLogger: mostra no controle de UI
    /// - ConsoleLogger: imprime no Console.WriteLine (útil para testes)
    /// - FileLogger: grava em arquivo de texto
    /// - NullLogger: descarta tudo (útil quando não queremos log nenhum)
    ///
    /// Quem usa o logger (FileIndexer, CodeMatcher, etc.) recebe um ILogger
    /// e nem precisa saber qual implementação está usando. Isso se chama
    /// "Dependency Inversion" — em vez de criar o logger, você o RECEBE.
    /// </remarks>
    public interface ILogger
    {
        /// <summary>Mensagem informativa neutra.</summary>
        void Info(string message);

        /// <summary>Aviso — algo digno de atenção mas não impede a execução.</summary>
        void Warn(string message);

        /// <summary>Sucesso — operação concluída com êxito.</summary>
        void Ok(string message);

        /// <summary>Erro — algo falhou.</summary>
        void Err(string message);
    }
}
