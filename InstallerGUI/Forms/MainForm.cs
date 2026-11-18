using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Media;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
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
        private CheckBox _chkShortcutSuporteNorte;
        private CheckBox _chkShortcutWebmailNortel;
        private CheckBox _chkShortcutWebmailTerceirizacao;
        private Panel _pnlPrograms;
        private Button _btnInstall;
        private Button _btnStop;
        private ProgressBar _progressBar;
        private Label _lblProgress;
        private RichTextBox _txtLog;

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
            Text = "Instalador Automático de Programas - EXABIT Soluções em TI";

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

            // ─── Lado esquerdo: Config + Log ───
            var leftSide = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = CorFundo,
            };
            leftSide.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(290 * scale)));
            leftSide.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            leftSide.Controls.Add(BuildConfigPanel(scale), 0, 0);
            leftSide.Controls.Add(BuildLogPanel(), 0, 1);

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
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, (int)(260 * scale)));
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
                Text = "Instalador Automático de Programas",
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

            // ─── Lado direito: logo EXABIT proporcional ───
            var rightPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45),
                Padding = new Padding((int)(10 * scale), (int)(4 * scale), (int)(15 * scale), (int)(6 * scale)),
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
        /// Cria o painel de configurações do sistema dividido em duas colunas:
        /// Lado esquerdo: campos de texto (nome do computador, senhas, usuário padrão)
        /// Lado direito: opções selecionáveis (restrição de senha, papel de parede, atalhos)
        /// </summary>
        private GroupBox BuildConfigPanel(float scale)
        {
            var group = new GroupBox
            {
                Text = "Configurações do Sistema",
                ForeColor = CorDestaque,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Dock = DockStyle.Fill,
                Margin = new Padding(0, (int)(3 * scale), 0, (int)(3 * scale)),
                BackColor = CorPainel,
                Padding = new Padding((int)(12 * scale), (int)(24 * scale), (int)(12 * scale), (int)(10 * scale)),
            };

            var mainTbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = CorPainel,
            };
            mainTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46f));
            mainTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54f));
            mainTbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // ─── Lado Esquerdo: Campos de Texto (Nome do PC e Usuários) ───
            var tblInputs = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                BackColor = CorPainel,
                Margin = new Padding(0, 0, (int)(15 * scale), 0),
            };
            tblInputs.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tblInputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            for (int i = 0; i < 4; i++)
                tblInputs.RowStyles.Add(new RowStyle(SizeType.Percent, 25f));

            // Linha 0: Nome do computador (vazio = não renomear)
            tblInputs.Controls.Add(CreateLabel("Nome do computador:"), 0, 0);
            _txtComputerName = CreateTextBox("Em branco = não renomear");
            _txtComputerName.Text = "";
            tblInputs.Controls.Add(_txtComputerName, 1, 0);

            // Linha 1: Senha do usuário Admin
            tblInputs.Controls.Add(CreateLabel("Senha do usuário Admin:"), 0, 1);
            _txtAdminPass = CreateTextBox("Senha do admin");
            _txtAdminPass.Text = "";
            tblInputs.Controls.Add(_txtAdminPass, 1, 1);

            // Linha 2: Criar usuário padrão (Nome do usuário)
            tblInputs.Controls.Add(CreateLabel("Criar usuário padrão:"), 0, 2);
            _txtStandardUserName = CreateTextBox("Nome do usuário");
            _txtStandardUserName.Text = "";
            tblInputs.Controls.Add(_txtStandardUserName, 1, 2);

            // Linha 3: Senha do usuário
            tblInputs.Controls.Add(CreateLabel("Senha do usuário:"), 0, 3);
            _txtStandardUserPass = CreateTextBox("Senha (opcional)");
            _txtStandardUserPass.Text = "";
            tblInputs.Controls.Add(_txtStandardUserPass, 1, 3);

            mainTbl.Controls.Add(tblInputs, 0, 0);

            // ─── Lado Direito: Opções Selecionáveis (Checkboxes) ───
            var tblChecks = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 7,
                BackColor = CorPainel,
                Margin = new Padding((int)(10 * scale), 0, 0, 0),
            };
            tblChecks.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            for (int i = 0; i < 7; i++)
                tblChecks.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 7f));

            // Linha 0: Ativar serviços de localização do Windows (ativo por padrão)
            _chkEnableLocation = CreateCheckBox("Ativar serviços de localização do Windows", true);
            tblChecks.Controls.Add(_chkEnableLocation, 0, 0);

            // Linha 1: Impedir que o usuário altere a senha
            _chkStandardUserNoPwdChange = CreateCheckBox("Impedir que o usuário altere a senha", false);
            tblChecks.Controls.Add(_chkStandardUserNoPwdChange, 0, 1);

            // Linha 2: Limpar atalhos da Área de Trabalho (ativo por padrão)
            _chkCleanDesktop = CreateCheckBox("Limpar atalhos da Área de Trabalho", true);
            tblChecks.Controls.Add(_chkCleanDesktop, 0, 2);

            // Linha 3: Wallpaper
            _chkWallpaper = CreateCheckBox("Aplicar papel de parede personalizado", false);
            tblChecks.Controls.Add(_chkWallpaper, 0, 3);

            // Linha 4: Atalho Suporte Norte
            _chkShortcutSuporteNorte = CreateCheckBox("Suporte Norte (suporte.exabit.com.br)", false);
            tblChecks.Controls.Add(_chkShortcutSuporteNorte, 0, 4);

            // Linha 5: Atalho Webmail Nortel
            _chkShortcutWebmailNortel = CreateCheckBox("Webmail Nortel (webmail.nortel.srv.br)", false);
            tblChecks.Controls.Add(_chkShortcutWebmailNortel, 0, 5);

            // Linha 6: Atalho Webmail Nortel Terceirização
            _chkShortcutWebmailTerceirizacao = CreateCheckBox("Webmail Nortel Terceirização (webmail.nortelterceirizacao.com.br)", false);
            tblChecks.Controls.Add(_chkShortcutWebmailTerceirizacao, 0, 6);

            mainTbl.Controls.Add(tblChecks, 1, 0);

            group.Controls.Add(mainTbl);
            return group;
        }

        /// <summary>
        /// Cria o painel de seleção de programas com checkboxes e scroll.
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
            tblContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(38 * scale)));

            // ─── Linha de botões Marcar/Desmarcar (centralizados na parte inferior) ───
            var btnPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = CorPainel,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0, (int)(2 * scale), 0, (int)(4 * scale)),
            };
            btnPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            btnPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            btnPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var btnSelectAll = new Button
            {
                Text = "Marcar",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = CorTexto,
                BackColor = Color.FromArgb(60, 60, 60),
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Cursor = Cursors.Hand,
                Size = new Size((int)(105 * scale), (int)(28 * scale)),
                Anchor = AnchorStyles.Right,
                Margin = new Padding(0, 0, (int)(4 * scale), 0),
            };
            btnSelectAll.FlatAppearance.MouseOverBackColor = Color.FromArgb(80, 80, 80);
            btnSelectAll.Click += (s, e) => SetAllCheckBoxes(true);

            var btnClearAll = new Button
            {
                Text = "Desmarcar",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = CorTexto,
                BackColor = Color.FromArgb(60, 60, 60),
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Cursor = Cursors.Hand,
                Size = new Size((int)(105 * scale), (int)(28 * scale)),
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

            // ─── Lista de checkboxes com scroll vertical (um por linha) ───
            _pnlPrograms = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                AutoScrollMargin = new Size(0, (int)(8 * scale)),
                BackColor = CorPainel,
                BorderStyle = BorderStyle.None,
                Margin = new Padding(0),
                Padding = new Padding(0, (int)(4 * scale), 0, (int)(4 * scale)),
            };

            int top = (int)(6 * scale);
            int leftMargin = (int)(10 * scale);
            foreach (ProgramInfo prog in GetDefaultProgramList())
            {
                if (prog.Id == "artcraft")
                {
                    top += (int)(6 * scale);

                    // Divisória horizontal abaixo do WinRAR separando os aplicativos da ArtCraft
                    var divider = new Panel
                    {
                        Location = new Point(leftMargin, top),
                        Size = new Size((int)(275 * scale), 1),
                        BackColor = Color.FromArgb(70, 70, 70),
                    };
                    _pnlPrograms.Controls.Add(divider);

                    top += (int)(10 * scale);
                }

                var chk = new CheckBox
                {
                    Text = prog.DisplayName,
                    Checked = prog.DefaultChecked,
                    Tag = prog,
                    ForeColor = CorTexto,
                    BackColor = Color.Transparent,
                    UseVisualStyleBackColor = false,
                    AutoSize = true,
                    Location = new Point(leftMargin, top),
                };
                chk.Size = chk.GetPreferredSize(new Size(int.MaxValue, int.MaxValue));
                _pnlPrograms.Controls.Add(chk);
                top += chk.Height + (int)(3 * scale);
            }

            tblContainer.Controls.Add(_pnlPrograms, 0, 0);
            tblContainer.Controls.Add(btnPanel, 0, 1);
            group.Controls.Add(tblContainer);

            return group;
        }

        /// <summary>
        /// Cria a barra de progresso e o rótulo de contagem (n / total).
        /// </summary>
        private Panel BuildProgressBar(float scale)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = CorPainel,
                Padding = new Padding(0, (int)(6 * scale), 0, (int)(6 * scale)),
            };

            _progressBar = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Style = ProgressBarStyle.Continuous,
                ForeColor = CorDestaque,
                BackColor = Color.FromArgb(60, 60, 60),
            };

            _lblProgress = new Label
            {
                Text = "0 / 0",
                ForeColor = CorTexto,
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Right,
                Width = (int)(90 * scale),
                Padding = new Padding((int)(5 * scale), 0, (int)(5 * scale), 0),
            };

            panel.Controls.Add(_progressBar);
            panel.Controls.Add(_lblProgress);

            return panel;
        }

        /// <summary>
        /// Cria o painel de log onde as mensagens da instalação aparecem.
        /// </summary>
        private Panel BuildLogPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = CorPainel,
                Margin = new Padding(0, 5, 0, 0),
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
            };

            panel.Controls.Add(_txtLog);
            return panel;
        }

        /// <summary>
        /// Cria o painel lateral direito combinando:
        /// lista de programas (scroll), botão instalar e barra de progresso.
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
                RowCount = 4,
                BackColor = CorFundo,
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Programas
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(50 * scale))); // Botão Instalar
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(44 * scale))); // Botão Parar
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)(36 * scale))); // Progresso

            layout.Controls.Add(BuildProgramsPanel(scale), 0, 0);

            // ─── Botão Instalar ───
            var btnInstallPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = CorFundo,
                Padding = new Padding(0, (int)(4 * scale), 0, (int)(2 * scale)),
            };

            _btnInstall = new Button
            {
                Text = "INICIAR INSTALAÇÃO",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = CorTexto,
                BackColor = Color.FromArgb(0, 150, 50),
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Cursor = Cursors.Hand,
                Dock = DockStyle.Fill,
            };
            _btnInstall.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 180, 70);
            _btnInstall.Click += BtnInstall_Click;
            btnInstallPanel.Controls.Add(_btnInstall);

            layout.Controls.Add(btnInstallPanel, 0, 1);

            // ─── Botão Parar ───
            var btnStopPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = CorFundo,
                Padding = new Padding(0, (int)(2 * scale), 0, (int)(2 * scale)),
            };

            _btnStop = new Button
            {
                Text = "PARAR",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Dock = DockStyle.Fill,
            };
            SetStopButtonState(false);
            _btnStop.Click += BtnStop_Click;
            btnStopPanel.Controls.Add(_btnStop);

            layout.Controls.Add(btnStopPanel, 0, 2);
            layout.Controls.Add(BuildProgressBar(scale), 0, 3);

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
            var internetRequiredPrograms = new List<string>();
            foreach (Control c in _pnlPrograms.Controls)
            {
                var chk = c as CheckBox;
                if (chk != null && chk.Checked)
                {
                    var prog = chk.Tag as ProgramInfo;
                    if (prog != null)
                    {
                        selectedPrograms.Add(prog);
                        if (prog.RequiresInternet)
                        {
                            string localPath = prog.GetFullPath(_service.BasePath);
                            if (prog.Id == "office365" || !File.Exists(localPath))
                            {
                                internetRequiredPrograms.Add(prog.DisplayName);
                            }
                        }
                    }
                }
            }

            // Prepara a interface para a instalação
            _installing = true;
            _btnInstall.Enabled = false;
            _btnInstall.BackColor = Color.FromArgb(0, 75, 25);
            _txtLog.Clear();

            try
            {
                if (internetRequiredPrograms.Count > 0)
                {
                    _btnInstall.Text = "VERIFICANDO INTERNET...";
                    while (!await _service.HasInternetConnectionAsync())
                    {
                        string progsMsg = string.Join("\n• ", internetRequiredPrograms.ToArray());
                        var retryResult = MessageBox.Show(
                            "Sem conexão com a internet.\n\n" +
                            "Os seguintes programas selecionados requerem acesso ativo à internet para download ou instalação:\n• " + progsMsg + "\n\n" +
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

                // ─── Atalhos de suporte ───
                options.CreateDesktopShortcutSuporteNorte = _chkShortcutSuporteNorte.Checked;
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

            string projectPath = @"D:\OneDrive\Programas\ISO\Personalização de ISO\Auto Install\InstallerGUI\Sound\R2D2.mp3";
            if (File.Exists(projectPath))
            {
                return projectPath;
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
                case ConsoleColor.Red: return Color.Red;
                case ConsoleColor.Yellow: return Color.Yellow;
                case ConsoleColor.Cyan: return Color.Cyan;
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
        /// Retorna a lista padrão de programas disponíveis para instalação.
        /// Cada item contém o nome, caminho do instalador e argumentos.
        /// 
        /// <summary>
        /// Retorna a lista de programas padrão disponíveis para instalação.
        /// Estrutura organizada:
        /// 1. Programas padrão corporativos ordenados alfabeticamente (finalizando no WinRAR).
        /// 2. Divisória visual no menu de programas.
        /// 3. ArtCraft Studio como primeiro item após a divisória.
        /// 4. Demais aplicativos da suíte Craft (Storytold) ordenados alfabeticamente com seus respectivos resumos entre parênteses.
        /// </summary>
        private static List<ProgramInfo> GetDefaultProgramList()
        {
            var standardList = new List<ProgramInfo>
            {
                new ProgramInfo("7zip",        "7-Zip",                          "7z.msi",                               "/qn /norestart"),
                new ProgramInfo("adobereader", "Adobe Acrobat Reader",           "AcroRdrDC.exe",                        "/sAll /msi EULA_ACCEPT=YES /qn /norestart"),
                new ProgramInfo("anydesk",     "AnyDesk",                        "AnyDesk.exe",                          "--install \"C:\\Program Files (x86)\\AnyDesk\" --silent --start-with-win --create-shortcuts --create-desktop-icon") { ProcessWorkingDir = "Programas", UseShellExecute = true, DefaultChecked = false },
                new ProgramInfo("dwgtrueview", "Autodesk DWG TrueView",          "DWGTrueView\\Setup.exe",               "-q --noeula") { ProcessWorkingDir = "DWGTrueView", UseShellExecute = true },
                new ProgramInfo("brave",       "Brave Browser",                  "BraveBrowserSetup.exe",                "/silent /install"),
                new ProgramInfo("citrix",      "Citrix Workspace",               "CitrixWorkspaceFullInstaller_x64.exe", "/silent /noreboot"),
                new ProgramInfo("chrome",      "Google Chrome",                  "ChromeStandaloneSetup64.exe",          "/silent /install"),
                new ProgramInfo("googleearth", "Google Earth Pro",               "googleearthprowin.exe",                "OMAHA=1"),
                new ProgramInfo("java",        "Java JRE 8",                     "jre-windows-x64.exe",                  "/s"),
                new ProgramInfo("office",      "Microsoft Office 2016",          "Office\\setup.exe",                    "/configure configuration.xml") { ProcessWorkingDir = "Office", FireAndForget = true, TimeoutMs = 600000, DefaultChecked = false },
                new ProgramInfo("office365",   "Microsoft Office 365 (Online)",  "OfficeSetup.exe",                      "/configure configuration_o365.xml") { UseShellExecute = true, TimeoutMs = 1800000, RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("powerbi",     "Microsoft Power BI Desktop",     "PBIDesktopSetup_x64.exe",              "-quiet -norestart ACCEPT_EULA=1") { ProcessWorkingDir = "Programas", UseShellExecute = true, TimeoutMs = 600000, DefaultChecked = true },
                new ProgramInfo("firefox",     "Mozilla Firefox",                "Firefox_Setup.msi",                    "/qn /norestart"),
                new ProgramInfo("radmin",      "Radmin VPN",                     "Radmin_VPN.exe",                       "/VERYSILENT /NORESTART") { DefaultChecked = false },
                new ProgramInfo("rustdesk",    "RustDesk",                       "rustdesk_x86_64.exe",                  "--silent-install") { FireAndForget = true },
                new ProgramInfo("winrar",      "WinRAR",                         "winrar.exe",                           "/S"),
            };
            standardList.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCultureIgnoreCase));

            var craftList = new List<ProgramInfo>
            {
                new ProgramInfo("cadcraft",    "CADCraft (Desenho Técnico CAD)",    "cadcraft-windows-x64.msi",    "/qn /norestart") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("deckcraft",   "DeckCraft (Apresentações e Slides)","deckcraft-windows-x64.msi",   "/qn /norestart") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("filmcraft",   "FilmCraft (Editor de Vídeo)",       "filmcraft-windows-x64.msi",   "/qn /norestart") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("gridcraft",   "GridCraft (Planilhas Eletrônicas)", "gridcraft-windows-x64.msi",   "/qn /norestart") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("pdfcraft",    "PdfCraft (Leitor e Editor de PDF)", "pdfcraft-windows-x64.msi",    "/qn /norestart") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("photocraft",  "PhotoCraft (Editor de Fotos)",      "photocraft-windows-x64.msi",  "/qn /norestart") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("soundcraft",  "SoundCraft (Editor de Áudio)",      "soundcraft-windows-x64.msi",  "/qn /norestart") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("vectorcraft", "VectorCraft (Ilustração Vetorial)", "vectorcraft-windows-x64.msi", "/qn /norestart") { RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("wordcraft",   "WordCraft (Editor de Texto)",       "wordcraft-windows-x64.msi",   "/qn /norestart") { RequiresInternet = true, DefaultChecked = false },
            };
            craftList.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCultureIgnoreCase));

            var list = new List<ProgramInfo>();
            // 1. Programas padrão corporativos (WinRAR fica por último nesta seção)
            list.AddRange(standardList);
            // 2. ArtCraft Studio como primeiro item após a divisória
            list.Add(new ProgramInfo("artcraft", "ArtCraft Studio (Suíte Criativa)", "ArtCraft_setup.exe", "/S") { DefaultChecked = false });
            // 3. Demais aplicativos da suíte Craft da Storytold em ordem alfabética
            list.AddRange(craftList);

            return list;
        }
    }
}
