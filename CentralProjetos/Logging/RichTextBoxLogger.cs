using System;
using System.Drawing;
using System.Windows.Forms;
using DrawingCollector.UI.Theme;

namespace DrawingCollector.Logging
{
    /// <summary>
    /// Logger que escreve em um controle RichTextBox da UI.
    /// </summary>
    /// <remarks>
    /// Usa Invoke/BeginInvoke porque o FileIndexer e outros podem chamar
    /// estes métodos a partir de uma thread que NÃO é a thread da UI.
    /// Em WinForms, todo controle só pode ser tocado pela thread que o criou.
    /// </remarks>
    public class RichTextBoxLogger : ILogger
    {
        private readonly RichTextBox _rtb;

        public RichTextBoxLogger(RichTextBox rtb)
        {
            _rtb = rtb ?? throw new ArgumentNullException(nameof(rtb));
        }

        public void Info(string message) => Append(message, color: null);
        public void Warn(string message) => Append(message, Palette.Warning);
        public void Ok(string message)   => Append(message, Palette.Success);
        public void Err(string message)  => Append(message, Palette.Error);

        private void Append(string msg, Color? color)
        {
            // Se chamarem de outra thread, precisamos "agendar" a operação
            // para rodar na thread da UI. BeginInvoke faz isso sem bloquear.
            if (_rtb.IsHandleCreated && _rtb.InvokeRequired)
            {
                _rtb.BeginInvoke(new Action(() => AppendInternal(msg, color)));
            }
            else
            {
                AppendInternal(msg, color);
            }
        }

        private void AppendInternal(string msg, Color? color)
        {
            try
            {
                Color c = color ?? _rtb.ForeColor;

                int start = _rtb.TextLength;
                _rtb.AppendText(msg + Environment.NewLine);
                _rtb.Select(start, msg.Length);
                _rtb.SelectionColor = c;

                // Reset da seleção e scroll para o final
                _rtb.Select(_rtb.TextLength, 0);
                _rtb.ScrollToCaret();
            }
            catch (ObjectDisposedException)
            {
                // O controle foi destruído (janela fechada). Ignorar é seguro.
            }
        }
    }
}
