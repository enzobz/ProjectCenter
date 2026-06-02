using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using DrawingCollector.UI.Controls;
using DrawingCollector.UI.Extensions;
using DrawingCollector.UI.Theme;

namespace DrawingCollector.UI
{
    /// <summary>
    /// Dashboard inicial da Central de Projetos.
    /// A ideia é evitar uma tela única cheia de botões e separar o fluxo em módulos.
    /// </summary>
    public class MainMenuForm : Form
    {
        private TableLayoutPanel root = null!;
        private FlowLayoutPanel cards = null!;
        private Label lblStatus = null!;

        public MainMenuForm()
        {
            Text = "Central de Projetos";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1120, 720);
            MinimumSize = new Size(980, 620);
            Font = new Font("Segoe UI", 10f);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.FromArgb(244, 247, 252);

            string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.ico");
            Icon = File.Exists(icoPath) ? new Icon(icoPath) : SystemIcons.Application;

            BuildLayout();
            root.SetDoubleBuffered();
            cards.SetDoubleBuffered();
        }

        private void BuildLayout()
        {
            root = new TableLayoutPanel
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

            var header = new GradientHeader
            {
                Dock = DockStyle.Top,
                Height = 92,
                Title = "Central de Projetos",
                CornerRadius = 24,
                Margin = new Padding(0, 0, 0, 18),
            };
            root.Controls.Add(header, 0, 0);

            var intro = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(2, 0, 2, 14),
            };
            intro.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            intro.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var titleBlock = new Label
            {
                AutoSize = true,
                Text = "Escolha uma etapa do processo",
                Font = new Font("Segoe UI Semibold", 16f),
                ForeColor = Color.FromArgb(30, 36, 52),
                Margin = new Padding(2, 0, 0, 2),
            };
            intro.Controls.Add(titleBlock, 0, 0);

            var btnOpenFolder = new RoundedButton
            {
                Text = "Abrir pasta do programa",
                MinimumSize = new Size(190, 38),
                FillColor = Color.FromArgb(72, 84, 116),
                HoverFill = Color.FromArgb(84, 98, 136),
                PressedFill = Color.FromArgb(56, 66, 92),
                BorderColor = Color.FromArgb(72, 84, 116),
                Margin = new Padding(8, 0, 0, 0),
            };
            btnOpenFolder.Click += (_, __) => Process.Start("explorer.exe", AppDomain.CurrentDomain.BaseDirectory);
            intro.Controls.Add(btnOpenFolder, 1, 0);

            var subtitle = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Text = "Fluxo organizado por módulos: gere as listas, colete arquivos ou faça a finalização guiada.",
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.FromArgb(96, 104, 122),
                Margin = new Padding(2, 0, 0, 0),
            };
            intro.Controls.Add(subtitle, 0, 1);
            intro.SetColumnSpan(subtitle, 2);

            root.Controls.Add(intro, 0, 1);

            cards = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = true,
                Padding = new Padding(0, 4, 0, 4),
                BackColor = Color.FromArgb(244, 247, 252),
            };
            root.Controls.Add(cards, 0, 2);

            AddCard(
                icon: "1",
                title: "Gerar Listas",
                subtitle: "Cria Lista Geral, Fornecedor e Barramento usando os templates reais do projeto.",
                badge: "Excel / Colar",
                accent: Palette.Accent,
                onClick: () => OpenModule(new ListGeneratorForm()));

            AddCard(
                icon: "2",
                title: "Coletar Arquivos",
                subtitle: "Busca PDF, DWG, DXF, STP e demais extensões nas pastas do servidor.",
                badge: "Drawing Collector",
                accent: Color.FromArgb(52, 120, 180),
                onClick: () => OpenModule(new MainForm()));

            AddCard(
                icon: "3",
                title: "Finalização Guiada",
                subtitle: "Gera as listas e busca os arquivos técnicos automaticamente a partir da Lista Geral.",
                badge: "Automático",
                accent: Color.FromArgb(46, 150, 104),
                onClick: () => OpenModule(new GuidedFinalizerForm()));

            AddCard(
                icon: "4",
                title: "Configurações",
                subtitle: "Verifique modelos, abra a pasta do programa e consulte orientações de uso.",
                badge: "Modelos e ajuda",
                accent: Color.FromArgb(120, 90, 180),
                onClick: () => OpenModule(new SettingsModuleForm()));

            lblStatus = new Label
            {
                Dock = DockStyle.Fill,
                Height = 28,
                TextAlign = ContentAlignment.MiddleRight,
                Text = "v6.0  •  Central de Projetos  •  Desenvolvido por Enzo Zeferino",
                Font = new Font("Segoe UI", 8.7f),
                ForeColor = Color.FromArgb(128, 136, 152),
                Padding = new Padding(0, 8, 6, 0),
            };
            root.Controls.Add(lblStatus, 0, 3);
        }

        private void AddCard(string icon, string title, string subtitle, string badge, Color accent, Action onClick)
        {
            var card = new ModuleCard
            {
                // Sem Width fixo — o card se ajusta ao FlowLayoutPanel.
                // MinimumSize = 260px vem do construtor do ModuleCard.
                Width  = 460,
                Height = 170,
                CardIcon     = icon,
                CardTitle    = title,
                CardSubtitle = subtitle,
                CardBadge    = badge,
                AccentColor  = accent,
            };
            card.Click += (_, __) => onClick();
            cards.Controls.Add(card);
        }

        // Rastreia janelas abertas para não duplicar instâncias do mesmo módulo
        private readonly System.Collections.Generic.Dictionary<Type, Form> _openModules = new();

        private void OpenModule(Form form)
        {
            var type = form.GetType();

            // Se já existe uma janela deste tipo aberta e não foi fechada, foca nela
            if (_openModules.TryGetValue(type, out var existing) && !existing.IsDisposed)
            {
                existing.BringToFront();
                existing.Focus();
                form.Dispose(); // descarta a instância nova que não vamos usar
                return;
            }

            form.StartPosition = FormStartPosition.CenterParent;
            form.FormClosed += (_, __) => _openModules.Remove(type);
            _openModules[type] = form;
            form.Show(this);
        }
    }
}
