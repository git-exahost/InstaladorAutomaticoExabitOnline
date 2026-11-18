using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;
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
        /// Obtém o caminho raiz onde estão as pastas Programas/ e Scripts/.
        /// </summary>
        public string BasePath
        {
            get { return _basePath; }
        }

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
                await EnsureGlobalPasswordNeverExpiresPolicyAsync();

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

            await EnsurePasswordNeverExpiresAsync("admin", false);
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

            await EnsurePasswordNeverExpiresAsync("admin", false);
        }

        /// <summary>
        /// Define a política global de segurança do Windows para que a duração máxima de senhas
        /// seja ILIMITADA em toda a máquina (net accounts /maxpwage:unlimited).
        /// </summary>
        private async Task EnsureGlobalPasswordNeverExpiresPolicyAsync()
        {
            try
            {
                int code = await RunProcessAsync("net", "accounts /maxpwage:unlimited", 10000, null, false, true);
                if (code == 0)
                {
                    Log("[ok] Política global do Windows: expiração de senhas definida como NUNCA (ilimitada).", ConsoleColor.Green);
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Garante que a opção 'A senha nunca expira' (PasswordNeverExpires) esteja SEMPRE ATIVA
        /// para o usuário informado utilizando o cmdlet nativo Set-LocalUser do Windows PowerShell.
        /// Também configura se o usuário pode ou não alterar a própria senha.
        /// </summary>
        /// <param name="username">Nome da conta local de usuário</param>
        /// <param name="cannotChangePassword">Se true, impede alteração da senha pelo usuário</param>
        private async Task EnsurePasswordNeverExpiresAsync(string username, bool cannotChangePassword = false)
        {
            if (string.IsNullOrWhiteSpace(username)) return;

            string safeUser = username.Replace("'", "''");
            bool mayChangePassword = !cannotChangePassword;
            string psMayChange = mayChangePassword ? "true" : "false";

            int psCode = await RunProcessAsync("powershell",
                string.Format("-NoProfile -ExecutionPolicy Bypass -Command \"Set-LocalUser -Name '{0}' -PasswordNeverExpires $true -UserMayChangePassword ${1}\"",
                safeUser, psMayChange), 15000);

            if (psCode == 0)
            {
                Log(string.Format("[ok] Senha do usuário \"{0}\" configurada para NUNCA expirar (obrigatório).", username), ConsoleColor.Green);
                if (cannotChangePassword)
                    Log("[ok] Restrição aplicada: usuário impedido de alterar a senha.", ConsoleColor.Green);
                else if (!string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase))
                    Log("[ok] Permissão de senha: usuário poderá alterar a própria senha.", ConsoleColor.Green);
            }
            else
            {
                Log(string.Format("[ok] Senha do usuário \"{0}\" protegida contra expiração via política do sistema.", username), ConsoleColor.Green);
            }
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
            await EnsurePasswordNeverExpiresAsync(username, cannotChangePassword);

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
        /// Configura e ativa os Serviços de Localização do Windows por padrão
        /// para o computador (nível de máquina/HKLM), para o serviço lfsvc,
        /// para o perfil atual (Admin) e para o perfil do usuário criado (Active Setup e Default).
        /// </summary>
        public async Task ConfigureLocationServicesAsync()
        {
            Log("Ativando serviços de localização do Windows por padrão...", ConsoleColor.Yellow);

            await Task.Run(new Action(() =>
            {
                try
                {
                    // 1. Configurar tipo de inicialização do serviço Geolocation (lfsvc)
                    try
                    {
                        using (var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Services\lfsvc"))
                        {
                            if (key != null)
                            {
                                key.SetValue("Start", 3, RegistryValueKind.DWord); // Manual (Trigger Start)
                            }
                        }

                        using (var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Services\lfsvc\Service\Configuration"))
                        {
                            if (key != null)
                            {
                                key.SetValue("Status", 1, RegistryValueKind.DWord);
                            }
                        }
                    }
                    catch
                    {
                    }

                    // 2. Chaves de registro de Sistema / Máquina (HKLM)
                    try
                    {
                        // Habilita acesso de localização a nível de sistema (Master switch do dispositivo)
                        using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location"))
                        {
                            if (key != null)
                            {
                                key.SetValue("Value", "Allow", RegistryValueKind.String);
                            }
                        }

                        // Habilita para aplicativos da área de trabalho (desktop apps)
                        using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location\NonPackaged"))
                        {
                            if (key != null)
                            {
                                key.SetValue("Value", "Allow", RegistryValueKind.String);
                            }
                        }

                        // Sensor de hardware e geolocalização do Windows
                        using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Sensor\Overrides\{BFA794E4-F964-4FDB-90F6-51056BFE4B44}"))
                        {
                            if (key != null)
                            {
                                key.SetValue("SensorPermissionState", 1, RegistryValueKind.DWord);
                            }
                        }

                        // Políticas de grupo (garante que nenhuma GPO desative localização ou sensores)
                        using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors"))
                        {
                            if (key != null)
                            {
                                key.SetValue("DisableLocation", 0, RegistryValueKind.DWord);
                                key.SetValue("DisableWindowsLocationProvider", 0, RegistryValueKind.DWord);
                                key.SetValue("DisableLocationScripting", 0, RegistryValueKind.DWord);
                                key.SetValue("DisableSensors", 0, RegistryValueKind.DWord);
                            }
                        }
                    }
                    catch
                    {
                    }

                    // 3. Chaves para o usuário atual (Admin logado)
                    try
                    {
                        using (var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location"))
                        {
                            if (key != null)
                            {
                                key.SetValue("Value", "Allow", RegistryValueKind.String);
                            }
                        }

                        using (var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location\NonPackaged"))
                        {
                            if (key != null)
                            {
                                key.SetValue("Value", "Allow", RegistryValueKind.String);
                            }
                        }

                        using (var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Sensor\Overrides\{BFA794E4-F964-4FDB-90F6-51056BFE4B44}"))
                        {
                            if (key != null)
                            {
                                key.SetValue("SensorPermissionState", 1, RegistryValueKind.DWord);
                            }
                        }
                    }
                    catch
                    {
                    }

                    // 4. Perfil padrão do Windows (HKEY_USERS\.DEFAULT)
                    try
                    {
                        using (var key = Registry.Users.CreateSubKey(@".DEFAULT\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location"))
                        {
                            if (key != null)
                            {
                                key.SetValue("Value", "Allow", RegistryValueKind.String);
                            }
                        }

                        using (var key = Registry.Users.CreateSubKey(@".DEFAULT\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location\NonPackaged"))
                        {
                            if (key != null)
                            {
                                key.SetValue("Value", "Allow", RegistryValueKind.String);
                            }
                        }

                        using (var key = Registry.Users.CreateSubKey(@".DEFAULT\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Sensor\Overrides\{BFA794E4-F964-4FDB-90F6-51056BFE4B44}"))
                        {
                            if (key != null)
                            {
                                key.SetValue("SensorPermissionState", 1, RegistryValueKind.DWord);
                            }
                        }
                    }
                    catch
                    {
                    }

                    // 5. Aplica a todos os perfis de usuários carregados em HKEY_USERS
                    try
                    {
                        foreach (string subName in Registry.Users.GetSubKeyNames())
                        {
                            if (string.Equals(subName, ".DEFAULT", StringComparison.OrdinalIgnoreCase) ||
                                subName.EndsWith("_Classes", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            try
                            {
                                using (var key = Registry.Users.CreateSubKey(subName + @"\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location"))
                                {
                                    if (key != null) key.SetValue("Value", "Allow", RegistryValueKind.String);
                                }
                                using (var key = Registry.Users.CreateSubKey(subName + @"\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location\NonPackaged"))
                                {
                                    if (key != null) key.SetValue("Value", "Allow", RegistryValueKind.String);
                                }
                                using (var key = Registry.Users.CreateSubKey(subName + @"\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Sensor\Overrides\{BFA794E4-F964-4FDB-90F6-51056BFE4B44}"))
                                {
                                    if (key != null) key.SetValue("SensorPermissionState", 1, RegistryValueKind.DWord);
                                }
                            }
                            catch
                            {
                            }
                        }
                    }
                    catch
                    {
                    }

                    // 6. Active Setup: garante que novos perfis criados recebam permissão de localização no primeiro login
                    try
                    {
                        using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Active Setup\Installed Components\{E7A18C3B-4491-49F1-8B35-EXABITLOC01}"))
                        {
                            if (key != null)
                            {
                                key.SetValue("ComponentID", "ExabitLocationConsent", RegistryValueKind.String);
                                key.SetValue("Version", "1,0,0,1", RegistryValueKind.String);
                                key.SetValue("StubPath", "reg.exe add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\location\" /v Value /t REG_SZ /d Allow /f", RegistryValueKind.String);
                            }
                        }

                        using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Active Setup\Installed Components\{E7A18C3B-4491-49F1-8B35-EXABITLOC02}"))
                        {
                            if (key != null)
                            {
                                key.SetValue("ComponentID", "ExabitLocationNonPackaged", RegistryValueKind.String);
                                key.SetValue("Version", "1,0,0,1", RegistryValueKind.String);
                                key.SetValue("StubPath", "reg.exe add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\location\\NonPackaged\" /v Value /t REG_SZ /d Allow /f", RegistryValueKind.String);
                            }
                        }

                        using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Active Setup\Installed Components\{E7A18C3B-4491-49F1-8B35-EXABITLOC03}"))
                        {
                            if (key != null)
                            {
                                key.SetValue("ComponentID", "ExabitLocationSensor", RegistryValueKind.String);
                                key.SetValue("Version", "1,0,0,1", RegistryValueKind.String);
                                key.SetValue("StubPath", "reg.exe add \"HKCU\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Sensor\\Overrides\\{BFA794E4-F964-4FDB-90F6-51056BFE4B44}\" /v SensorPermissionState /t REG_DWORD /d 1 /f", RegistryValueKind.String);
                            }
                        }
                    }
                    catch
                    {
                    }

                    // 7. Iniciar os serviços lfsvc e SensorService se não estiverem em execução
                    try
                    {
                        using (var sc = new System.ServiceProcess.ServiceController("lfsvc"))
                        {
                            if (sc.Status != System.ServiceProcess.ServiceControllerStatus.Running &&
                                sc.Status != System.ServiceProcess.ServiceControllerStatus.StartPending)
                            {
                                sc.Start();
                            }
                        }
                    }
                    catch
                    {
                    }

                    try
                    {
                        using (var sc = new System.ServiceProcess.ServiceController("SensorService"))
                        {
                            if (sc.Status != System.ServiceProcess.ServiceControllerStatus.Running &&
                                sc.Status != System.ServiceProcess.ServiceControllerStatus.StartPending)
                            {
                                sc.Start();
                            }
                        }
                    }
                    catch
                    {
                    }
                }
                catch (Exception ex)
                {
                    Log(string.Format("X Aviso ao configurar localização: {0}", ex.Message), ConsoleColor.Yellow);
                }
            }));

            // 8. Chamar o utilitário nativo do Windows 11 para ativar a chave mestra no CAM (Capability Access Manager)
            try
            {
                string adminFlowsExe = Path.Combine(Environment.SystemDirectory, "SystemSettingsAdminFlows.exe");
                if (File.Exists(adminFlowsExe))
                {
                    await RunProcessAsync(adminFlowsExe, "SetCamSystemGlobal location 1", 10000, null, false, true);
                    await RunProcessAsync(adminFlowsExe, "SetCamSystemGlobal location 2", 10000, null, false, true);
                }
            }
            catch
            {
            }

            Log("[ok] Serviços de localização do Windows ativados com sucesso.", ConsoleColor.Green);
            await Task.Delay(200);
        }

        /// <summary>
        /// Aplica os papéis de parede personalizados adicionando todos os arquivos da pasta Wallpapers
        /// como papel de parede em todos os temas do Windows (Aero, Dark, ThemeA-D, Spotlight)
        /// e configurando para novos usuários e usuário atual.
        /// </summary>
        public async Task SetWallpaperAsync()
        {
            Log("Configurando papéis de parede em todos os temas do Windows...", ConsoleColor.Yellow);

            string psScript = Path.Combine(_basePath, "Scripts", "apply_wallpapers.ps1");
            string cmdScript = Path.Combine(_basePath, "Scripts", "wallpaper.cmd");

            if (File.Exists(psScript))
            {
                string safeBasePath = _basePath ?? string.Empty;
                string cleanBasePath = safeBasePath.TrimEnd('\\', '/');
                if (cleanBasePath.Length == 2 && cleanBasePath.EndsWith(":"))
                {
                    cleanBasePath += "\\";
                }

                string quotedPsScript = QuoteProcessArgument(psScript);
                string quotedBasePath = QuoteProcessArgument(cleanBasePath);

                int exitCode = await RunProcessAsync("powershell",
                    string.Format("-NoProfile -ExecutionPolicy Bypass -File {0} -BasePath {1}", quotedPsScript, quotedBasePath),
                    120000, null, false);

                if (exitCode == 0)
                {
                    Log("[ok] Papéis de parede configurados em todos os temas do Windows.", ConsoleColor.Green);
                }
                else
                {
                    Log(string.Format("X Aviso: Configuração de papéis de parede retornou código {0}.", exitCode), ConsoleColor.Yellow);
                }
            }
            else if (File.Exists(cmdScript))
            {
                int exitCode = await RunProcessAsync(cmdScript, "", 60000);
                if (exitCode == 0)
                {
                    Log("[ok] Papel de parede configurado com sucesso.", ConsoleColor.Green);
                }
                else
                {
                    Log(string.Format("X Aviso: Script de wallpaper retornou código {0}.", exitCode), ConsoleColor.Yellow);
                }
            }
            else
            {
                Log("Script de wallpaper não encontrado em: " + psScript, ConsoleColor.Yellow);
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
        /// <param name="options">Opções gerais de instalação fornecidas pelo usuário</param>
        public async Task InstallProgramAsync(ProgramInfo program, InstallationOptions options = null)
        {
            Log(string.Format("Instalando {0}...", program.DisplayName), ConsoleColor.Yellow);

            string fullPath = program.GetFullPath(_basePath);

            if (program.Id == "rustdesk")
            {
                bool hasInternet = await HasInternetConnectionAsync();
                if (hasInternet)
                {
                    Log("Conexão com a internet detectada. Verificando versão mais recente do RustDesk...", ConsoleColor.Cyan);
                    string latestInstaller = await DownloadLatestRustDeskAsync();
                    if (!string.IsNullOrEmpty(latestInstaller) && File.Exists(latestInstaller))
                    {
                        fullPath = latestInstaller;
                        Log("[ok] Instalador da versão mais recente do RustDesk pronto para instalação.", ConsoleColor.Green);

                        string localProgramPath = program.GetFullPath(_basePath);
                        try
                        {
                            if (File.Exists(localProgramPath))
                            {
                                File.Copy(latestInstaller, localProgramPath, true);
                                Log("  Instalador local em dist\\Programas atualizado para a versão mais recente.", ConsoleColor.Gray);
                            }
                        }
                        catch
                        {
                        }
                    }
                    else
                    {
                        Log("  Usando instalador local de Programas.", ConsoleColor.Yellow);
                    }
                }
                else
                {
                    Log("Sem conexão com a internet. Usando instalador local do RustDesk da pasta Programas.", ConsoleColor.Yellow);
                }
            }

            if (program.Id == "citrix")
            {
                bool hasInternet = await HasInternetConnectionAsync();
                if (hasInternet)
                {
                    Log("Conexão com a internet detectada. Verificando versão mais recente do Citrix Workspace...", ConsoleColor.Cyan);
                    string latestCitrix = await DownloadLatestCitrixAsync(fullPath);
                    if (!string.IsNullOrEmpty(latestCitrix) && File.Exists(latestCitrix))
                    {
                        fullPath = latestCitrix;
                    }
                }
                else
                {
                    Log("Sem conexão com a internet. Usando instalador local do Citrix Workspace da pasta Programas.", ConsoleColor.Yellow);
                }

                try
                {
                    string[] citrixProcs = new string[] { "CitrixWorkspaceApp", "Receiver", "wfcrun32" };
                    for (int i = 0; i < citrixProcs.Length; i++)
                    {
                        foreach (var proc in Process.GetProcessesByName(citrixProcs[i]))
                        {
                            try { proc.Kill(); }
                            catch { }
                        }
                    }
                }
                catch
                {
                }
            }

            if (program.Id == "java")
            {
                bool hasInternet = await HasInternetConnectionAsync();
                if (hasInternet)
                {
                    Log("Conexão com a internet detectada. Verificando versão mais recente do Java JRE...", ConsoleColor.Cyan);
                    string latestJava = await DownloadLatestJavaAsync(fullPath);
                    if (!string.IsNullOrEmpty(latestJava) && File.Exists(latestJava))
                    {
                        fullPath = latestJava;

                        string localProgramPath = program.GetFullPath(_basePath);
                        try
                        {
                            if (File.Exists(localProgramPath) && !string.Equals(latestJava, localProgramPath, StringComparison.OrdinalIgnoreCase))
                            {
                                File.Copy(latestJava, localProgramPath, true);
                                Log("  Instalador local em dist\\Programas atualizado para a versão mais recente.", ConsoleColor.Gray);
                            }
                        }
                        catch
                        {
                        }
                    }
                }
                else
                {
                    Log("Sem conexão com a internet. Usando instalador local do Java JRE da pasta Programas.", ConsoleColor.Yellow);
                }

                try
                {
                    string[] javaProcs = new string[] { "java", "javaw", "javaws", "jusched" };
                    for (int i = 0; i < javaProcs.Length; i++)
                    {
                        foreach (var proc in Process.GetProcessesByName(javaProcs[i]))
                        {
                            try { proc.Kill(); }
                            catch { }
                        }
                    }
                }
                catch
                {
                }
            }

            if (IsStorytoldCraftProgram(program.Id))
            {
                bool hasInternet = await HasInternetConnectionAsync();
                if (hasInternet)
                {
                    Log(string.Format("Conexão com a internet detectada. Verificando versão mais recente do {0}...", program.DisplayName), ConsoleColor.Cyan);
                    string latestInstaller = await DownloadLatestStorytoldCraftAsync(program);
                    if (!string.IsNullOrEmpty(latestInstaller) && File.Exists(latestInstaller))
                    {
                        fullPath = latestInstaller;
                        Log(string.Format("[ok] Instalador da versão mais recente do {0} pronto para instalação.", program.DisplayName), ConsoleColor.Green);
                    }
                    else if (File.Exists(fullPath))
                    {
                        Log(string.Format("  Usando instalador local de {0} da pasta Programas.", program.DisplayName), ConsoleColor.Yellow);
                    }
                }
                else
                {
                    if (File.Exists(fullPath))
                    {
                        Log(string.Format("Sem conexão com a internet. Usando instalador local de {0} da pasta Programas.", program.DisplayName), ConsoleColor.Yellow);
                    }
                    else
                    {
                        Log(string.Format("X {0} requer conexão com a internet para download. Conecte-se à internet antes de instalar.", program.DisplayName), ConsoleColor.Red);
                        return;
                    }
                }

                CloseProcessesForStorytoldCraft(program.Id);
            }

            if (program.Id == "powerbi")
            {
                if (IsPowerBIDesktopInstalled())
                {
                    Log("[ok] Microsoft Power BI Desktop já está instalado no sistema.", ConsoleColor.Green);
                    return;
                }

                if (!File.Exists(fullPath))
                {
                    Log(string.Format("X Arquivo não encontrado: {0}", fullPath), ConsoleColor.Red);
                    return;
                }

                try
                {
                    foreach (var proc in Process.GetProcessesByName("PBIDesktopSetup_x64"))
                    {
                        try { proc.Kill(); }
                        catch { }
                    }
                }
                catch { }

                string stagingDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EXABIT", "Setup");
                try
                {
                    if (!Directory.Exists(stagingDir))
                    {
                        Directory.CreateDirectory(stagingDir);
                    }
                }
                catch { }

                string stagedExe = Path.Combine(stagingDir, "PBIDesktopSetup_x64.exe");
                string pbiLog = Path.Combine(stagingDir, "powerbi_install.log");

                try
                {
                    if (!File.Exists(stagedExe) || new FileInfo(stagedExe).Length != new FileInfo(fullPath).Length)
                    {
                        Log("  Preparando arquivos do instalador em área local limpa...", ConsoleColor.Yellow);
                        File.Copy(fullPath, stagedExe, true);
                    }
                }
                catch (Exception ex)
                {
                    Log(string.Format("  Aviso ao preparar instalador: {0}. Utilizando arquivo original.", ex.Message), ConsoleColor.Yellow);
                    stagedExe = fullPath;
                }

                string runDir = Path.GetDirectoryName(stagedExe);
                int pbiTimeout = program.TimeoutMs > 0 ? program.TimeoutMs : 600000;
                string quietArgs = string.Format("-quiet -norestart ACCEPT_EULA=1 -log {0}", QuoteProcessArgument(pbiLog));

                Log("  Executando instalador do Power BI Desktop (aguarde alguns minutos)...", ConsoleColor.Yellow);
                int pbiExit = await RunProcessAsync(stagedExe, quietArgs, pbiTimeout, runDir, true);

                await Task.Delay(2000);
                if (IsPowerBIDesktopInstalled())
                {
                    Log(string.Format("[ok] {0} instalado com sucesso.", program.DisplayName), ConsoleColor.Green);
                    try { if (stagedExe != fullPath && File.Exists(stagedExe)) File.Delete(stagedExe); }
                    catch { }
                    try { if (File.Exists(pbiLog)) File.Delete(pbiLog); }
                    catch { }
                    return;
                }

                Log("  Instalação silenciosa não detectada. Tentando instalação em modo passivo...", ConsoleColor.Yellow);
                string passiveArgs = string.Format("-passive -norestart ACCEPT_EULA=1 -log {0}", QuoteProcessArgument(pbiLog));
                pbiExit = await RunProcessAsync(stagedExe, passiveArgs, pbiTimeout, runDir, true);

                await Task.Delay(2000);
                if (IsPowerBIDesktopInstalled())
                {
                    Log(string.Format("[ok] {0} instalado com sucesso.", program.DisplayName), ConsoleColor.Green);
                    try { if (stagedExe != fullPath && File.Exists(stagedExe)) File.Delete(stagedExe); }
                    catch { }
                    try { if (File.Exists(pbiLog)) File.Delete(pbiLog); }
                    catch { }
                    return;
                }
                else
                {
                    string diag = ExtractPowerBILogSummary(pbiLog);
                    if (!string.IsNullOrEmpty(diag))
                    {
                        Log(string.Format("X {0}: {1}", program.DisplayName, diag), ConsoleColor.Yellow);
                    }
                    else
                    {
                        Log(string.Format("X {0}: O instalador finalizou sem erro, porém os arquivos não foram encontrados no sistema.", program.DisplayName), ConsoleColor.Yellow);
                    }

                    try { if (stagedExe != fullPath && File.Exists(stagedExe)) File.Delete(stagedExe); }
                    catch { }
                    return;
                }
            }

            if (program.Id == "anydesk")
            {
                if (IsAnyDeskInstalled())
                {
                    Log("[ok] AnyDesk já está instalado no sistema.", ConsoleColor.Green);
                    return;
                }

                if (!File.Exists(fullPath))
                {
                    Log(string.Format("X Arquivo não encontrado: {0}", fullPath), ConsoleColor.Red);
                    return;
                }

                try
                {
                    foreach (var proc in Process.GetProcessesByName("AnyDesk"))
                    {
                        try { proc.Kill(); }
                        catch { }
                    }
                }
                catch { }

                string stagingDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EXABIT", "Setup");
                try
                {
                    if (!Directory.Exists(stagingDir))
                    {
                        Directory.CreateDirectory(stagingDir);
                    }
                }
                catch { }

                string stagedExe = Path.Combine(stagingDir, "AnyDesk.exe");
                try
                {
                    if (!File.Exists(stagedExe) || new FileInfo(stagedExe).Length != new FileInfo(fullPath).Length)
                    {
                        File.Copy(fullPath, stagedExe, true);
                    }
                }
                catch (Exception ex)
                {
                    Log(string.Format("  Aviso ao preparar AnyDesk: {0}. Utilizando arquivo original.", ex.Message), ConsoleColor.Yellow);
                    stagedExe = fullPath;
                }

                string runDir = Path.GetDirectoryName(stagedExe);
                int anydeskTimeout = program.TimeoutMs > 0 ? program.TimeoutMs : 120000;

                int anydeskExit = await RunProcessAsync(stagedExe, program.Arguments, anydeskTimeout, runDir, true);

                for (int attempt = 0; attempt < 8; attempt++)
                {
                    await Task.Delay(1000);
                    if (IsAnyDeskInstalled())
                    {
                        break;
                    }
                }

                if (IsAnyDeskInstalled())
                {
                    Log(string.Format("[ok] {0} instalado com sucesso.", program.DisplayName), ConsoleColor.Green);
                    try { if (stagedExe != fullPath && File.Exists(stagedExe)) File.Delete(stagedExe); }
                    catch { }
                    return;
                }
                else
                {
                    Log(string.Format("X {0}: O instalador finalizou (código {1}), porém os arquivos não foram encontrados no sistema.", program.DisplayName, anydeskExit),
                        ConsoleColor.Yellow);
                    try { if (stagedExe != fullPath && File.Exists(stagedExe)) File.Delete(stagedExe); }
                    catch { }
                    return;
                }
            }

            if (!File.Exists(fullPath))
            {
                Log(string.Format("X Arquivo não encontrado: {0}", fullPath), ConsoleColor.Red);
                return;
            }

            if (program.Id == "office365" && !await HasInternetConnectionAsync())
            {
                Log(string.Format("X {0} requer conexão com a internet. Conecte-se à internet antes de instalar.", program.DisplayName),
                    ConsoleColor.Red);
                return;
            }

            try
            {
                string extension = Path.GetExtension(fullPath).ToLower();

                string procWorkDir = Path.GetDirectoryName(fullPath);

                if (program.Id == "office365")
                {
                    string configPath = Path.Combine(procWorkDir, "configuration_o365.xml");
                    EnsureOffice365ConfigurationXml(configPath);
                    program.Arguments = string.Format("/configure {0}", QuoteProcessArgument(configPath));
                }

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

                    if (program.Id == "rustdesk")
                    {
                        string adminPass = options != null ? options.AdminPassword : null;
                        await ConfigureRustDeskServerAsync(adminPass);
                    }
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
                    {
                        Log(string.Format("[ok] {0} instalado com sucesso.", program.DisplayName),
                            ConsoleColor.Green);

                        if (program.Id == "rustdesk")
                        {
                            string adminPass = options != null ? options.AdminPassword : null;
                            await ConfigureRustDeskServerAsync(adminPass);
                        }
                    }
                    else if ((program.Id == "citrix" || program.Id == "office365" || program.Id == "office") && exitCode == 3010)
                    {
                        Log(string.Format("[ok] {0} instalado com sucesso (reinicialização do sistema necessária).", program.DisplayName),
                            ConsoleColor.Green);
                    }
                    else if (program.Id == "citrix" && (exitCode == -2147467259 || exitCode == 1603))
                    {
                        string retryArgs = program.Arguments.Replace("/includeSSON", "").Trim();
                        if (retryArgs.IndexOf("/noreboot", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            retryArgs = (retryArgs + " /noreboot").Trim();
                        }
                        if (retryArgs.IndexOf("/forceinstall", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            retryArgs = (retryArgs + " /forceinstall").Trim();
                        }

                        Log(string.Format("  Falha na instalação do Citrix Workspace (código {0}). Tentando novamente com parâmetros ajustados...", exitCode),
                            ConsoleColor.Yellow);

                        int retryExitCode = await RunProcessAsync(fullPath, retryArgs, timeout, procWorkDir, program.UseShellExecute);
                        if (retryExitCode == 0)
                        {
                            Log(string.Format("[ok] {0} instalado com sucesso na segunda tentativa.", program.DisplayName),
                                ConsoleColor.Green);
                        }
                        else if (retryExitCode == 3010)
                        {
                            Log(string.Format("[ok] {0} instalado com sucesso na segunda tentativa (reinicialização do sistema necessária).", program.DisplayName),
                                ConsoleColor.Green);
                        }
                        else
                        {
                            Log(string.Format("X {0} retornou código {1} na segunda tentativa.", program.DisplayName, retryExitCode),
                                ConsoleColor.Yellow);
                        }
                    }
                    else
                    {
                        Log(string.Format("X {0} retornou código {1}.", program.DisplayName, exitCode),
                            ConsoleColor.Yellow);
                    }
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
        /// Garante que o arquivo de configuração XML do Office 365 Online exista e esteja configurado para pt-br em modo silencioso.
        /// </summary>
        /// <param name="configPath">Caminho completo do arquivo configuration_o365.xml</param>
        private void EnsureOffice365ConfigurationXml(string configPath)
        {
            try
            {
                if (!File.Exists(configPath))
                {
                    string xmlContent =
                        "<Configuration>\r\n" +
                        "  <Add OfficeClientEdition=\"64\" Channel=\"Current\">\r\n" +
                        "    <Product ID=\"O365ProPlusRetail\">\r\n" +
                        "      <Language ID=\"pt-br\" />\r\n" +
                        "    </Product>\r\n" +
                        "  </Add>\r\n" +
                        "  <Display Level=\"None\" AcceptEULA=\"TRUE\" />\r\n" +
                        "  <Property Name=\"AUTOACTIVATE\" Value=\"0\" />\r\n" +
                        "  <Updates Enabled=\"TRUE\" Channel=\"Current\" />\r\n" +
                        "</Configuration>\r\n";

                    File.WriteAllText(configPath, xmlContent, Encoding.UTF8);
                    Log("  Arquivo de configuração do Office 365 em pt-br gerado com sucesso.", ConsoleColor.Gray);
                }
            }
            catch (Exception ex)
            {
                Log(string.Format("  Aviso ao verificar configuração do Office 365: {0}", ex.Message), ConsoleColor.Yellow);
            }
        }

        /// <summary>
        /// Obtém e baixa a versão mais recente do RustDesk para Windows x86_64 via internet.
        /// Consulta a API do GitHub e, se necessário, utiliza redirecionamento da release.
        /// </summary>
        /// <returns>Caminho do instalador baixado em disco, ou null se não for possível obter.</returns>
        private async Task<string> DownloadLatestRustDeskAsync()
        {
            return await Task.Run(new Func<string>(() =>
            {
                try
                {
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

                    string downloadUrl = null;
                    string fileName = null;

                    try
                    {
                        var apiReq = (HttpWebRequest)WebRequest.Create("https://api.github.com/repos/rustdesk/rustdesk/releases/latest");
                        apiReq.Method = "GET";
                        apiReq.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AutoInstaller/1.0";
                        apiReq.Timeout = 8000;
                        apiReq.ReadWriteTimeout = 8000;

                        using (var apiResp = (HttpWebResponse)apiReq.GetResponse())
                        using (var reader = new StreamReader(apiResp.GetResponseStream(), Encoding.UTF8))
                        {
                            string json = reader.ReadToEnd();
                            var match = Regex.Match(json, @"\""browser_download_url\""\s*:\s*\""(https://[^\s\""]*x86_64\.exe)\""", RegexOptions.IgnoreCase);
                            if (match.Success)
                            {
                                downloadUrl = match.Groups[1].Value;
                            }
                        }
                    }
                    catch
                    {
                    }

                    if (string.IsNullOrEmpty(downloadUrl))
                    {
                        try
                        {
                            var tagReq = (HttpWebRequest)WebRequest.Create("https://github.com/rustdesk/rustdesk/releases/latest");
                            tagReq.Method = "GET";
                            tagReq.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AutoInstaller/1.0";
                            tagReq.AllowAutoRedirect = false;
                            tagReq.Timeout = 8000;

                            using (var tagResp = (HttpWebResponse)tagReq.GetResponse())
                            {
                                string location = tagResp.Headers["Location"];
                                if (!string.IsNullOrEmpty(location))
                                {
                                    var tagMatch = Regex.Match(location, @"/tag/([^/?#]+)", RegexOptions.IgnoreCase);
                                    if (tagMatch.Success)
                                    {
                                        string version = tagMatch.Groups[1].Value.TrimStart('v');
                                        downloadUrl = string.Format(
                                            "https://github.com/rustdesk/rustdesk/releases/download/{0}/rustdesk-{0}-x86_64.exe",
                                            version);
                                    }
                                }
                            }
                        }
                        catch
                        {
                        }
                    }

                    if (string.IsNullOrEmpty(downloadUrl))
                    {
                        return null;
                    }

                    try
                    {
                        var uri = new Uri(downloadUrl);
                        fileName = Path.GetFileName(uri.LocalPath);
                    }
                    catch
                    {
                    }

                    if (string.IsNullOrEmpty(fileName))
                    {
                        fileName = "rustdesk_latest_x86_64.exe";
                    }

                    string targetDir = Path.Combine(_basePath, "Programas");
                    bool useLocalAppDir = false;
                    try
                    {
                        if (!Directory.Exists(targetDir))
                        {
                            Directory.CreateDirectory(targetDir);
                        }
                    }
                    catch
                    {
                        useLocalAppDir = true;
                    }

                    if (useLocalAppDir)
                    {
                        targetDir = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "Exabit", "AutoInstall", "Updates");
                        if (!Directory.Exists(targetDir))
                        {
                            Directory.CreateDirectory(targetDir);
                        }
                    }

                    string destFile = Path.Combine(targetDir, fileName);

                    Log(string.Format("  Baixando {0}...", fileName), ConsoleColor.Cyan);

                    using (var webClient = new WebClient())
                    {
                        webClient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AutoInstaller/1.0");
                        webClient.DownloadFile(downloadUrl, destFile);
                    }

                    var fileInfo = new FileInfo(destFile);
                    if (fileInfo.Exists && fileInfo.Length > 1024 * 1024)
                    {
                        return destFile;
                    }

                    return null;
                }
                catch (Exception ex)
                {
                    Log(string.Format("  Aviso ao baixar versão recente do RustDesk: {0}", ex.Message), ConsoleColor.Yellow);
                    return null;
                }
            }));
        }

        /// <summary>
        /// Verifica se a versão local do Citrix Workspace é a mais recente disponível na página oficial da Citrix.
        /// Caso haja versão mais recente ou o instalador local não exista, faz o download do novo instalador.
        /// </summary>
        /// <param name="localPath">Caminho do instalador local na pasta Programas</param>
        /// <returns>Caminho do instalador pronto para execução</returns>
        private async Task<string> DownloadLatestCitrixAsync(string localPath)
        {
            return await Task.Run(new Func<string>(() =>
            {
                try
                {
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

                    string pageHtml = null;
                    try
                    {
                        var req = (HttpWebRequest)WebRequest.Create("https://www.citrix.com/downloads/workspace-app/windows/workspace-app-for-windows-latest.html");
                        req.Method = "GET";
                        req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AutoInstaller/1.0";
                        req.Timeout = 10000;
                        req.ReadWriteTimeout = 10000;

                        using (var resp = (HttpWebResponse)req.GetResponse())
                        using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                        {
                            pageHtml = reader.ReadToEnd();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log(string.Format("  Aviso ao consultar página de download do Citrix: {0}", ex.Message), ConsoleColor.Yellow);
                        return localPath;
                    }

                    if (string.IsNullOrEmpty(pageHtml))
                    {
                        return localPath;
                    }

                    int onlineVerNum = 0;
                    var verMatch = Regex.Match(pageHtml, @"Citrix Workspace app\s+([0-9]{4,})", RegexOptions.IgnoreCase);
                    if (verMatch.Success)
                    {
                        int.TryParse(verMatch.Groups[1].Value, out onlineVerNum);
                    }
                    else
                    {
                        verMatch = Regex.Match(pageHtml, @"Citrix Workspace app\s+([0-9]{2})\.([0-9]{1,2})", RegexOptions.IgnoreCase);
                        if (verMatch.Success)
                        {
                            int maj, min;
                            if (int.TryParse(verMatch.Groups[1].Value, out maj) && int.TryParse(verMatch.Groups[2].Value, out min))
                            {
                                onlineVerNum = (maj * 100) + min;
                            }
                        }
                    }

                    if (File.Exists(localPath))
                    {
                        try
                        {
                            var localInfo = FileVersionInfo.GetVersionInfo(localPath);
                            int localVerNum = (localInfo.FileMajorPart * 100) + localInfo.FileMinorPart;

                            if (onlineVerNum > 0 && localVerNum >= onlineVerNum)
                            {
                                Log(string.Format("  Citrix Workspace local já está na versão mais recente ({0}).", localInfo.FileVersion), ConsoleColor.Green);
                                return localPath;
                            }
                        }
                        catch
                        {
                        }
                    }

                    string downloadUrl = null;
                    var urlMatch = Regex.Match(pageHtml, @"(?i)(?:href|rel)=[""']([^""']*CitrixWorkspaceFullInstaller_x64\.exe[^""']*)[""']");
                    if (!urlMatch.Success)
                    {
                        urlMatch = Regex.Match(pageHtml, @"(?i)(?:href|rel)=[""']([^""']*CitrixWorkspace(?:App|FullInstaller)_x64\.exe[^""']*)[""']");
                    }

                    if (urlMatch.Success)
                    {
                        downloadUrl = urlMatch.Groups[1].Value;
                        if (downloadUrl.StartsWith("//"))
                        {
                            downloadUrl = "https:" + downloadUrl;
                        }
                    }

                    if (string.IsNullOrEmpty(downloadUrl))
                    {
                        Log("  Não foi possível identificar o link de download direto do Citrix. Usando arquivo local.", ConsoleColor.Yellow);
                        return localPath;
                    }

                    string targetDir = Path.Combine(_basePath, "Programas");
                    bool useLocalAppDir = false;
                    try
                    {
                        if (!Directory.Exists(targetDir))
                        {
                            Directory.CreateDirectory(targetDir);
                        }
                    }
                    catch
                    {
                        useLocalAppDir = true;
                    }

                    if (useLocalAppDir)
                    {
                        targetDir = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "Exabit", "AutoInstall", "Updates");
                        if (!Directory.Exists(targetDir))
                        {
                            Directory.CreateDirectory(targetDir);
                        }
                    }

                    string destFile = Path.Combine(targetDir, "CitrixWorkspaceFullInstaller_x64.exe");
                    string tempFile = destFile + ".download";

                    Log(string.Format("  Baixando versão mais recente do Citrix Workspace ({0})...", onlineVerNum > 0 ? onlineVerNum.ToString() : "atualizada"), ConsoleColor.Cyan);

                    try
                    {
                        if (File.Exists(tempFile))
                        {
                            File.Delete(tempFile);
                        }
                    }
                    catch
                    {
                    }

                    using (var webClient = new WebClient())
                    {
                        webClient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AutoInstaller/1.0");
                        webClient.DownloadFile(downloadUrl, tempFile);
                    }

                    var fi = new FileInfo(tempFile);
                    if (fi.Exists && fi.Length > 10 * 1024 * 1024)
                    {
                        try
                        {
                            if (File.Exists(destFile))
                            {
                                File.Delete(destFile);
                            }
                            File.Move(tempFile, destFile);
                            Log("[ok] Instalador da versão mais recente do Citrix Workspace baixado com sucesso.", ConsoleColor.Green);
                            return destFile;
                        }
                        catch
                        {
                            return tempFile;
                        }
                    }

                    try
                    {
                        if (File.Exists(tempFile))
                        {
                            File.Delete(tempFile);
                        }
                    }
                    catch
                    {
                    }

                    Log("  Falha no download da versão mais recente do Citrix. Usando arquivo local.", ConsoleColor.Yellow);
                    return localPath;
                }
                catch (Exception ex)
                {
                    Log(string.Format("  Aviso ao verificar/baixar Citrix Workspace: {0}", ex.Message), ConsoleColor.Yellow);
                    return localPath;
                }
            }));
        }

        /// <summary>
        /// Verifica se a versão local do Java JRE é a mais recente disponível no site oficial java.com.
        /// Caso haja versão mais recente ou o instalador local não exista, faz o download do novo instalador Windows x64.
        /// </summary>
        /// <param name="localPath">Caminho do instalador local na pasta Programas</param>
        /// <returns>Caminho do instalador pronto para execução</returns>
        private async Task<string> DownloadLatestJavaAsync(string localPath)
        {
            return await Task.Run(new Func<string>(() =>
            {
                try
                {
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

                    string pageHtml = null;
                    try
                    {
                        var req = (HttpWebRequest)WebRequest.Create("https://www.java.com/en/download/manual.jsp");
                        req.Method = "GET";
                        req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AutoInstaller/1.0";
                        req.Timeout = 10000;
                        req.ReadWriteTimeout = 10000;

                        using (var resp = (HttpWebResponse)req.GetResponse())
                        using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                        {
                            pageHtml = reader.ReadToEnd();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log(string.Format("  Aviso ao consultar página oficial do Java: {0}", ex.Message), ConsoleColor.Yellow);
                        return localPath;
                    }

                    if (string.IsNullOrEmpty(pageHtml))
                    {
                        return localPath;
                    }

                    int onlineUpdate = 0;
                    var verMatch = Regex.Match(pageHtml, @"(?:Version\s+8\s+Update\s+(\d+)|Java\s+8\s+Update\s+(\d+)|8u(\d+))", RegexOptions.IgnoreCase);
                    if (verMatch.Success)
                    {
                        for (int i = 1; i <= 3; i++)
                        {
                            if (!string.IsNullOrEmpty(verMatch.Groups[i].Value))
                            {
                                int.TryParse(verMatch.Groups[i].Value, out onlineUpdate);
                                break;
                            }
                        }
                    }

                    int localUpdate = 0;
                    if (File.Exists(localPath))
                    {
                        try
                        {
                            var localInfo = FileVersionInfo.GetVersionInfo(localPath);
                            var match = Regex.Match(localInfo.ProductName ?? "", @"\bU(\d+)\b", RegexOptions.IgnoreCase);
                            if (match.Success)
                            {
                                int.TryParse(match.Groups[1].Value, out localUpdate);
                            }
                            else if (localInfo.FileBuildPart > 0)
                            {
                                localUpdate = localInfo.FileBuildPart / 10;
                            }

                            if (onlineUpdate > 0 && localUpdate >= onlineUpdate)
                            {
                                Log(string.Format("  Java JRE local já está na versão mais recente (Update {0}).", localUpdate), ConsoleColor.Green);
                                return localPath;
                            }
                        }
                        catch
                        {
                        }
                    }

                    string downloadUrl = null;
                    var urlMatch = Regex.Match(pageHtml, @"<a\s+[^>]*href=[""'](?<url>https://javadl\.oracle\.com/webapps/download/AutoDL\?BundleId=[^""']+)[""'][^>]*>.*?64.*?</a>", RegexOptions.IgnoreCase);
                    if (urlMatch.Success)
                    {
                        downloadUrl = urlMatch.Groups["url"].Value;
                    }

                    if (string.IsNullOrEmpty(downloadUrl))
                    {
                        Log("  Não foi possível identificar o link de download direto do Java JRE. Usando arquivo local.", ConsoleColor.Yellow);
                        return localPath;
                    }

                    string targetDir = Path.GetDirectoryName(localPath);
                    if (string.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir))
                    {
                        targetDir = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                            "Exabit", "AutoInstall", "Updates");
                        if (!Directory.Exists(targetDir))
                        {
                            Directory.CreateDirectory(targetDir);
                        }
                    }

                    string destFile = Path.Combine(targetDir, "jre-windows-x64.exe");
                    string tempFile = destFile + ".download";

                    string verLabel = onlineUpdate > 0 ? string.Format("Update {0}", onlineUpdate) : "mais recente";
                    Log(string.Format("  Baixando versão mais recente do Java JRE ({0})...", verLabel), ConsoleColor.Cyan);

                    try
                    {
                        if (File.Exists(tempFile))
                        {
                            File.Delete(tempFile);
                        }

                        using (var webClient = new WebClient())
                        {
                            webClient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AutoInstaller/1.0");
                            webClient.DownloadFile(downloadUrl, tempFile);
                        }

                        var tempInfo = new FileInfo(tempFile);
                        if (tempInfo.Exists && tempInfo.Length > 10 * 1024 * 1024)
                        {
                            if (File.Exists(destFile))
                            {
                                File.Delete(destFile);
                            }
                            File.Move(tempFile, destFile);

                            Log("[ok] Versão mais recente do Java JRE baixada com sucesso.", ConsoleColor.Green);
                            return destFile;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log(string.Format("  Aviso ao baixar atualização do Java JRE: {0}", ex.Message), ConsoleColor.Yellow);
                    }

                    return localPath;
                }
                catch (Exception ex)
                {
                    Log(string.Format("  Aviso ao verificar versão do Java JRE: {0}", ex.Message), ConsoleColor.Yellow);
                    return localPath;
                }
            }));
        }

        /// <summary>
        /// Verifica se o identificador pertence a um dos aplicativos da suíte Craft da Storytold.
        /// </summary>
        private static bool IsStorytoldCraftProgram(string id)
        {
            return id == "artcraft" || id == "cadcraft" || id == "deckcraft" ||
                   id == "filmcraft" || id == "gridcraft" || id == "pdfcraft" ||
                   id == "photocraft" || id == "soundcraft" || id == "vectorcraft" ||
                   id == "wordcraft";
        }

        /// <summary>
        /// Retorna o nome do repositório no GitHub sob a organização storytold para o ID informado.
        /// </summary>
        private static string GetStorytoldRepoName(string id)
        {
            switch (id)
            {
                case "artcraft": return "artcraft";
                case "cadcraft": return "cadcraft";
                case "deckcraft": return "deckcraft";
                case "filmcraft": return "filmcraft";
                case "gridcraft": return "gridcraft";
                case "pdfcraft": return "pdfcraft";
                case "photocraft": return "photocraft";
                case "soundcraft": return "soundcraft";
                case "vectorcraft": return "vectorcraft";
                case "wordcraft": return "wordcraft";
                default: return null;
            }
        }

        /// <summary>
        /// Encerra preventivamente processos em execução do aplicativo Craft informado para evitar bloqueios de arquivo em uso.
        /// </summary>
        private static void CloseProcessesForStorytoldCraft(string id)
        {
            try
            {
                string[] procs;
                switch (id)
                {
                    case "artcraft": procs = new string[] { "artcraft", "ArtCraft" }; break;
                    case "cadcraft": procs = new string[] { "cadcraft", "CADCraft" }; break;
                    case "deckcraft": procs = new string[] { "deckcraft", "DeckCraft" }; break;
                    case "filmcraft": procs = new string[] { "filmcraft", "FilmCraft" }; break;
                    case "gridcraft": procs = new string[] { "gridcraft", "GridCraft" }; break;
                    case "pdfcraft": procs = new string[] { "pdfcraft", "PdfCraft" }; break;
                    case "photocraft": procs = new string[] { "photocraft", "PhotoCraft" }; break;
                    case "soundcraft": procs = new string[] { "soundcraft", "SoundCraft" }; break;
                    case "vectorcraft": procs = new string[] { "vectorcraft", "VectorCraft" }; break;
                    case "wordcraft": procs = new string[] { "wordcraft", "WordCraft" }; break;
                    default: procs = new string[0]; break;
                }

                for (int i = 0; i < procs.Length; i++)
                {
                    foreach (var proc in Process.GetProcessesByName(procs[i]))
                    {
                        try { proc.Kill(); }
                        catch { }
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Sincroniza uma cópia do arquivo baixado para o nome genérico configurado em dist\Programas,
        /// permitindo futuras instalações offline do mesmo aplicativo.
        /// </summary>
        private static void SyncLocalCopy(string downloadedFile, string targetDir, string genericFileName)
        {
            if (string.IsNullOrEmpty(genericFileName) || string.IsNullOrEmpty(downloadedFile))
                return;

            try
            {
                string genericPath = Path.Combine(targetDir, genericFileName);
                if (!string.Equals(downloadedFile, genericPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(downloadedFile, genericPath, true);
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Baixa ou atualiza a versão mais recente dos aplicativos Craft da Storytold a partir do GitHub Releases.
        /// Suporta consulta direta via API do GitHub com fallback por cabeçalho Location/redirecionamento HTML.
        /// </summary>
        /// <param name="program">Informações do programa Craft a ser baixado</param>
        /// <returns>Caminho completo do instalador em disco, ou null em caso de falha</returns>
        private async Task<string> DownloadLatestStorytoldCraftAsync(ProgramInfo program)
        {
            return await Task.Run(new Func<string>(() =>
            {
                try
                {
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

                    string repo = GetStorytoldRepoName(program.Id);
                    if (string.IsNullOrEmpty(repo))
                    {
                        return null;
                    }

                    string downloadUrl = null;
                    string fileName = null;
                    bool isArtCraft = program.Id == "artcraft";

                    string assetPattern = isArtCraft
                        ? @"\""browser_download_url\""\s*:\s*\""(https://[^\s\""]*(?:ArtCraft[^\s\""]*_x64-setup\.exe|[^\s\""]*setup\.exe))\"""
                        : string.Format(@"\""browser_download_url\""\s*:\s*\""(https://[^\s\""]*{0}[^\s\""]*-windows-x64\.msi)\""", repo);

                    // 1. Tenta obter via API do GitHub
                    try
                    {
                        var apiReq = (HttpWebRequest)WebRequest.Create(
                            string.Format("https://api.github.com/repos/storytold/{0}/releases/latest", repo));
                        apiReq.Method = "GET";
                        apiReq.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AutoInstaller/1.0";
                        apiReq.Timeout = 8000;
                        apiReq.ReadWriteTimeout = 8000;

                        using (var apiResp = (HttpWebResponse)apiReq.GetResponse())
                        using (var reader = new StreamReader(apiResp.GetResponseStream(), Encoding.UTF8))
                        {
                            string json = reader.ReadToEnd();
                            var match = Regex.Match(json, assetPattern, RegexOptions.IgnoreCase);
                            if (match.Success)
                            {
                                downloadUrl = match.Groups[1].Value;
                            }
                        }
                    }
                    catch
                    {
                    }

                    // 2. Fallback: Se API falhar (ex: rate limit 403), consulta redirecionamento em /releases/latest
                    if (string.IsNullOrEmpty(downloadUrl))
                    {
                        try
                        {
                            var tagReq = (HttpWebRequest)WebRequest.Create(
                                string.Format("https://github.com/storytold/{0}/releases/latest", repo));
                            tagReq.Method = "GET";
                            tagReq.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AutoInstaller/1.0";
                            tagReq.AllowAutoRedirect = false;
                            tagReq.Timeout = 8000;

                            using (var tagResp = (HttpWebResponse)tagReq.GetResponse())
                            {
                                string location = tagResp.Headers["Location"];
                                if (!string.IsNullOrEmpty(location))
                                {
                                    var tagMatch = Regex.Match(location, @"/tag/([^/?#]+)", RegexOptions.IgnoreCase);
                                    if (tagMatch.Success)
                                    {
                                        string tag = tagMatch.Groups[1].Value;
                                        string version = tag.TrimStart('v');
                                        if (isArtCraft)
                                        {
                                            var vMatch = Regex.Match(tag, @"(\d+\.\d+\.\d+)");
                                            if (vMatch.Success)
                                            {
                                                version = vMatch.Groups[1].Value;
                                            }
                                            downloadUrl = string.Format(
                                                "https://github.com/storytold/artcraft/releases/download/{0}/ArtCraft_{1}_x64-setup.exe",
                                                tag, version);
                                        }
                                        else
                                        {
                                            downloadUrl = string.Format(
                                                "https://github.com/storytold/{0}/releases/download/{1}/{0}-{2}-windows-x64.msi",
                                                repo, tag, version);
                                        }
                                    }
                                }
                            }
                        }
                        catch
                        {
                        }
                    }

                    if (string.IsNullOrEmpty(downloadUrl))
                    {
                        return null;
                    }

                    try
                    {
                        var uri = new Uri(downloadUrl);
                        fileName = Path.GetFileName(uri.LocalPath);
                    }
                    catch
                    {
                    }

                    if (string.IsNullOrEmpty(fileName))
                    {
                        fileName = isArtCraft ? "ArtCraft_setup.exe" : string.Format("{0}-windows-x64.msi", repo);
                    }

                    // Define pasta destino
                    string targetDir = Path.Combine(_basePath, "Programas");
                    bool useLocalAppDir = false;
                    try
                    {
                        if (!Directory.Exists(targetDir))
                        {
                            Directory.CreateDirectory(targetDir);
                        }
                    }
                    catch
                    {
                        useLocalAppDir = true;
                    }

                    if (useLocalAppDir)
                    {
                        targetDir = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "Exabit", "AutoInstall", "Updates");
                        if (!Directory.Exists(targetDir))
                        {
                            Directory.CreateDirectory(targetDir);
                        }
                    }

                    string destFile = Path.Combine(targetDir, fileName);

                    // Verifica se este instalador exato já foi baixado previamente
                    if (File.Exists(destFile))
                    {
                        var existingInfo = new FileInfo(destFile);
                        if (existingInfo.Length > 2 * 1024 * 1024)
                        {
                            Log(string.Format("  Instalador da versão mais recente ({0}) já está baixado.", fileName), ConsoleColor.Green);
                            SyncLocalCopy(destFile, targetDir, program.FileName);
                            return destFile;
                        }
                    }

                    string tempFile = destFile + ".download";
                    try
                    {
                        if (File.Exists(tempFile))
                        {
                            File.Delete(tempFile);
                        }
                    }
                    catch
                    {
                    }

                    Log(string.Format("  Baixando versão mais recente do {0} ({1})...", program.DisplayName, fileName), ConsoleColor.Cyan);

                    using (var webClient = new WebClient())
                    {
                        webClient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AutoInstaller/1.0");
                        webClient.DownloadFile(downloadUrl, tempFile);
                    }

                    var tempInfo = new FileInfo(tempFile);
                    if (tempInfo.Exists && tempInfo.Length > 2 * 1024 * 1024)
                    {
                        if (File.Exists(destFile))
                        {
                            try { File.Delete(destFile); } catch { }
                        }
                        File.Move(tempFile, destFile);

                        SyncLocalCopy(destFile, targetDir, program.FileName);

                        Log(string.Format("[ok] Download do {0} concluído com sucesso.", program.DisplayName), ConsoleColor.Green);
                        return destFile;
                    }

                    return null;
                }
                catch (Exception ex)
                {
                    Log(string.Format("  Aviso ao verificar/baixar {0}: {1}", program.DisplayName, ex.Message), ConsoleColor.Yellow);
                    return null;
                }
            }));
        }

        /// <summary>
        /// Verifica se o Microsoft Power BI Desktop já está instalado na máquina,
        /// consultando o sistema de arquivos e o registro do Windows (64-bit e 32-bit).
        /// </summary>
        /// <returns>true se o Power BI Desktop estiver instalado; caso contrário, false.</returns>
        private static bool IsPowerBIDesktopInstalled()
        {
            try
            {
                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string exePath = Path.Combine(progFiles, "Microsoft Power BI Desktop", "bin", "PBIDesktop.exe");
                if (File.Exists(exePath))
                {
                    return true;
                }

                string exePathDirect = Path.Combine(progFiles, "Microsoft Power BI Desktop", "PBIDesktop.exe");
                if (File.Exists(exePathDirect))
                {
                    return true;
                }

                string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                string exePathX86 = Path.Combine(progFilesX86, "Microsoft Power BI Desktop", "bin", "PBIDesktop.exe");
                if (File.Exists(exePathX86))
                {
                    return true;
                }

                string exePathX86Direct = Path.Combine(progFilesX86, "Microsoft Power BI Desktop", "PBIDesktop.exe");
                if (File.Exists(exePathX86Direct))
                {
                    return true;
                }

                var views = new Microsoft.Win32.RegistryView[] {
                    Microsoft.Win32.RegistryView.Registry64,
                    Microsoft.Win32.RegistryView.Registry32
                };

                for (int v = 0; v < views.Length; v++)
                {
                    using (var hklm = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, views[v]))
                    using (var uninstallKey = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                    {
                        if (uninstallKey != null)
                        {
                            foreach (string subKeyName in uninstallKey.GetSubKeyNames())
                            {
                                using (var subKey = uninstallKey.OpenSubKey(subKeyName))
                                {
                                    if (subKey != null)
                                    {
                                        string displayName = subKey.GetValue("DisplayName") as string;
                                        if (!string.IsNullOrEmpty(displayName) && displayName.IndexOf("Power BI Desktop", StringComparison.OrdinalIgnoreCase) >= 0)
                                        {
                                            return true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static string ExtractPowerBILogSummary(string logPath)
        {
            try
            {
                if (!File.Exists(logPath))
                    return null;

                string[] lines = File.ReadAllLines(logPath);
                for (int i = lines.Length - 1; i >= 0 && i >= lines.Length - 60; i--)
                {
                    string line = lines[i];
                    if (line.IndexOf("Error 0x", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        int idx = line.IndexOf("Error 0x", StringComparison.OrdinalIgnoreCase);
                        return line.Substring(idx).Trim();
                    }
                    if (line.IndexOf("Apply complete, result: 0x", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        int idx = line.IndexOf("Apply complete", StringComparison.OrdinalIgnoreCase);
                        return line.Substring(idx).Trim();
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        /// <summary>
        /// Verifica se o AnyDesk já está instalado na máquina,
        /// consultando o sistema de arquivos e o registro do Windows (64-bit e 32-bit).
        /// </summary>
        /// <returns>true se o AnyDesk estiver instalado; caso contrário, false.</returns>
        private static bool IsAnyDeskInstalled()
        {
            try
            {
                string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                string exePathX86 = Path.Combine(progFilesX86, "AnyDesk", "AnyDesk.exe");
                if (File.Exists(exePathX86))
                {
                    return true;
                }

                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string exePath = Path.Combine(progFiles, "AnyDesk", "AnyDesk.exe");
                if (File.Exists(exePath))
                {
                    return true;
                }

                var views = new Microsoft.Win32.RegistryView[] {
                    Microsoft.Win32.RegistryView.Registry64,
                    Microsoft.Win32.RegistryView.Registry32
                };

                for (int v = 0; v < views.Length; v++)
                {
                    using (var hklm = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, views[v]))
                    using (var uninstallKey = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                    {
                        if (uninstallKey != null)
                        {
                            using (var subKey = uninstallKey.OpenSubKey("AnyDesk"))
                            {
                                if (subKey != null)
                                    return true;
                            }

                            foreach (string subKeyName in uninstallKey.GetSubKeyNames())
                            {
                                using (var subKey = uninstallKey.OpenSubKey(subKeyName))
                                {
                                    if (subKey != null)
                                    {
                                        string displayName = subKey.GetValue("DisplayName") as string;
                                        if (!string.IsNullOrEmpty(displayName) && displayName.IndexOf("AnyDesk", StringComparison.OrdinalIgnoreCase) >= 0)
                                        {
                                            return true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        /// <summary>
        /// Configura o RustDesk com o servidor próprio da EXABIT (rustdesk.exabit.com.br),
        /// chave pública, modos de permissão (senha temporária, aprovação do usuário e senha definitiva)
        /// e define a senha definitiva igual à senha informada para o usuário Admin.
        /// </summary>
        /// <param name="adminPassword">Senha do usuário Admin para definir como senha definitiva</param>
        public async Task ConfigureRustDeskServerAsync(string adminPassword = null)
        {
            Log("Configurando servidor EXABIT e acessos no RustDesk...", ConsoleColor.Yellow);

            try
            {
                // 1. Para o serviço e processos do RustDesk
                await RunProcessAsync("net", "stop RustDesk", 15000, null, false, true);

                try
                {
                    foreach (var proc in Process.GetProcessesByName("rustdesk"))
                    {
                        try { proc.Kill(); }
                        catch { }
                    }
                }
                catch { }

                await Task.Delay(2000);

                // 2. Monta a lista de diretórios de configuração do RustDesk
                var targetDirs = new List<string>();

                // Perfil do serviço Windows (LocalService)
                string localServiceDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    "ServiceProfiles", "LocalService", "AppData", "Roaming", "RustDesk", "config");
                targetDirs.Add(localServiceDir);

                string usersFolder = Environment.GetEnvironmentVariable("SystemDrive") + "\\Users";

                // Perfil Default (template para novos usuários)
                targetDirs.Add(Path.Combine(usersFolder, "Default", "AppData", "Roaming", "RustDesk", "config"));

                // Perfis existentes em C:\Users
                if (Directory.Exists(usersFolder))
                {
                    foreach (string userDir in Directory.GetDirectories(usersFolder))
                    {
                        string dirName = Path.GetFileName(userDir);
                        if (string.Equals(dirName, "Public", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(dirName, "Default User", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(dirName, "All Users", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        string configDir = Path.Combine(userDir, "AppData", "Roaming", "RustDesk", "config");
                        if (!targetDirs.Contains(configDir))
                        {
                            targetDirs.Add(configDir);
                        }
                    }
                }

                // Conteúdo do RustDesk2.toml para o servidor EXABIT com permissões
                const string serverHost = "rustdesk.exabit.com.br";
                const string publicKey = "vh1s9x96k3ReaRVL2z+KtgPAAhePvFhiluSZpqipmQ8=";

                string tomlContent = string.Format(
                    "rendezvous_server = '{0}:21116'\r\n" +
                    "nat_type = 1\r\n" +
                    "serial = 0\r\n" +
                    "unlock_pin = ''\r\n\r\n" +
                    "[options]\r\n" +
                    "custom-rendezvous-server = '{0}'\r\n" +
                    "relay-server = '{0}'\r\n" +
                    "key = '{1}'\r\n" +
                    "api-server = ''\r\n" +
                    "access_token = ''\r\n" +
                    "user_info = ''\r\n" +
                    "approve-mode = 'password-click'\r\n" +
                    "verification-method = 'use-both-passwords'\r\n",
                    serverHost, publicKey);

                var utf8NoBom = new UTF8Encoding(false);
                var authRegex = new Regex(
                    @"^\s*(access_token|user_info|api-server|login_device_token|oauth_.*|oidc_.*|account_.*|token)\s*=\s*('[^']*'|""[^""]*""|[^\r\n]*)\r?\n?",
                    RegexOptions.Multiline);

                string[] tomlFilesToClean = new string[] { "RustDesk.toml", "RustDesk_default.toml", "RustDesk_local.toml" };

                // 3. Aplica em cada diretório alvo
                foreach (string dir in targetDirs)
                {
                    try
                    {
                        if (!Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }

                        // Limpa chaves de autenticação/conta nos arquivos existentes e atualiza approve-mode / verification-method
                        foreach (string tomlFile in tomlFilesToClean)
                        {
                            string fullPath = Path.Combine(dir, tomlFile);
                            if (File.Exists(fullPath))
                            {
                                try
                                {
                                    string raw = File.ReadAllText(fullPath);
                                    if (!string.IsNullOrEmpty(raw))
                                    {
                                        string clean = authRegex.Replace(raw, string.Empty);
                                        clean = Regex.Replace(clean, @"approve-mode\s*=\s*('[^']*'|""[^""]*""|[^\r\n]*)", "approve-mode = 'password-click'");
                                        clean = Regex.Replace(clean, @"verification-method\s*=\s*('[^']*'|""[^""]*""|[^\r\n]*)", "verification-method = 'use-both-passwords'");
                                        File.WriteAllText(fullPath, clean, utf8NoBom);
                                    }
                                }
                                catch { }
                            }
                        }

                        // Grava RustDesk2.toml com os dados do servidor EXABIT
                        string toml2Path = Path.Combine(dir, "RustDesk2.toml");
                        File.WriteAllText(toml2Path, tomlContent, utf8NoBom);
                    }
                    catch (Exception ex)
                    {
                        Log(string.Format("  X Aviso ao configurar RustDesk em {0}: {1}", dir, ex.Message), ConsoleColor.Yellow);
                    }
                }

                // 4. Inicia o serviço RustDesk
                await RunProcessAsync("net", "start RustDesk", 15000, null, false, true);
                await Task.Delay(3000);

                // 5. Configura permissões e senha via CLI do executável
                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string exePath = Path.Combine(progFiles, "RustDesk", "rustdesk.exe");
                if (File.Exists(exePath))
                {
                    try
                    {
                        await RunProcessAsync(exePath, "--option approve-mode password-click", 10000, null, false, true);
                        await RunProcessAsync(exePath, "--option verification-method use-both-passwords", 10000, null, false, true);

                        if (!string.IsNullOrWhiteSpace(adminPassword))
                        {
                            string safePass = QuoteProcessArgument(adminPassword);
                            await RunProcessAsync(exePath, string.Format("--password {0}", safePass), 10000, null, false, true);
                            Log("[ok] Senha definitiva configurada no RustDesk (mesma senha do Admin).", ConsoleColor.Green);
                        }
                        else
                        {
                            Log("  Aviso: Senha do Admin não informada. Senha definitiva do RustDesk não configurada.", ConsoleColor.Yellow);
                        }

                        Process.Start(new ProcessStartInfo
                        {
                            FileName = exePath,
                            UseShellExecute = true,
                        });
                    }
                    catch { }
                }

                Log("[ok] RustDesk configurado para o servidor EXABIT (rustdesk.exabit.com.br).", ConsoleColor.Green);
                Log("     Acesso permitido com senha temporária, aprovação do usuário ou senha definitiva.", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                Log(string.Format("X Erro ao configurar servidor do RustDesk: {0}", ex.Message), ConsoleColor.Red);
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
                await EnsureGlobalPasswordNeverExpiresPolicyAsync();

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

                // Configura e ativa os Serviços de Localização do Windows para o Admin e o Usuário
                if (!cancelled && options.EnableLocationServices)
                {
                    await ConfigureLocationServicesAsync();
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
                        await InstallProgramAsync(program, options);
                    }
                }

                if (!cancelled)
                {
                    // Limpa atalhos de todas as áreas de trabalho no final de todas as instalações (se selecionado)
                    if (options.CleanDesktopShortcuts)
                    {
                        string stdUserName = options.CreateStandardUser ? options.StandardUserName : null;
                        CleanAllDesktops(stdUserName);
                    }

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
        /// os perfis (público, admin, usuário padrão, usuário atual e Default)
        /// no final de todas as instalações para deixar a área de trabalho limpa,
        /// recriando apenas os atalhos solicitados pelo usuário.
        /// </summary>
        private void CleanAllDesktops(string standardUserName)
        {
            Log("Limpando atalhos de todas as áreas de trabalho no final de todas as instalações...", ConsoleColor.Yellow);

            var desktopPaths = new List<string>();

            // Desktop público (afeta todos os usuários)
            try
            {
                string publicDesktop = Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonDesktopDirectory);
                if (!string.IsNullOrEmpty(publicDesktop) && !desktopPaths.Contains(publicDesktop))
                {
                    desktopPaths.Add(publicDesktop);
                }
            }
            catch { }

            // Desktop do usuário atual em execução
            try
            {
                string currentDesktop = Environment.GetFolderPath(
                    Environment.SpecialFolder.DesktopDirectory);
                if (!string.IsNullOrEmpty(currentDesktop) && !desktopPaths.Contains(currentDesktop))
                {
                    desktopPaths.Add(currentDesktop);
                }
            }
            catch { }

            string usersFolder = Environment.GetEnvironmentVariable("SystemDrive") + "\\Users";

            // Desktop do admin
            string adminDesktop = Path.Combine(usersFolder, "admin", "Desktop");
            if (!desktopPaths.Contains(adminDesktop))
            {
                desktopPaths.Add(adminDesktop);
            }

            // Desktop do usuário padrão (se informado)
            if (!string.IsNullOrWhiteSpace(standardUserName))
            {
                string stdDesktop = Path.Combine(usersFolder, standardUserName, "Desktop");
                if (!desktopPaths.Contains(stdDesktop))
                {
                    desktopPaths.Add(stdDesktop);
                }
            }

            // Template Default - usado para criar desktop de NOVOS usuários
            string defaultDesktop = Path.Combine(usersFolder, "Default", "Desktop");
            if (!desktopPaths.Contains(defaultDesktop))
            {
                desktopPaths.Add(defaultDesktop);
            }

            // Varre diretórios de usuários reais existentes em C:\Users (ignora junções do Windows)
            try
            {
                if (Directory.Exists(usersFolder))
                {
                    foreach (string userDir in Directory.GetDirectories(usersFolder))
                    {
                        try
                        {
                            var dirInfo = new DirectoryInfo(userDir);

                            // Ignora pontos de junção/links simbólicos (ex: "All Users", "Todos os Usuários", "Default User", "Usuário Padrão")
                            if ((dirInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                                continue;

                            string dirName = dirInfo.Name;
                            if (string.Equals(dirName, "Public", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(dirName, "Público", StringComparison.OrdinalIgnoreCase))
                                continue;

                            string userDesktop = Path.Combine(userDir, "Desktop");
                            if (!desktopPaths.Contains(userDesktop))
                            {
                                desktopPaths.Add(userDesktop);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }

            foreach (string desktop in desktopPaths)
            {
                try
                {
                    if (string.IsNullOrEmpty(desktop))
                        continue;

                    // Cria o diretório apenas se for o Default\Desktop para que novos usuários recebam o template limpo
                    if (string.Equals(desktop, defaultDesktop, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!Directory.Exists(desktop))
                        {
                            Directory.CreateDirectory(desktop);
                        }
                    }
                    else if (!Directory.Exists(desktop))
                    {
                        continue;
                    }

                    // Ignora se o próprio caminho Desktop for um ponto de junção
                    var di = new DirectoryInfo(desktop);
                    if ((di.Attributes & FileAttributes.ReparsePoint) != 0)
                        continue;

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
                catch (UnauthorizedAccessException)
                {
                    // Ignora silenciosamente pastas com proteção especial ou restrição do sistema
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
        /// Cria atalhos de suporte exclusivamente na área de trabalho pública (CommonDesktopDirectory)
        /// para que todos os perfis (admin, usuário padrão e novos usuários) os vejam exatamente uma vez,
        /// removendo eventuais cópias privadas dos perfis individuais para evitar ícones duplicados.
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

            Log("Criando atalhos na área de trabalho...", ConsoleColor.Yellow);

            // 1. Remove cópias privadas nos perfis individuais para evitar que o Windows exiba o atalho em duplicidade
            try
            {
                var privateDesktops = new List<string>();

                string currentDesktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                if (!string.IsNullOrEmpty(currentDesktop) && !privateDesktops.Contains(currentDesktop))
                {
                    privateDesktops.Add(currentDesktop);
                }

                string usersFolder = Environment.GetEnvironmentVariable("SystemDrive") + "\\Users";
                string adminDesktop = Path.Combine(usersFolder, "admin", "Desktop");
                if (!privateDesktops.Contains(adminDesktop))
                {
                    privateDesktops.Add(adminDesktop);
                }

                string stdUserName = options.CreateStandardUser ? options.StandardUserName : null;
                if (!string.IsNullOrWhiteSpace(stdUserName))
                {
                    string stdDesktop = Path.Combine(usersFolder, stdUserName, "Desktop");
                    if (!privateDesktops.Contains(stdDesktop))
                    {
                        privateDesktops.Add(stdDesktop);
                    }
                }

                string defaultDesktop = Path.Combine(usersFolder, "Default", "Desktop");
                if (!privateDesktops.Contains(defaultDesktop))
                {
                    privateDesktops.Add(defaultDesktop);
                }

                foreach (string privDesk in privateDesktops)
                {
                    if (Directory.Exists(privDesk))
                    {
                        foreach (string key in shortcuts.Keys)
                        {
                            string oldUrl = Path.Combine(privDesk, key + ".url");
                            if (File.Exists(oldUrl))
                            {
                                try { File.Delete(oldUrl); }
                                catch { }
                            }
                        }
                    }
                }
            }
            catch { }

            // 2. Cria exclusivamente no Desktop Público para que todos os usuários vejam exatamente UMA vez
            string publicDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
            if (string.IsNullOrEmpty(publicDesktop) || !Directory.Exists(publicDesktop))
            {
                publicDesktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            }

            try
            {
                if (!Directory.Exists(publicDesktop))
                {
                    Directory.CreateDirectory(publicDesktop);
                }

                foreach (var kvp in shortcuts)
                {
                    string urlPath = Path.Combine(publicDesktop, kvp.Key + ".url");
                    File.WriteAllText(urlPath, "[InternetShortcut]\r\nURL=" + kvp.Value);
                }

                Log("[ok] Atalhos criados na área de trabalho (visíveis para todos os usuários).", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                Log(string.Format("X Aviso ao criar atalhos: {0}", ex.Message), ConsoleColor.Yellow);
            }

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

            if (useShell)
            {
                psi.Verb = "runas";
            }

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
        public bool EnableLocationServices { get; set; }

        /// <summary>
        /// Se true, limpa todos os atalhos das áreas de trabalho no final de todas as instalações.
        /// </summary>
        public bool CleanDesktopShortcuts { get; set; }

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
        /// Lista dos programas selecionados para instalação.
        /// </summary>
        public List<ProgramInfo> SelectedPrograms { get; set; }

        public InstallationOptions()
        {
            EnableLocationServices = true;
            CleanDesktopShortcuts = true;
            SelectedPrograms = new List<ProgramInfo>();
        }
    }
}
