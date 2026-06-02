namespace DrawingCollector.Logging
{
    /// <summary>
    /// Implementação de ILogger que descarta silenciosamente todas as mensagens.
    /// </summary>
    /// <remarks>
    /// Usado onde precisamos passar um ILogger mas ainda não temos um real,
    /// por exemplo ao instanciar leitores fora do contexto de um Form.
    ///
    /// Por que centralizar aqui em vez de ter um por arquivo?
    /// Antes havia três cópias idênticas espalhadas (CodeInputPanel, 
    /// ListGeneratorForm, GuidedFinalizerForm). Ter uma única declaração
    /// 'public' no namespace correto elimina a duplicata e permite que
    /// qualquer parte do projeto use sem redeclarar.
    ///
    /// Uso:
    ///   ILogger logger = new NullLogger();
    ///   var reader = new GeneralListReader(logger ?? new NullLogger());
    /// </remarks>
    public sealed class NullLogger : ILogger
    {
        /// <summary>Instância compartilhável — não tem estado, é seguro reutilizar.</summary>
        public static readonly NullLogger Instance = new();

        public void Info(string message) { }
        public void Warn(string message) { }
        public void Ok(string message)   { }
        public void Err(string message)  { }
    }
}
