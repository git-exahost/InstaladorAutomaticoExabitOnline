using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
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

        /// <summary>
        /// Disparado quando toda a instalação é concluída com sucesso.
        /// </summary>
        public event Action OnInstallationCompleted;

        // ──────────────────────────────────────────────
        // Campos
        // ──────────────────────────────────────────────

        /// <summary>
        /// Caminho raiz onde estão as pastas Programas/ e Scripts/.
        /// Normalmente é o mesmo diretório do executável.
        /// </summary>
        private readonly string _basePath;

        /// <summary>
        /// Token para cancelar a instalação.
        /// </summary>
        private System.Threading.CancellationTokenSource _cancellationTokenSource;

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

        /// <summary>
        /// Inicia uma nova instalação (cria token de cancelamento).
        /// </summary>
        public void StartInstallation()
        {
            _cancellationTokenSource = new System.Threading.CancellationTokenSource();
        }

        /// <summary>
        /// Solicita o cancelamento da instalação.
        /// </summary>
        public void CancelInstallation()
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
            }
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
        /// Cria o usuário administrador "admin" com a senha fornecida e o adiciona
        /// ao grupo de administradores locais, ou apenas altera sua senha caso já exista.
        /// </summary>
        /// <param name="password">Senha do usuário admin</param>
        public async Task CreateAdminUserAsync(string password)
        {
            try
            {
                bool exists = await UserExistsAsync("admin");
                if (exists)
                    await UpdateExistingAdminPasswordAsync(password);
                else
                    await CreateNewAdminUserAsync(password);
            }
            catch (Exception ex)
            {
                Log(string.Format("X Erro ao configurar admin: {0}", ex.Message), ConsoleColor.Red);
            }

            await Task.Delay(500);
        }

        private async Task<bool> UserExistsAsync(string username)
        {
            int code = await RunProcessAsync(
                "net",
                string.Format("user {0}", QuoteProcessArgument(username)),
                10000,
                null,
                false,
                true);
            return code == 0;
        }

        private async Task UpdateExistingAdminPasswordAsync(string password)
        {
            if (!string.IsNullOrWhiteSpace(password))
            {
                Log("Usuário 'admin' já existe. Alterando a senha...", ConsoleColor.Yellow);

                int code = await RunProcessAsync(
                    "net",
                    string.Format("user admin {0}", QuoteProcessArgument(password)),
                    10000);

                if (code == 0)
                    Log("[ok] Senha do usuário 'admin' alterada com sucesso.", ConsoleColor.Green);
                else
                    Log("X Não foi possível alterar a senha do usuário 'admin'.", ConsoleColor.Red);
            }
            else
            {
                Log("Usuário 'admin' já existe e nenhuma senha foi informada. Mantendo senha atual.", ConsoleColor.Yellow);
            }

            await EnsureAdminPasswordNeverExpiresAsync();
        }

        private async Task CreateNewAdminUserAsync(string password)
        {
            Log("Criando usuário 'admin'...", ConsoleColor.Yellow);

            string passwordArg = string.IsNullOrWhiteSpace(password)
                ? "\"\""
                : QuoteProcessArgument(password);

            int code = await RunProcessAsync(
                "net",
                string.Format("user admin {0} /add", passwordArg),
                10000);

            if (code != 0)
            {
                Log("X Não foi possível criar o usuário 'admin'.", ConsoleColor.Red);
                return;
            }

            Log("[ok] Usuário 'admin' criado.", ConsoleColor.Green);

            Log("Adicionando 'admin' aos administradores...", ConsoleColor.Yellow);
            code = await RunProcessAsync("net", "localgroup Administradores admin /add", 600000, null, false, true);
            if (code != 0)
                code = await RunProcessAsync("net", "localgroup Administrators admin /add");

            if (code != 0)
                Log("X Aviso: Não foi possível adicionar 'admin' ao grupo de administradores.", ConsoleColor.Yellow);

            await EnsureAdminPasswordNeverExpiresAsync();
        }

        private async Task EnsureAdminPasswordNeverExpiresAsync()
        {
            int code = await RunProcessAsync("powershell",
                "-Command \"Set-LocalUser -Name 'admin' -PasswordNeverExpires $true\"", 10000);

            if (code == 0)
                Log("[ok] Senha do admin configurada para nunca expirar.", ConsoleColor.Green);
            else
                Log("X Aviso: Não foi possível configurar a senha do admin para nunca expirar.", ConsoleColor.Yellow);
        }

        /// <summary>
        /// Cria um usuário padrão com nome e senha fornecidos (ou atualiza caso já exista),
        /// garantindo que a senha nunca expire e aplicando a política de alteração de senha.
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

            try
            {
                bool exists = await UserExistsAsync(username);
                if (exists)
                    await UpdateExistingStandardUserAsync(username, password, cannotChangePassword);
                else
                    await CreateNewStandardUserAsync(username, password, cannotChangePassword);
            }
            catch (Exception ex)
            {
                Log(string.Format("X Erro ao configurar \"{0}\": {1}", username, ex.Message),
                    ConsoleColor.Red);
            }

            await Task.Delay(500);
        }

        private async Task CreateNewStandardUserAsync(string username, string password, bool cannotChangePassword)
        {
            Log(string.Format("Criando usuário \"{0}\"...", username), ConsoleColor.Yellow);

            string pwdChg = cannotChangePassword ? "/passwordchg:no" : "/passwordchg:yes";
            int code;
            if (string.IsNullOrWhiteSpace(password))
                code = await RunProcessAsync("net",
                    string.Format("user {0} /add {1}", QuoteProcessArgument(username), pwdChg));
            else
                code = await RunProcessAsync("net",
                    string.Format("user {0} {1} /add {2}", QuoteProcessArgument(username), QuoteProcessArgument(password), pwdChg));

            if (code != 0)
            {
                Log(string.Format("X Não foi possível criar o usuário \"{0}\".", username), ConsoleColor.Red);
                return;
            }

            await ConfigureStandardUserSettingsAsync(username, cannotChangePassword);

            Log(string.Format("[ok] Usuário \"{0}\" criado.", username), ConsoleColor.Green);
        }

        private async Task UpdateExistingStandardUserAsync(string username, string password, bool cannotChangePassword)
        {
            string pwdChg = cannotChangePassword ? "/passwordchg:no" : "/passwordchg:yes";

            if (!string.IsNullOrWhiteSpace(password))
            {
                Log(string.Format("Usuário \"{0}\" já existe. Alterando a senha...", username), ConsoleColor.Yellow);
                int code = await RunProcessAsync("net",
                    string.Format("user {0} {1} {2}", QuoteProcessArgument(username), QuoteProcessArgument(password), pwdChg));

                if (code == 0)
                    Log(string.Format("[ok] Senha do usuário \"{0}\" alterada com sucesso.", username), ConsoleColor.Green);
                else
                    Log(string.Format("X Não foi possível alterar a senha do usuário \"{0}\".", username), ConsoleColor.Red);
            }
            else
            {
                Log(string.Format("Usuário \"{0}\" já existe e nenhuma senha foi informada. Mantendo senha atual.", username), ConsoleColor.Yellow);
                await RunProcessAsync("net",
                    string.Format("user {0} {1}", QuoteProcessArgument(username), pwdChg));
            }

            await ConfigureStandardUserSettingsAsync(username, cannotChangePassword);
        }

        private async Task ConfigureStandardUserSettingsAsync(string username, bool cannotChangePassword)
        {
            int code = await RunProcessAsync("powershell",
                string.Format("-Command \"Set-LocalUser -Name '{0}' -PasswordNeverExpires $true -UserMayNotChangePassword ${1}\"",
                username.Replace("'", "''"), cannotChangePassword ? "true" : "false"), 10000);

            if (code == 0)
            {
                Log(string.Format("[ok] Senha do usuário \"{0}\" configurada para nunca expirar.", username), ConsoleColor.Green);
                if (cannotChangePassword)
                    Log("[ok] Restrição aplicada: usuário impedido de alterar a senha.", ConsoleColor.Green);
                else
                    Log("[ok] Permissão de senha: usuário poderá alterar a própria senha.", ConsoleColor.Green);
            }
            else
            {
                Log(string.Format("X Aviso: Não foi possível configurar expiração/restrição de senha para \"{0}\".", username), ConsoleColor.Yellow);
            }

            Log("Adicionando ao grupo de área de trabalho remota...", ConsoleColor.Yellow);
            int rdpCode = await RunProcessAsync("net",
                string.Format("localgroup \"Usuários da Área de Trabalho Remota\" {0} /add", QuoteProcessArgument(username)),
                600000, null, false, true);
            if (rdpCode != 0)
                rdpCode = await RunProcessAsync("net",
                    string.Format("localgroup \"Remote Desktop Users\" {0} /add", QuoteProcessArgument(username)),
                    600000, null, false, true);

            if (rdpCode == 0)
                Log("[ok] Usuário adicionado ao grupo de área de trabalho remota.", ConsoleColor.Green);
            else
                Log("X Aviso: Não foi possível adicionar ao grupo de área de trabalho remota.", ConsoleColor.Yellow);
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
        /// Verifica se há conexão ativa com a internet.
        /// </summary>
        /// <returns>true se houver conectividade com a internet; caso contrário, false.</returns>
        public async Task<bool> HasInternetConnectionAsync()
        {
            return await Task.Run(new Func<bool>(() => CheckInternetConnectivity()));
        }

        private static bool CheckInternetConnectivity()
        {
            if (TryHttpGetContains("http://www.msftconnecttest.com/connecttest.txt", "Microsoft Connect Test"))
                return true;

            return TryHttpHead("https://www.microsoft.com");
        }

        private static bool TryHttpGetContains(string url, string expectedText)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.Timeout = 4000;
                request.ReadWriteTimeout = 4000;
                request.AllowAutoRedirect = false;

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                        return false;

                    using (var stream = response.GetResponseStream())
                    {
                        if (stream == null)
                            return false;

                        using (var reader = new StreamReader(stream))
                        {
                            string body = reader.ReadToEnd();
                            return body.IndexOf(expectedText, StringComparison.OrdinalIgnoreCase) >= 0;
                        }
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool TryHttpHead(string url)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "HEAD";
                request.Timeout = 4000;
                request.AllowAutoRedirect = true;

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    int code = (int)response.StatusCode;
                    return code >= 200 && code < 400;
                }
            }
            catch
            {
                return false;
            }
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

            if (program.RequiresInternet && !await HasInternetConnectionAsync())
            {
                Log(string.Format("X {0} requer conexão com a internet. Conecte-se à internet antes de instalar.", program.DisplayName),
                    ConsoleColor.Red);
                return;
            }

            try
            {
                string extension = Path.GetExtension(program.FileName).ToLower();

                string procWorkDir = !string.IsNullOrEmpty(program.ProcessWorkingDir)
                    ? Path.GetDirectoryName(fullPath) : null;

                if (program.FireAndForget)
                {
                    // Inicia e aguarda um tempo para o instalador trabalhar em segundo plano
                    int delay = program.TimeoutMs > 0 ? program.TimeoutMs : 30000;
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = fullPath,
                        Arguments = program.Arguments,
                        WorkingDirectory = procWorkDir ?? string.Empty,
                        UseShellExecute = true,
                    });
                    Log(string.Format("  Aguardando {0} segundos para conclusão...", delay / 1000), ConsoleColor.Gray);
                    await Task.Delay(delay);
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
            bool cancelled = false;

            Log("=== INICIANDO INSTALAÇÃO ===", ConsoleColor.Cyan);
            Log(string.Format("Data: {0:dd/MM/yyyy HH:mm:ss}", DateTime.Now), ConsoleColor.Gray);
            Log("", null);

            try
            {
                // 1. Renomear computador
                if (!cancelled && options.RenameComputer && !string.IsNullOrWhiteSpace(options.ComputerName))
                {
                    if (_cancellationTokenSource != null && _cancellationTokenSource.Token.IsCancellationRequested)
                        cancelled = true;

                    if (!cancelled)
                    {
                        currentStep++;
                        ReportProgress(currentStep, totalSteps);
                        await RenameComputerAsync(options.ComputerName);
                    }
                }

                // 2. Criar usuário admin
                if (!cancelled && options.CreateAdmin)
                {
                    if (_cancellationTokenSource != null && _cancellationTokenSource.Token.IsCancellationRequested)
                        cancelled = true;

                    if (!cancelled)
                    {
                        currentStep++;
                        ReportProgress(currentStep, totalSteps);
                        await CreateAdminUserAsync(options.AdminPassword);
                    }
                }

                // 3. Criar usuário padrão
                if (!cancelled && options.CreateStandardUser)
                {
                    if (_cancellationTokenSource != null && _cancellationTokenSource.Token.IsCancellationRequested)
                        cancelled = true;

                    if (!cancelled)
                    {
                        currentStep++;
                        ReportProgress(currentStep, totalSteps);
                        await CreateStandardUserAsync(options.StandardUserName, options.StandardUserPassword, options.StandardUserCannotChangePassword);
                    }
                }

                // 4. Wallpaper
                if (!cancelled && options.SetWallpaper)
                {
                    if (_cancellationTokenSource != null && _cancellationTokenSource.Token.IsCancellationRequested)
                        cancelled = true;

                    if (!cancelled)
                    {
                        currentStep++;
                        ReportProgress(currentStep, totalSteps);
                        await SetWallpaperAsync();
                    }
                }

                // Limpa todas as áreas de trabalho ANTES de instalar programas,
                // pois alguns instaladores criam atalhos imediatamente.
                if (!cancelled)
                {
                    string stdUserName = options.CreateStandardUser ? options.StandardUserName : null;
                    CleanAllDesktops(stdUserName);
                }

                // 5. Instalar cada programa selecionado
                if (!cancelled)
                {
                    foreach (var program in options.SelectedPrograms)
                    {
                        if (_cancellationTokenSource != null && _cancellationTokenSource.Token.IsCancellationRequested)
                        {
                            cancelled = true;
                            break;
                        }

                        currentStep++;
                        ReportProgress(currentStep, totalSteps);
                        await InstallProgramAsync(program);
                    }
                }

                if (!cancelled)
                {
                    // Limpa NOVAMENTE após a instalação para remover atalhos
                    // criados por processos-filho ou ações adiadas dos instaladores.
                    Log("Removendo atalhos deixados pelos instaladores...", ConsoleColor.Yellow);
                    string stdUserName = options.CreateStandardUser ? options.StandardUserName : null;
                    CleanAllDesktops(stdUserName);

                    // Recria apenas os atalhos personalizados (conforme opções do usuário)
                    await CreateDesktopShortcutsAsync(options);

                    Log("=== INSTALAÇÃO CONCLUÍDA ===", ConsoleColor.Green);

                    var handler = OnInstallationCompleted;
                    if (handler != null)
                    {
                        handler();
                    }
                }
                else
                {
                    Log("=== INSTALAÇÃO CANCELADA ===", ConsoleColor.Yellow);
                }
            }
            catch (OperationCanceledException)
            {
                Log("=== INSTALAÇÃO CANCELADA ===", ConsoleColor.Yellow);
            }
        }

        /// <summary>
        /// Remove todos os atalhos (.lnk e .url) da área de trabalho de todos
        /// os perfis (público, admin, usuário padrão e Default) para deixar apenas
        /// os que serão criados pelo método <see cref="CreateDesktopShortcutsAsync"/>.
        /// 
        /// Também limpa o template Default\Desktop para que novos usuários
        /// que ainda não fizeram login também recebam a área limpa.
        /// </summary>
        private void CleanAllDesktops(string standardUserName)
        {
            Log("Limpando atalhos de todas as áreas de trabalho...", ConsoleColor.Yellow);

            var desktopPaths = new List<string>();

            // Desktop público (afeta todos os usuários)
            try
            {
                desktopPaths.Add(Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonDesktopDirectory));
            }
            catch { }

            string usersFolder = Environment.GetEnvironmentVariable("SystemDrive") + "\\Users";

            // Desktop do admin
            desktopPaths.Add(Path.Combine(usersFolder, "admin", "Desktop"));

            // Desktop do usuário padrão (se já existir)
            if (!string.IsNullOrWhiteSpace(standardUserName))
            {
                desktopPaths.Add(Path.Combine(usersFolder, standardUserName, "Desktop"));
            }

            // Template Default - usado para criar desktop de NOVOS usuários
            desktopPaths.Add(Path.Combine(usersFolder, "Default", "Desktop"));

            foreach (string desktop in desktopPaths)
            {
                try
                {
                    if (!Directory.Exists(desktop))
                    {
                        // Cria o diretório se não existir (ex: Default\Desktop ou usuário que nunca logou)
                        Directory.CreateDirectory(desktop);
                    }

                    // Remove atalhos .lnk (programas)
                    foreach (string file in Directory.GetFiles(desktop, "*.lnk"))
                    {
                        try { File.Delete(file); }
                        catch { }
                    }

                    // Remove atalhos .url (websites)
                    foreach (string file in Directory.GetFiles(desktop, "*.url"))
                    {
                        try { File.Delete(file); }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    Log(string.Format("X Aviso ao limpar {0}: {1}", desktop, ex.Message),
                        ConsoleColor.Yellow);
                }
            }

            Log("[ok] Áreas de trabalho limpas.", ConsoleColor.Green);
        }

        /// <summary>
        /// Cria atalhos de suporte na área de trabalho dos usuários criados
        /// e no desktop público, conforme as opções selecionadas pelo usuário.
        /// Se solicitado, também adiciona os atalhos no Menu Iniciar (Todos os Usuários).
        /// </summary>
        private async Task CreateDesktopShortcutsAsync(InstallationOptions options)
        {
            // Monta dicionário apenas com os atalhos selecionados
            var shortcuts = new Dictionary<string, string>();
            if (options.CreateDesktopShortcutSuporteNorte)
                shortcuts.Add("Suporte Norte", "https://suporte.exabit.com.br");
            if (options.CreateDesktopShortcutWebmailNortel)
                shortcuts.Add("Webmail Nortel", "http://webmail.nortel.srv.br");
            if (options.CreateDesktopShortcutWebmailTerceirizacao)
                shortcuts.Add("Webmail Nortel Terceirização", "http://webmail.nortelterceirizacao.com.br");

            if (shortcuts.Count == 0)
            {
                Log("Nenhum atalho selecionado. Pulando...", ConsoleColor.Yellow);
                return;
            }

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
            string stdUserName = options.CreateStandardUser ? options.StandardUserName : null;
            if (!string.IsNullOrWhiteSpace(stdUserName))
            {
                desktopPaths.Add(Path.Combine(usersFolder, stdUserName, "Desktop"));
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

            // Se solicitado, cria também no Menu Iniciar (Todos os Usuários)
            if (options.AddShortcutsToStartMenu)
            {
                string startMenu = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                    "Programs");

                Log("Adicionando atalhos ao Menu Iniciar...", ConsoleColor.Yellow);

                foreach (var kvp in shortcuts)
                {
                    string urlPath = Path.Combine(startMenu, kvp.Key + ".url");
                    try
                    {
                        File.WriteAllText(urlPath,
                            "[InternetShortcut]\r\nURL=" + kvp.Value);
                    }
                    catch (Exception ex)
                    {
                        Log(string.Format("X Aviso ao criar atalho no Menu Iniciar: {0}",
                            ex.Message), ConsoleColor.Yellow);
                    }
                }

                Log("[ok] Atalhos adicionados ao Menu Iniciar.", ConsoleColor.Green);
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
        /// <param name="timeoutMs">Tempo máximo de espera (padrão: 10 minutos)</param>
        /// <param name="workingDirectory">Diretório de trabalho do processo</param>
        /// <param name="useShell">Define se usa UseShellExecute</param>
        /// <param name="suppressStdErrLog">Se true, não registra saída de erro padrão no log</param>
        /// <returns>Código de saída do processo</returns>
        private async Task<int> RunProcessAsync(string fileName, string arguments,
            int timeoutMs = 600000, string workingDirectory = null, bool useShell = false,
            bool suppressStdErrLog = false)
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
                    if (!suppressStdErrLog && !string.IsNullOrWhiteSpace(stdErr))
                        Log(string.Format("  {0}", stdErr.Trim()), ConsoleColor.Yellow);
                }

                return process.ExitCode;
            }
        }

        private static string QuoteProcessArgument(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "\"\"";

            var sb = new StringBuilder();
            sb.Append('"');

            int backslashCount = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\')
                {
                    backslashCount++;
                }
                else if (c == '"')
                {
                    sb.Append('\\', backslashCount * 2 + 1);
                    sb.Append('"');
                    backslashCount = 0;
                }
                else
                {
                    if (backslashCount > 0)
                    {
                        sb.Append('\\', backslashCount);
                        backslashCount = 0;
                    }
                    sb.Append(c);
                }
            }

            if (backslashCount > 0)
                sb.Append('\\', backslashCount * 2);

            sb.Append('"');
            return sb.ToString();
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

        // ─── Opções de atalhos de suporte ───

        /// <summary>
        /// Cria atalho "Suporte Norte" na área de trabalho.
        /// </summary>
        public bool CreateDesktopShortcutSuporteNorte { get; set; }

        /// <summary>
        /// Cria atalho "Webmail Nortel" na área de trabalho.
        /// </summary>
        public bool CreateDesktopShortcutWebmailNortel { get; set; }

        /// <summary>
        /// Cria atalho "Webmail Nortel Terceirização" na área de trabalho.
        /// </summary>
        public bool CreateDesktopShortcutWebmailTerceirizacao { get; set; }

        /// <summary>
        /// Adiciona os atalhos também no Menu Iniciar (Todos os Usuários).
        /// </summary>
        public bool AddShortcutsToStartMenu { get; set; }

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
