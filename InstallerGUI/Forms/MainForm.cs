using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Media;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using InstallerGUI.Models;
using InstallerGUI.Services;

namespace InstallerGUI.Forms
{
    /// <summary>
    /// Formulário principal do instalador automatizado.
    /// Contém a interface gráfica com checkboxes, barra de progresso
    /// e log em tempo real. A lógica de instalação fica no
    /// <see cref="InstallerService"/>.
    /// </summary>
    public class MainForm : Form
    {
        // ──────────────────────────────────────────────
        // Constantes de cores do tema escuro
        // ──────────────────────────────────────────────

        private static readonly Color CorFundo = Color.FromArgb(30, 30, 30);
        private static readonly Color CorPainel = Color.FromArgb(40, 40, 40);
        private static readonly Color CorDestaque = Color.FromArgb(243, 146, 0);
        private static readonly Color CorSucesso = Color.FromArgb(0, 200, 80);
        private static readonly Color CorTexto = Color.White;
        private static readonly Color CorInputBg = Color.FromArgb(60, 60, 60);

        // ──────────────────────────────────────────────
        // Campos
        // ──────────────────────────────────────────────

        private readonly InstallerService _service;
        private bool _installing;

        // ──────────────────────────────────────────────
        // Controles da UI
        // ──────────────────────────────────────────────

        private TextBox _txtComputerName;
        private TextBox _txtAdminPass;
        private TextBox _txtStandardUserName;
        private TextBox _txtStandardUserPass;
        private CheckBox _chkEnableLocation;
        private CheckBox _chkStandardUserNoPwdChange;
        private CheckBox _chkCleanDesktop;
        private CheckBox _chkWallpaper;
        private CheckBox _chkWallpaperNortel;
        private CheckBox _chkUninstallGames;
        private CheckBox _chkRemoveOtherProfiles;
        private CheckBox _chkUninstallThirdPartyAntivirus;
        private CheckBox _chkShortcutSuporteExabit;
        private CheckBox _chkShortcutExabit;
        private CheckBox _chkShortcutExahost;
        private CheckBox _chkShortcutRemoteNeoenergia;
        private CheckBox _chkShortcutWebmailNortel;
        private CheckBox _chkShortcutWebmailTerceirizacao;
        private Panel _pnlPrograms;
        private Button _btnInstall;
        private Button _btnStop;
        private ThinProgressBar _progressBar;
        private Label _lblProgress;
        private ThinProgressBar _downloadProgressBar;
        private Label _lblDownloadStatus;
        private RichTextBox _txtLog;
        private DarkScrollBar _scrollBarPrograms;
        private DarkScrollBar _scrollBarLog;
        private Action _updateProgramListLayout;
        private bool _isSyncingLogScroll;

        // ─── Controles das Abas (Estilo Bootstrap With Tabs) ───
        private Button _btnTabConfig;
        private Button _btnTabShortcuts;
        private Button _btnTabOptions;
        private Button _btnTabFeatures;
        private Panel _pnlTabContent;
        private Panel _pnlTabConfig;
        private Panel _pnlTabShortcuts;
        private Panel _pnlTabOptions;
        private Panel _pnlTabFeatures;
        private int _currentTabIndex;

        // ──────────────────────────────────────────────
        // Construtor
        // ──────────────────────────────────────────────

        /// <summary>
        /// Inicializa o formulário, configura a aparência,
        /// instancia o serviço de instalação e monta a interface.
        /// </summary>
        public MainForm()
        {
            float scale = GetDpiScale();

            // Configurações da janela responsivas ao DPI
            Text = "Auto Instalador de Programas - EXABIT Soluções em TI";

            // Calcula dimensões adaptáveis ao tamanho do monitor e escala
            var workingArea = Screen.PrimaryScreen.WorkingArea;
            int desiredWidth = (int)(1220 * scale);
            int desiredHeight = (int)(780 * scale);

            int formWidth = Math.Min(workingArea.Width - 20, Math.Max(1100, desiredWidth));
            int formHeight = Math.Min(workingArea.Height - 20, Math.Max(700, desiredHeight));

            Size = new Size(formWidth, formHeight);
            MinimumSize = new Size(Math.Min(1050, formWidth), Math.Min(680, formHeight));
            MinimizeBox = true;
            MaximizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = CorFundo;
            ForeColor = CorTexto;
            Font = new Font("Segoe UI", 9.5f);

            // Carrega o ícone do próprio executável
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                // Se não conseguir extrair, segue sem ícone
            }

            // Cria o serviço que fará as instalações
            _service = new InstallerService(Application.StartupPath);

            // Conecta os eventos do serviço aos métodos da UI
            _service.OnLog += AppendLog;
            _service.OnProgress += UpdateProgress;
            _service.OnInstallationCompleted += HandleInstallationCompleted;
            _service.OnDownloadProgress += HandleDownloadProgress;

            // Monta todos os controles na tela
            BuildUI();
        }

        /// <summary>
        /// Obtém o fator de escala de DPI atual do monitor (1.0 = 100%, 1.25 = 125%, 1.5 = 150%).
        /// Permite renderizar a interface de forma nítida e responsiva sem cortar textos em resoluções altas.
        /// </summary>
        private float GetDpiScale()
        {
            try
            {
                int dpi = DeviceDpi;
                if (dpi > 0)
                    return dpi / 96f;
            }
            catch
            {
            }

            try
            {
                using (var g = CreateGraphics())
                {
                    return g.DpiX / 96f;
                }
            }
            catch
            {
                return 1.0f;
            }
        }

        // ──────────────────────────────────────────────
        // P/Invoke DWM e Customização da Barra de Título
        // ──────────────────────────────────────────────

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private const int EM_GETLINECOUNT = 0x00BA;
        private const int EM_LINESCROLL = 0x00B6;
        private const int EM_GETFIRSTVISIBLELINE = 0x00CE;

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWA_TEXT_COLOR = 36;

