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
        private CheckBox _chkStandardUserNoPwdChange;
        private CheckBox _chkWallpaper;
        private CheckBox _chkShortcutSuporteNorte;
        private CheckBox _chkShortcutWebmailNortel;
        private CheckBox _chkShortcutWebmailTerceirizacao;
        private CheckBox _chkShortcutStartMenu;
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
            // Configurações da janela
            Text = "Instalador Automático de Programas - EXABIT Soluções em TI";
            Size = new Size(1200, 750);
            MinimumSize = new Size(1200, 750);
            MinimizeBox = false;
            MaximizeBox = false;
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
            // ─────────── Layout raiz: 1 coluna, 2 linhas ───────────
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(10),
                BackColor = CorFundo,
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            root.Controls.Add(BuildHeader(), 0, 0);

            // ─────────── Conteúdo: 2 colunas (esquerda / direita) ───────────
            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = CorFundo,
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));

            // ─── Lado esquerdo: Config + Log ───
            var leftSide = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = CorFundo,
            };
            leftSide.RowStyles.Add(new RowStyle(SizeType.Absolute, 215));
            leftSide.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            leftSide.Controls.Add(BuildConfigPanel(), 0, 0);
            leftSide.Controls.Add(BuildLogPanel(), 0, 1);

            content.Controls.Add(leftSide, 0, 0);

            // ─── Lado direito: Programas + Instalar + Progresso ───
            content.Controls.Add(BuildRightPanel(), 1, 0);

            root.Controls.Add(content, 0, 1);
            Controls.Add(root);
        }

        /// <summary>
        /// Cria o cabeçalho com o título e a logo EXABIT à direita.
        /// </summary>
        private Panel BuildHeader()
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
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));

            // ─── Lado esquerdo: título ───
            var leftPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45),
            };

            leftPanel.Controls.Add(new Label
            {
                Text = "Instalador Automático de Programas",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = CorDestaque,
                Location = new Point(10, 7),
                AutoSize = true,
            });

            leftPanel.Controls.Add(new Label
            {
                Text = string.Format("Diretório: {0}", Application.StartupPath),
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.Gray,
                Location = new Point(10, 35),
                AutoSize = true,
            });

            tbl.Controls.Add(leftPanel, 0, 0);

            // ─── Lado direito: logo ───
            var rightPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45),
                Padding = new Padding(80, 3, 80, 55),
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
        private GroupBox BuildConfigPanel()
        {
            var group = new GroupBox
            {
                Text = "Configurações do Sistema",
                ForeColor = CorDestaque,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 3),
                BackColor = CorPainel,
            };

            var mainTbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(8, 5, 8, 5),
                BackColor = CorPainel,
            };
            mainTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
            mainTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
            mainTbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // ─── Lado Esquerdo: Campos de Texto (Nome do PC e Usuários) ───
            var tblInputs = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                BackColor = CorPainel,
                Margin = new Padding(0, 0, 10, 0),
            };
            tblInputs.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 185));
            tblInputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            for (int i = 0; i < 4; i++)
                tblInputs.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            tblInputs.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

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
                Margin = new Padding(5, 0, 0, 0),
            };
            tblChecks.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            for (int i = 0; i < 6; i++)
                tblChecks.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
            tblChecks.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // Linha 0: Impedir que o usuário altere a senha
            _chkStandardUserNoPwdChange = CreateCheckBox("Impedir que o usuário altere a senha", false);
            tblChecks.Controls.Add(_chkStandardUserNoPwdChange, 0, 0);

            // Linha 1: Wallpaper
            _chkWallpaper = CreateCheckBox("Aplicar papel de parede personalizado", false);
            tblChecks.Controls.Add(_chkWallpaper, 0, 1);

            // Linha 2: Atalho Suporte Norte
            _chkShortcutSuporteNorte = CreateCheckBox("Suporte Norte (suporte.exabit.com.br)", false);
            tblChecks.Controls.Add(_chkShortcutSuporteNorte, 0, 2);

            // Linha 3: Atalho Webmail Nortel
            _chkShortcutWebmailNortel = CreateCheckBox("Webmail Nortel (webmail.nortel.srv.br)", false);
            tblChecks.Controls.Add(_chkShortcutWebmailNortel, 0, 3);

            // Linha 4: Atalho Webmail Nortel Terceirização
            _chkShortcutWebmailTerceirizacao = CreateCheckBox("Webmail Nortel Terceirização (webmail.nortelterceirizacao.com.br)", false);
            tblChecks.Controls.Add(_chkShortcutWebmailTerceirizacao, 0, 4);

            // Linha 5: Opção de Menu Iniciar
            _chkShortcutStartMenu = CreateCheckBox("Adicionar atalhos também no Menu Iniciar", false);
            tblChecks.Controls.Add(_chkShortcutStartMenu, 0, 5);

            mainTbl.Controls.Add(tblChecks, 1, 0);

            group.Controls.Add(mainTbl);
            return group;
        }

        /// <summary>
        /// Cria o painel de seleção de programas com checkboxes e scroll.
        /// </summary>
        private GroupBox BuildProgramsPanel()
        {
            var group = new GroupBox
            {
                Text = "Programas para Instalar",
                ForeColor = CorDestaque,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 0),
                BackColor = CorPainel,
            };

            // ─── Linha de botões Marcar/Desmarcar ───
            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 32,
                BackColor = CorPainel,
                Padding = new Padding(8, 2, 8, 2),
            };

            var btnSelectAll = new Button
            {
                Text = "Marcar Todos",
                ForeColor = CorTexto,
                BackColor = Color.FromArgb(60, 60, 60),
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Cursor = Cursors.Hand,
                Size = new Size(105, 26),
                Margin = new Padding(2),
            };
            btnSelectAll.FlatAppearance.MouseOverBackColor = Color.FromArgb(80, 80, 80);
            btnSelectAll.Click += (s, e) => SetAllCheckBoxes(true);

            var btnClearAll = new Button
            {
                Text = "Desmarcar Todos",
                ForeColor = CorTexto,
                BackColor = Color.FromArgb(60, 60, 60),
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Cursor = Cursors.Hand,
                Size = new Size(115, 26),
                Margin = new Padding(2),
            };
            btnClearAll.FlatAppearance.MouseOverBackColor = Color.FromArgb(80, 80, 80);
            btnClearAll.Click += (s, e) => SetAllCheckBoxes(false);

            btnPanel.Controls.Add(btnSelectAll);
            btnPanel.Controls.Add(btnClearAll);
            group.Controls.Add(btnPanel);

            // ─── Lista de checkboxes com scroll vertical (um por linha) ───
            _pnlPrograms = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = CorPainel,
                BorderStyle = BorderStyle.None,
                Padding = new Padding(0, 6, 0, 0),
            };

            int top = 40;
            foreach (ProgramInfo prog in GetDefaultProgramList())
            {
                var chk = new CheckBox
                {
                    Text = prog.DisplayName,
                    Checked = prog.DefaultChecked,
                    Tag = prog,
                    ForeColor = CorTexto,
                    BackColor = Color.Transparent,
                    UseVisualStyleBackColor = false,
                    AutoSize = true,
                    Location = new Point(10, top),
                };
                _pnlPrograms.Controls.Add(chk);
                top = chk.Bottom + 2;
            }

            group.Controls.Add(_pnlPrograms);
            return group;
        }

        /// <summary>
        /// Cria a barra de progresso e o rótulo de contagem (n / total).
        /// </summary>
        private Panel BuildProgressBar()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = CorPainel,
                Padding = new Padding(0, 6, 0, 6),
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
                Width = 100,
                Padding = new Padding(5, 0, 5, 0),
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
        private Panel BuildRightPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = CorFundo,
                Padding = new Padding(10, 0, 0, 0),
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = CorFundo,
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Programas
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55)); // Botão Instalar
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // Botão Parar
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35)); // Progresso

            layout.Controls.Add(BuildProgramsPanel(), 0, 0);

            // ─── Botão Instalar ───
            var btnInstallPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = CorFundo,
                Padding = new Padding(0, 5, 0, 2),
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
                Padding = new Padding(0, 2, 0, 2),
            };

            _btnStop = new Button
            {
                Text = "PARAR",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = CorTexto,
                BackColor = Color.FromArgb(180, 50, 0),
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Cursor = Cursors.Hand,
                Dock = DockStyle.Fill,
                Enabled = false,
            };
            _btnStop.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 70, 0);
            _btnStop.Click += BtnStop_Click;
            btnStopPanel.Controls.Add(_btnStop);

            layout.Controls.Add(btnStopPanel, 0, 2);
            layout.Controls.Add(BuildProgressBar(), 0, 3);

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
            bool requiresInternet = false;
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
                            requiresInternet = true;
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
                if (requiresInternet)
                {
                    _btnInstall.Text = "VERIFICANDO INTERNET...";
                    while (!await _service.HasInternetConnectionAsync())
                    {
                        var retryResult = MessageBox.Show(
                            "Sem conexão com a internet.\n\n" +
                            "A instalação do Microsoft Office 365 (Online) requer acesso ativo à internet.\n\n" +
                            "Conecte-se à internet antes de instalar e clique em \"Repetir\", ou em \"Cancelar\" para voltar.",
                            "Conexão com a internet necessária",
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
                _btnStop.Enabled = true;

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

                options.SetWallpaper = _chkWallpaper.Checked;

                // ─── Atalhos de suporte ───
                options.CreateDesktopShortcutSuporteNorte = _chkShortcutSuporteNorte.Checked;
                options.CreateDesktopShortcutWebmailNortel = _chkShortcutWebmailNortel.Checked;
                options.CreateDesktopShortcutWebmailTerceirizacao = _chkShortcutWebmailTerceirizacao.Checked;
                options.AddShortcutsToStartMenu = _chkShortcutStartMenu.Checked;

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
                _btnStop.Enabled = false;
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
                _btnStop.Enabled = false;
                AppendLog("Solicitação de parada enviada...", ConsoleColor.Yellow);
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
        /// Cria um CheckBox no padrão do tema escuro.
        /// </summary>
        private static CheckBox CreateCheckBox(string text, bool checkedState)
        {
            return new CheckBox
            {
                Text = text,
                Checked = checkedState,
                ForeColor = CorTexto,
                BackColor = Color.Transparent,
                UseVisualStyleBackColor = false,
                AutoSize = true,
            };
        }

        // ══════════════════════════════════════════════
        // LISTA DE PROGRAMAS
        // ══════════════════════════════════════════════

        /// <summary>
        /// Retorna a lista padrão de programas disponíveis para instalação.
        /// Cada item contém o nome, caminho do instalador e argumentos.
        /// 
        /// Para adicionar um novo programa:
        /// 1. Coloque o instalador na pasta dist\Programas\
        /// 2. Adicione um novo item nesta lista
        /// 3. Recompile o projeto
        /// </summary>
        private static List<ProgramInfo> GetDefaultProgramList()
        {
            return new List<ProgramInfo>
            {
                new ProgramInfo("7zip",        "7-Zip",                           "7z.msi",                               "/qn /norestart"),
                new ProgramInfo("adobereader", "Adobe Acrobat Reader",            "AcroRdrDC.exe",                        "/sAll /msi EULA_ACCEPT=YES /qn /norestart"),
                new ProgramInfo("anydesk",     "AnyDesk",                         "AnyDesk.exe",                          "--install \"C:\\Program Files (x86)\\AnyDesk\" --silent --start-with-win --create-shortcuts --create-desktop-icon") { ProcessWorkingDir = "Programas", UseShellExecute = true, DefaultChecked = false },
                new ProgramInfo("brave",       "Brave Browser",                   "BraveBrowserSetup.exe",                "/silent /install"),
                new ProgramInfo("chrome",      "Google Chrome",                   "ChromeStandaloneSetup64.exe",           "/silent /install"),
                new ProgramInfo("firefox",     "Mozilla Firefox",                 "Firefox_Setup.msi",                    "/qn /norestart"),
                new ProgramInfo("googleearth", "Google Earth Pro",                "googleearthprowin.exe",                "OMAHA=1"),
                new ProgramInfo("java",        "Java JRE 8",                      "jre-windows-x64.exe",                  "/s"),
                new ProgramInfo("radmin",      "Radmin VPN",                      "Radmin_VPN.exe",                       "/VERYSILENT /NORESTART") { DefaultChecked = false },
                new ProgramInfo("citrix",      "Citrix Workspace",                "CitrixWorkspaceFullInstaller_x64.exe", "/silent /noreboot"),
                new ProgramInfo("rustdesk",    "RustDesk",                        "rustdesk_x86_64.exe",                  "--silent-install") { FireAndForget = true },
                new ProgramInfo("winrar",      "WinRAR",                          "winrar.exe",                           "/S"),
                new ProgramInfo("dwgtrueview", "Autodesk DWG TrueView",           "DWGTrueView\\Setup.exe",               "-q --noeula") { ProcessWorkingDir = "DWGTrueView", UseShellExecute = true },
                new ProgramInfo("office",      "Microsoft Office",                "Office\\setup.exe",                    "/configure configuration.xml") { ProcessWorkingDir = "Office", FireAndForget = true, TimeoutMs = 600000, DefaultChecked = false },
                new ProgramInfo("office365",   "Microsoft Office 365 (Online)",   "OfficeSetup.exe",                      "") { UseShellExecute = true, TimeoutMs = 1800000, RequiresInternet = true, DefaultChecked = false },
                new ProgramInfo("powerbi",     "Microsoft Power BI Desktop",      "PBIDesktopSetup_x64.exe",              "-quiet -norestart ACCEPT_EULA=1") { ProcessWorkingDir = "Programas", UseShellExecute = true, TimeoutMs = 600000, DefaultChecked = false },
                new ProgramInfo("artcraft",    "ArtCraft",                        "ArtCraft_setup.exe",                   "/S") { DefaultChecked = false },
            };
        }
    }
}
