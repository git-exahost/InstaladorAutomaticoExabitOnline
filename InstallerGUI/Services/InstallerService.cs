using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using InstallerGUI.Models;

namespace InstallerGUI.Services
{
    /// <summary>
    /// Serviço central que coordena todas as etapas da instalação automatizada.
    /// Cada etapa (renomear PC, criar usuários, wallpaper, instalar programas)
    /// é um método separado que pode ser chamado individualmente ou em sequência.
    /// </summary>
    public class InstallerService
    {
        // ──────────────────────────────────────────────
        // Eventos para comunicação com a interface
        // ──────────────────────────────────────────────

        /// <summary>
        /// Disparado quando uma mensagem de log deve ser exibida.
        /// A cor pode ser usada para diferenciar tipos de mensagem.
        /// </summary>
        public event Action<string, ConsoleColor?> OnLog;

        /// <summary>
        /// Disparado quando o progresso da instalação é atualizado.
        /// Parâmetros: (etapaAtual, totalEtapas).
        /// </summary>
        public event Action<int, int> OnProgress;

        // ──────────────────────────────────────────────
        // Campos
        // ──────────────────────────────────────────────

        /// <summary>
        /// Caminho raiz onde estão as pastas Programas/ e Scripts/.
        /// Normalmente é o mesmo diretório do executável.
        /// </summary>
        private readonly string _basePath;

        // ──────────────────────────────────────────────
        // Construtor
        // ──────────────────────────────────────────────

        /// <summary>
        /// Cria o serviço de instalação apontando para o diretório
        /// que contém as pastas "Programas" e "Scripts".
        /// </summary>
        /// <param name="basePath">
        /// Caminho raiz. Use <see cref="AppDomain.CurrentDomain.BaseDirectory"/>
        /// ou <see cref="Application.StartupPath"/>.
        /// </param>
        public InstallerService(string basePath)
        {
            _basePath = basePath;
        }

        // ──────────────────────────────────────────────
        // Métodos públicos - cada etapa da instalação
        // ──────────────────────────────────────────────

        /// <summary>
        /// Altera o nome do computador usando PowerShell.
        /// A alteração só terá efeito após a reinicialização.
        /// </summary>
        /// <param name="newName">Novo nome do computador</param>
        public async Task RenameComputerAsync(string newName)
        {
            Log(string.Format("Alterando nome do computador para \"{0}\"...", newName), ConsoleColor.Yellow);

            try
            {
                string safeName = newName.Replace("'", "''");
                int exitCode = await RunProcessAsync("powershell",
                    string.Format("-Command \"Rename-Computer -NewName '{0}' -Force\"", safeName),
                    30000);

                if (exitCode == 0)
                    Log("[ok] Nome alterado (válido após reinicialização).", ConsoleColor.Green);
                else
                    Log("X Aviso: Não foi possível renomear o computador.", ConsoleColor.Yellow);
            }
            catch (Exception ex)
            {
                Log(string.Format("X Erro ao renomear: {0}", ex.Message), ConsoleColor.Red);
            }

            await Task.Delay(500);
        }

        /// <summary>
        /// Cria o usuário administrador "admin" com a senha fornecida
        /// e o adiciona ao grupo de administradores locais.
        /// </summary>
        /// <param name="password">Senha do usuário admin</param>
        public async Task CreateAdminUserAsync(string password)
        {
            Log("Criando usuário 'admin'...", ConsoleColor.Yellow);

            try
            {
                int code = await RunProcessAsync("net",
                    string.Format("user admin \"{0}\" /add", password), 10000);

                if (code == 0)
                    Log("[ok] Usuário 'admin' criado.", ConsoleColor.Green);
                else
                    Log("X Usuário 'admin' já existe ou acesso negado.", ConsoleColor.Yellow);

                Log("Adicionando 'admin' aos administradores...", ConsoleColor.Yellow);
                code = await RunProcessAsync("net", "localgroup Administradores admin /add");
                if (code != 0)
                    await RunProcessAsync("net", "localgroup Administrators admin /add");

                code = await RunProcessAsync("wmic",
                    "useraccount where name=\"admin\" set PasswordExpires=False");

                if (code == 0)
                    Log("[ok] Senha do admin configurada para nunca expirar.", ConsoleColor.Green);
                else
                    Log("X Aviso: Não foi possível configurar a senha.", ConsoleColor.Yellow);
            }
            catch (Exception ex)
            {
                Log(string.Format("X Erro ao criar admin: {0}", ex.Message), ConsoleColor.Red);
            }

            await Task.Delay(500);
        }