        /// <summary>
        /// Aplica tema escuro nativo e cor personalizada à barra de título assim que a janela é criada.
        /// </summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyDarkTitleBar();
        }

        /// <summary>
        /// Configura os atributos DWM para renderizar a barra de título em modo escuro/preto com título branco.
        /// </summary>
        private void ApplyDarkTitleBar()
        {
            try
            {
                int useDarkMode = 1;
                if (DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int)) != 0)
                {
                    DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useDarkMode, sizeof(int));
                }

                int captionColor = ColorTranslator.ToWin32(CorFundo);
                DwmSetWindowAttribute(Handle, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));

                int textColor = ColorTranslator.ToWin32(Color.White);
                DwmSetWindowAttribute(Handle, DWMWA_TEXT_COLOR, ref textColor, sizeof(int));
            }
            catch
            {
            }
        }

        /// <summary>
        /// Disparado quando a janela é exibida pela primeira vez na tela.
        /// Executa o diagnóstico inicial do sistema e exibe as informações no painel de log.
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_updateProgramListLayout != null)
            {
                _updateProgramListLayout();
            }
            SyncLogScrollBar();
            DisplayInitialSystemInfoAsync();
        }

        // ══════════════════════════════════════════════
        // CONSTRUÇÃO DA INTERFACE
        // ══════════════════════════════════════════════

        /// <summary>
        /// Monta todos os elementos da interface gráfica
        /// usando TableLayoutPanel como container principal.
        /// </summary>
        private void BuildUI()
        {
            float scale = GetDpiScale();

            // ─────────── Layout raiz: 1 coluna, 2 linhas ───────────
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding((int)(8 * scale)),
                BackColor = CorFundo,
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(78 * scale)));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            root.Controls.Add(BuildHeader(scale), 0, 0);

            // ─────────── Conteúdo: 2 colunas (esquerda / direita) ───────────
            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = CorFundo,
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, (int)(340 * scale)));

            // ─── Lado esquerdo: Abas + Log + Barra de Download ───
            var leftSide = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = CorFundo,
            };
            leftSide.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(250 * scale)));
            leftSide.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            leftSide.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(25 * scale)));

            leftSide.Controls.Add(BuildTabsPanel(scale), 0, 0);
            leftSide.Controls.Add(BuildLogPanel(scale), 0, 1);
            leftSide.Controls.Add(BuildDownloadProgressPanel(scale), 0, 2);

            content.Controls.Add(leftSide, 0, 0);

            // ─── Lado direito: Programas + Instalar + Progresso ───
            content.Controls.Add(BuildRightPanel(scale), 1, 0);

            root.Controls.Add(content, 0, 1);
            Controls.Add(root);
        }

        /// <summary>
        /// Cria o cabeçalho com o título e a logo EXABIT à direita.
        /// </summary>
        private Panel BuildHeader(float scale)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45),
            };

            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.FromArgb(45, 45, 45),
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, (int)(340 * scale)));
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // ─── Lado esquerdo: título e diretório empilhados verticalmente ───
            var leftPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.FromArgb(45, 45, 45),
                Padding = new Padding((int)(10 * scale), (int)(10 * scale), (int)(10 * scale), 0),
            };

            leftPanel.Controls.Add(new Label
            {
                Text = "Auto Instalador de Programas",
                Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
                ForeColor = CorDestaque,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, (int)(2 * scale)),
            });

            leftPanel.Controls.Add(new Label
            {
                Text = string.Format("Diretório: {0}", Application.StartupPath),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.Gray,
                AutoSize = true,
                Margin = new Padding(0),
            });

            tbl.Controls.Add(leftPanel, 0, 0);

            // ─── Lado direito: logo EXABIT proporcional centralizada com o painel de programas ───
            var rightPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45),
                Padding = new Padding((int)(10 * scale), (int)(4 * scale), 0, (int)(6 * scale)),
            };

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var stream = assembly.GetManifestResourceStream("InstallerGUI.logo_exabit.png");
                if (stream != null)
                {
                    var pb = new PictureBox
                    {
                        Image = Image.FromStream(stream),
                        SizeMode = PictureBoxSizeMode.Zoom,
                        Dock = DockStyle.Fill,
                        BackColor = Color.Transparent,
                    };
                    rightPanel.Controls.Add(pb);
                }
            }
            catch
            {
            }

            tbl.Controls.Add(rightPanel, 1, 0);

            panel.Controls.Add(tbl);
            return panel;
        }

        /// <summary>
        /// Cria o container de abas com visual moderno estilo Bootstrap "With Tabs".
        /// Permite alternar entre "Configurações do Sistema", "Atalhos" e "Opções do Windows".
        /// </summary>
        private Panel BuildTabsPanel(float scale)
        {
            var container = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = CorPainel,
                Margin = new Padding(0, (int)(2 * scale), 0, (int)(2 * scale)),
                Padding = new Padding((int)(6 * scale), (int)(4 * scale), (int)(6 * scale), (int)(4 * scale)),
            };

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = CorPainel,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(44 * scale))); // Barra de abas com altura generosa (44px)
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));                // Conteúdo da aba ativa

            // ─── Barra de Abas (Estilo Bootstrap With Tabs) ───
            var tabHeader = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = Color.FromArgb(32, 32, 32),
                Margin = new Padding(0, 0, 0, (int)(4 * scale)),
                Padding = Padding.Empty,
            };
            tabHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32f));
            tabHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18f));
            tabHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24f));
            tabHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26f));
            tabHeader.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // Aba 1: Configurações do Sistema
            _btnTabConfig = CreateTabHeaderButton("Configurações do Sistema", 0, scale);
            tabHeader.Controls.Add(_btnTabConfig, 0, 0);

            // Aba 2: Atalhos
            _btnTabShortcuts = CreateTabHeaderButton("Atalhos", 1, scale);
            tabHeader.Controls.Add(_btnTabShortcuts, 1, 0);

            // Aba 3: Opções do Windows
            _btnTabOptions = CreateTabHeaderButton("Opções do Windows", 2, scale);
            tabHeader.Controls.Add(_btnTabOptions, 2, 0);

            // Aba 4: Funcionalidades
            _btnTabFeatures = CreateTabHeaderButton("Funcionalidades", 3, scale);
            tabHeader.Controls.Add(_btnTabFeatures, 3, 0);

            mainLayout.Controls.Add(tabHeader, 0, 0);

            // ─── Container de Conteúdo das Abas ───
            _pnlTabContent = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = CorPainel,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };

            _pnlTabConfig = BuildTabConfigContent(scale);
            _pnlTabShortcuts = BuildTabShortcutsContent(scale);
            _pnlTabOptions = BuildTabOptionsContent(scale);
            _pnlTabFeatures = BuildTabFeaturesContent(scale);

            _pnlTabContent.Controls.Add(_pnlTabConfig);
            _pnlTabContent.Controls.Add(_pnlTabShortcuts);
            _pnlTabContent.Controls.Add(_pnlTabOptions);
            _pnlTabContent.Controls.Add(_pnlTabFeatures);

            mainLayout.Controls.Add(_pnlTabContent, 0, 1);
            container.Controls.Add(mainLayout);

            // Abre a primeira aba ("Configurações do Sistema") por padrão
            SwitchTab(0);

            return container;
        }

        /// <summary>
        /// Cria o botão de cabeçalho da aba com indicador inferior de seleção no estilo Bootstrap.
        /// </summary>
        private Button CreateTabHeaderButton(string title, int tabIndex, float scale)
        {
            var btn = new Button
            {
                Text = title,
                Tag = tabIndex,
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 10f, FontStyle.Regular),
                ForeColor = Color.FromArgb(170, 170, 170),
                BackColor = Color.FromArgb(32, 32, 32),
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                UseVisualStyleBackColor = false,
            };
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(48, 48, 48);

            int targetIndex = tabIndex;
            btn.Click += (sender, args) => SwitchTab(targetIndex);

            btn.Paint += (sender, e) =>
            {
                var b = (Button)sender;
                int idx = (int)b.Tag;
                bool active = (idx == _currentTabIndex);

                int indicatorHeight = (int)(3 * scale);
                if (indicatorHeight < 2) indicatorHeight = 2;
                int y = b.Height - indicatorHeight;

                if (active)
                {
                    using (var brush = new SolidBrush(CorDestaque))
                    {
                        e.Graphics.FillRectangle(brush, 0, y, b.Width, indicatorHeight);
                    }
                }
                else
                {
                    using (var brush = new SolidBrush(Color.FromArgb(55, 55, 55)))
                    {
                        e.Graphics.FillRectangle(brush, 0, b.Height - 1, b.Width, 1);
                    }
                }
            };

            return btn;
        }

        /// <summary>
        /// Alterna a exibição entre as abas do painel esquerdo.
        /// </summary>
        private void SwitchTab(int index)
        {
            _currentTabIndex = index;

            if (_pnlTabConfig != null)
            {
                _pnlTabConfig.Visible = (index == 0);
                if (index == 0) _pnlTabConfig.BringToFront();
            }
            if (_pnlTabShortcuts != null)
            {
                _pnlTabShortcuts.Visible = (index == 1);
                if (index == 1) _pnlTabShortcuts.BringToFront();
            }
            if (_pnlTabOptions != null)
            {
                _pnlTabOptions.Visible = (index == 2);
                if (index == 2) _pnlTabOptions.BringToFront();
            }
            if (_pnlTabFeatures != null)
            {
                _pnlTabFeatures.Visible = (index == 3);
                if (index == 3) _pnlTabFeatures.BringToFront();
            }

            UpdateTabButtonStyle(_btnTabConfig, index == 0);
            UpdateTabButtonStyle(_btnTabShortcuts, index == 1);
            UpdateTabButtonStyle(_btnTabOptions, index == 2);
            UpdateTabButtonStyle(_btnTabFeatures, index == 3);

            if (_btnTabConfig != null) _btnTabConfig.Invalidate();
            if (_btnTabShortcuts != null) _btnTabShortcuts.Invalidate();
            if (_btnTabOptions != null) _btnTabOptions.Invalidate();
            if (_btnTabFeatures != null) _btnTabFeatures.Invalidate();
        }

        /// <summary>
        /// Atualiza o estilo visual do botão da aba (destaque ativo vs inativo).
        /// </summary>
        private void UpdateTabButtonStyle(Button btn, bool isActive)
        {
            if (btn == null) return;

            if (isActive)
            {
                btn.ForeColor = CorDestaque;
                btn.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
                btn.BackColor = CorPainel;
            }
            else
            {
                btn.ForeColor = Color.FromArgb(170, 170, 170);
                btn.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
                btn.BackColor = Color.FromArgb(32, 32, 32);
            }
        }

        /// <summary>
        /// Conteúdo da Aba 1: Configurações do Sistema (Nome do computador, senhas e usuário).
        /// </summary>
        private Panel BuildTabConfigContent(float scale)
        {
            var pnl = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = CorPainel,
                Padding = new Padding((int)(12 * scale), (int)(10 * scale), (int)(12 * scale), (int)(6 * scale)),
            };

            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                BackColor = CorPainel,
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            for (int i = 0; i < 4; i++)
                tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 25f));

            tbl.Controls.Add(CreateLabel("Nome do computador:"), 0, 0);
            _txtComputerName = CreateTextBox("Em branco = não renomear");
            _txtComputerName.Text = "";
            tbl.Controls.Add(_txtComputerName, 1, 0);

            tbl.Controls.Add(CreateLabel("Senha do Admin:"), 0, 1);
            _txtAdminPass = CreateTextBox("Senha do admin");
            _txtAdminPass.Text = "";
            tbl.Controls.Add(_txtAdminPass, 1, 1);

            tbl.Controls.Add(CreateLabel("Usuário padrão:"), 0, 2);
            _txtStandardUserName = CreateTextBox("Nome do usuário");
            _txtStandardUserName.Text = "";
            tbl.Controls.Add(_txtStandardUserName, 1, 2);

            tbl.Controls.Add(CreateLabel("Senha do usuário:"), 0, 3);
            _txtStandardUserPass = CreateTextBox("Senha (opcional)");
            _txtStandardUserPass.Text = "";
            tbl.Controls.Add(_txtStandardUserPass, 1, 3);

            pnl.Controls.Add(tbl);
            return pnl;
        }

        /// <summary>
        /// Conteúdo da Aba 2: Atalhos corporativos da Área de Trabalho.
        /// </summary>
        private Panel BuildTabShortcutsContent(float scale)
        {
            var pnl = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = CorPainel,
                Padding = new Padding((int)(12 * scale), (int)(8 * scale), (int)(12 * scale), (int)(6 * scale)),
            };

            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                BackColor = CorPainel,
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

            for (int i = 0; i < 4; i++)
                tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 25f));

            // Coluna 0
            _chkShortcutSuporteExabit = CreateCheckBox("Atalho Suporte Exabit", false);
            tbl.Controls.Add(_chkShortcutSuporteExabit, 0, 0);

            _chkShortcutExabit = CreateCheckBox("Atalho Exabit Soluções em TI", false);
            tbl.Controls.Add(_chkShortcutExabit, 0, 1);

            _chkShortcutExahost = CreateCheckBox("Atalho Exahost Hospedagem", false);
            tbl.Controls.Add(_chkShortcutExahost, 0, 2);

            _chkShortcutRemoteNeoenergia = CreateCheckBox("Atalho Remote Neoenergia", false);
            tbl.Controls.Add(_chkShortcutRemoteNeoenergia, 0, 3);

            // Coluna 1
            _chkShortcutWebmailNortel = CreateCheckBox("Atalho Webmail Nortel", false);
            tbl.Controls.Add(_chkShortcutWebmailNortel, 1, 0);

            _chkShortcutWebmailTerceirizacao = CreateCheckBox("Atalho Webmail Nortel Terceirização", false);
            tbl.Controls.Add(_chkShortcutWebmailTerceirizacao, 1, 1);

            _chkCleanDesktop = CreateCheckBox("Limpar atalhos da Área de Trabalho", true);
            tbl.Controls.Add(_chkCleanDesktop, 1, 2);

            pnl.Controls.Add(tbl);
            return pnl;
        }

        /// <summary>
        /// Conteúdo da Aba 3: Opções do Windows (Localização, restrição de senha e papel de parede).
        /// </summary>
        private Panel BuildTabOptionsContent(float scale)
        {
            var pnl = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = CorPainel,
                Padding = new Padding((int)(16 * scale), (int)(12 * scale), (int)(16 * scale), (int)(8 * scale)),
            };

            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                BackColor = CorPainel,
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 22f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 22f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 22f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 22f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 12f));

            _chkEnableLocation = CreateCheckBox("Ativar serviços de localização do Windows", true);
            tbl.Controls.Add(_chkEnableLocation, 0, 0);

            _chkStandardUserNoPwdChange = CreateCheckBox("Impedir que o usuário altere a senha", false);
            tbl.Controls.Add(_chkStandardUserNoPwdChange, 0, 1);

            _chkWallpaper = CreateCheckBox("Aplicar papel de parede personalizado", false);
            _chkWallpaperNortel = CreateCheckBox("Aplicar papel de parede Nortel", false);

            _chkWallpaper.CheckedChanged += (s, e) =>
            {
                if (_chkWallpaper.Checked && _chkWallpaperNortel.Checked)
                {
                    _chkWallpaperNortel.Checked = false;
                }
            };

            _chkWallpaperNortel.CheckedChanged += (s, e) =>
            {
                if (_chkWallpaperNortel.Checked && _chkWallpaper.Checked)
                {
                    _chkWallpaper.Checked = false;
                }
            };

            tbl.Controls.Add(_chkWallpaper, 0, 2);
            tbl.Controls.Add(_chkWallpaperNortel, 0, 3);

            var lblHint = new Label
            {
                Text = "Configurações de segurança e personalização aplicadas durante o processo.",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                ForeColor = Color.FromArgb(160, 160, 160),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            tbl.Controls.Add(lblHint, 0, 4);

            pnl.Controls.Add(tbl);
            return pnl;
        }

        /// <summary>
        /// Conteúdo da Aba 4: Funcionalidades (Remoção de jogos, limpeza de perfis e remoção de antivírus).
        /// </summary>
        private Panel BuildTabFeaturesContent(float scale)
        {
            var pnl = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = CorPainel,
                Padding = new Padding((int)(16 * scale), (int)(12 * scale), (int)(16 * scale), (int)(8 * scale)),
            };

            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = CorPainel,
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 28f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 28f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 28f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 16f));

            _chkUninstallGames = CreateCheckBox("Desinstalar silenciosamente todos os jogos do computador", false);
            _chkRemoveOtherProfiles = CreateCheckBox("Remover e apagar todos os perfis do Windows (exceto novos)", false);
            _chkUninstallThirdPartyAntivirus = CreateCheckBox("Desinstalar silenciosamente todo e qualquer antivírus de terceiros", false);

            tbl.Controls.Add(_chkUninstallGames, 0, 0);
            tbl.Controls.Add(_chkRemoveOtherProfiles, 0, 1);
            tbl.Controls.Add(_chkUninstallThirdPartyAntivirus, 0, 2);

            var lblHint = new Label
            {
                Text = "Estas ações preliminares serão executadas primeiro, antes de qualquer instalação.",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                ForeColor = Color.FromArgb(160, 160, 160),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            tbl.Controls.Add(lblHint, 0, 3);

            pnl.Controls.Add(tbl);
            return pnl;
        }

        /// <summary>
        /// Cria a barra de carregamento dedicada para downloads online abaixo da janela de log,
        /// com perfil ultrafino (5px) no estilo RustDesk corporativo.
        /// </summary>
        private Panel BuildDownloadProgressPanel(float scale)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = CorPainel,
                Margin = Padding.Empty,
                Padding = new Padding(0, (int)(1 * scale), 0, (int)(1 * scale)),
            };

            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = CorPainel,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(5 * scale))); // Barra ultrafina de download
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));             // Status do download

            _downloadProgressBar = new ThinProgressBar
            {
                Dock = DockStyle.Fill,
                TrackColor = Color.FromArgb(45, 45, 45),
                ProgressColor = CorDestaque,
                Value = 0,
                Maximum = 100,
                Margin = Padding.Empty,
            };

            _lblDownloadStatus = new Label
            {
                Text = "Downloads: Aguardando...",
                ForeColor = Color.FromArgb(170, 170, 170),
                Font = new Font("Segoe UI", 8.25f, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                Margin = new Padding((int)(2 * scale), 0, 0, 0),
                AutoEllipsis = true,
            };

            tbl.Controls.Add(_downloadProgressBar, 0, 0);
            tbl.Controls.Add(_lblDownloadStatus, 0, 1);

            panel.Controls.Add(tbl);
            return panel;
        }

        /// <summary>
        /// Cria o painel de seleção de programas com checkboxes e barra de rolagem moderna personalizada.
        /// </summary>
        private GroupBox BuildProgramsPanel(float scale)
        {
            var group = new GroupBox
            {
                Text = "Programas para Instalar",
                ForeColor = CorDestaque,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                BackColor = CorPainel,
                Padding = new Padding((int)(6 * scale), (int)(24 * scale), (int)(6 * scale), (int)(4 * scale)),
            };

            // Container em tabela para isolar a lista com barra de rolagem dos botões inferiores
            var tblContainer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = CorPainel,
                Margin = new Padding(0),
                Padding = new Padding(0),
            };
            tblContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            tblContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            tblContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(32 * scale)));

            // ─── Linha de botões Marcar/Desmarcar (centralizados na parte inferior) ───
            var btnPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = CorPainel,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0, (int)(1 * scale), 0, (int)(2 * scale)),
            };
            btnPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            btnPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            btnPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var btnSelectAll = new Button
            {
                Text = "Marcar",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = CorTexto,
                BackColor = Color.FromArgb(60, 60, 60),
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Cursor = Cursors.Hand,
                Size = new Size((int)(100 * scale), (int)(26 * scale)),
                Anchor = AnchorStyles.Right,
                Margin = new Padding(0, 0, (int)(4 * scale), 0),
            };
            btnSelectAll.FlatAppearance.MouseOverBackColor = Color.FromArgb(80, 80, 80);
            btnSelectAll.Click += (s, e) => SetAllCheckBoxes(true);

            var btnClearAll = new Button
            {
                Text = "Desmarcar",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = CorTexto,
                BackColor = Color.FromArgb(60, 60, 60),
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Cursor = Cursors.Hand,
                Size = new Size((int)(100 * scale), (int)(26 * scale)),
                Anchor = AnchorStyles.Left,
                Margin = new Padding((int)(4 * scale), 0, 0, 0),
            };
            btnClearAll.FlatAppearance.MouseOverBackColor = Color.FromArgb(80, 80, 80);
            btnClearAll.Click += (s, e) => SetAllCheckBoxes(false);

            var toolTip = new ToolTip();
            toolTip.SetToolTip(btnSelectAll, "Marcar todos os programas");
            toolTip.SetToolTip(btnClearAll, "Desmarcar todos os programas");

            btnPanel.Controls.Add(btnSelectAll, 0, 0);
            btnPanel.Controls.Add(btnClearAll, 1, 0);

            // ─── Container viewport para a lista de programas com barra de rolagem personalizada ───
            var pnlViewport = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = CorPainel,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            EnableDoubleBuffering(pnlViewport);

            _scrollBarPrograms = new DarkScrollBar
            {
                Dock = DockStyle.Right,
                Width = (int)(10 * scale),
                TrackColor = CorPainel,
                ThumbColor = CorDestaque,
                SmallChange = (int)(24 * scale),
                Margin = Padding.Empty,
            };

            // ─── Lista interna de checkboxes (sem barra nativa do Win32) ───
            _pnlPrograms = new Panel
            {
                Location = new Point(0, 0),
                BackColor = CorPainel,
                BorderStyle = BorderStyle.None,
                AutoScroll = false,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                Margin = Padding.Empty,
                Padding = new Padding(0, (int)(4 * scale), 0, (int)(4 * scale)),
            };
            EnableDoubleBuffering(_pnlPrograms);

            // Rolagem fluida com a roda do mouse em qualquer ponto do viewport ou dos controles
            Action<MouseEventArgs> handleProgramMouseWheel = (e) =>
            {
                if (_scrollBarPrograms != null && _scrollBarPrograms.Maximum > 0)
                {
                    int step = (int)(24 * scale);
                    int delta = -Math.Sign(e.Delta) * step;
                    _scrollBarPrograms.ScrollBy(delta);
                }
            };

            pnlViewport.MouseWheel += (s, e) => handleProgramMouseWheel(e);
            _pnlPrograms.MouseWheel += (s, e) => handleProgramMouseWheel(e);

            int top = (int)(6 * scale);
            int leftMargin = (int)(10 * scale);
            string lastCategory = null;

            foreach (ProgramInfo prog in GetDefaultProgramList())
            {
                string cat = prog.Category ?? "Geral";
                if (lastCategory != null && !string.Equals(cat, lastCategory, StringComparison.OrdinalIgnoreCase))
                {
                    top += (int)(6 * scale);

                    // Divisória horizontal separando as categorias
                    var divider = new Panel
                    {
                        Location = new Point(leftMargin, top),
                        Size = new Size(Math.Max(100, (int)(320 * scale) - leftMargin - (int)(15 * scale)), 1),
                        BackColor = Color.FromArgb(70, 70, 70),
                        Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    };
                    divider.MouseWheel += (s, e) => handleProgramMouseWheel(e);
                    _pnlPrograms.Controls.Add(divider);
                    top += (int)(6 * scale);

                    // Rótulo com o título da categoria
                    var lblCategory = new Label
                    {
                        Text = cat,
                        Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                        ForeColor = CorDestaque,
                        Location = new Point(leftMargin, top),
                        AutoSize = true,
                    };
                    lblCategory.MouseWheel += (s, e) => handleProgramMouseWheel(e);
                    _pnlPrograms.Controls.Add(lblCategory);
                    top += lblCategory.Height + (int)(4 * scale);
                }
                lastCategory = cat;

                var chk = new CheckBox
                {
                    Text = prog.DisplayName,
                    Checked = prog.DefaultChecked,
                    Tag = prog,
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                    ForeColor = CorTexto,
                    BackColor = Color.Transparent,
                    UseVisualStyleBackColor = false,
                    AutoSize = true,
                    Location = new Point(leftMargin, top),
                };
                chk.Size = chk.GetPreferredSize(new Size(int.MaxValue, int.MaxValue));
                chk.MouseWheel += (s, e) => handleProgramMouseWheel(e);

                var capturedChk = chk;
                capturedChk.GotFocus += (s, e) =>
                {
                    int chkTop = capturedChk.Top + _pnlPrograms.Top;
                    int chkBottom = chkTop + capturedChk.Height;
                    if (chkTop < 0)
                    {
                        _scrollBarPrograms.Value = Math.Max(0, capturedChk.Top - (int)(8 * scale));
                    }
                    else if (pnlViewport.ClientSize.Height > 0 && chkBottom > pnlViewport.ClientSize.Height)
                    {
                        int targetVal = capturedChk.Bottom - pnlViewport.ClientSize.Height + (int)(8 * scale);
                        _scrollBarPrograms.Value = Math.Min(_scrollBarPrograms.Maximum, targetVal);
                    }
                };

                _pnlPrograms.Controls.Add(chk);
                top += chk.Height + (int)(3 * scale);
            }

            int totalContentHeight = top + (int)(12 * scale);
            _pnlPrograms.Height = totalContentHeight;

            _updateProgramListLayout = () =>
            {
                if (pnlViewport.ClientSize.Width <= 0 || pnlViewport.ClientSize.Height <= 0) return;

                int scrollWidth = _scrollBarPrograms.Width;
                int availW = Math.Max(100, pnlViewport.ClientSize.Width - scrollWidth);
                _pnlPrograms.Width = availW;

                int viewH = pnlViewport.ClientSize.Height;
                int maxScroll = Math.Max(0, totalContentHeight - viewH);
                _scrollBarPrograms.Maximum = maxScroll;
                _scrollBarPrograms.LargeChange = Math.Max(1, viewH);

                if (_scrollBarPrograms.Value > maxScroll)
                {
                    _scrollBarPrograms.Value = maxScroll;
                }

                _pnlPrograms.Top = -_scrollBarPrograms.Value;
            };

            pnlViewport.Resize += (s, e) => _updateProgramListLayout();
            _scrollBarPrograms.ValueChanged += (s, e) =>
            {
                _pnlPrograms.Top = -_scrollBarPrograms.Value;
            };

            pnlViewport.Controls.Add(_pnlPrograms);
            pnlViewport.Controls.Add(_scrollBarPrograms);
            _scrollBarPrograms.BringToFront();

            tblContainer.Controls.Add(pnlViewport, 0, 0);
            tblContainer.Controls.Add(btnPanel, 0, 1);
            group.Controls.Add(tblContainer);

            return group;
        }

        /// <summary>
        /// Cria a barra de progresso geral ultrafina (estilo RustDesk) e o rótulo de contagem (n / total).
        /// </summary>
        private Panel BuildProgressBar(float scale)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = CorPainel,
                Margin = Padding.Empty,
                Padding = new Padding(0, (int)(1 * scale), 0, (int)(1 * scale)),
            };

            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = CorPainel,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(5 * scale))); // Barra ultrafina de progresso geral
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));             // Contagem da etapa

            _progressBar = new ThinProgressBar
            {
                Dock = DockStyle.Fill,
                TrackColor = Color.FromArgb(45, 45, 45),
                ProgressColor = CorSucesso,
                Value = 0,
                Maximum = 100,
                Margin = Padding.Empty,
            };

            _lblProgress = new Label
            {
                Text = "0 / 0",
                ForeColor = Color.FromArgb(180, 180, 180),
                Font = new Font("Segoe UI", 8.25f, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, (int)(2 * scale), 0),
            };

            tbl.Controls.Add(_progressBar, 0, 0);
            tbl.Controls.Add(_lblProgress, 0, 1);

            panel.Controls.Add(tbl);
            return panel;
        }

        /// <summary>
        /// Cria o painel de log onde as mensagens da instalação aparecem,
        /// com barra de rolagem personalizada na mesma cor do fundo e thumb laranja.
        /// </summary>
        private Panel BuildLogPanel(float scale)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 20, 20),
                Margin = new Padding(0, (int)(5 * scale), 0, 0),
            };

            _scrollBarLog = new DarkScrollBar
            {
                Dock = DockStyle.Right,
                Width = (int)(10 * scale),
                TrackColor = Color.FromArgb(20, 20, 20),
                ThumbColor = CorDestaque,
                SmallChange = 1,
                LargeChange = 5,
                Margin = Padding.Empty,
            };

            _txtLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 20, 20),
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Consolas", 9),
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                WordWrap = true,
                ScrollBars = RichTextBoxScrollBars.None, // Oculta barra nativa branca do Win32
            };

            _scrollBarLog.ValueChanged += (s, e) =>
            {
                if (_isSyncingLogScroll || !_txtLog.IsHandleCreated) return;
                try
                {
                    _isSyncingLogScroll = true;
                    int currentFirst = SendMessage(_txtLog.Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero).ToInt32();
                    int targetFirst = _scrollBarLog.Value;
                    int delta = targetFirst - currentFirst;
                    if (delta != 0)
                    {
                        SendMessage(_txtLog.Handle, EM_LINESCROLL, IntPtr.Zero, (IntPtr)delta);
                    }
                }
                finally
                {
                    _isSyncingLogScroll = false;
                }
            };

            _txtLog.MouseWheel += (s, e) =>
            {
                if (_scrollBarLog != null && _scrollBarLog.Maximum > 0)
                {
                    int step = 3;
                    int delta = -Math.Sign(e.Delta) * step;
                    _scrollBarLog.ScrollBy(delta);
                }
            };

            _txtLog.KeyDown += (s, e) =>
            {
                BeginInvoke(new Action(SyncLogScrollBar));
            };

            _txtLog.Resize += (s, e) => SyncLogScrollBar();
            _txtLog.TextChanged += (s, e) => SyncLogScrollBar();

            panel.Controls.Add(_txtLog);
            panel.Controls.Add(_scrollBarLog);
            _scrollBarLog.BringToFront();

            return panel;
        }

        /// <summary>
        /// Cria o painel lateral direito combinando:
        /// lista de programas (scroll), botões de ação (Iniciar e Parar lado a lado) e barra de progresso.
        /// </summary>
        private Panel BuildRightPanel(float scale)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = CorFundo,
                Padding = new Padding((int)(10 * scale), 0, 0, 0),
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = CorFundo,
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Programas
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(40 * scale))); // Botões Instalar e Parar (lado a lado)
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(25 * scale))); // Progresso (ultrafino)

            layout.Controls.Add(BuildProgramsPanel(scale), 0, 0);

            // ─── Linha de Ação: Botões Instalar e Parar Lado a Lado ───
            var actionButtonsTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = CorFundo,
                Margin = Padding.Empty,
                Padding = new Padding(0, (int)(2 * scale), 0, (int)(2 * scale)),
            };
            actionButtonsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72f)); // INICIAR INSTALAÇÃO
            actionButtonsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28f)); // PARAR
            actionButtonsTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _btnInstall = new Button
            {
                Text = "INICIAR INSTALAÇÃO",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = CorTexto,
                BackColor = Color.FromArgb(0, 150, 50),
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Cursor = Cursors.Hand,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, (int)(4 * scale), 0),
            };
            _btnInstall.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 180, 70);
            _btnInstall.Click += BtnInstall_Click;
            actionButtonsTable.Controls.Add(_btnInstall, 0, 0);

            _btnStop = new Button
            {
                Text = "PARAR",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Dock = DockStyle.Fill,
                Margin = new Padding((int)(2 * scale), 0, 0, 0),
            };
            SetStopButtonState(false);
            _btnStop.Click += BtnStop_Click;
            actionButtonsTable.Controls.Add(_btnStop, 1, 0);

            layout.Controls.Add(actionButtonsTable, 0, 1);
            layout.Controls.Add(BuildProgressBar(scale), 0, 2);

            panel.Controls.Add(layout);
            return panel;
        }

        // ══════════════════════════════════════════════
        // EVENTOS DA INTERFACE
        // ══════════════════════════════════════════════

        /// <summary>
        /// Disparado ao clicar no botão "INICIAR INSTALAÇÃO".
        /// Valida as opções, monta o <see cref="InstallationOptions"/>
        /// e chama o serviço de instalação.
        /// </summary>
        private async void BtnInstall_Click(object sender, EventArgs e)
        {
            // Evita cliques duplos durante a instalação
            if (_installing) return;

            var selectedPrograms = new List<ProgramInfo>();
            foreach (Control c in _pnlPrograms.Controls)
            {
                var chk = c as CheckBox;
                if (chk != null && chk.Checked)
                {
                    var prog = chk.Tag as ProgramInfo;
                    if (prog != null)
                    {
                        selectedPrograms.Add(prog);
                    }
                }
            }

            // Prepara a interface para a instalação
            _installing = true;
            _btnInstall.Enabled = false;
            _btnInstall.BackColor = Color.FromArgb(0, 75, 25);
            if (_downloadProgressBar != null) _downloadProgressBar.Value = 0;
            if (_lblDownloadStatus != null)
            {
                _lblDownloadStatus.Text = "Downloads: Aguardando...";
                _lblDownloadStatus.ForeColor = Color.FromArgb(180, 180, 180);
            }
            AppendLog("──────────────────────────────────────────────────────────────────────", ConsoleColor.Cyan);
            AppendLog(" INICIANDO PROCESSO DE INSTALAÇÃO", ConsoleColor.Cyan);
            AppendLog("──────────────────────────────────────────────────────────────────────", ConsoleColor.Cyan);

            try
            {
                if (selectedPrograms.Count > 0)
                {
                    _btnInstall.Text = "VERIFICANDO INTERNET...";
                    while (!await _service.HasInternetConnectionAsync())
                    {
                        var retryResult = MessageBox.Show(
                            "Sem conexão com a internet.\n\n" +
                            "Os programas selecionados serão baixados e instalados online diretamente da internet.\n\n" +
                            "Conecte-se à internet antes de continuar e clique em \"Repetir\", ou em \"Cancelar\" para voltar.",
                            "Conexão com a Internet Necessária",
                            MessageBoxButtons.RetryCancel,
                            MessageBoxIcon.Warning);

                        if (retryResult != DialogResult.Retry)
                        {
                            AppendLog("X Instalação cancelada: conecte-se à internet antes de instalar.", ConsoleColor.Yellow);
                            return;
                        }
                    }
                }

                _btnInstall.Text = "INSTALANDO...";
                SetStopButtonState(true);

                // Inicia a instalação no serviço
                _service.StartInstallation();

                // Monta as opções com base no que o usuário selecionou
                var options = new InstallationOptions();

                options.RenameComputer = !string.IsNullOrWhiteSpace(_txtComputerName.Text);
                options.ComputerName = _txtComputerName.Text.Trim();

                options.CreateAdmin = true;
                options.AdminPassword = _txtAdminPass.Text;

                string stdName = _txtStandardUserName.Text.Trim();
                bool criarUsuarioPadrao = !string.IsNullOrWhiteSpace(stdName);

                options.CreateStandardUser = criarUsuarioPadrao;
                options.StandardUserName = criarUsuarioPadrao ? stdName : null;
                options.StandardUserPassword = criarUsuarioPadrao ? _txtStandardUserPass.Text : null;
                options.StandardUserCannotChangePassword = criarUsuarioPadrao && _chkStandardUserNoPwdChange.Checked;

                options.EnableLocationServices = _chkEnableLocation.Checked;
                options.CleanDesktopShortcuts = _chkCleanDesktop.Checked;
                options.SetWallpaper = _chkWallpaper.Checked;
                options.SetWallpaperNortel = _chkWallpaperNortel.Checked;
                options.UninstallGames = _chkUninstallGames != null && _chkUninstallGames.Checked;
                options.RemoveOtherUserProfiles = _chkRemoveOtherProfiles != null && _chkRemoveOtherProfiles.Checked;
                options.UninstallThirdPartyAntivirus = _chkUninstallThirdPartyAntivirus != null && _chkUninstallThirdPartyAntivirus.Checked;

                // ─── Atalhos corporativos ───
                options.CreateDesktopShortcutSuporteExabit = _chkShortcutSuporteExabit.Checked;
                options.CreateDesktopShortcutSuporteNorte = _chkShortcutSuporteExabit.Checked;
                options.CreateDesktopShortcutExabit = _chkShortcutExabit.Checked;
                options.CreateDesktopShortcutExahost = _chkShortcutExahost.Checked;
                options.CreateDesktopShortcutRemoteNeoenergia = _chkShortcutRemoteNeoenergia.Checked;
                options.CreateDesktopShortcutWebmailNortel = _chkShortcutWebmailNortel.Checked;
                options.CreateDesktopShortcutWebmailTerceirizacao = _chkShortcutWebmailTerceirizacao.Checked;

                options.SelectedPrograms.AddRange(selectedPrograms);

                // Executa toda a instalação
                await _service.RunFullInstallationAsync(options);
            }
            catch (Exception ex)
            {
                AppendLog(string.Format("\nX ERRO GERAL: {0}", ex.Message), ConsoleColor.Red);
                MessageBox.Show(
                    string.Format("Ocorreu um erro durante a instalação:\n\n{0}",
                    ex.Message),
                    "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _installing = false;
                _btnInstall.Enabled = true;
                _btnInstall.BackColor = Color.FromArgb(0, 150, 50);
                _btnInstall.Text = "INICIAR INSTALAÇÃO";
                SetStopButtonState(false);
            }
        }

        /// <summary>
        /// Disparado ao clicar no botão "PARAR".
        /// Solicita o cancelamento da instalação.
        /// </summary>
        private void BtnStop_Click(object sender, EventArgs e)
        {
            if (!_installing) return;

            var result = MessageBox.Show(
                "Tem certeza que deseja parar a instalação?",
                "Confirmar parada",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                _service.CancelInstallation();
                SetStopButtonState(false);
                AppendLog("Solicitação de parada enviada...", ConsoleColor.Yellow);
            }
        }

        /// <summary>
        /// Atualiza o estado visual e funcional do botão PARAR.
        /// Quando ativo (durante as instalações): habilitado, com fundo vermelho vivo e texto destacado.
        /// Quando inativo (antes de iniciar ou após finalizar): desabilitado, com fundo escuro/neutro e texto apagado.
        /// </summary>
        private void SetStopButtonState(bool active)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<bool>(SetStopButtonState), active);
                return;
            }

            if (active)
            {
                _btnStop.Enabled = true;
                _btnStop.BackColor = Color.FromArgb(190, 40, 40);
                _btnStop.ForeColor = Color.White;
                _btnStop.Cursor = Cursors.Hand;
                _btnStop.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 50, 50);
            }
            else
            {
                _btnStop.Enabled = false;
                _btnStop.BackColor = Color.FromArgb(50, 50, 50);
                _btnStop.ForeColor = Color.FromArgb(110, 110, 110);
                _btnStop.Cursor = Cursors.Default;
                _btnStop.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 50, 50);
            }
        }

        // ══════════════════════════════════════════════
        // MÉTODOS AUXILIARES
        // ══════════════════════════════════════════════

        /// <summary>
        /// Define o estado de todos os checkboxes de programas.
        /// </summary>
        /// <param name="checkedState">true para marcar, false para desmarcar</param>
        private void SetAllCheckBoxes(bool checkedState)
        {
            foreach (Control c in _pnlPrograms.Controls)
            {
                var chk = c as CheckBox;
                if (chk != null) chk.Checked = checkedState;
            }
        }

        /// <summary>
        /// Atualiza a barra de progresso e o contador (etapa / total).
        /// Chamado pelo evento <see cref="InstallerService.OnProgress"/>.
        /// </summary>
        private void UpdateProgress(int currentStep, int totalSteps)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateProgress(currentStep, totalSteps)));
                return;
            }
            _progressBar.Maximum = Math.Max(totalSteps, 1);
            _progressBar.Value = Math.Min(currentStep, totalSteps);
            _lblProgress.Text = string.Format("{0} / {1}", currentStep, totalSteps);
        }

        /// <summary>
        /// Atualiza a barra de progresso dedicada e o rótulo de download durante a transferência de arquivos online.
        /// Chamado pelo evento <see cref="InstallerService.OnDownloadProgress"/>.
        /// </summary>
        private void HandleDownloadProgress(string displayName, int percent, long bytesReceived, long totalBytes)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => HandleDownloadProgress(displayName, percent, bytesReceived, totalBytes)));
                return;
            }

            if (_downloadProgressBar == null || _lblDownloadStatus == null) return;

            if (percent >= 0)
            {
                _downloadProgressBar.Value = Math.Max(0, Math.Min(100, percent));
                double mbRead = (double)bytesReceived / (1024 * 1024);
                double mbTotal = (double)totalBytes / (1024 * 1024);
                _lblDownloadStatus.Text = string.Format("{0}: {1}% ({2:N0}/{3:N0} MB)", displayName, percent, mbRead, mbTotal);
                _lblDownloadStatus.ForeColor = CorDestaque;
            }
            else
            {
                double mbRead = (double)bytesReceived / (1024 * 1024);
                _lblDownloadStatus.Text = string.Format("{0}: {1:N1} MB", displayName, mbRead);
                _lblDownloadStatus.ForeColor = CorDestaque;
            }
        }

        /// <summary>
        /// Coleta e exibe o diagnóstico e informações do sistema operacional ao inicializar o aplicativo.
        /// </summary>
        private async void DisplayInitialSystemInfoAsync()
        {
            _txtLog.Clear();
            SyncLogScrollBar();
            AppendLog("======================================================================", ConsoleColor.Cyan);
            AppendLog(" INFORMAÇÕES DO SISTEMA", ConsoleColor.Cyan);
            AppendLog("======================================================================", ConsoleColor.Cyan);
            AppendLog(string.Format("Nome do computador : {0}", Environment.MachineName), ConsoleColor.Gray);

            string userRole = "";
            try
            {
                var principal = new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent());
                if (principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))
                    userRole = " (Administrador)";
            }
            catch { }
            AppendLog(string.Format("Nome do usuário    : {0}{1}", Environment.UserName, userRole), ConsoleColor.Gray);

            string ip = GetLocalIpAddress();
            AppendLog(string.Format("Endereço IP        : {0}", ip), ConsoleColor.Gray);

            bool hasInternet = await _service.HasInternetConnectionAsync();
            if (hasInternet)
            {
                AppendLog("Status de conexão  : Conectado à Internet", ConsoleColor.Green);
                var pingDiag = await _service.MeasureInternetPingAsync();
                AppendLog(string.Format("Teste de Ping      : {0}", pingDiag.Text), pingDiag.Color);
            }
            else
            {
                AppendLog("Status de conexão  : Desconectado da Internet", ConsoleColor.Yellow);
            }

            var diskDiag = GetSystemDriveFreeSpaceDiagnosis();
            AppendLog(string.Format("Espaço livre em C: : {0}", diskDiag.Text), diskDiag.Color);

            string winVer = GetWindowsVersionInfo();
            AppendLog(string.Format("Versão do Windows  : {0}", winVer), ConsoleColor.Gray);
            AppendLog("======================================================================\n", ConsoleColor.Cyan);
            AppendLog("Pronto para iniciar. Configure as opções e selecione os programas desejados.\n", ConsoleColor.Gray);
        }

        /// <summary>
        /// Obtém o diagnóstico de espaço total e livre da unidade de sistema (C:) com classificação de cor para alerta visual.
        /// </summary>
        private static DiskSpaceDiagnosis GetSystemDriveFreeSpaceDiagnosis()
        {
            try
            {
                string sysDrive = Path.GetPathRoot(Environment.SystemDirectory);
                if (string.IsNullOrEmpty(sysDrive)) sysDrive = "C:\\";
                var drive = new DriveInfo(sysDrive);
                if (drive.IsReady)
                {
                    double freeGb = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                    double totalGb = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                    double percentFree = (totalGb > 0) ? (freeGb / totalGb * 100.0) : 0.0;

                    if (freeGb < 15.0 || percentFree < 10.0)
                    {
                        return new DiskSpaceDiagnosis
                        {
                            FreeGb = freeGb,
                            TotalGb = totalGb,
                            PercentFree = percentFree,
                            Text = string.Format("{0:N1} GB livres de {1:N1} GB ({2:N0}% livre) - Pouco espaço", freeGb, totalGb, percentFree),
                            Color = ConsoleColor.Red
                        };
                    }
                    else if (freeGb < 30.0 || percentFree < 20.0)
                    {
                        return new DiskSpaceDiagnosis
                        {
                            FreeGb = freeGb,
                            TotalGb = totalGb,
                            PercentFree = percentFree,
                            Text = string.Format("{0:N1} GB livres de {1:N1} GB ({2:N0}% livre) - Atenção", freeGb, totalGb, percentFree),
                            Color = ConsoleColor.Yellow
                        };
                    }
                    else
                    {
                        return new DiskSpaceDiagnosis
                        {
                            FreeGb = freeGb,
                            TotalGb = totalGb,
                            PercentFree = percentFree,
                            Text = string.Format("{0:N1} GB livres de {1:N1} GB ({2:N0}% livre) - Bom", freeGb, totalGb, percentFree),
                            Color = ConsoleColor.Green
                        };
                    }
                }
            }
            catch { }

            return new DiskSpaceDiagnosis
            {
                FreeGb = 0,
                TotalGb = 0,
                PercentFree = 0,
                Text = "Não disponível",
                Color = ConsoleColor.Yellow
            };
        }

        /// <summary>
        /// Obtém o(s) endereço(s) IP IPv4 locais ativos da máquina.
        /// </summary>
        private static string GetLocalIpAddress()
        {
            try
            {
                var ips = new List<string>();
                var nics = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
                foreach (var nic in nics)
                {
                    if (nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
                        continue;
                    if (nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                        continue;

                    var ipProps = nic.GetIPProperties();
                    foreach (var addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            string ipStr = addr.Address.ToString();
                            if (!ipStr.StartsWith("127.") && !ipStr.StartsWith("169.254.") && !ips.Contains(ipStr))
                            {
                                ips.Add(ipStr);
                            }
                        }
                    }
                }

                if (ips.Count > 0)
                {
                    return string.Join(", ", ips.ToArray());
                }

                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        string ipStr = ip.ToString();
                        if (!ipStr.StartsWith("127.") && !ipStr.StartsWith("169.254."))
                            return ipStr;
                    }
                }
            }
            catch { }
            return "Indisponível";
        }

        /// <summary>
        /// Obtém a versão detalhada do Windows com nome comercial, arquitetura, versão de lançamento e build.
        /// </summary>
        private static string GetWindowsVersionInfo()
        {
            try
            {
                string prodName = "";
                string displayVer = "";
                string build = "";

                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    if (key != null)
                    {
                        prodName = (key.GetValue("ProductName") as string) ?? "";
                        displayVer = (key.GetValue("DisplayVersion") as string) ?? (key.GetValue("ReleaseId") as string) ?? "";
                        build = (key.GetValue("CurrentBuild") as string) ?? (key.GetValue("CurrentBuildNumber") as string) ?? "";
                        string ubr = key.GetValue("UBR") != null ? key.GetValue("UBR").ToString() : "";
                        if (!string.IsNullOrEmpty(ubr) && !string.IsNullOrEmpty(build))
                        {
                            build = build + "." + ubr;
                        }
                    }
                }

                string arch = Environment.Is64BitOperatingSystem ? "64 bits" : "32 bits";

                int buildNum = 0;
                if (!string.IsNullOrEmpty(build))
                {
                    string firstPart = build.Split('.')[0];
                    int.TryParse(firstPart, out buildNum);
                }

                if (buildNum >= 22000 && prodName.IndexOf("Windows 10", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    prodName = prodName.Replace("Windows 10", "Windows 11");
                }

                var parts = new List<string>();
                if (!string.IsNullOrEmpty(prodName)) parts.Add(prodName);
                parts.Add(arch);
                if (!string.IsNullOrEmpty(displayVer)) parts.Add("Versão " + displayVer);
                if (!string.IsNullOrEmpty(build)) parts.Add("Build " + build);

                return string.Join(" - ", parts.ToArray());
            }
            catch
            {
                return Environment.OSVersion.VersionString + (Environment.Is64BitOperatingSystem ? " 64 bits" : " 32 bits");
            }
        }

        /// <summary>
        /// Adiciona uma linha ao log com a cor especificada.
        /// Chamado pelo evento <see cref="InstallerService.OnLog"/>.
        /// Mapeia ConsoleColor para Color do Windows Forms.
        /// </summary>
        private void AppendLog(string message, ConsoleColor? color)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => AppendLog(message, color)));
                return;
            }

            Color cor = MapColor(color);

            _txtLog.SelectionStart = _txtLog.TextLength;
            _txtLog.SelectionLength = 0;
            _txtLog.SelectionColor = cor;
            _txtLog.AppendText(message + "\n");
            _txtLog.ScrollToCaret();
            SyncLogScrollBar();
        }

        /// <summary>
        /// Sincroniza o estado e a posição da barra de rolagem personalizada com as linhas do RichTextBox de log.
        /// </summary>
        private void SyncLogScrollBar()
        {
            if (_txtLog == null || _scrollBarLog == null || !_txtLog.IsHandleCreated) return;
            if (_isSyncingLogScroll) return;

            try
            {
                _isSyncingLogScroll = true;

                int lineCount = SendMessage(_txtLog.Handle, EM_GETLINECOUNT, IntPtr.Zero, IntPtr.Zero).ToInt32();
                int firstVisible = SendMessage(_txtLog.Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero).ToInt32();

                int fontHeight = _txtLog.Font.Height;
                int visibleLines = fontHeight > 0 ? Math.Max(1, _txtLog.ClientSize.Height / fontHeight) : 10;

                int maxScroll = Math.Max(0, lineCount - visibleLines);
                _scrollBarLog.Maximum = maxScroll;
                _scrollBarLog.LargeChange = visibleLines;
                _scrollBarLog.SmallChange = 1;
                _scrollBarLog.Value = Math.Min(maxScroll, Math.Max(0, firstVisible));
            }
            finally
            {
                _isSyncingLogScroll = false;
            }
        }

        /// <summary>
        /// Ativa DoubleBuffering em controles de container (Panel) para rolagem fluida e sem oscilações visuais.
        /// </summary>
        private static void EnableDoubleBuffering(Control control)
        {
            if (control == null) return;
            try
            {
                typeof(Control).InvokeMember("DoubleBuffered",
                    BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                    null, control, new object[] { true });
            }
            catch
            {
            }
        }

        /// <summary>
        /// Manipulador do evento disparado quando a instalação é concluída com sucesso.
        /// Garante execução na thread de UI e reproduz o som de notificação.
        /// </summary>
        private void HandleInstallationCompleted()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(HandleInstallationCompleted));
                return;
            }

            if (_downloadProgressBar != null) _downloadProgressBar.Value = 100;
            if (_lblDownloadStatus != null)
            {
                _lblDownloadStatus.Text = "Downloads: Concluídos";
                _lblDownloadStatus.ForeColor = CorSucesso;
            }

            PlayNotificationSound();
        }

        /// <summary>
        /// Reproduz o áudio personalizado do R2D2 ao finalizar as instalações.
        /// Tenta reproduzir o arquivo MP3 usando o Windows Media Player COM e,
        /// caso não seja possível, recorre a sons de notificação do Windows.
        /// </summary>
        private void PlayNotificationSound()
        {
            Task.Run(new Action(() =>
            {
                bool played = false;

                try
                {
                    string soundPath = GetR2D2SoundPath();
                    if (!string.IsNullOrEmpty(soundPath) && File.Exists(soundPath))
                    {
                        Type wmpType = Type.GetTypeFromProgID("WMPlayer.OCX");
                        if (wmpType != null)
                        {
                            dynamic wmp = Activator.CreateInstance(wmpType);
                            try
                            {
                                wmp.settings.volume = 100;
                                wmp.URL = soundPath;
                                wmp.controls.play();
                                System.Threading.Thread.Sleep(3500);
                                played = true;
                            }
                            finally
                            {
                                try
                                {
                                    wmp.close();
                                }
                                catch
                                {
                                }
                                Marshal.ReleaseComObject(wmp);
                            }
                        }
                    }
                }
                catch
                {
                }

                if (!played)
                {
                    PlayFallbackSystemSound();
                }
            }));
        }

        /// <summary>
        /// Localiza o arquivo de áudio R2D2.mp3 no disco ou o extrai dos recursos
        /// embutidos no executável para o diretório temporário.
        /// </summary>
        private static string GetR2D2SoundPath()
        {
            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sound", "R2D2.mp3");
            if (File.Exists(localPath))
            {
                return localPath;
            }

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream("InstallerGUI.R2D2.mp3"))
                {
                    if (stream != null)
                    {
                        string tempPath = Path.Combine(Path.GetTempPath(), "R2D2.mp3");
                        using (var fileStream = File.Create(tempPath))
                        {
                            stream.CopyTo(fileStream);
                        }
                        return tempPath;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        /// <summary>
        /// Reproduz som padrão do Windows como fallback caso o áudio R2D2 não possa ser tocado.
        /// </summary>
        private static void PlayFallbackSystemSound()
        {
            try
            {
                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                string[] candidates = new string[]
                {
                    Path.Combine(winDir, "Media", "tada.wav"),
                    Path.Combine(winDir, "Media", "Windows Notify System Generic.wav"),
                    Path.Combine(winDir, "Media", "notify.wav")
                };

                foreach (var soundPath in candidates)
                {
                    if (File.Exists(soundPath))
                    {
                        using (var player = new SoundPlayer(soundPath))
                        {
                            player.PlaySync();
                        }
                        return;
                    }
                }

                SystemSounds.Exclamation.Play();
            }
            catch
            {
                try
                {
                    SystemSounds.Exclamation.Play();
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Converte ConsoleColor para System.Drawing.Color.
        /// Usado pelo log para exibir mensagens coloridas.
        /// </summary>
        private static Color MapColor(ConsoleColor? color)
        {
            if (color == null) return Color.FromArgb(200, 200, 200);

            switch (color.Value)
            {
                case ConsoleColor.Green: return Color.FromArgb(0, 200, 80);
                case ConsoleColor.Red: return Color.FromArgb(255, 80, 80);
                case ConsoleColor.Yellow: return Color.Yellow;
                case ConsoleColor.Cyan: return Color.Cyan;
                case ConsoleColor.Blue: return Color.FromArgb(0, 180, 255);
                case ConsoleColor.DarkBlue: return Color.FromArgb(0, 180, 255);
                case ConsoleColor.Gray: return Color.Gray;
                default: return Color.FromArgb(200, 200, 200);
            }
        }

        /// <summary>
        /// Cria um Label no padrão do tema escuro.
        /// </summary>
        private static Label CreateLabel(string text)
        {
            return new Label
            {
                Text = text,
                ForeColor = CorTexto,
                Anchor = AnchorStyles.Left,
                AutoSize = true,
            };
        }

        /// <summary>
        /// Cria uma TextBox no padrão do tema escuro.
        /// </summary>
        private static TextBox CreateTextBox(string placeholder)
        {
            return new TextBox
            {
                ForeColor = CorTexto,
                BackColor = CorInputBg,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(5, 2, 5, 2),
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
            };
        }

        /// <summary>
        /// Cria um CheckBox no padrão do tema escuro com fonte e margens otimizadas contra corte de texto.
        /// </summary>
        private static CheckBox CreateCheckBox(string text, bool checkedState)
        {
            return new CheckBox
            {
                Text = text,
                Checked = checkedState,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                ForeColor = CorTexto,
                BackColor = Color.Transparent,
                UseVisualStyleBackColor = false,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(2, 0, 2, 0),
            };
        }

        // ══════════════════════════════════════════════
        // LISTA DE PROGRAMAS
        // ══════════════════════════════════════════════

        /// <summary>
        /// Retorna a lista de programas padrão disponíveis para instalação organizados em categorias:
        /// 1. Programas corporativos padrão (Geral)
        /// 2. Manutenção e Recuperação (CrystalDisk, EaseUS, Recuva)
        /// 3. Inteligência Artificial (ChatGPT, Claude, DeepSeek, Gemini)
        /// 4. Suíte Criativa ArtCraft (Storytold)
        /// 5. Ferramentas para Desenvolvimento (Antigravity, Antigravity IDE, VSCode, Node.js, FileZilla, DBeaver)
        /// 6. Criação 3D, Design e Edição Multimídia (Affinity, Blender, TraceDraw)
        /// </summary>
        private static List<ProgramInfo> GetDefaultProgramList()
        {
            var standardList = new List<ProgramInfo>
            {
                new ProgramInfo("7zip",        "7-Zip",                          "7z_setup.exe",                         "/S", "", "Geral"),
                new ProgramInfo("adobereader", "Adobe Acrobat Reader",           "AcroRdrDC_x64.exe",                    "/sAll /msi EULA_ACCEPT=YES /qn /norestart", "", "Geral"),
                new ProgramInfo("anydesk",     "AnyDesk",                        "AnyDesk.exe",                          "--install \"C:\\Program Files (x86)\\AnyDesk\" --silent --start-with-win --create-shortcuts --create-desktop-icon", "", "Geral") { UseShellExecute = true, DefaultChecked = false },
                new ProgramInfo("dwgtrueview", "Autodesk DWG TrueView",          "Setup.exe",                            "-q --noeula", "", "Geral") { UseShellExecute = false, TimeoutMs = 1800000, RequiresInternet = true },
                new ProgramInfo("brave",       "Brave Browser",                  "BraveBrowserSetup.exe",                "/silent /install", "", "Geral"),
                new ProgramInfo("citrix",      "Citrix Workspace",               "CitrixWorkspaceFullInstaller_x64.exe", "/silent /noreboot", "", "Geral"),
                new ProgramInfo("chrome",      "Google Chrome",                  "googlechromestandaloneenterprise64.msi", "/qn /norestart", "", "Geral"),
                new ProgramInfo("googleearth", "Google Earth Pro",               "googleearthprowin.exe",                "OMAHA=1", "", "Geral"),
                new ProgramInfo("java",        "Java JRE 8",                     "jre-windows-x64.exe",                  "/s", "", "Geral"),
                new ProgramInfo("office365",   "Microsoft Office 365",           "setup.exe",                            "/configure configuration_o365.xml", "", "Geral") { UseShellExecute = false, TimeoutMs = 1800000, RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("powerbi",     "Microsoft Power BI Desktop",     "PBIDesktopSetup_x64.exe",              "-quiet -norestart ACCEPT_EULA=1", "", "Geral") { UseShellExecute = true, TimeoutMs = 600000, DefaultChecked = true },
                new ProgramInfo("firefox",     "Mozilla Firefox",                "Firefox_Setup.msi",                    "/qn /norestart", "", "Geral"),
                new ProgramInfo("radmin",      "Radmin VPN",                     "Radmin_VPN.exe",                       "/VERYSILENT /NORESTART", "", "Geral") { DefaultChecked = false },
                new ProgramInfo("rustdesk",    "RustDesk",                       "rustdesk_x86_64.exe",                  "--silent-install", "", "Geral") { FireAndForget = true },
                new ProgramInfo("winrar",      "WinRAR",                         "winrar.exe",                           "/S", "", "Geral"),
            };
            standardList.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCultureIgnoreCase));

            var manutencaoList = new List<ProgramInfo>
            {
                new ProgramInfo("crystaldiskinfo", "CrystalDiskInfo",         "CrystalDiskInfo9_9_5.exe", "/VERYSILENT /NORESTART /SP-",               "", "Manutenção") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("crystaldiskmark", "CrystalDiskMark",         "CrystalDiskMark9_0_3.exe", "/VERYSILENT /NORESTART /SP-",               "", "Manutenção") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("easeustodo",      "EaseUS Todo Backup Free", "tb_free.exe",              "/VERYSILENT /NORESTART /SUPPRESSMSGBOXES",  "", "Manutenção") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("recuva",          "Recuva Free",             "rcsetup155.exe",           "/S",                                        "", "Manutenção") { RequiresInternet = true, DefaultChecked = false },
            };
            manutencaoList.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCultureIgnoreCase));

            var craftList = new List<ProgramInfo>
            {
                new ProgramInfo("cadcraft",    "CADCraft (Desenho Técnico CAD)",     "cadcraft-windows-x64.msi",    "/qn /norestart", "", "ArtCraft") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("deckcraft",   "DeckCraft (Apresentações e Slides)", "deckcraft-windows-x64.msi",   "/qn /norestart", "", "ArtCraft") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("designcraft", "DesignCraft (Design Gráfico e UI)",  "designcraft-windows-x64.msi", "/qn /norestart", "", "ArtCraft") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("effectcraft", "EffectCraft (Efeitos Visuais e VFX)","effectcraft-windows-x64.msi","/qn /norestart", "", "ArtCraft") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("filmcraft",   "FilmCraft (Editor de Vídeo)",        "filmcraft-windows-x64.msi",   "/qn /norestart", "", "ArtCraft") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("gridcraft",   "GridCraft (Planilhas Eletrônicas)",  "gridcraft-windows-x64.msi",   "/qn /norestart", "", "ArtCraft") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("lightcraft",  "LightCraft (Iluminação e Estúdio 3D)","lightcraft-windows-x64.msi", "/qn /norestart", "", "ArtCraft") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("pdfcraft",    "PdfCraft (Leitor e Editor de PDF)",  "pdfcraft-windows-x64.msi",    "/qn /norestart", "", "ArtCraft") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("photocraft",  "PhotoCraft (Editor de Fotos)",       "photocraft-windows-x64.msi",  "/qn /norestart", "", "ArtCraft") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("soundcraft",  "SoundCraft (Editor de Áudio)",       "soundcraft-windows-x64.msi",  "/qn /norestart", "", "ArtCraft") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("texturecraft","TextureCraft (Texturização 3D)",     "texturecraft-windows-x64.msi","/qn /norestart", "", "ArtCraft") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("vectorcraft", "VectorCraft (Ilustração Vetorial)",  "vectorcraft-windows-x64.msi", "/qn /norestart", "", "ArtCraft") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("wordcraft",   "WordCraft (Editor de Texto)",        "wordcraft-windows-x64.msi",   "/qn /norestart", "", "ArtCraft") { RequiresInternet = true, DefaultChecked = false },
            };
            craftList.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCultureIgnoreCase));

            var devList = new List<ProgramInfo>
            {
                new ProgramInfo("antigravity",    "Antigravity",         "Antigravity-x64.exe", "/S",                                              "", "Desenvolvimento") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("antigravityide", "Antigravity IDE",     "Antigravity_IDE.exe", "/VERYSILENT /NORESTART /MERGETASKS=\"!runcode\"", "", "Desenvolvimento") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("vscode",         "Visual Studio Code",  "VSCodeSetup_x64.exe", "/VERYSILENT /NORESTART /MERGETASKS=\"!runcode\"", "", "Desenvolvimento") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("nodejs",         "Node.js LTS",         "node_lts_x64.msi",    "/qn /norestart",                                  "", "Desenvolvimento") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("filezilla",      "FileZilla Client",    "FileZilla_setup.exe", "/S",                                              "", "Desenvolvimento") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("dbeaver",        "DBeaver Community",   "dbeaver_setup.exe",   "/S /allusers",                                    "", "Desenvolvimento") { RequiresInternet = true, DefaultChecked = false },
            };

            var mediaList = new List<ProgramInfo>
            {
                new ProgramInfo("affinity",  "Affinity Studio (Português)",        "Affinity.msix",            "",                                            "", "Design & Multimídia") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("blender",   "Blender (3D e Criação)",             "blender.msi",              "/qn /norestart ALLUSERS=1",                   "", "Design & Multimídia") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("tracedraw", "TraceDraw (Vetorização Automática)", "tracedraw-setup-x64.exe", "/VERYSILENT /NORESTART /SUPPRESSMSGBOXES /SP-", "", "Design & Multimídia") { RequiresInternet = true, DefaultChecked = false },
            };

            var aiList = new List<ProgramInfo>
            {
                new ProgramInfo("chatgpt",  "ChatGPT Desktop",  "ChatGPT-x64.msix",             "",                                    "", "Inteligência Artificial") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("claude",   "Claude Desktop",   "Claude-Setup-x64.exe",         "--silent",                            "", "Inteligência Artificial") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("deepseek", "DeepSeek Desktop", "deepseek-harness-win-x64.exe", "/S",                                  "", "Inteligência Artificial") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("gemini",   "Gemini Desktop",   "GeminiSetup.exe",              "/S",                                  "", "Inteligência Artificial") { RequiresInternet = true, DefaultChecked = false },
            };
            aiList.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCultureIgnoreCase));

            var list = new List<ProgramInfo>();
            // 1. Programas corporativos padrão (Geral)
            list.AddRange(standardList);
            // 2. Manutenção
            list.AddRange(manutencaoList);
            // 3. Inteligência Artificial
            list.AddRange(aiList);
            // 4. ArtCraft Studio e apps Craft
            list.Add(new ProgramInfo("artcraft", "ArtCraft Studio (Suíte Criativa)", "ArtCraft_setup.exe", "/S", "", "ArtCraft") { DefaultChecked = false });
            list.AddRange(craftList);
            // 5. Desenvolvimento
            list.AddRange(devList);
            // 6. Design & Multimídia
            list.AddRange(mediaList);

            return list;
        }
    }

    /// <summary>
    /// Diagnóstico de espaço em disco da unidade de sistema com texto e classificação de cor.
    /// </summary>
    public class DiskSpaceDiagnosis
    {
        public double FreeGb { get; set; }
        public double TotalGb { get; set; }
        public double PercentFree { get; set; }
        public string Text { get; set; }
        public ConsoleColor Color { get; set; }
    }

    /// <summary>
    /// Barra de progresso ultrafina e moderna personalizada (estilo RustDesk corporativo).
    /// Renderizada com desenho nativo GDI+ com trilho escuro e preenchimento colorido,
    /// sem bordas brancas do UxTheme do Windows.
    /// </summary>
    public class ThinProgressBar : Control
    {
        private int _value;
        private int _maximum = 100;
        private Color _trackColor = Color.FromArgb(45, 45, 45);
        private Color _progressColor = Color.FromArgb(243, 146, 0);

        public ThinProgressBar()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            Height = 5;
            BackColor = Color.FromArgb(40, 40, 40);
        }

        public int Value
        {
            get { return _value; }
            set
            {
                int clamped = Math.Max(0, Math.Min(_maximum, value));
                if (_value != clamped)
                {
                    _value = clamped;
                    Invalidate();
                }
            }
        }

        public int Maximum
        {
            get { return _maximum; }
            set
            {
                if (value > 0 && _maximum != value)
                {
                    _maximum = value;
                    if (_value > _maximum) _value = _maximum;
                    Invalidate();
                }
            }
        }

        public Color TrackColor
        {
            get { return _trackColor; }
            set
            {
                _trackColor = value;
                Invalidate();
            }
        }

        public Color ProgressColor
        {
            get { return _progressColor; }
            set
            {
                _progressColor = value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;

            using (var trackBrush = new SolidBrush(_trackColor))
            {
                g.FillRectangle(trackBrush, 0, 0, Width, Height);
            }

            if (_maximum > 0 && _value > 0)
            {
                int fillWidth = (int)((float)_value / _maximum * Width);
                if (fillWidth > Width) fillWidth = Width;
                if (fillWidth > 0)
                {
                    using (var fillBrush = new SolidBrush(_progressColor))
                    {
                        g.FillRectangle(fillBrush, 0, 0, fillWidth, Height);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Barra de rolagem vertical personalizada com tema escuro elegante.
    /// O trilho é renderizado na mesma cor de fundo do container hospedeiro
    /// e a parte clicável (thumb) em formato de pílula arredondada na cor laranja de destaque.
    /// </summary>
    public class DarkScrollBar : Control
    {
        private int _minimum = 0;
        private int _maximum = 100;
        private int _value = 0;
        private int _largeChange = 10;
        private int _smallChange = 1;

        private Color _trackColor = Color.FromArgb(40, 40, 40);
        private Color _thumbColor = Color.FromArgb(243, 146, 0);
        private Color _thumbHoverColor = Color.FromArgb(255, 170, 40);
        private Color _thumbPressedColor = Color.FromArgb(220, 125, 0);

        private bool _isHovered = false;
        private bool _isDragging = false;
        private int _dragStartY = 0;
        private int _dragStartValue = 0;

        /// <summary>
        /// Evento disparado quando o valor de rolagem é alterado.
        /// </summary>
        public event EventHandler ValueChanged;

        public DarkScrollBar()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.Selectable, false);
            Width = 10;
            Cursor = Cursors.Default;
            BackColor = Color.FromArgb(40, 40, 40);
        }

        public int Minimum
        {
            get { return _minimum; }
            set
            {
                if (_minimum != value)
                {
                    _minimum = value;
                    if (_value < _minimum) _value = _minimum;
                    Invalidate();
                }
            }
        }

        public int Maximum
        {
            get { return _maximum; }
            set
            {
                if (_maximum != value)
                {
                    _maximum = value;
                    if (_value > _maximum) _value = _maximum;
                    Invalidate();
                }
            }
        }

        public int Value
        {
            get { return _value; }
            set
            {
                int clamped = Math.Max(_minimum, Math.Min(_maximum, value));
                if (_value != clamped)
                {
                    _value = clamped;
                    Invalidate();
                    if (ValueChanged != null)
                    {
                        ValueChanged(this, EventArgs.Empty);
                    }
                }
            }
        }

        public int LargeChange
        {
            get { return _largeChange; }
            set
            {
                if (_largeChange != value && value > 0)
                {
                    _largeChange = value;
                    Invalidate();
                }
            }
        }

        public int SmallChange
        {
            get { return _smallChange; }
            set
            {
                if (_smallChange != value && value > 0)
                {
                    _smallChange = value;
                }
            }
        }

        public Color TrackColor
        {
            get { return _trackColor; }
            set
            {
                _trackColor = value;
                BackColor = value;
                Invalidate();
            }
        }

        public Color ThumbColor
        {
            get { return _thumbColor; }
            set
            {
                _thumbColor = value;
                Invalidate();
            }
        }

        public Color ThumbHoverColor
        {
            get { return _thumbHoverColor; }
            set
            {
                _thumbHoverColor = value;
                Invalidate();
            }
        }

        public Color ThumbPressedColor
        {
            get { return _thumbPressedColor; }
            set
            {
                _thumbPressedColor = value;
                Invalidate();
            }
        }

        public void ScrollBy(int delta)
        {
            Value = Math.Max(_minimum, Math.Min(_maximum, _value + delta));
        }

        private Rectangle GetThumbRectangle()
        {
            int range = _maximum - _minimum;
            if (range <= 0 || Height <= 0)
            {
                return Rectangle.Empty;
            }

            int trackH = Height;
            float visibleRatio = (float)_largeChange / (float)(range + _largeChange);
            int thumbH = (int)(visibleRatio * trackH);
            if (thumbH < 22) thumbH = 22;
            if (thumbH > trackH) thumbH = trackH;

            int travelH = trackH - thumbH;
            float posRatio = travelH > 0 ? (float)(_value - _minimum) / (float)range : 0f;
            int thumbY = (int)(posRatio * travelH);

            int thumbMargin = 1;
            int thumbW = Math.Max(4, Width - (thumbMargin * 2));
            return new Rectangle(thumbMargin, thumbY, thumbW, thumbH);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;

            // Fundo do trilho na mesma cor do fundo do painel
            using (var trackBrush = new SolidBrush(_trackColor))
            {
                g.FillRectangle(trackBrush, ClientRectangle);
            }

            if (_maximum <= _minimum) return;

            Rectangle thumbRect = GetThumbRectangle();
            if (thumbRect.IsEmpty || thumbRect.Height <= 0) return;

            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color thumbColorToUse = _isDragging ? _thumbPressedColor : (_isHovered ? _thumbHoverColor : _thumbColor);

            int radius = Math.Min(4, thumbRect.Width / 2);
            using (var path = CreateRoundedRectanglePath(thumbRect, radius))
            using (var thumbBrush = new SolidBrush(thumbColorToUse))
            {
                g.FillPath(thumbBrush, path);
            }
        }

        private static GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            int diameter = radius * 2;
            if (diameter > bounds.Width) diameter = bounds.Width;
            if (diameter > bounds.Height) diameter = bounds.Height;

            var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || _maximum <= _minimum) return;

            Rectangle thumbRect = GetThumbRectangle();
            if (thumbRect.Contains(e.Location))
            {
                _isDragging = true;
                _dragStartY = e.Y;
                _dragStartValue = _value;
                Capture = true;
                Invalidate();
            }
            else
            {
                if (e.Y < thumbRect.Top)
                {
                    Value = Math.Max(_minimum, _value - _largeChange);
                }
                else if (e.Y > thumbRect.Bottom)
                {
                    Value = Math.Min(_maximum, _value + _largeChange);
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isDragging)
            {
                int trackH = Height;
                Rectangle thumbRect = GetThumbRectangle();
                int travelH = trackH - thumbRect.Height;
                if (travelH > 0)
                {
                    int deltaY = e.Y - _dragStartY;
                    float deltaRatio = (float)deltaY / travelH;
                    int deltaVal = (int)Math.Round(deltaRatio * (_maximum - _minimum));
                    Value = Math.Max(_minimum, Math.Min(_maximum, _dragStartValue + deltaVal));
                }
            }
            else
            {
                Rectangle thumbRect = GetThumbRectangle();
                bool hover = thumbRect.Contains(e.Location);
                if (_isHovered != hover)
                {
                    _isHovered = hover;
                    Invalidate();
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isDragging)
            {
                _isDragging = false;
                Capture = false;
                Invalidate();
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int step = _smallChange > 0 ? _smallChange * 3 : 3;
            if (e.Delta > 0)
            {
                Value = Math.Max(_minimum, _value - step);
            }
            else if (e.Delta < 0)
            {
                Value = Math.Min(_maximum, _value + step);
            }
        }
    }
}
