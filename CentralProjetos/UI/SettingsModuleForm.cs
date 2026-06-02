using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DrawingCollector.UI.Controls;
using DrawingCollector.UI.Extensions;
using DrawingCollector.UI.Theme;

namespace DrawingCollector.UI
{
    /// <summary>
    /// Módulo simples de configurações e diagnóstico dos arquivos do programa.
    /// </summary>
    public class SettingsModuleForm : Form
    {
        private RichTextBox txtInfo = null!;

        public SettingsModuleForm()
        {
            Text = "Módulo 4 - Configurações";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(900, 620);
            MinimumSize = new Size(780, 520);
            Font = new Font("Segoe UI", 10f);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.FromArgb(244, 247, 252);

            string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.ico");
            Icon = File.Exists(icoPath) ? new Icon(icoPath) : SystemIcons.Application;

            BuildLayout();
            LoadDiagnostics(); // chamada APÓS BuildLayout — txtInfo já existe
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                Padding = new Padding(24),
                BackColor = Color.FromArgb(244, 247, 252),
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            root.Controls.Add(new GradientHeader
            {
                Dock = DockStyle.Top,
                Height = 82,
                Title = "Configurações",
                CornerRadius = 22,
                Margin = new Padding(0, 0, 0, 18),
            }, 0, 0);

            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 12),
            };

            actions.Controls.Add(MakeAction("Abrir pasta do programa", () => Process.Start("explorer.exe", AppDomain.CurrentDomain.BaseDirectory)));
            actions.Controls.Add(MakeAction("Abrir pasta modelos", OpenModelsFolder));
            actions.Controls.Add(MakeAction("Atualizar diagnóstico", LoadDiagnostics));
            actions.Controls.Add(MakeAction("Voltar", Close));

            root.Controls.Add(actions, 0, 1);

            txtInfo = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(35, 40, 50),
                Font = new Font("Consolas", 10f),
                Padding = new Padding(12),
            };

            var infoPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(14),
            };
            infoPanel.Controls.Add(txtInfo);
            root.Controls.Add(infoPanel, 0, 2);

            root.SetDoubleBuffered();
        }

        private static RoundedButton MakeAction(string text, System.Action action)
        {
            var btn = new RoundedButton
            {
                Text = text,
                MinimumSize = new Size(160, 38),
                Margin = new Padding(0, 0, 8, 8),
            };
            btn.Click += (_, __) => action();
            return btn;
        }

        private void OpenModelsFolder()
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "modelos");
            Directory.CreateDirectory(dir);
            Process.Start("explorer.exe", dir);
        }

        private void LoadDiagnostics()
        {
            if (txtInfo == null) return;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string modelsDir = Path.Combine(baseDir, "modelos");
            string[] required =
            {
                "LEPXXXX-PXXXX-C0X-CH-R00.xlsx",
                "LEP2XXXX-LISTA GERAL-FORNECEDOR-CH-R00.xlsx",
                "LEPXXXX-PXXXX-C0X-BA-R00.xlsx",
            };

            txtInfo.Clear();
            txtInfo.AppendText("FINALIZADOR DE PROJETO - DIAGNÓSTICO\n");
            txtInfo.AppendText("==================================================\n\n");
            txtInfo.AppendText("Pasta do programa:\n");
            txtInfo.AppendText(baseDir + "\n\n");
            txtInfo.AppendText("Pasta de modelos:\n");
            txtInfo.AppendText(modelsDir + "\n\n");

            txtInfo.AppendText("Templates necessários:\n");
            foreach (string file in required)
            {
                string path = Path.Combine(modelsDir, file);
                txtInfo.AppendText((File.Exists(path) ? "[OK]    " : "[FALTA] ") + file + "\n");
            }

            txtInfo.AppendText("\nArquivos encontrados em modelos:\n");
            if (Directory.Exists(modelsDir))
            {
                foreach (string f in Directory.GetFiles(modelsDir).OrderBy(f => Path.GetFileName(f)))
                    txtInfo.AppendText("- " + Path.GetFileName(f) + "\n");
            }
            else
            {
                txtInfo.AppendText("A pasta modelos ainda não existe.\n");
            }
        }
    }
}
