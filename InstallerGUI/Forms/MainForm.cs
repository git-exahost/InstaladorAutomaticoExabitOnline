using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Net.NetworkInformation;
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
        private static readonly Color CorDestaqueLaranja = Color.FromArgb(243, 146, 0);
        private static readonly Color CorDestaqueAzul = Color.FromArgb(0, 180, 255);
        private static readonly Color CorSucesso = Color.FromArgb(0, 200, 80);
        private static readonly Color CorTexto = Color.White;
        private static readonly Color CorInputBg = Color.FromArgb(60, 60, 60);

        // ──────────────────────────────────────────────
        // Campos
        // ──────────────────────────────────────────────

        private readonly InstallerService _service;
        private bool _installing;
        private bool _installationStarted;
        private System.Windows.Forms.Timer _connectionMonitorTimer;
        private System.Windows.Forms.Timer _metricsRefreshTimer;
        private bool? _lastDetectedInternet;
        private string _lastDetectedIp;
        private bool _isUpdatingSystemInfo;
        private bool _isPeriodicMetricsRunning;
        private int _connectionMonitorTicks;

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
        private CheckBox _chkPreventWallpaperChange;
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

        // ─── Controles de Diagnóstico do Sistema ───
        private TableLayoutPanel _pnlNormalLeftView;
        private Panel _pnlExpandedDiagnostics;
        private FullSystemDiagnosticInfo _lastDiag;

        // Card Compacto (Aba 1)
        private Label _lblDiagComputer;
        private Label _lblDiagUser;
        private Label _lblDiagOs;
        private MiniUsageBar _barDiagDisk;
        private Label _lblDiagNet;
        private Label _lblDiagIp;
        private Button _btnMoreDetails;

        // Card Expandido (Painel Completo)
        private Button _btnBackToMain;
        private Panel _pnlIdentViewport;
        private Panel _pnlIdentContent;
        private DarkScrollBar _scrollBarIdent;
        private Label _lblExpComputer;
        private Label _lblExpUser;
        private Label _lblExpOs;
        private Label _lblExpNetStatus;
        private Label _lblExpInterfaces;
        private Panel _pnlStorageViewport;
        private Panel _pnlStorageContent;
        private DarkScrollBar _scrollBarStorage;
        private Panel _pnlNetIpsViewport;
        private Panel _pnlNetIpsContent;
        private DarkScrollBar _scrollBarNetIps;
        private Label _lblExpCpu;
        private Label _lblExpRam;
        private MiniUsageBar _barExpRam;
        private Label _lblExpManufacturer;
        private Label _lblExpModel;
        private Label _lblExpSerial;
        private Panel _pnlSecViewport;
        private Panel _pnlSecContent;
        private DarkScrollBar _scrollBarSec;
        private Label _lblSecTitle;
        private Label _lblExpSecurity;
        private Label _lblStateTitle;
        private Label _lblExpSystemState;

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
            Text = string.Format("Auto Instalador de Programas v{0} - EXABIT Soluções em TI", GetAppVersion());

            // Calcula o maior tamanho confortável para caber em resolução 720p (1280x720) sem cortar
            var workingArea = Screen.PrimaryScreen.WorkingArea;
            int desiredWidth = (int)(1200 * scale);
            int desiredHeight = (int)(650 * scale);

            int formWidth = Math.Min(workingArea.Width - 16, desiredWidth);
            int formHeight = Math.Min(workingArea.Height - 16, desiredHeight);

            // Trava o redimensionamento e define apenas os botões Minimizar e Fechar (sem Maximizar)
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            Size = new Size(formWidth, formHeight);
            MinimumSize = new Size(formWidth, formHeight);
            MaximumSize = new Size(formWidth, formHeight);
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

            // Inicia o monitor de conectividade de rede em tempo real
            InitConnectionMonitor();
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

        /// <summary>
        /// Obtém a versão do aplicativo a partir dos metadados do assembly (ex: "2.0.0.83").
        /// </summary>
        private static string GetAppVersion()
        {
            try
            {
                var ver = Assembly.GetExecutingAssembly().GetName().Version;
                if (ver != null)
                {
                    return ver.ToString();
                }
            }
            catch
            {
            }
            return "2.0.0.93";
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
        private const int WM_SETREDRAW = 0x000B;

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
            TriggerNetworkAvailabilityCheck();
        }

        /// <summary>
        /// Libera os recursos e timers em background ao fechar a janela.
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                if (_connectionMonitorTimer != null)
                {
                    _connectionMonitorTimer.Stop();
                    _connectionMonitorTimer.Dispose();
                    _connectionMonitorTimer = null;
                }
                if (_metricsRefreshTimer != null)
                {
                    _metricsRefreshTimer.Stop();
                    _metricsRefreshTimer.Dispose();
                    _metricsRefreshTimer = null;
                }
                System.Net.NetworkInformation.NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
            }
            catch { }
            base.OnFormClosing(e);
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
            leftSide.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(44 * scale)));
            leftSide.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            leftSide.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(25 * scale)));

            // Row 0: Barra de Abas (Bootstrap with tabs)
            leftSide.Controls.Add(BuildTabsHeader(scale), 0, 0);

            // Row 1: Painel do meio (alterna entre Visão Normal e Diagnóstico Completo)
            var pnlLeftMiddle = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = CorFundo,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };

            // Visão normal: Aba ativa + Log console
            _pnlNormalLeftView = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = CorFundo,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            _pnlNormalLeftView.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(265 * scale)));
            _pnlNormalLeftView.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _pnlNormalLeftView.Controls.Add(BuildTabContentContainer(scale), 0, 0);
            _pnlNormalLeftView.Controls.Add(BuildLogPanel(scale), 0, 1);
            pnlLeftMiddle.Controls.Add(_pnlNormalLeftView);

            // Visão expandida: Diagnóstico completo do sistema (Image 1)
            _pnlExpandedDiagnostics = BuildExpandedDiagnosticsPanel(scale);
            _pnlExpandedDiagnostics.Visible = false;
            pnlLeftMiddle.Controls.Add(_pnlExpandedDiagnostics);

            leftSide.Controls.Add(pnlLeftMiddle, 0, 1);

            // Row 2: Barra de Download
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
        /// Cria a barra de cabeçalho das abas com visual moderno estilo Bootstrap "With Tabs".
        /// </summary>
        private TableLayoutPanel BuildTabsHeader(float scale)
        {
            var tabHeader = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = Color.FromArgb(32, 32, 32),
                Margin = new Padding(0, 0, 0, (int)(2 * scale)),
                Padding = Padding.Empty,
            };
            tabHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31f));
            tabHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18f));
            tabHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 21f));
            tabHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30f));
            tabHeader.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // Aba 1: Configurações
            _btnTabConfig = CreateTabHeaderButton("Configurações", 0, scale);
            tabHeader.Controls.Add(_btnTabConfig, 0, 0);

            // Aba 2: Atalhos
            _btnTabShortcuts = CreateTabHeaderButton("Atalhos", 1, scale);
            tabHeader.Controls.Add(_btnTabShortcuts, 1, 0);

            // Aba 3: Opções
            _btnTabOptions = CreateTabHeaderButton("Opções", 2, scale);
            tabHeader.Controls.Add(_btnTabOptions, 2, 0);

            // Aba 4: Funcionalidades
            _btnTabFeatures = CreateTabHeaderButton("Funcionalidades", 3, scale);
            tabHeader.Controls.Add(_btnTabFeatures, 3, 0);

            return tabHeader;
        }

        /// <summary>
        /// Cria o container onde o conteúdo de cada aba é alternado.
        /// </summary>
        private Panel BuildTabContentContainer(float scale)
        {
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

            // Abre a primeira aba ("Configurações do Sistema") por padrão
            SwitchTab(0);

            return _pnlTabContent;
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
                Font = new Font("Segoe UI", 9.0f, FontStyle.Regular),
                AutoEllipsis = true,
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
            if (_pnlExpandedDiagnostics != null && _pnlExpandedDiagnostics.Visible)
            {
                ShowExpandedDiagnostics(false);
            }

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
                btn.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                btn.BackColor = CorPainel;
            }
            else
            {
                btn.ForeColor = Color.FromArgb(170, 170, 170);
                btn.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
                btn.BackColor = Color.FromArgb(32, 32, 32);
            }
        }

        /// <summary>
        /// Conteúdo da Aba 1: Configurações do Sistema.
        /// Exibe lado a lado o card "DADOS DA MÁQUINA E CONTAS" e o card "DIAGNÓSTICO DO SISTEMA".
        /// </summary>
        private Panel BuildTabConfigContent(float scale)
        {
            var pnl = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = CorPainel,
                Padding = new Padding((int)(6 * scale), (int)(4 * scale), (int)(6 * scale), (int)(4 * scale)),
            };

            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = CorPainel,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // Card Esquerdo: DADOS DA MÁQUINA E CONTAS
            tbl.Controls.Add(BuildMachineAccountsCard(scale), 0, 0);

            // Card Direito: DIAGNÓSTICO DO SISTEMA
            tbl.Controls.Add(BuildCompactDiagnosticsCard(scale), 1, 0);

            pnl.Controls.Add(tbl);
            return pnl;
        }

        /// <summary>
        /// Constrói o card esquerdo da Aba 1 com campos de nome da máquina, senha de admin e usuário padrão.
        /// Estruturado com labels imediatamente acima de seus campos de texto (padrão de formulário UX/UI corporativo).
        /// </summary>
        private Panel BuildMachineAccountsCard(float scale)
        {
            var pnlCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(32, 32, 32),
                Margin = new Padding((int)(2 * scale), (int)(2 * scale), (int)(4 * scale), (int)(2 * scale)),
                Padding = new Padding((int)(12 * scale), (int)(10 * scale), (int)(12 * scale), (int)(10 * scale)),
            };
            pnlCard.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(70, 52, 38), 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, pnlCard.Width - 1, pnlCard.Height - 1);
                }
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(26 * scale))); // Título com respiro
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(50 * scale))); // Nome do computador (Label + Input)
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(50 * scale))); // Senha do Admin (Label + Input)
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(50 * scale))); // Usuário padrão e Senha (Labels + Inputs)
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(24 * scale))); // Dica informativa (padrão idêntico às demais abas)
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));                // Espaçador inferior flexível

            var lblTitle = new Label
            {
                Text = "DADOS DA MÁQUINA E CONTAS",
                ForeColor = CorDestaqueLaranja,
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                AutoEllipsis = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 0, (int)(4 * scale)),
            };
            layout.Controls.Add(lblTitle, 0, 0);

            // 1. Nome do computador (Label imediatamente acima do TextBox)
            var tblComp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            tblComp.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(18 * scale)));
            tblComp.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(28 * scale)));
            var lblComp = new Label
            {
                Text = "Nome do computador:",
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Segoe UI", 8.5f),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = Padding.Empty,
            };
            _txtComputerName = CreateTextBox("Em branco = não renomear");
            _txtComputerName.Text = "";
            _txtComputerName.Dock = DockStyle.Fill;
            _txtComputerName.Margin = Padding.Empty;
            tblComp.Controls.Add(lblComp, 0, 0);
            tblComp.Controls.Add(_txtComputerName, 0, 1);
            layout.Controls.Add(tblComp, 0, 1);

            // 2. Senha do Admin (Label imediatamente acima do TextBox)
            var tblAdmin = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            tblAdmin.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(18 * scale)));
            tblAdmin.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(28 * scale)));
            var lblAdmin = new Label
            {
                Text = "Senha do Admin:",
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Segoe UI", 8.5f),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = Padding.Empty,
            };
            _txtAdminPass = CreateTextBox("Senha do admin");
            _txtAdminPass.Text = "";
            _txtAdminPass.Dock = DockStyle.Fill;
            _txtAdminPass.Margin = Padding.Empty;
            tblAdmin.Controls.Add(lblAdmin, 0, 0);
            tblAdmin.Controls.Add(_txtAdminPass, 0, 1);
            layout.Controls.Add(tblAdmin, 0, 2);

            // 3. Usuário padrão e Senha lado a lado (Labels imediatamente acima dos TextBoxes)
            var tblUsers = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            tblUsers.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            tblUsers.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            tblUsers.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(18 * scale)));
            tblUsers.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(28 * scale)));

            var lblStdUser = new Label
            {
                Text = "Usuário padrão:",
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Segoe UI", 8.5f),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, (int)(4 * scale), 0),
            };
            _txtStandardUserName = CreateTextBox("Nome do usuário");
            _txtStandardUserName.Text = "";
            _txtStandardUserName.Dock = DockStyle.Fill;
            _txtStandardUserName.Margin = new Padding(0, 0, (int)(4 * scale), 0);
            tblUsers.Controls.Add(lblStdUser, 0, 0);
            tblUsers.Controls.Add(_txtStandardUserName, 0, 1);

            var lblStdPass = new Label
            {
                Text = "Senha do usuário:",
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Segoe UI", 8.5f),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding((int)(4 * scale), 0, 0, 0),
            };
            _txtStandardUserPass = CreateTextBox("Senha (opcional)");
            _txtStandardUserPass.Text = "";
            _txtStandardUserPass.Dock = DockStyle.Fill;
            _txtStandardUserPass.Margin = new Padding((int)(4 * scale), 0, 0, 0);
            tblUsers.Controls.Add(lblStdPass, 1, 0);
            tblUsers.Controls.Add(_txtStandardUserPass, 1, 1);

            layout.Controls.Add(tblUsers, 0, 3);

            var lblHint = new Label
            {
                Text = "Os campos acima só funcionam caso preenchidos (em branco = não alterar).",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                ForeColor = Color.FromArgb(160, 160, 160),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = Padding.Empty,
            };
            layout.Controls.Add(lblHint, 0, 4);

            pnlCard.Controls.Add(layout);
            return pnlCard;
        }

        /// <summary>
        /// Constrói o card direito da Aba 1 com o resumo do diagnóstico do sistema e botão para detalhes.
        /// </summary>
        private Panel BuildCompactDiagnosticsCard(float scale)
        {
            var pnlCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(32, 32, 32),
                Margin = new Padding((int)(4 * scale), (int)(2 * scale), (int)(2 * scale), (int)(2 * scale)),
                Padding = new Padding((int)(12 * scale), (int)(10 * scale), (int)(12 * scale), (int)(10 * scale)),
            };
            pnlCard.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(0, 119, 182), 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, pnlCard.Width - 1, pnlCard.Height - 1);
                }
            };

            // 1. Título no topo
            var lblTitle = new Label
            {
                Text = "DIAGNÓSTICO DO SISTEMA",
                ForeColor = CorDestaqueAzul,
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                AutoEllipsis = true,
                Dock = DockStyle.Top,
                Height = (int)(24 * scale),
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false,
                Margin = Padding.Empty,
            };

            // 2. Barra inferior com botão Mais Detalhes (ancorado na base de pnlCard, nunca cortado!)
            var pnlBtnBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = (int)(28 * scale),
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };

            _btnMoreDetails = new Button
            {
                Text = "▼ Mais detalhes (+6)",
                ForeColor = CorDestaqueAzul,
                BackColor = Color.FromArgb(24, 24, 24),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Height = (int)(26 * scale),
                Width = (int)(140 * scale),
                Cursor = Cursors.Hand,
                Dock = DockStyle.Right,
            };
            _btnMoreDetails.FlatAppearance.BorderColor = CorDestaqueAzul;
            _btnMoreDetails.FlatAppearance.BorderSize = 1;
            _btnMoreDetails.Click += (s, e) =>
            {
                ShowExpandedDiagnostics(true);
            };
            pnlBtnBottom.Controls.Add(_btnMoreDetails);

            // 3. Tabela de 6 campos no meio preenchendo o espaço disponível
            var tblData = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 6,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = new Padding(0, (int)(4 * scale), 0, (int)(4 * scale)),
            };
            tblData.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, (int)(105 * scale))); // 105px: "Computador:" nunca corta!
            tblData.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            for (int i = 0; i < 6; i++)
                tblData.RowStyles.Add(new RowStyle(SizeType.Percent, 16.66f));

            // 1. Computador
            tblData.Controls.Add(CreateDiagHeaderLabel("Computador:"), 0, 0);
            _lblDiagComputer = CreateDiagValueLabel("Identificando...");
            tblData.Controls.Add(_lblDiagComputer, 1, 0);

            // 2. Usuário
            tblData.Controls.Add(CreateDiagHeaderLabel("Usuário:"), 0, 1);
            _lblDiagUser = CreateDiagValueLabel("Identificando...");
            tblData.Controls.Add(_lblDiagUser, 1, 1);

            // 3. Sistema
            tblData.Controls.Add(CreateDiagHeaderLabel("Sistema:"), 0, 2);
            _lblDiagOs = CreateDiagValueLabel("Identificando...");
            tblData.Controls.Add(_lblDiagOs, 1, 2);

            // 4. Disco C: (somente com a barra verde/amarela/vermelha)
            tblData.Controls.Add(CreateDiagHeaderLabel("Disco C:"), 0, 3);
            var pnlDiskContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            var initDisk = GetSystemDriveFreeSpaceDiagnosis();
            int initPercent = (initDisk != null) ? (int)initDisk.PercentUsed : 30;
            Color initColor = CorSucesso;
            if (initDisk != null)
            {
                if (initDisk.Color == ConsoleColor.Red) initColor = Color.FromArgb(230, 50, 50);
                else if (initDisk.Color == ConsoleColor.Yellow) initColor = Color.FromArgb(240, 180, 0);
            }

            _barDiagDisk = new MiniUsageBar
            {
                Location = new Point(0, (int)(6 * scale)),
                Size = new Size((int)(115 * scale), (int)(10 * scale)),
                Percent = initPercent,
                FillColor = initColor,
                Cursor = Cursors.Hand,
            };
            pnlDiskContainer.Controls.Add(_barDiagDisk);
            tblData.Controls.Add(pnlDiskContainer, 1, 3);

            // 5. Conexão
            tblData.Controls.Add(CreateDiagHeaderLabel("Conexão:"), 0, 4);
            _lblDiagNet = CreateDiagValueLabel("Verificando conexão...");
            _lblDiagNet.ForeColor = CorSucesso;
            _lblDiagNet.Cursor = Cursors.Hand;
            _lblDiagNet.Click += (s, e) =>
            {
                _lblDiagNet.Text = "Atualizando teste de ping...";
                MetricsRefreshTimer_Tick(s, e);
            };
            tblData.Controls.Add(_lblDiagNet, 1, 4);

            // 6. IP Local
            tblData.Controls.Add(CreateDiagHeaderLabel("IP Local:"), 0, 5);
            _lblDiagIp = CreateDiagValueLabel("Detectando IP...");
            _lblDiagIp.ForeColor = CorDestaque;
            _lblDiagIp.Cursor = Cursors.Hand;
            _lblDiagIp.Click += (s, e) =>
            {
                CopyLocalIpToClipboard();
            };
            tblData.Controls.Add(_lblDiagIp, 1, 5);

            // Ordem de adição para garantir o Docking correto
            pnlCard.Controls.Add(tblData);
            pnlCard.Controls.Add(pnlBtnBottom);
            pnlCard.Controls.Add(lblTitle);

            return pnlCard;
        }

        /// <summary>
        /// Conteúdo da Aba 2: Atalhos corporativos da Área de Trabalho.
        /// Espaçamento rigorosamente padronizado em 30px por linha de checkbox.
        /// </summary>
        private Panel BuildTabShortcutsContent(float scale)
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
                ColumnCount = 2,
                RowCount = 5,
                BackColor = CorPainel,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(30 * scale)));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(30 * scale)));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(30 * scale)));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(30 * scale)));
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // Espaçador inferior flexível

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
        /// Espaçamento rigorosamente padronizado em 30px por linha de checkbox.
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
                RowCount = 7,
                BackColor = CorPainel,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(30 * scale)));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(30 * scale)));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(30 * scale)));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(30 * scale)));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(30 * scale)));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(24 * scale)));
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // Espaçador inferior flexível

            _chkEnableLocation = CreateCheckBox("Ativar serviços de localização do Windows", true);
            tbl.Controls.Add(_chkEnableLocation, 0, 0);

            _chkStandardUserNoPwdChange = CreateCheckBox("Impedir que o usuário altere a senha", false);
            tbl.Controls.Add(_chkStandardUserNoPwdChange, 0, 1);

            _chkWallpaper = CreateCheckBox("Aplicar papel de parede personalizado", false);
            _chkWallpaperNortel = CreateCheckBox("Aplicar papel de parede Nortel", false);
            _chkPreventWallpaperChange = CreateCheckBox("Bloquear alteração do papel de parede", false);

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
                if (_chkWallpaperNortel.Checked)
                {
                    _chkPreventWallpaperChange.Checked = true;
                }
            };

            tbl.Controls.Add(_chkWallpaper, 0, 2);
            tbl.Controls.Add(_chkWallpaperNortel, 0, 3);
            tbl.Controls.Add(_chkPreventWallpaperChange, 0, 4);

            var lblHint = new Label
            {
                Text = "Configurações de segurança e personalização aplicadas durante o processo.",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                ForeColor = Color.FromArgb(160, 160, 160),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            tbl.Controls.Add(lblHint, 0, 5);

            pnl.Controls.Add(tbl);
            return pnl;
        }

        /// <summary>
        /// Conteúdo da Aba 4: Funcionalidades (Remoção de jogos, limpeza de perfis e remoção de antivírus).
        /// Espaçamento rigorosamente padronizado em 30px por linha de checkbox.
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
                RowCount = 5,
                BackColor = CorPainel,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(30 * scale)));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(30 * scale)));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(30 * scale)));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(24 * scale)));
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // Espaçador inferior flexível

            _chkUninstallGames = CreateCheckBox("Desinstalar todos os jogos deste computador", false);
            _chkRemoveOtherProfiles = CreateCheckBox("Remover e apagar todos os perfis do Windows", false);
            _chkUninstallThirdPartyAntivirus = CreateCheckBox("Desinstalar todo e qualquer antivírus de terceiros", false);

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
        /// Constrói o painel expandido de Diagnóstico do Sistema (Completo) exibido ao clicar em "Mais detalhes (+6)".
        /// </summary>
        private Panel BuildExpandedDiagnosticsPanel(float scale)
        {
            var pnl = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = CorFundo,
                Margin = Padding.Empty,
                Padding = new Padding((int)(6 * scale), (int)(4 * scale), (int)(6 * scale), (int)(4 * scale)),
            };

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = CorFundo,
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(36 * scale))); // Cabeçalho com botão voltar
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));                // Grid de 6 cards
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(24 * scale))); // Rodapé

            // ─── Header: Título + Botão Voltar ───
            var pnlHeader = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            var lblTitle = new Label
            {
                Text = "DIAGNÓSTICO DO SISTEMA (COMPLETO)",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
                Dock = DockStyle.Left,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            _btnBackToMain = new Button
            {
                Text = "← Voltar para Painel Principal",
                ForeColor = CorDestaqueLaranja,
                BackColor = Color.FromArgb(28, 28, 28),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Height = (int)(28 * scale),
                Width = (int)(210 * scale),
                Cursor = Cursors.Hand,
                Dock = DockStyle.Right,
            };
            _btnBackToMain.FlatAppearance.BorderColor = CorDestaqueLaranja;
            _btnBackToMain.FlatAppearance.BorderSize = 1;
            _btnBackToMain.Click += (s, e) =>
            {
                ShowExpandedDiagnostics(false);
            };
            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(_btnBackToMain);
            mainLayout.Controls.Add(pnlHeader, 0, 0);

            // ─── Grid de 6 Cards (2x3) ───
            var tblCards = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                BackColor = CorFundo,
            };
            tblCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            tblCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            tblCards.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
            tblCards.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
            tblCards.RowStyles.Add(new RowStyle(SizeType.Percent, 33.34f));

            // Card 1: Identificacion & OS
            tblCards.Controls.Add(BuildCardIdentAndOs(scale), 0, 0);

            // Card 2: Connection & Network
            tblCards.Controls.Add(BuildCardNetwork(scale), 1, 0);

            // Card 3: Storage & Partition (Lista todos os discos fixos da máquina)
            tblCards.Controls.Add(BuildCardStorage(scale), 0, 1);

            // Card 4: Hardware Resources
            tblCards.Controls.Add(BuildCardHardware(scale), 1, 1);

            // Card 5: Device & Warranty
            tblCards.Controls.Add(BuildCardDevice(scale), 0, 2);

            // Card 6: Security & Status
            tblCards.Controls.Add(BuildCardSecurity(scale), 1, 2);

            mainLayout.Controls.Add(tblCards, 0, 1);

            // ─── Rodapé informativo ───
            var lblFooter = new Label
            {
                Text = "As configurações de contas e o console de progresso estão ocultos. Clique em \"Voltar\" para gerenciar ou iniciar a instalação.",
                ForeColor = Color.FromArgb(140, 140, 140),
                Font = new Font("Segoe UI", 8.0f),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
            };
            mainLayout.Controls.Add(lblFooter, 0, 2);

            pnl.Controls.Add(mainLayout);
            return pnl;
        }

        private Panel BuildCardIdentAndOs(float scale)
        {
            _pnlIdentViewport = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            EnableDoubleBuffering(_pnlIdentViewport);

            _scrollBarIdent = new DarkScrollBar
            {
                Dock = DockStyle.Right,
                Width = (int)(8 * scale),
                TrackColor = Color.FromArgb(28, 28, 28),
                ThumbColor = CorDestaqueLaranja,
                SmallChange = (int)(18 * scale),
                Visible = false,
            };

            _pnlIdentContent = new Panel
            {
                Location = new Point(0, 0),
                BackColor = Color.Transparent,
                AutoScroll = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            EnableDoubleBuffering(_pnlIdentContent);

            Action<MouseEventArgs> handleIdentWheel = (e) =>
            {
                if (_scrollBarIdent != null && _scrollBarIdent.Visible)
                {
                    _scrollBarIdent.ScrollBy(-Math.Sign(e.Delta) * (int)(18 * scale));
                }
            };
            _pnlIdentViewport.MouseWheel += (s, e) => handleIdentWheel(e);
            _pnlIdentContent.MouseWheel += (s, e) => handleIdentWheel(e);

            _scrollBarIdent.ValueChanged += (s, e) =>
            {
                _pnlIdentContent.Top = -_scrollBarIdent.Value;
            };

            _pnlIdentViewport.Resize += (s, e) => UpdateIdentLayout();

            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 3,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, (int)(102 * scale))); // 102px acomoda "Computador:" sem corte ou quebra
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(22 * scale)));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(22 * scale)));
            tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            tbl.Controls.Add(CreateDiagHeaderLabel("Computador:"), 0, 0);
            _lblExpComputer = CreateDiagValueLabel("Identificando...");
            tbl.Controls.Add(_lblExpComputer, 1, 0);

            tbl.Controls.Add(CreateDiagHeaderLabel("Usuário:"), 0, 1);
            _lblExpUser = CreateDiagValueLabel("Identificando...");
            tbl.Controls.Add(_lblExpUser, 1, 1);

            var lblSys = CreateDiagHeaderLabel("Sistema:");
            lblSys.TextAlign = ContentAlignment.TopLeft;
            tbl.Controls.Add(lblSys, 0, 2);

            _lblExpOs = new Label
            {
                Text = "Identificando...",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.5f),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
                AutoSize = true,
                UseMnemonic = false,
                Margin = Padding.Empty,
            };
            tbl.Controls.Add(_lblExpOs, 1, 2);

            _pnlIdentContent.Controls.Add(tbl);
            _pnlIdentViewport.Controls.Add(_pnlIdentContent);
            _pnlIdentViewport.Controls.Add(_scrollBarIdent);

            return CreateCardContainer("Identificação e Sistema", _pnlIdentViewport, scale);
        }

        private void UpdateIdentLayout()
        {
            if (_pnlIdentViewport == null || _pnlIdentContent == null || _scrollBarIdent == null) return;
            int viewH = _pnlIdentViewport.ClientSize.Height;
            int viewW = _pnlIdentViewport.ClientSize.Width;

            int scrollW = _scrollBarIdent.Visible ? _scrollBarIdent.Width : 0;
            _pnlIdentContent.Width = Math.Max(10, viewW - scrollW);
            _pnlIdentContent.PerformLayout();

            int totalH = 0;
            foreach (Control c in _pnlIdentContent.Controls)
            {
                if (c.Bottom > totalH) totalH = c.Bottom;
            }
            _pnlIdentContent.Height = Math.Max(totalH, viewH);
            int maxScroll = Math.Max(0, totalH - viewH);
            _scrollBarIdent.Maximum = maxScroll;
            _scrollBarIdent.LargeChange = Math.Max(1, viewH);
            _scrollBarIdent.Visible = maxScroll > 0;
            _pnlIdentContent.Width = Math.Max(10, viewW - (_scrollBarIdent.Visible ? _scrollBarIdent.Width : 0));
        }

        private Panel BuildCardNetwork(float scale)
        {
            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, (int)(95 * scale))); // 95px suficiente para "Conexão:" e "Interfaces:"
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(24 * scale)));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(24 * scale)));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // 1. Conexão
            content.Controls.Add(CreateDiagHeaderLabel("Conexão:"), 0, 0);
            _lblExpNetStatus = CreateDiagValueLabel("Verificando...");
            _lblExpNetStatus.ForeColor = CorSucesso;
            _lblExpNetStatus.Cursor = Cursors.Hand;
            _lblExpNetStatus.Click += (s, e) =>
            {
                _lblExpNetStatus.Text = "Atualizando teste...";
                MetricsRefreshTimer_Tick(s, e);
            };
            content.Controls.Add(_lblExpNetStatus, 1, 0);

            // 2. Interfaces
            content.Controls.Add(CreateDiagHeaderLabel("Interfaces:"), 0, 1);
            _lblExpInterfaces = CreateDiagValueLabel("Detectando...");
            content.Controls.Add(_lblExpInterfaces, 1, 1);

            // 3. IPs Locais com barra de rolagem customizada
            var lblIps = CreateDiagHeaderLabel("IPs Locais:");
            lblIps.TextAlign = ContentAlignment.TopLeft;
            content.Controls.Add(lblIps, 0, 2);

            _pnlNetIpsViewport = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            EnableDoubleBuffering(_pnlNetIpsViewport);

            _scrollBarNetIps = new DarkScrollBar
            {
                Dock = DockStyle.Right,
                Width = (int)(8 * scale),
                TrackColor = Color.FromArgb(28, 28, 28),
                ThumbColor = CorDestaque,
                SmallChange = (int)(18 * scale),
                Visible = false,
            };

            _pnlNetIpsContent = new Panel
            {
                Location = new Point(0, 0),
                BackColor = Color.Transparent,
                AutoScroll = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            EnableDoubleBuffering(_pnlNetIpsContent);

            Action<MouseEventArgs> handleNetWheel = (e) =>
            {
                if (_scrollBarNetIps != null && _scrollBarNetIps.Visible)
                {
                    _scrollBarNetIps.ScrollBy(-Math.Sign(e.Delta) * (int)(18 * scale));
                }
            };
            _pnlNetIpsViewport.MouseWheel += (s, e) => handleNetWheel(e);
            _pnlNetIpsContent.MouseWheel += (s, e) => handleNetWheel(e);

            _scrollBarNetIps.ValueChanged += (s, e) =>
            {
                _pnlNetIpsContent.Top = -_scrollBarNetIps.Value;
            };

            _pnlNetIpsViewport.Resize += (s, e) => UpdateNetIpsLayout();

            _pnlNetIpsViewport.Controls.Add(_pnlNetIpsContent);
            _pnlNetIpsViewport.Controls.Add(_scrollBarNetIps);

            content.Controls.Add(_pnlNetIpsViewport, 1, 2);

            return CreateCardContainer("Conexão e Rede", content, scale);
        }

        private void UpdateNetIpsLayout()
        {
            if (_pnlNetIpsViewport == null || _pnlNetIpsContent == null || _scrollBarNetIps == null) return;
            int viewH = _pnlNetIpsViewport.ClientSize.Height;
            int totalH = 0;
            foreach (Control c in _pnlNetIpsContent.Controls)
            {
                if (c.Bottom > totalH) totalH = c.Bottom;
            }
            _pnlNetIpsContent.Height = Math.Max(totalH, viewH);
            int maxScroll = Math.Max(0, totalH - viewH);
            _scrollBarNetIps.Maximum = maxScroll;
            _scrollBarNetIps.LargeChange = Math.Max(1, viewH);
            _scrollBarNetIps.Visible = maxScroll > 0;
            _pnlNetIpsContent.Width = Math.Max(10, _pnlNetIpsViewport.ClientSize.Width - (_scrollBarNetIps.Visible ? _scrollBarNetIps.Width : 0));
        }

        private Panel BuildCardStorage(float scale)
        {
            _pnlStorageViewport = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            EnableDoubleBuffering(_pnlStorageViewport);

            _scrollBarStorage = new DarkScrollBar
            {
                Dock = DockStyle.Right,
                Width = (int)(8 * scale),
                TrackColor = Color.FromArgb(28, 28, 28),
                ThumbColor = CorDestaque,
                SmallChange = (int)(20 * scale),
                Visible = false,
            };

            _pnlStorageContent = new Panel
            {
                Location = new Point(0, 0),
                BackColor = Color.Transparent,
                AutoScroll = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            EnableDoubleBuffering(_pnlStorageContent);

            Action<MouseEventArgs> handleStorageWheel = (e) =>
            {
                if (_scrollBarStorage != null && _scrollBarStorage.Visible)
                {
                    _scrollBarStorage.ScrollBy(-Math.Sign(e.Delta) * (int)(20 * scale));
                }
            };
            _pnlStorageViewport.MouseWheel += (s, e) => handleStorageWheel(e);
            _pnlStorageContent.MouseWheel += (s, e) => handleStorageWheel(e);

            _scrollBarStorage.ValueChanged += (s, e) =>
            {
                _pnlStorageContent.Top = -_scrollBarStorage.Value;
            };

            _pnlStorageViewport.Resize += (s, e) => UpdateStorageDrivesLayout();

            _pnlStorageViewport.Controls.Add(_pnlStorageContent);
            _pnlStorageViewport.Controls.Add(_scrollBarStorage);

            return CreateCardContainer("Armazenamento e Partições", _pnlStorageViewport, scale);
        }

        private void UpdateStorageDrivesLayout()
        {
            if (_pnlStorageViewport == null || _pnlStorageContent == null || _scrollBarStorage == null) return;
            int viewH = _pnlStorageViewport.ClientSize.Height;
            int totalH = 0;
            foreach (Control c in _pnlStorageContent.Controls)
            {
                if (c.Bottom > totalH) totalH = c.Bottom;
            }
            _pnlStorageContent.Height = Math.Max(totalH, viewH);
            int maxScroll = Math.Max(0, totalH - viewH);
            _scrollBarStorage.Maximum = maxScroll;
            _scrollBarStorage.LargeChange = Math.Max(1, viewH);
            _scrollBarStorage.Visible = maxScroll > 0;
            _pnlStorageContent.Width = Math.Max(10, _pnlStorageViewport.ClientSize.Width - (_scrollBarStorage.Visible ? _scrollBarStorage.Width : 0));
        }

        private Panel BuildCardHardware(float scale)
        {
            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, (int)(95 * scale))); // 95px suficiente para "Memória RAM:" e "Processador:"
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(38 * scale))); // Processador (2 linhas)
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(32 * scale))); // Memória RAM (texto + barra próxima, padrão idêntico ao disco)
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));                // Espaçador inferior flexível

            var lblCpu = CreateDiagHeaderLabel("Processador:");
            lblCpu.TextAlign = ContentAlignment.TopLeft;
            content.Controls.Add(lblCpu, 0, 0);

            _lblExpCpu = new Label
            {
                Text = "Identificando...",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.5f),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
                AutoSize = false,
                UseMnemonic = false,
                Margin = Padding.Empty,
            };
            content.Controls.Add(_lblExpCpu, 1, 0);

            var lblRam = CreateDiagHeaderLabel("Memória RAM:");
            lblRam.TextAlign = ContentAlignment.TopLeft;
            content.Controls.Add(lblRam, 0, 1);

            var pnlRam = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            _lblExpRam = new Label
            {
                Text = "Calculando...",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.5f),
                Dock = DockStyle.Top,
                Height = (int)(18 * scale),
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false,
                AutoEllipsis = true,
                Margin = Padding.Empty,
            };
            _barExpRam = new MiniUsageBar
            {
                Dock = DockStyle.Bottom,
                Height = (int)(8 * scale),
                Percent = 50,
                FillColor = Color.FromArgb(70, 140, 200),
                Margin = Padding.Empty,
            };
            pnlRam.Controls.Add(_barExpRam);
            pnlRam.Controls.Add(_lblExpRam);
            content.Controls.Add(pnlRam, 1, 1);

            return CreateCardContainer("Recursos de Hardware", content, scale);
        }

        private Panel BuildCardDevice(float scale)
        {
            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, (int)(102 * scale))); // 102px suficiente para "Número de Série:"
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(24 * scale)));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(24 * scale)));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(24 * scale)));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // Espaçador inferior flexível para manter 24px uniformes acima

            content.Controls.Add(CreateDiagHeaderLabel("Fabricante:"), 0, 0);
            _lblExpManufacturer = CreateDiagValueLabel("Identificando...");
            content.Controls.Add(_lblExpManufacturer, 1, 0);

            content.Controls.Add(CreateDiagHeaderLabel("Modelo:"), 0, 1);
            _lblExpModel = CreateDiagValueLabel("Identificando...");
            content.Controls.Add(_lblExpModel, 1, 1);

            content.Controls.Add(CreateDiagHeaderLabel("Número de Série:"), 0, 2);
            _lblExpSerial = CreateDiagValueLabel("Identificando...");
            content.Controls.Add(_lblExpSerial, 1, 2);

            return CreateCardContainer("Dispositivo e Garantia", content, scale);
        }

        private Panel BuildCardSecurity(float scale)
        {
            _pnlSecViewport = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            EnableDoubleBuffering(_pnlSecViewport);

            _scrollBarSec = new DarkScrollBar
            {
                Dock = DockStyle.Right,
                Width = (int)(8 * scale),
                TrackColor = Color.FromArgb(28, 28, 28),
                ThumbColor = CorDestaqueLaranja,
                SmallChange = (int)(18 * scale),
                Visible = false,
            };

            _pnlSecContent = new Panel
            {
                Location = new Point(0, 0),
                BackColor = Color.Transparent,
                AutoScroll = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            EnableDoubleBuffering(_pnlSecContent);

            Action<MouseEventArgs> handleSecWheel = (e) =>
            {
                if (_scrollBarSec != null && _scrollBarSec.Visible)
                {
                    _scrollBarSec.ScrollBy(-Math.Sign(e.Delta) * (int)(18 * scale));
                }
            };
            _pnlSecViewport.MouseWheel += (s, e) => handleSecWheel(e);
            _pnlSecContent.MouseWheel += (s, e) => handleSecWheel(e);

            _scrollBarSec.ValueChanged += (s, e) =>
            {
                _pnlSecContent.Top = -_scrollBarSec.Value;
            };

            _pnlSecViewport.Resize += (s, e) => UpdateSecurityLayout();

            _lblSecTitle = new Label
            {
                Text = "Segurança:",
                ForeColor = Color.FromArgb(170, 170, 170),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Location = new Point(0, 0),
                Size = new Size(280, (int)(16 * scale)),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = Padding.Empty,
            };
            _lblExpSecurity = new Label
            {
                Text = "Windows Defender: Ativo | BitLocker: Desativado",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(0, (int)(16 * scale)),
                Size = new Size(280, (int)(20 * scale)),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = true,
                Margin = Padding.Empty,
            };

            _lblStateTitle = new Label
            {
                Text = "Estado do Sistema:",
                ForeColor = Color.FromArgb(170, 170, 170),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Location = new Point(0, (int)(38 * scale)),
                Size = new Size(280, (int)(16 * scale)),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = Padding.Empty,
            };
            _lblExpSystemState = new Label
            {
                Text = "Reboot Pendente: Não | Fonte: Na tomada",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(0, (int)(54 * scale)),
                Size = new Size(280, (int)(20 * scale)),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = true,
                Margin = Padding.Empty,
            };

            _pnlSecContent.Controls.Add(_lblSecTitle);
            _pnlSecContent.Controls.Add(_lblExpSecurity);
            _pnlSecContent.Controls.Add(_lblStateTitle);
            _pnlSecContent.Controls.Add(_lblExpSystemState);

            _pnlSecViewport.Controls.Add(_pnlSecContent);
            _pnlSecViewport.Controls.Add(_scrollBarSec);

            return CreateCardContainer("Segurança e Status", _pnlSecViewport, scale);
        }

        private void UpdateSecurityLayout()
        {
            if (_pnlSecViewport == null || _pnlSecContent == null || _scrollBarSec == null || _lblSecTitle == null || _lblExpSecurity == null || _lblStateTitle == null || _lblExpSystemState == null) return;
            float scale = GetDpiScale();
            int viewH = _pnlSecViewport.ClientSize.Height;
            int viewW = _pnlSecViewport.ClientSize.Width;
            int scrollW = _scrollBarSec.Visible ? _scrollBarSec.Width : 0;
            int contentW = Math.Max(50, viewW - scrollW);

            _lblSecTitle.Width = contentW;
            _lblExpSecurity.MaximumSize = new Size(contentW, 0);
            _lblStateTitle.Width = contentW;
            _lblExpSystemState.MaximumSize = new Size(contentW, 0);

            _lblSecTitle.Top = 0;
            _lblExpSecurity.Top = _lblSecTitle.Bottom;
            _lblStateTitle.Top = _lblExpSecurity.Bottom + (int)(3 * scale);
            _lblExpSystemState.Top = _lblStateTitle.Bottom;

            int totalH = _lblExpSystemState.Bottom + (int)(2 * scale);
            _pnlSecContent.Height = Math.Max(totalH, viewH);
            int maxScroll = Math.Max(0, totalH - viewH);
            _scrollBarSec.Maximum = maxScroll;
            _scrollBarSec.LargeChange = Math.Max(1, viewH);
            _scrollBarSec.Visible = maxScroll > 0;
            _pnlSecContent.Width = Math.Max(10, viewW - (_scrollBarSec.Visible ? _scrollBarSec.Width : 0));
        }

        private Panel CreateCardContainer(string title, Control content, float scale, Color? titleColor = null)
        {
            var pnlCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(32, 32, 32),
                Margin = new Padding((int)(4 * scale)),
                Padding = new Padding((int)(10 * scale), (int)(6 * scale), (int)(10 * scale), (int)(6 * scale)),
            };
            pnlCard.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(50, 50, 50), 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, pnlCard.Width - 1, pnlCard.Height - 1);
                }
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(22 * scale))); // Título com altura otimizada
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));                // Conteúdo dos itens

            var lblTitle = new Label
            {
                Text = title,
                ForeColor = titleColor ?? CorDestaqueLaranja,
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false,
                Margin = new Padding(0, 0, 0, (int)(2 * scale)),
            };
            layout.Controls.Add(lblTitle, 0, 0);

            content.Dock = DockStyle.Fill;
            content.Margin = Padding.Empty;
            layout.Controls.Add(content, 0, 1);

            pnlCard.Controls.Add(layout);
            return pnlCard;
        }

        private static Label CreateDiagHeaderLabel(string text)
        {
            return new Label
            {
                Text = text,
                ForeColor = Color.FromArgb(170, 170, 170),
                Font = new Font("Segoe UI", 8.5f),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = false,
                UseMnemonic = false,
                Margin = Padding.Empty,
            };
        }

        private static Label CreateDiagValueLabel(string text)
        {
            return new Label
            {
                Text = text,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.5f),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = false,
                UseMnemonic = false,
                Margin = Padding.Empty,
            };
        }

        /// <summary>
        /// Alterna entre a visualização padrão de abas/log e o painel de diagnóstico completo.
        /// </summary>
        private void ShowExpandedDiagnostics(bool show)
        {
            if (_pnlNormalLeftView == null || _pnlExpandedDiagnostics == null) return;
            if (show)
            {
                _pnlNormalLeftView.Visible = false;
                _pnlExpandedDiagnostics.Visible = true;
                _pnlExpandedDiagnostics.BringToFront();
                if (_lastDiag != null)
                {
                    RefreshExpandedDiagnosticsUI(_lastDiag);
                }
                UpdateIdentLayout();
                UpdateNetIpsLayout();
                UpdateStorageDrivesLayout();
                UpdateSecurityLayout();
            }
            else
            {
                _pnlExpandedDiagnostics.Visible = false;
                _pnlNormalLeftView.Visible = true;
                _pnlNormalLeftView.BringToFront();
            }
        }

        /// <summary>
        /// Copia o endereço IP local ativo para a área de transferência do Windows.
        /// </summary>
        private void CopyLocalIpToClipboard()
        {
            try
            {
                if (_lastDiag != null && !string.IsNullOrEmpty(_lastDiag.LocalIp) && _lastDiag.LocalIp != "Indisponível")
                {
                    Clipboard.SetText(_lastDiag.LocalIp);
                    if (_lastDiag.AllLocalIps != null && _lastDiag.AllLocalIps.Count > 1)
                    {
                        MessageBox.Show(
                            string.Format("IP principal ({0}) copiado para a área de transferência!\n\nTodos os IPs IPv4 detectados:\n• {1}",
                                _lastDiag.LocalIp, string.Join("\n• ", _lastDiag.AllLocalIps.ToArray())),
                            "Endereços IP da Máquina",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show(
                            string.Format("IP {0} copiado para a área de transferência com sucesso!", _lastDiag.LocalIp),
                            "IP Copiado",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                }
            }
            catch { }
        }

        private void CopyIpToClipboard(string ip)
        {
            try
            {
                if (!string.IsNullOrEmpty(ip) && ip != "Indisponível")
                {
                    Clipboard.SetText(ip);
                    MessageBox.Show(
                        string.Format("IP {0} copiado para a área de transferência com sucesso!", ip),
                        "IP Copiado",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch { }
        }

        /// <summary>
        /// Atualiza os cards compacto e expandido de diagnóstico na interface.
        /// </summary>
        private void RefreshDiagnosticViews(FullSystemDiagnosticInfo diag)
        {
            if (diag == null || IsDisposed) return;
            _lastDiag = diag;

            if (InvokeRequired)
            {
                Invoke(new Action<FullSystemDiagnosticInfo>(RefreshDiagnosticViews), diag);
                return;
            }

            // 1. Atualiza Card Compacto (Aba 1)
            if (_lblDiagComputer != null)
                _lblDiagComputer.Text = string.Format("{0} ({1})", diag.ComputerName, diag.DomainOrWorkgroup);

            if (_lblDiagUser != null)
                _lblDiagUser.Text = string.Format("{0}{1}", diag.UserName, diag.IsAdmin ? " (Admin)" : "");

            if (_lblDiagOs != null)
                _lblDiagOs.Text = diag.WindowsEdition;

            if (_barDiagDisk != null)
            {
                DiskDriveDiagnosis cDrive = null;
                if (diag.Drives != null)
                {
                    cDrive = diag.Drives.FirstOrDefault(d => d.DriveLetter.Equals("C:", StringComparison.OrdinalIgnoreCase)) ?? diag.Drives.FirstOrDefault();
                }
                if (cDrive != null)
                {
                    _barDiagDisk.Percent = cDrive.PercentUsed;
                    if (cDrive.PercentFree < 10.0 || cDrive.FreeGb < 15.0)
                        _barDiagDisk.FillColor = Color.FromArgb(230, 50, 50); // Cheio / Crítico (Vermelho)
                    else if (cDrive.PercentFree < 20.0 || cDrive.FreeGb < 30.0)
                        _barDiagDisk.FillColor = Color.FromArgb(240, 180, 0); // Quase cheio / Atenção (Amarelo)
                    else
                        _barDiagDisk.FillColor = CorSucesso; // Normal / Bom (Verde)
                }
            }

            if (_lblDiagNet != null)
            {
                _lblDiagNet.Text = diag.ConnectionStatusText + " ↻";
                _lblDiagNet.ForeColor = (diag.ConnectionStatusColor == ConsoleColor.Green) 
                    ? CorSucesso 
                    : ((diag.ConnectionStatusColor == ConsoleColor.Yellow) ? Color.FromArgb(240, 180, 0) : Color.FromArgb(230, 50, 50));
            }

            if (_lblDiagIp != null)
            {
                _lblDiagIp.Text = diag.LocalIp + " [+]";
            }

            // 2. Atualiza Visão Expandida
            RefreshExpandedDiagnosticsUI(diag);
        }

        /// <summary>
        /// Atualiza todos os 6 cards da visão expandida de diagnóstico do sistema.
        /// </summary>
        private void RefreshExpandedDiagnosticsUI(FullSystemDiagnosticInfo diag)
        {
            if (diag == null || _pnlExpandedDiagnostics == null) return;

            // Card 1: Identificação e Sistema
            if (_lblExpComputer != null) _lblExpComputer.Text = string.Format("{0} ({1})", diag.ComputerName, diag.DomainOrWorkgroup);
            if (_lblExpUser != null) _lblExpUser.Text = string.Format("{0}{1}", diag.UserName, diag.IsAdmin ? " (Admin)" : "");
            if (_lblExpOs != null) _lblExpOs.Text = string.Format("{0}\n{1}", diag.WindowsEdition, diag.WindowsBuild);
            UpdateIdentLayout();

            // Card 2: Conexão e Rede
            if (_lblExpNetStatus != null)
            {
                _lblExpNetStatus.Text = diag.ConnectionStatusText + " ↻";
                _lblExpNetStatus.ForeColor = (diag.ConnectionStatusColor == ConsoleColor.Green) 
                    ? CorSucesso 
                    : ((diag.ConnectionStatusColor == ConsoleColor.Yellow) ? Color.FromArgb(240, 180, 0) : Color.FromArgb(230, 50, 50));
            }
            if (_lblExpInterfaces != null) _lblExpInterfaces.Text = diag.NetworkInterfacesSummary;

            if (_pnlNetIpsContent != null && _pnlNetIpsViewport != null && _scrollBarNetIps != null)
            {
                _pnlNetIpsContent.SuspendLayout();
                _pnlNetIpsContent.Controls.Clear();
                int ipTop = 0;
                var ipList = (diag.AllLocalIps != null && diag.AllLocalIps.Count > 0)
                    ? diag.AllLocalIps
                    : new List<string> { diag.LocalIp };

                float scale = GetDpiScale();
                foreach (var ipItem in ipList)
                {
                    string capturedIpItem = ipItem;
                    var lblIp = new Label
                    {
                        Text = capturedIpItem + " [+]",
                        ForeColor = CorDestaque,
                        Font = new Font("Segoe UI", 8.5f),
                        Location = new Point(0, ipTop),
                        Size = new Size(Math.Max(100, _pnlNetIpsViewport.ClientSize.Width - 12), (int)(18 * scale)),
                        Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                        TextAlign = ContentAlignment.MiddleLeft,
                        Cursor = Cursors.Hand,
                        UseMnemonic = false,
                    };
                    lblIp.Click += (s, e) =>
                    {
                        string cleanIp = capturedIpItem.Split(' ')[0].Trim();
                        CopyIpToClipboard(cleanIp);
                    };
                    _pnlNetIpsContent.Controls.Add(lblIp);
                    ipTop += (int)(18 * scale);
                }

                _pnlNetIpsContent.Height = Math.Max(ipTop, _pnlNetIpsViewport.ClientSize.Height);
                UpdateNetIpsLayout();
                _pnlNetIpsContent.ResumeLayout();
            }

            // Card 3: Armazenamento e Partições (Lista todos os discos fixos da máquina com DarkScrollBar)
            if (_pnlStorageContent != null && _pnlStorageViewport != null && _scrollBarStorage != null)
            {
                _pnlStorageContent.SuspendLayout();
                _pnlStorageContent.Controls.Clear();

                float scale = GetDpiScale();
                if (diag.Drives != null && diag.Drives.Count > 0)
                {
                    int driveTop = 0;
                    for (int i = 0; i < diag.Drives.Count; i++)
                    {
                        var drive = diag.Drives[i];
                        int itemH = (int)(38 * scale);
                        var driveItemPanel = new Panel
                        {
                            Location = new Point(0, driveTop),
                            Size = new Size(Math.Max(100, _pnlStorageViewport.ClientSize.Width - 12), itemH),
                            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                            BackColor = Color.Transparent,
                        };

                        var lblDriveText = new Label
                        {
                            Text = string.Format("Disco {0} ({1}): {2}", drive.DriveLetter, drive.MediaType, drive.StatusText),
                            ForeColor = Color.White,
                            Font = new Font("Segoe UI", 8.5f),
                            Dock = DockStyle.Top,
                            Height = (int)(18 * scale),
                            TextAlign = ContentAlignment.MiddleLeft,
                            UseMnemonic = false,
                        };

                        var barDrive = new MiniUsageBar
                        {
                            Dock = DockStyle.Bottom,
                            Height = (int)(8 * scale),
                            Percent = drive.PercentUsed,
                            FillColor = (drive.StatusColor == ConsoleColor.Red) 
                                ? Color.FromArgb(230, 50, 50) 
                                : ((drive.StatusColor == ConsoleColor.Yellow) ? Color.FromArgb(240, 180, 0) : CorSucesso),
                        };

                        driveItemPanel.Controls.Add(barDrive);
                        driveItemPanel.Controls.Add(lblDriveText);
                        _pnlStorageContent.Controls.Add(driveItemPanel);
                        driveTop += itemH + (int)(4 * scale);
                    }
                }
                else
                {
                    var lblNone = new Label
                    {
                        Text = "Nenhuma unidade fixa detectada.",
                        ForeColor = Color.Gray,
                        Dock = DockStyle.Fill,
                    };
                    _pnlStorageContent.Controls.Add(lblNone);
                }
                _pnlStorageContent.ResumeLayout();
                UpdateStorageDrivesLayout();
            }

            // Card 4: Recursos de Hardware
            if (_lblExpCpu != null)
            {
                _lblExpCpu.Text = string.Format("{0}\n{1}", diag.CpuName, diag.CpuSummary);
            }
            if (_lblExpRam != null)
            {
                _lblExpRam.Text = string.Format("{0} ({1:N1} GB em uso - {2:N0}%)", diag.RamSummary, diag.UsedRamGb, diag.RamUsagePercent);
            }
            if (_barExpRam != null)
            {
                _barExpRam.Percent = diag.RamUsagePercent;
            }

            // Card 5: Dispositivo e Garantia
            if (_lblExpManufacturer != null)
            {
                _lblExpManufacturer.Text = string.IsNullOrEmpty(diag.Manufacturer) ? "Genérico / Padrão" : diag.Manufacturer;
            }
            if (_lblExpModel != null)
            {
                _lblExpModel.Text = string.IsNullOrEmpty(diag.Model) ? "Placa-Mãe / Padrão" : diag.Model;
            }
            if (_lblExpSerial != null)
            {
                _lblExpSerial.Text = string.IsNullOrEmpty(diag.SerialNumber) ? "Não informado" : diag.SerialNumber;
            }

            // Card 6: Segurança e Status
            if (_lblExpSecurity != null)
            {
                _lblExpSecurity.Text = string.Format("Windows Defender: {0} | BitLocker: {1}", diag.DefenderStatusText, diag.BitLockerStatusText);
            }
            if (_lblExpSystemState != null)
            {
                _lblExpSystemState.Text = string.Format("Reboot Pendente: {0} | Fonte: {1}", diag.PendingRebootText, diag.PowerSourceText);
            }
            UpdateSecurityLayout();
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

            var pnlTextWrapper = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 20, 20),
                Padding = new Padding((int)(8 * scale), (int)(8 * scale), (int)(4 * scale), (int)(8 * scale)),
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

            pnlTextWrapper.Controls.Add(_txtLog);
            panel.Controls.Add(pnlTextWrapper);
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
            ShowExpandedDiagnostics(false);

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
            _installationStarted = true;
            if (_connectionMonitorTimer != null)
            {
                _connectionMonitorTimer.Stop();
            }
            if (_metricsRefreshTimer != null)
            {
                _metricsRefreshTimer.Stop();
            }
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
                options.PreventWallpaperChange = _chkPreventWallpaperChange != null && _chkPreventWallpaperChange.Checked;
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
        /// Inicializa o timer de monitoramento contínuo de conectividade de rede (a cada 2 segundos).
        /// Permite detectar imediatamente quando a internet cai ou volta na tela inicial, atualizando IP e Ping sem travamento.
        /// </summary>
        private void InitConnectionMonitor()
        {
            _connectionMonitorTimer = new System.Windows.Forms.Timer();
            _connectionMonitorTimer.Interval = 2000;
            _connectionMonitorTimer.Tick += ConnectionMonitorTimer_Tick;
            _connectionMonitorTimer.Start();

            _metricsRefreshTimer = new System.Windows.Forms.Timer();
            _metricsRefreshTimer.Interval = 5000;
            _metricsRefreshTimer.Tick += MetricsRefreshTimer_Tick;
            _metricsRefreshTimer.Start();

            try
            {
                System.Net.NetworkInformation.NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
            }
            catch { }
        }

        /// <summary>
        /// Atualiza periodicamente a cada 5 segundos as informações de espaço em disco e teste de ping,
        /// além do uso de memória RAM, tanto no card compacto quanto no diagnóstico expandido.
        /// </summary>
        private async void MetricsRefreshTimer_Tick(object sender, EventArgs e)
        {
            if (_installing || _installationStarted || IsDisposed || _isPeriodicMetricsRunning) return;
            _isPeriodicMetricsRunning = true;
            try
            {
                // 1. Atualiza espaço em disco de forma instantânea (via DriveInfo nativo)
                var diskDiag = GetSystemDriveFreeSpaceDiagnosis();
                if (_barDiagDisk != null && diskDiag != null)
                {
                    _barDiagDisk.Percent = diskDiag.PercentUsed;
                    if (diskDiag.Color == ConsoleColor.Red)
                        _barDiagDisk.FillColor = Color.FromArgb(230, 50, 50); // Cheio / Crítico (< 10% livre ou < 15 GB)
                    else if (diskDiag.Color == ConsoleColor.Yellow)
                        _barDiagDisk.FillColor = Color.FromArgb(240, 180, 0); // Atenção (< 20% livre ou < 30 GB)
                    else
                        _barDiagDisk.FillColor = CorSucesso; // Saudável / Bom (Verde)
                    _barDiagDisk.Invalidate();
                }

                // Se houver diagnóstico prévio, atualiza as métricas dinâmicas completas (RAM e Discos no painel expandido)
                if (_lastDiag != null)
                {
                    _service.UpdateDynamicMetrics(_lastDiag);
                    RefreshDynamicMetricsViews(_lastDiag);
                }

                // 2. Atualiza teste de ping de forma assíncrona (sem congelar a UI)
                var pingDiag = await _service.MeasureInternetPingAsync();
                if (IsDisposed || _installing || _installationStarted) return;

                if (pingDiag != null && pingDiag.Success)
                {
                    string pingText = string.Format("Online ({0} ms) ↻", pingDiag.LatencyMs);
                    Color pingColor = (pingDiag.LatencyMs < 80)
                        ? CorSucesso
                        : ((pingDiag.LatencyMs < 180) ? Color.FromArgb(240, 180, 0) : Color.FromArgb(230, 50, 50));

                    if (_lblDiagNet != null)
                    {
                        _lblDiagNet.Text = pingText;
                        _lblDiagNet.ForeColor = pingColor;
                    }
                    if (_lblExpNetStatus != null)
                    {
                        _lblExpNetStatus.Text = pingText;
                        _lblExpNetStatus.ForeColor = pingColor;
                    }

                    if (_lastDiag != null)
                    {
                        _lastDiag.HasInternet = true;
                        _lastDiag.ConnectionStatusText = string.Format("Online ({0} ms)", pingDiag.LatencyMs);
                        _lastDiag.PingLatencyText = string.Format("{0} ms", pingDiag.LatencyMs);
                        _lastDiag.ConnectionStatusColor = (pingDiag.LatencyMs < 80)
                            ? ConsoleColor.Green
                            : ((pingDiag.LatencyMs < 180) ? ConsoleColor.Yellow : ConsoleColor.Red);
                    }
                }
                else
                {
                    bool online = await _service.HasInternetConnectionAsync();
                    if (IsDisposed || _installing || _installationStarted) return;

                    string statusText = online ? "Online ↻" : "Offline ↻";
                    Color statusColor = online ? CorSucesso : Color.FromArgb(230, 50, 50);

                    if (_lblDiagNet != null)
                    {
                        _lblDiagNet.Text = statusText;
                        _lblDiagNet.ForeColor = statusColor;
                    }
                    if (_lblExpNetStatus != null)
                    {
                        _lblExpNetStatus.Text = statusText;
                        _lblExpNetStatus.ForeColor = statusColor;
                    }

                    if (_lastDiag != null)
                    {
                        _lastDiag.HasInternet = online;
                        _lastDiag.ConnectionStatusText = online ? "Online" : "Offline";
                        _lastDiag.ConnectionStatusColor = online ? ConsoleColor.Green : ConsoleColor.Red;
                    }
                }
            }
            catch { }
            finally
            {
                _isPeriodicMetricsRunning = false;
            }
        }

        /// <summary>
        /// Atualiza periodicamente a cada 3 segundos as métricas de uso de memória RAM e espaço em disco
        /// na tela inicial e no painel expandido de diagnóstico sem congelar a interface.
        /// </summary>
        private void RefreshDynamicMetricsViews(FullSystemDiagnosticInfo diag)
        {
            if (diag == null || IsDisposed) return;

            // 1. Atualiza Mini Bar de Disco C: no card compacto
            if (_barDiagDisk != null && diag.Drives != null)
            {
                var cDrive = diag.Drives.FirstOrDefault(d => d.DriveLetter.Equals("C:", StringComparison.OrdinalIgnoreCase)) ?? diag.Drives.FirstOrDefault();
                if (cDrive != null)
                {
                    _barDiagDisk.Percent = cDrive.PercentUsed;
                    if (cDrive.PercentFree < 10.0 || cDrive.FreeGb < 15.0)
                        _barDiagDisk.FillColor = Color.FromArgb(230, 50, 50);
                    else if (cDrive.PercentFree < 20.0 || cDrive.FreeGb < 30.0)
                        _barDiagDisk.FillColor = Color.FromArgb(240, 180, 0);
                    else
                        _barDiagDisk.FillColor = CorSucesso;
                    _barDiagDisk.Invalidate();
                }
            }

            // 2. Atualiza RAM na visão expandida
            if (_lblExpRam != null)
            {
                _lblExpRam.Text = string.Format("{0} ({1:N1} GB em uso - {2:N0}%)", diag.RamSummary, diag.UsedRamGb, diag.RamUsagePercent);
            }
            if (_barExpRam != null)
            {
                _barExpRam.Percent = diag.RamUsagePercent;
                _barExpRam.Invalidate();
            }

            // 3. Atualiza Discos na visão expandida se o painel estiver visível
            if (_pnlExpandedDiagnostics != null && _pnlExpandedDiagnostics.Visible && _pnlStorageContent != null && diag.Drives != null)
            {
                int driveIndex = 0;
                foreach (Control c in _pnlStorageContent.Controls)
                {
                    var panel = c as Panel;
                    if (panel != null && driveIndex < diag.Drives.Count)
                    {
                        var drive = diag.Drives[driveIndex];
                        foreach (Control inner in panel.Controls)
                        {
                            var lbl = inner as Label;
                            if (lbl != null)
                            {
                                lbl.Text = string.Format("Disco {0} ({1}): {2}", drive.DriveLetter, drive.MediaType, drive.StatusText);
                            }
                            var bar = inner as MiniUsageBar;
                            if (bar != null)
                            {
                                bar.Percent = drive.PercentUsed;
                                bar.FillColor = (drive.StatusColor == ConsoleColor.Red)
                                    ? Color.FromArgb(230, 50, 50)
                                    : ((drive.StatusColor == ConsoleColor.Yellow) ? Color.FromArgb(240, 180, 0) : CorSucesso);
                                bar.Invalidate();
                            }
                        }
                        driveIndex++;
                    }
                }
            }
        }

        private void OnNetworkAvailabilityChanged(object sender, System.Net.NetworkInformation.NetworkAvailabilityEventArgs e)
        {
            if (_installing || _installationStarted || IsDisposed) return;
            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(TriggerNetworkAvailabilityCheck));
                }
                else
                {
                    TriggerNetworkAvailabilityCheck();
                }
            }
            catch { }
        }

        private async void TriggerNetworkAvailabilityCheck()
        {
            if (_installing || _installationStarted || IsDisposed) return;
            await CheckAndRefreshConnectionStatusAsync(true);
        }

        private async void ConnectionMonitorTimer_Tick(object sender, EventArgs e)
        {
            if (_installing || _installationStarted || IsDisposed) return;
            await CheckAndRefreshConnectionStatusAsync(false);
        }

        /// <summary>
        /// Verifica a conectividade com a internet e atualiza a tela inicial dinamicamente se o status mudou.
        /// </summary>
        private async Task CheckAndRefreshConnectionStatusAsync(bool forceCheck)
        {
            if (_installing || _installationStarted || _isUpdatingSystemInfo || IsDisposed) return;

            _isUpdatingSystemInfo = true;
            try
            {
                _connectionMonitorTicks++;
                bool currentInternet = await _service.HasInternetConnectionAsync();
                string currentIp = GetLocalIpAddress();

                bool statusChanged = (!_lastDetectedInternet.HasValue) ||
                                     (currentInternet != _lastDetectedInternet.Value) ||
                                     (currentIp != _lastDetectedIp);

                if (statusChanged || forceCheck)
                {
                    _lastDetectedInternet = currentInternet;
                    _lastDetectedIp = currentIp;

                    var diag = await _service.GetFullSystemDiagnosisAsync();
                    RefreshDiagnosticViews(diag);

                    if (!_installing && !_installationStarted && !IsDisposed)
                    {
                        RenderInitialSystemInfo();
                    }
                }
            }
            catch { }
            finally
            {
                _isUpdatingSystemInfo = false;
            }
        }

        /// <summary>
        /// Exibe a mensagem inicial de prontidão no log quando o sistema é iniciado.
        /// </summary>
        private void RenderInitialSystemInfo()
        {
            if (_installing || _installationStarted || IsDisposed) return;

            SendMessage(_txtLog.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            try
            {
                _txtLog.Clear();
                AppendLog("Pronto para iniciar. Configure as opções e selecione os programas desejados.\n", ConsoleColor.Gray);
            }
            finally
            {
                SendMessage(_txtLog.Handle, WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
                _txtLog.Invalidate();
                SyncLogScrollBar();
            }
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
                new ProgramInfo("winrar",      "WinRAR",                         "winrar.exe",                           "/S", "", "Geral") { DefaultChecked = false },
            };
            standardList.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCultureIgnoreCase));

            var manutencaoList = new List<ProgramInfo>
            {
                new ProgramInfo("crystaldiskinfo", "CrystalDiskInfo",         "CrystalDiskInfo9_9_5.exe", "/VERYSILENT /NORESTART /ALLUSERS /SP-",               "", "Manutenção") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("crystaldiskmark", "CrystalDiskMark",         "CrystalDiskMark9_0_3.exe", "/VERYSILENT /NORESTART /ALLUSERS /SP-",               "", "Manutenção") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("easeustodo",      "EaseUS Todo Backup Free", "tb_free.exe",              "/VERYSILENT /NORESTART /ALLUSERS /SUPPRESSMSGBOXES",  "", "Manutenção") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("recuva",          "Recuva Free",             "rcsetup155.exe",           "/S /allusers",                              "", "Manutenção") { RequiresInternet = true, DefaultChecked = false },
            };
            manutencaoList.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCultureIgnoreCase));

            var craftList = new List<ProgramInfo>
            {
                new ProgramInfo("cadcraft",    "CADCraft (Desenho Técnico CAD)",     "cadcraft-windows-x64.msi",    "/qn /norestart ALLUSERS=1", "", "ArtCraft") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("deckcraft",   "DeckCraft (Apresentações e Slides)", "deckcraft-windows-x64.msi",   "/qn /norestart ALLUSERS=1", "", "ArtCraft") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("designcraft", "DesignCraft (Design Gráfico e UI)",  "designcraft-windows-x64.msi", "/qn /norestart ALLUSERS=1", "", "ArtCraft") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("effectcraft", "EffectCraft (Efeitos Visuais e VFX)","effectcraft-windows-x64.msi","/qn /norestart ALLUSERS=1", "", "ArtCraft") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("filmcraft",   "FilmCraft (Editor de Vídeo)",        "filmcraft-windows-x64.msi",   "/qn /norestart ALLUSERS=1", "", "ArtCraft") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("gridcraft",   "GridCraft (Planilhas Eletrônicas)",  "gridcraft-windows-x64.msi",   "/qn /norestart ALLUSERS=1", "", "ArtCraft") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("lightcraft",  "LightCraft (Iluminação e Estúdio 3D)","lightcraft-windows-x64.msi", "/qn /norestart ALLUSERS=1", "", "ArtCraft") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("pdfcraft",    "PdfCraft (Leitor e Editor de PDF)",  "pdfcraft-windows-x64.msi",    "/qn /norestart ALLUSERS=1", "", "ArtCraft") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("photocraft",  "PhotoCraft (Editor de Fotos)",       "photocraft-windows-x64.msi",  "/qn /norestart ALLUSERS=1", "", "ArtCraft") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("soundcraft",  "SoundCraft (Editor de Áudio)",       "soundcraft-windows-x64.msi",  "/qn /norestart ALLUSERS=1", "", "ArtCraft") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("texturecraft","TextureCraft (Texturização 3D)",     "texturecraft-windows-x64.msi","/qn /norestart ALLUSERS=1", "", "ArtCraft") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("vectorcraft", "VectorCraft (Ilustração Vetorial)",  "vectorcraft-windows-x64.msi", "/qn /norestart ALLUSERS=1", "", "ArtCraft") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("wordcraft",   "WordCraft (Editor de Texto)",        "wordcraft-windows-x64.msi",   "/qn /norestart ALLUSERS=1", "", "ArtCraft") { RequiresInternet = true, DefaultChecked = false },
            };
            craftList.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCultureIgnoreCase));

            var devList = new List<ProgramInfo>
            {
                new ProgramInfo("antigravity",    "Antigravity",         "Antigravity-x64.exe", "/S /allusers",                                            "", "Desenvolvimento") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("antigravityide", "Antigravity IDE",     "Antigravity_IDE.exe", "/VERYSILENT /NORESTART /ALLUSERS /MERGETASKS=\"!runcode\"", "", "Desenvolvimento") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("vscode",         "Visual Studio Code",  "VSCodeSetup_x64.exe", "/VERYSILENT /NORESTART /ALLUSERS /MERGETASKS=\"!runcode\"", "", "Desenvolvimento") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("nodejs",         "Node.js LTS",         "node_lts_x64.msi",    "/qn /norestart ALLUSERS=1",                               "", "Desenvolvimento") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("filezilla",      "FileZilla Client",    "FileZilla_setup.exe", "/S",                                              "", "Desenvolvimento") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("dbeaver",        "DBeaver Community",   "dbeaver_setup.exe",   "/S /allusers",                                    "", "Desenvolvimento") { RequiresInternet = true, DefaultChecked = false },
            };

            var mediaList = new List<ProgramInfo>
            {
                new ProgramInfo("affinity",  "Affinity Studio (Português)",        "Affinity.msix",            "",                                            "", "Design & Multimídia") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("blender",   "Blender (3D e Criação)",             "blender.msi",              "/qn /norestart ALLUSERS=1",                   "", "Design & Multimídia") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("tracedraw", "TraceDraw (Vetorização Automática)", "tracedraw-setup-x64.exe", "/VERYSILENT /NORESTART /ALLUSERS /SUPPRESSMSGBOXES /SP-", "", "Design & Multimídia") { RequiresInternet = true, DefaultChecked = false },
            };

            var aiList = new List<ProgramInfo>
            {
                new ProgramInfo("chatgpt",  "ChatGPT Desktop",  "ChatGPT-x64.msix",             "",                                    "", "Inteligência Artificial") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("claude",   "Claude Desktop",   "Claude-Setup-x64.exe",         "--silent /allusers",                  "", "Inteligência Artificial") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("deepseek", "DeepSeek Desktop", "deepseek-harness-win-x64.exe", "/S /allusers",                        "", "Inteligência Artificial") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("gemini",   "Gemini Desktop",   "GeminiSetup.exe",              "/S /allusers",                        "", "Inteligência Artificial") { RequiresInternet = true, DefaultChecked = false },
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
            list.Add(new ProgramInfo("artcraft", "ArtCraft Studio (Suíte Criativa)", "ArtCraft_setup.exe", "/S /allusers", "", "ArtCraft") { DefaultChecked = false });
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
        public double PercentUsed
        {
            get { return Math.Max(0.0, 100.0 - PercentFree); }
        }
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

    /// <summary>
    /// Barra minimalista de uso e percentual para memória RAM e unidades de disco.
    /// </summary>
    public class MiniUsageBar : Control
    {
        private double _percent = 0;
        private Color _fillColor = Color.FromArgb(0, 200, 80);

        public MiniUsageBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.FromArgb(48, 48, 48);
            Height = 8;
        }

        public double Percent
        {
            get { return _percent; }
            set
            {
                _percent = Math.Max(0, Math.Min(100, value));
                Invalidate();
            }
        }

        public Color FillColor
        {
            get { return _fillColor; }
            set
            {
                _fillColor = value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.Clear(BackColor);
            int fillWidth = (int)((Width * _percent) / 100.0);
            if (fillWidth > 0)
            {
                using (var brush = new SolidBrush(_fillColor))
                {
                    g.FillRectangle(brush, 0, 0, fillWidth, Height);
                }
            }
        }
    }
}