        /// <summary>
        /// Cria um usuário padrão com nome e senha fornecidos.
        /// Se a senha estiver vazia, o usuário poderá definir a própria senha no login.
        /// </summary>
        /// <param name="username">Nome do usuário</param>
        /// <param name="password">Senha do usuário (opcional)</param>
        /// <param name="cannotChangePassword">Impede o usuário de alterar a senha</param>
        public async Task CreateStandardUserAsync(string username, string password, bool cannotChangePassword = false)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                Log("Nome de usuário inválido. Pulando...", ConsoleColor.Yellow);
                return;
            }

            Log(string.Format("Criando usuário \"{0}\"...", username), ConsoleColor.Yellow);

            try
            {
                int code;
                string pwdChg = cannotChangePassword ? "/passwordchg:no" : "/passwordchg:yes";
                if (string.IsNullOrWhiteSpace(password))
                    code = await RunProcessAsync("net",
                        string.Format("user \"{0}\" /add {1}", username, pwdChg));
                else
                    code = await RunProcessAsync("net",
                        string.Format("user \"{0}\" \"{1}\" /add {2}", username, password, pwdChg));

                if (code != 0)
                {
                    Log(string.Format("X Falha ao criar \"{0}\". Acesso negado ou usuário já existe.",
                        username), ConsoleColor.Yellow);
                    return;
                }

                await RunProcessAsync("wmic",
                    string.Format("useraccount where name=\"{0}\" set PasswordExpires=False",
                    username));

                Log(string.Format("[ok] Usuário \"{0}\" criado.", username), ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                Log(string.Format("X Erro ao criar \"{0}\": {1}", username, ex.Message),
                    ConsoleColor.Red);
            }

            await Task.Delay(500);
        }

        /// <summary>
        /// Aplica o papel de parede personalizado executando o script
        /// Scripts\wallpaper.cmd que deve estar presente na pasta de instalação.
        /// </summary>
        public async Task SetWallpaperAsync()
        {
            Log("Configurando papel de parede...", ConsoleColor.Yellow);

            string scriptPath = Path.Combine(_basePath, "Scripts", "wallpaper.cmd");

            if (File.Exists(scriptPath))
            {
                await RunProcessAsync(scriptPath, "");
                Log("[ok] Papel de parede configurado.", ConsoleColor.Green);
            }
            else
            {
                Log("Script de wallpaper não encontrado em: " + scriptPath, ConsoleColor.Yellow);
            }

            await Task.Delay(500);
        }

