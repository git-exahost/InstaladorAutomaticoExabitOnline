using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
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
        private TextBox _txtStandardUserSurname;
        private TextBox _txtStandardUserPass;
        private CheckBox _chkStandardUserNoPwdChange;
        private CheckBox _chkWallpaper;
        private CheckBox _chkShortcutSuporteNorte;
        private CheckBox _chkShortcutWebmailNortel;
        private CheckBox _chkShortcutWebmailTerceirizacao;
        private CheckBox _chkShortcutStartMenu;
        private Panel _pnlPrograms;
        private Button _btnInstall;
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

            // Monta todos os controles na tela
            BuildUI();
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
            leftSide.RowStyles.Add(new RowStyle(SizeType.Absolute, 365));
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
        /// Cria o painel de configurações do sistema:
        /// nome do computador, criação de usuários e wallpaper.
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

            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 10,
                Padding = new Padding(8, 5, 8, 5),
                BackColor = CorPainel,
            };

            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));

            for (int i = 0; i < 10; i++)
                tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

            // Linha 0: Nome do computador (vazio = não renomear)
            tbl.Controls.Add(CreateLabel("Nome do computador:"), 0, 0);
            _txtComputerName = CreateTextBox("Em branco = não renomear");
            _txtComputerName.Text = "";
            tbl.Controls.Add(_txtComputerName, 1, 0);
            tbl.SetColumnSpan(_txtComputerName, 3);

            // Linha 1: Criar admin
            tbl.Controls.Add(CreateLabel("Criar usuário admin:"), 0, 1);

            _txtAdminPass = CreateTextBox("Senha do admin");
            _txtAdminPass.Text = "NT@admin#";
            tbl.Controls.Add(_txtAdminPass, 1, 1);
            tbl.SetColumnSpan(_txtAdminPass, 3);

            // Linha 2: Criar usuário padrão (Nome + Sobrenome)
            tbl.Controls.Add(CreateLabel("Criar usuário padrão:"), 0, 2);

            _txtStandardUserName = CreateTextBox("Nome");
            _txtStandardUserName.Text = "Nome";
            tbl.Controls.Add(_txtStandardUserName, 1, 2);

            _txtStandardUserSurname = CreateTextBox("Sobrenome");
            _txtStandardUserSurname.Text = "Sobrenome";
            tbl.Controls.Add(_txtStandardUserSurname, 2, 2);
            tbl.SetColumnSpan(_txtStandardUserSurname, 2);

            // Linha 3: Senha
            tbl.Controls.Add(CreateLabel("Senha:"), 0, 3);

            _txtStandardUserPass = CreateTextBox("Senha (opcional)");
            _txtStandardUserPass.Text = "NT@colaborador";
            tbl.Controls.Add(_txtStandardUserPass, 1, 3);
            tbl.SetColumnSpan(_txtStandardUserPass, 3);

            // Linha 4: Usuário padrão não pode alterar senha
            _chkStandardUserNoPwdChange = CreateCheckBox("Usuário padrão não poderá alterar a senha", true);
            tbl.Controls.Add(_chkStandardUserNoPwdChange, 0, 4);
            tbl.SetColumnSpan(_chkStandardUserNoPwdChange, 4);

            // Linha 5: Wallpaper
            _chkWallpaper = CreateCheckBox("Aplicar papel de parede personalizado", true);
            tbl.Controls.Add(_chkWallpaper, 0, 5);
            tbl.SetColumnSpan(_chkWallpaper, 4);

            // ─── Atalhos de suporte ───

            // Linha 6: Atalho Suporte Norte
            _chkShortcutSuporteNorte = CreateCheckBox("Suporte Norte (suporte.exabit.com.br)", false);
            tbl.Controls.Add(_chkShortcutSuporteNorte, 0, 6);
            tbl.SetColumnSpan(_chkShortcutSuporteNorte, 4);

            // Linha 7: Atalho Webmail Nortel
            _chkShortcutWebmailNortel = CreateCheckBox("Webmail Nortel (webmail.nortel.srv.br)", false);
            tbl.Controls.Add(_chkShortcutWebmailNortel, 0, 7);
            tbl.SetColumnSpan(_chkShortcutWebmailNortel, 4);

            // Linha 8: Atalho Webmail Nortel Terceirização
            _chkShortcutWebmailTerceirizacao = CreateCheckBox("Webmail Nortel Terceirização (webmail.nortelterceirizacao.com.br)", false);
            tbl.Controls.Add(_chkShortcutWebmailTerceirizacao, 0, 8);
            tbl.SetColumnSpan(_chkShortcutWebmailTerceirizacao, 4);

            // Linha 9: Opção de Menu Iniciar
            _chkShortcutStartMenu = CreateCheckBox("Adicionar atalhos também no Menu Iniciar", false);
            tbl.Controls.Add(_chkShortcutStartMenu, 0, 9);
            tbl.SetColumnSpan(_chkShortcutStartMenu, 4);

            group.Controls.Add(tbl);
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
                    Checked = prog.Id != "office" && prog.Id != "radmin",
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
                RowCount = 3,
                BackColor = CorFundo,
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Programas
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55)); // Botão
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35)); // Progresso

            layout.Controls.Add(BuildProgramsPanel(), 0, 0);

            // ─── Botão Instalar ───
            var btnPanel = new Panel
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
            btnPanel.Controls.Add(_btnInstall);

            layout.Controls.Add(btnPanel, 0, 1);
            layout.Controls.Add(BuildProgressBar(), 0, 2);

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

            // Prepara a interface para a instalação
            _installing = true;
            _btnInstall.Enabled = false;
            _btnInstall.Text = "INSTALANDO...";
            _txtLog.Clear();

            try
            {
                // Monta as opções com base no que o usuário selecionou
                var options = new InstallationOptions();

                options.RenameComputer = !string.IsNullOrWhiteSpace(_txtComputerName.Text);
                options.ComputerName = _txtComputerName.Text.Trim();

                options.CreateAdmin = true;
                options.AdminPassword = _txtAdminPass.Text;

                string stdName = _txtStandardUserName.Text.Trim();
                string stdSurname = _txtStandardUserSurname.Text.Trim();
                bool nomePreenchido = !string.IsNullOrWhiteSpace(stdName) && stdName != "Nome";
                bool sobrenomePreenchido = !string.IsNullOrWhiteSpace(stdSurname) && stdSurname != "Sobrenome";
                bool criarUsuarioPadrao = nomePreenchido || sobrenomePreenchido;

                options.CreateStandardUser = criarUsuarioPadrao;
                options.StandardUserName = criarUsuarioPadrao ? (stdName + " " + stdSurname).Trim() : null;
                options.StandardUserPassword = criarUsuarioPadrao ? _txtStandardUserPass.Text : null;
                options.StandardUserCannotChangePassword = criarUsuarioPadrao && _chkStandardUserNoPwdChange.Checked;

                options.SetWallpaper = _chkWallpaper.Checked;

                // ─── Atalhos de suporte ───
                options.CreateDesktopShortcutSuporteNorte = _chkShortcutSuporteNorte.Checked;
                options.CreateDesktopShortcutWebmailNortel = _chkShortcutWebmailNortel.Checked;
                options.CreateDesktopShortcutWebmailTerceirizacao = _chkShortcutWebmailTerceirizacao.Checked;
                options.AddShortcutsToStartMenu = _chkShortcutStartMenu.Checked;

                // Coleta os programas marcados
                foreach (Control c in _pnlPrograms.Controls)
                {
                    var chk = c as CheckBox;
                    if (chk != null && chk.Checked)
                    {
                        var prog = chk.Tag as ProgramInfo;
                        if (prog != null)
                            options.SelectedPrograms.Add(prog);
                    }
                }

                // Executa toda a instalação
                await _service.RunFullInstallationAsync(options);

                // Log final
                AppendLog("", null);
                AppendLog("Instalação concluída com sucesso!", ConsoleColor.Green);
                MessageBox.Show(
                    "Instalação concluída com sucesso!",
                    "Concluído",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
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
                _btnInstall.Text = "INICIAR INSTALAÇÃO";
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
                new ProgramInfo("7zip",        "7-Zip",                 "7z.msi",                             "/qn /norestart"),
                new ProgramInfo("adobereader", "Adobe Acrobat Reader",  "AcroRdrDC.exe",                      "/sAll /msi EULA_ACCEPT=YES /qn /norestart"),
                new ProgramInfo("brave",       "Brave Browser",         "BraveBrowserSetup.exe",              "/silent /install"),
                new ProgramInfo("chrome",      "Google Chrome",         "ChromeStandaloneSetup64.exe",         "/silent /install"),
                new ProgramInfo("firefox",     "Mozilla Firefox",       "Firefox_Setup.msi",                  "/qn /norestart"),
                new ProgramInfo("googleearth", "Google Earth Pro",      "googleearthprowin.exe",              "OMAHA=1"),
                new ProgramInfo("java",        "Java JRE 8",            "jre-windows-x64.exe",                "/s"),
                new ProgramInfo("radmin",      "Radmin VPN",            "Radmin_VPN.exe",                     "/VERYSILENT /NORESTART"),
                new ProgramInfo("rustdesk",    "RustDesk",              "rustdesk_x86_64.exe",                "--silent-install") { FireAndForget = true },
                new ProgramInfo("winrar",      "WinRAR",                "winrar.exe",                         "/S"),
                new ProgramInfo("dwgtrueview", "Autodesk DWG TrueView","DWGTrueView\\Setup.exe",              "-q --noeula") { ProcessWorkingDir = "DWGTrueView", UseShellExecute = true },
                new ProgramInfo("office",      "Microsoft Office",      "Office\\setup.exe",                  "/configure configuration.xml") { ProcessWorkingDir = "Office", FireAndForget = true, TimeoutMs = 600000 },
            };
        }
    }
}