        /// <summary>
        /// Instala um programa específico com base nas informações do <see cref="ProgramInfo"/>.
        /// Suporta arquivos .exe e .msi com parâmetros de instalação silenciosa.
        /// </summary>
        /// <param name="program">Informações do programa a ser instalado</param>
        public async Task InstallProgramAsync(ProgramInfo program)
        {
            Log(string.Format("Instalando {0}...", program.DisplayName), ConsoleColor.Yellow);

            string fullPath = program.GetFullPath(_basePath);

            if (!File.Exists(fullPath))
            {
                Log(string.Format("X Arquivo não encontrado: {0}", fullPath), ConsoleColor.Red);
                return;
            }

            try
            {
                string extension = Path.GetExtension(program.FileName).ToLower();

                string procWorkDir = !string.IsNullOrEmpty(program.ProcessWorkingDir)
                    ? Path.GetDirectoryName(fullPath) : null;

                if (program.FireAndForget)
                {
                    // Inicia e não aguarda (programas que ficam em execução)
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = fullPath,
                        Arguments = program.Arguments,
                        WorkingDirectory = procWorkDir ?? string.Empty,
                        UseShellExecute = true,
                    });
                    await Task.Delay(30000);
                    Log(string.Format("[ok] {0} instalado com sucesso.", program.DisplayName),
                        ConsoleColor.Green);
                }
                else
                {
                    int timeout = program.TimeoutMs > 0 ? program.TimeoutMs : 600000;

                    int exitCode;
                    if (extension == ".msi")
                    {
                        exitCode = await RunProcessAsync("msiexec",
                            string.Format("/i \"{0}\" {1}", fullPath, program.Arguments),
                            timeout, procWorkDir);
                    }
                    else
                    {
                        exitCode = await RunProcessAsync(fullPath, program.Arguments,
                            timeout, procWorkDir, program.UseShellExecute);
                    }

                    if (exitCode == 0)
                        Log(string.Format("[ok] {0} instalado com sucesso.", program.DisplayName),
                            ConsoleColor.Green);
                    else
                        Log(string.Format("X {0} retornou código {1}.", program.DisplayName, exitCode),
                            ConsoleColor.Yellow);
                }
            }
            catch (Exception ex)
            {
                Log(string.Format("X Erro ao instalar {0}: {1}", program.DisplayName, ex.Message),
                    ConsoleColor.Red);
            }

            await Task.Delay(300);
        }

        /// <summary>
        /// Executa a instalação completa em sequência: renomear PC, criar usuários,
        /// wallpaper e instalar todos os programas selecionados.
        /// </summary>
        /// <param name="options">Opções configuradas pelo usuário na interface</param>
        public async Task RunFullInstallationAsync(InstallationOptions options)
        {
            // Calcula o total de etapas para a barra de progresso
            int totalSteps = 0;
            if (options.RenameComputer) totalSteps++;
            if (options.CreateAdmin) totalSteps++;
            if (options.CreateStandardUser) totalSteps++;
            if (options.SetWallpaper) totalSteps++;
            totalSteps += options.SelectedPrograms.Count;

            int currentStep = 0;

            Log("=== INICIANDO INSTALAÇÃO ===", ConsoleColor.Cyan);
            Log(string.Format("Data: {0:dd/MM/yyyy HH:mm:ss}", DateTime.Now), ConsoleColor.Gray);
            Log("", null);

            // 1. Renomear computador
            if (options.RenameComputer && !string.IsNullOrWhiteSpace(options.ComputerName))
            {
                currentStep++;
                ReportProgress(currentStep, totalSteps);
                await RenameComputerAsync(options.ComputerName);
            }

            // 2. Criar usuário admin
            if (options.CreateAdmin)
            {
                currentStep++;
                ReportProgress(currentStep, totalSteps);
                await CreateAdminUserAsync(options.AdminPassword ?? "EXA@admin#202_");
            }

            // 3. Criar usuário padrão
            if (options.CreateStandardUser)
            {
                currentStep++;
                ReportProgress(currentStep, totalSteps);
                await CreateStandardUserAsync(options.StandardUserName, options.StandardUserPassword, options.StandardUserCannotChangePassword);
            }

            // 4. Wallpaper
            if (options.SetWallpaper)
            {
                currentStep++;
                ReportProgress(currentStep, totalSteps);
                await SetWallpaperAsync();
            }

            // 5. Instalar cada programa selecionado
            foreach (var program in options.SelectedPrograms)
            {
                currentStep++;
                ReportProgress(currentStep, totalSteps);
                await InstallProgramAsync(program);
            }

            // Finaliza
            currentStep++;
            ReportProgress(currentStep, totalSteps);

            Log("", null);

            // Limpa atalhos do desktop público e cria os personalizados
            CleanPublicDesktop();
            await CreateDesktopShortcutsAsync(options.AdminPassword,
                options.CreateStandardUser ? options.StandardUserName : null);

            Log("=== INSTALAÇÃO CONCLUÍDA ===", ConsoleColor.Green);
        }

        /// <summary>
        /// Remove todos os atalhos (.lnk) do desktop público para deixar
        /// a área de trabalho limpa após a instalação.
        /// </summary>
        private void CleanPublicDesktop()
        {
            Log("Limpando atalhos do Desktop público...", ConsoleColor.Yellow);

            try
            {
                string publicDesktop = Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonDesktopDirectory);

                foreach (string file in Directory.GetFiles(publicDesktop, "*.lnk"))
                    File.Delete(file);

                Log("[ok] Desktop público limpo.", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                Log(string.Format("X Aviso ao limpar Desktop: {0}", ex.Message),
                    ConsoleColor.Yellow);
            }
        }

        /// <summary>
        /// Cria atalhos de suporte na área de trabalho dos usuários criados
        /// e no desktop público.
        /// </summary>
        private async Task CreateDesktopShortcutsAsync(string adminPassword, string standardUserName)
        {
            var shortcuts = new Dictionary<string, string>
            {
                { "Suporte Norte", "https://suporte.exabit.com.br" },
                { "Webmail Nortel", "http://webmail.nortel.srv.br" },
            };

            // Coleta os caminhos dos desktops onde criar os atalhos
            var desktopPaths = new List<string>();

            // Desktop público
            try
            {
                desktopPaths.Add(Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonDesktopDirectory));
            }
            catch { }

            string usersFolder = Environment.GetEnvironmentVariable("SystemDrive") + "\\Users";

            // Desktop do admin
            desktopPaths.Add(Path.Combine(usersFolder, "admin", "Desktop"));

            // Desktop do usuário padrão
            if (!string.IsNullOrWhiteSpace(standardUserName))
            {
                desktopPaths.Add(Path.Combine(usersFolder, standardUserName, "Desktop"));
            }

            Log("Criando atalhos na área de trabalho...", ConsoleColor.Yellow);

            foreach (string desktop in desktopPaths)
            {
                try
                {
                    if (!Directory.Exists(desktop))
                        Directory.CreateDirectory(desktop);

                    foreach (var kvp in shortcuts)
                    {
                        string urlPath = Path.Combine(desktop, kvp.Key + ".url");
                        File.WriteAllText(urlPath,
                            "[InternetShortcut]\r\nURL=" + kvp.Value);
                    }
                }
                catch (Exception ex)
                {
                    Log(string.Format("X Aviso ao criar atalhos em {0}: {1}",
                        desktop, ex.Message), ConsoleColor.Yellow);
                }
            }

            Log("[ok] Atalhos criados na área de trabalho.", ConsoleColor.Green);
            await Task.Delay(200);
        }

        // ──────────────────────────────────────────────
        // Métodos privados utilitários
        // ──────────────────────────────────────────────

        /// <summary>
        /// Executa um processo e aguarda a conclusão.
        /// Retorna o código de saída (0 = sucesso).
        /// </summary>
        /// <param name="fileName">Executável ou comando</param>
        /// <param name="arguments">Argumentos da linha de comando</param>
        /// <param name="timeoutMs">Tempo máximo de espera (padrão: 2 minutos)</param>
        /// <returns>Código de saída do processo</returns>
        /// <param name="timeoutMs">Tempo máximo de espera (padrão: 10 minutos)</param>
        private async Task<int> RunProcessAsync(string fileName, string arguments,
            int timeoutMs = 600000, string workingDirectory = null, bool useShell = false)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = useShell,
                CreateNoWindow = !useShell,
                RedirectStandardOutput = !useShell,
                RedirectStandardError = !useShell,
                WorkingDirectory = workingDirectory ?? string.Empty,
            };

            var tcs = new TaskCompletionSource<int>();

            using (var process = new Process { StartInfo = psi })
            {
                process.EnableRaisingEvents = true;
                process.Exited += (sender, args) =>
                {
                    tcs.TrySetResult(process.ExitCode);
                };

                process.Start();

                // Aguarda o processo com timeout
                await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));

                if (!process.HasExited)
                {
                    process.Kill();
                    Log(string.Format("  Processo {0} excedeu o tempo limite.", fileName),
                        ConsoleColor.Yellow);
                    return -1;
                }

                // Lê output apenas quando redirecionado
                if (!useShell)
                {
                    string stdOut = await process.StandardOutput.ReadToEndAsync();
                    string stdErr = await process.StandardError.ReadToEndAsync();
                    if (!string.IsNullOrWhiteSpace(stdErr))
                        Log(string.Format("  {0}", stdErr.Trim()), ConsoleColor.Yellow);
                }

                return process.ExitCode;
            }
        }

        /// <summary>
        /// Dispara o evento de log para a interface tratar.
        /// </summary>
        private void Log(string message, ConsoleColor? color)
        {
            if (OnLog != null)
                OnLog(message, color ?? ConsoleColor.Gray);
        }

        /// <summary>
        /// Dispara o evento de progresso para a interface atualizar a barra.
        /// </summary>
        private void ReportProgress(int current, int total)
        {
            if (OnProgress != null)
                OnProgress(current, total);
        }
    }

    // ──────────────────────────────────────────────
    // Modelo de opções da instalação
    // ──────────────────────────────────────────────

    /// <summary>
    /// Agrupa todas as opções/configurações escolhidas pelo usuário
    /// para passar ao <see cref="InstallerService.RunFullInstallationAsync"/>.
    /// </summary>
    public class InstallationOptions
    {
        public bool RenameComputer { get; set; }
        public string ComputerName { get; set; }

        public bool CreateAdmin { get; set; }
        public string AdminPassword { get; set; }

        public bool CreateStandardUser { get; set; }
        public string StandardUserName { get; set; }
        public string StandardUserPassword { get; set; }
        public bool StandardUserCannotChangePassword { get; set; }

        public bool SetWallpaper { get; set; }

        /// <summary>
        /// Lista dos programas selecionados para instalação.
        /// </summary>
        public List<ProgramInfo> SelectedPrograms { get; set; }

        public InstallationOptions()
        {
            SelectedPrograms = new List<ProgramInfo>();
        }
    }
}
