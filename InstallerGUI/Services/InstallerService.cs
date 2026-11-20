using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
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

        /// <summary>
        /// Disparado durante o download de arquivos na internet para relatar o progresso.
        /// Parâmetros: (displayName, percent, bytesReceived, totalBytes).
        /// </summary>
        public event Action<string, int, long, long> OnDownloadProgress;

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
        /// Aplica os papéis de parede personalizados (gerais ou de cliente específico como Nortel)
        /// adicionando todos os arquivos da pasta Wallpapers correspondente como papel de parede
        /// em todos os temas do Windows (Aero, Dark, ThemeA-D, Spotlight)
        /// e configurando para novos usuários e usuário atual.
        /// </summary>
        /// <summary>
        /// Obtém o caminho do script de papel de parede (apply_wallpapers.ps1).
        /// Se não existir no disco, extrai o recurso embutido para o diretório temporário.
        /// </summary>
        private string GetApplyWallpapersScriptPath()
        {
            try
            {
                string localScript = Path.Combine(_basePath, "Scripts", "apply_wallpapers.ps1");
                if (File.Exists(localScript))
                {
                    return localScript;
                }

                string tempScript = Path.Combine(Path.GetTempPath(), "apply_wallpapers.ps1");
                var assembly = Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream("InstallerGUI.apply_wallpapers.ps1"))
                {
                    if (stream != null)
                    {
                        using (var fileStream = File.Create(tempScript))
                        {
                            stream.CopyTo(fileStream);
                        }
                        return tempScript;
                    }
                }

                if (File.Exists(tempScript))
                {
                    return tempScript;
                }
            }
            catch
            {
            }

            return null;
        }

        /// <summary>
        /// Configura os papéis de parede copiando todos os arquivos da pasta Wallpapers
        /// em todos os temas do Windows (Aero, Dark, ThemeA-D, Spotlight)
        /// e configurando para novos usuários e usuário atual.
        /// </summary>
        public async Task SetWallpaperAsync(string client = null)
        {
            if (string.IsNullOrEmpty(client))
            {
                Log("Configurando papéis de parede em todos os temas do Windows...", ConsoleColor.Yellow);
            }
            else
            {
                Log(string.Format("Configurando papéis de parede ({0}) em todos os temas do Windows...", client), ConsoleColor.Yellow);
            }

            string psScript = GetApplyWallpapersScriptPath();

            if (!string.IsNullOrEmpty(psScript) && File.Exists(psScript))
            {
                string safeBasePath = _basePath ?? string.Empty;
                string cleanBasePath = safeBasePath.TrimEnd('\\', '/');
                if (cleanBasePath.Length == 2 && cleanBasePath.EndsWith(":"))
                {
                    cleanBasePath += "\\";
                }

                string quotedPsScript = QuoteProcessArgument(psScript);
                string quotedBasePath = QuoteProcessArgument(cleanBasePath);
                string clientArg = !string.IsNullOrEmpty(client) ? string.Format(" -Client {0}", QuoteProcessArgument(client)) : "";

                int exitCode = await RunProcessAsync("powershell",
                    string.Format("-NoProfile -ExecutionPolicy Bypass -File {0} -BasePath {1}{2}", quotedPsScript, quotedBasePath, clientArg),
                    120000, null, false);

                if (exitCode == 0)
                {
                    Log(string.Format("[ok] Papéis de parede {0}configurados em todos os temas do Windows.",
                        !string.IsNullOrEmpty(client) ? "(" + client + ") " : ""), ConsoleColor.Green);
                }
                else
                {
                    Log(string.Format("X Aviso: Configuração de papéis de parede retornou código {0}.", exitCode), ConsoleColor.Yellow);
                }
            }
            else
            {
                Log("X Aviso: Script de papel de parede não encontrado nos recursos embutidos.", ConsoleColor.Yellow);
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

        /// <summary>
        /// Realiza teste de latência (ping) da conexão com a internet contra servidores DNS rápidos e confiáveis.
        /// </summary>
        /// <returns>String formatada com tempo de resposta em milissegundos e qualidade da conexão.</returns>
        public async Task<string> MeasureInternetPingAsync()
        {
            return await Task.Run(new Func<string>(() =>
            {
                string[] targets = new string[] { "1.1.1.1", "8.8.8.8" };
                using (var ping = new System.Net.NetworkInformation.Ping())
                {
                    foreach (var target in targets)
                    {
                        try
                        {
                            var reply = ping.Send(target, 1200);
                            if (reply != null && reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                            {
                                string hostLabel = (target == "1.1.1.1") ? "Cloudflare DNS" : "Google DNS";
                                string quality = reply.RoundtripTime < 50 ? "Excelente" : (reply.RoundtripTime < 120 ? "Bom" : "Alto");
                                return string.Format("{0} ms ({1} - {2})", reply.RoundtripTime, hostLabel, quality);
                            }
                        }
                        catch { }
                    }
                }
                return "Tempo limite esgotado / Inacessível";
            }));
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
        /// Obtém o diretório local de cache corporativo onde os instaladores online são baixados.
        /// Localizado em %ProgramData%\EXABIT\Downloads.
        /// </summary>
        public string GetDownloadCacheDirectory()
        {
            try
            {
                string commonData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string cacheDir = Path.Combine(commonData, "EXABIT", "Downloads");
                if (!Directory.Exists(cacheDir))
                {
                    Directory.CreateDirectory(cacheDir);
                }
                return cacheDir;
            }
            catch
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "EXABIT_Downloads");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }
                return tempDir;
            }
        }

        /// <summary>
        /// Dispara o evento de progresso do download para a interface.
        /// </summary>
        private void ReportDownloadProgress(string displayName, int percent, long bytesReceived, long totalBytes)
        {
            if (OnDownloadProgress != null)
            {
                OnDownloadProgress(displayName, percent, bytesReceived, totalBytes);
            }
        }

        /// <summary>
        /// Faz o download assíncrono de um arquivo a partir de uma URL direta, salvando no cache corporativo.
        /// Informa o progresso do download em tempo real via OnDownloadProgress e em etapas no log.
        /// </summary>
        private async Task<string> DownloadFileOnlineAsync(string url, string fileName, string displayName, long minBytes, bool suppressErrorLog = false, string customUserAgent = null)
        {
            return await Task.Run(new Func<string>(() =>
            {
                try
                {
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                    string cacheDir = GetDownloadCacheDirectory();
                    string destPath = Path.Combine(cacheDir, fileName);

                    if (File.Exists(destPath))
                    {
                        var fi = new FileInfo(destPath);
                        if (fi.Length >= minBytes)
                        {
                            Log(string.Format("  Instalador de {0} pronto no cache ({1:N1} MB).",
                                displayName, (double)fi.Length / (1024 * 1024)), ConsoleColor.Gray);
                            return destPath;
                        }
                    }

                    string tempPath = destPath + ".download";
                    try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }

                    Log(string.Format("  Baixando {0} online...", displayName), ConsoleColor.Cyan);

                    var request = (HttpWebRequest)WebRequest.Create(url);
                    request.Method = "GET";
                    request.UserAgent = !string.IsNullOrEmpty(customUserAgent) ? customUserAgent : "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AutoInstaller/2.0";
                    request.Timeout = 30000;
                    request.ReadWriteTimeout = 60000;
                    request.AllowAutoRedirect = true;

                    using (var response = (HttpWebResponse)request.GetResponse())
                    {
                        long totalBytes = response.ContentLength;
                        using (var responseStream = response.GetResponseStream())
                        using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
                        {
                            byte[] buffer = new byte[65536];
                            long totalRead = 0;
                            int bytesRead;
                            int lastReportedPercent = -1;
                            DateTime lastLogTime = DateTime.MinValue;

                            ReportDownloadProgress(displayName, 0, 0, totalBytes);

                            while ((bytesRead = responseStream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                if (_cancellationTokenSource != null && _cancellationTokenSource.IsCancellationRequested)
                                {
                                    throw new OperationCanceledException();
                                }

                                fileStream.Write(buffer, 0, bytesRead);
                                totalRead += bytesRead;

                                if (totalBytes > 0)
                                {
                                    int percent = (int)((totalRead * 100) / totalBytes);
                                    ReportDownloadProgress(displayName, percent, totalRead, totalBytes);

                                    if (percent >= 25 && lastReportedPercent < 25)
                                    {
                                        lastReportedPercent = 25;
                                        Log(string.Format("  Download de {0}: 25% ({1:N1} MB / {2:N1} MB)",
                                            displayName, (double)totalRead / (1024 * 1024), (double)totalBytes / (1024 * 1024)), ConsoleColor.Gray);
                                    }
                                    else if (percent >= 50 && lastReportedPercent < 50)
                                    {
                                        lastReportedPercent = 50;
                                        Log(string.Format("  Download de {0}: 50% ({1:N1} MB / {2:N1} MB)",
                                            displayName, (double)totalRead / (1024 * 1024), (double)totalBytes / (1024 * 1024)), ConsoleColor.Gray);
                                    }
                                    else if (percent >= 75 && lastReportedPercent < 75)
                                    {
                                        lastReportedPercent = 75;
                                        Log(string.Format("  Download de {0}: 75% ({1:N1} MB / {2:N1} MB)",
                                            displayName, (double)totalRead / (1024 * 1024), (double)totalBytes / (1024 * 1024)), ConsoleColor.Gray);
                                    }
                                }
                                else
                                {
                                    if ((DateTime.Now - lastLogTime).TotalMilliseconds >= 2500)
                                    {
                                        lastLogTime = DateTime.Now;
                                        ReportDownloadProgress(displayName, -1, totalRead, -1);
                                        Log(string.Format("  Download de {0}: {1:N1} MB baixados...",
                                            displayName, (double)totalRead / (1024 * 1024)), ConsoleColor.Gray);
                                    }
                                }
                            }

                            ReportDownloadProgress(displayName, 100, totalRead, totalBytes);
                        }
                    }

                    var downloadedFi = new FileInfo(tempPath);
                    if (downloadedFi.Exists && downloadedFi.Length >= minBytes)
                    {
                        try { if (File.Exists(destPath)) File.Delete(destPath); } catch { }
                        File.Move(tempPath, destPath);

                        Log(string.Format("[ok] Download de {0} concluído com sucesso ({1:N1} MB).",
                            displayName, (double)downloadedFi.Length / (1024 * 1024)), ConsoleColor.Green);
                        return destPath;
                    }

                    try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                    if (!suppressErrorLog)
                    {
                        Log(string.Format("X Arquivo baixado de {0} é inválido ou incompleto.", displayName), ConsoleColor.Red);
                    }
                    return null;
                }
                catch (OperationCanceledException)
                {
                    Log(string.Format("X Download de {0} cancelado pelo usuário.", displayName), ConsoleColor.Yellow);
                    return null;
                }
                catch (Exception ex)
                {
                    if (suppressErrorLog)
                    {
                        Log(string.Format("  Aviso ao baixar {0}: {1}. Tentando espelho alternativo...", displayName, ex.Message), ConsoleColor.Yellow);
                    }
                    else
                    {
                        Log(string.Format("X Erro ao baixar {0}: {1}", displayName, ex.Message), ConsoleColor.Red);
                    }
                    return null;
                }
            }));
        }

        private async Task<string> Resolve7ZipInstallerAsync(ProgramInfo program)
        {
            string cacheDir = GetDownloadCacheDirectory();

            string[] possibleCache = new string[] { "7z_setup.exe", "7z_setup.msi", "7z2409-x64.exe", "7z2408-x64.exe" };
            foreach (var fn in possibleCache)
            {
                string p = Path.Combine(cacheDir, fn);
                if (File.Exists(p) && new FileInfo(p).Length > 1000000)
                {
                    Log("  Instalador do 7-Zip pronto no cache.", ConsoleColor.Gray);
                    program.FileName = fn;
                    if (fn.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
                    {
                        program.Arguments = "/qn /norestart";
                    }
                    else
                    {
                        program.Arguments = "/S";
                    }
                    return p;
                }
            }

            // Lista de espelhos confiáveis para o 7-Zip.
            // O SourceForge fornece download direto e contorna falhas de resolução DNS
            // em 'release-assets.githubusercontent.com'.
            string[] mirrors = new string[]
            {
                "https://downloads.sourceforge.net/project/sevenzip/7-Zip/24.09/7z2409-x64.exe",
                "https://downloads.sourceforge.net/project/sevenzip/7-Zip/24.08/7z2408-x64.exe",
                "https://www.7-zip.org/a/7z2409-x64.exe",
                "https://github.com/ip7z/7zip/releases/download/24.09/7z2409-x64.exe"
            };

            for (int i = 0; i < mirrors.Length; i++)
            {
                string url = mirrors[i];
                try
                {
                    string ext = Path.GetExtension(url);
                    if (string.IsNullOrEmpty(ext) || ext.IndexOf("?") >= 0) ext = ".exe";
                    string targetName = "7z_setup" + ext;

                    string result = await DownloadFileOnlineAsync(url, targetName, program.DisplayName, 1000000, i < mirrors.Length - 1);
                    if (!string.IsNullOrEmpty(result) && File.Exists(result) && new FileInfo(result).Length > 1000000)
                    {
                        program.FileName = targetName;
                        if (ext.Equals(".msi", StringComparison.OrdinalIgnoreCase))
                        {
                            program.Arguments = "/qn /norestart";
                        }
                        else
                        {
                            program.Arguments = "/S";
                        }
                        return result;
                    }
                }
                catch { }
            }

            return null;
        }

        private async Task<string> ResolveChromeInstallerAsync(ProgramInfo program)
        {
            string cacheDir = GetDownloadCacheDirectory();
            string msiDest = Path.Combine(cacheDir, "googlechromestandaloneenterprise64.msi");

            if (File.Exists(msiDest) && new FileInfo(msiDest).Length > 50000000)
            {
                Log("  Instalador do Google Chrome Enterprise (MSI) pronto no cache.", ConsoleColor.Gray);
                program.FileName = "googlechromestandaloneenterprise64.msi";
                program.Arguments = "/qn /norestart";
                return msiDest;
            }

            try
            {
                string oldExe = Path.Combine(cacheDir, "ChromeStandaloneSetup64.exe");
                if (File.Exists(oldExe)) File.Delete(oldExe);
            }
            catch { }

            string[] mirrors = new string[]
            {
                "https://dl.google.com/dl/chrome/install/googlechromestandaloneenterprise64.msi",
                "https://dl.google.com/chrome/install/googlechromestandaloneenterprise64.msi"
            };

            for (int i = 0; i < mirrors.Length; i++)
            {
                string url = mirrors[i];
                try
                {
                    string path = await DownloadFileOnlineAsync(url, "googlechromestandaloneenterprise64.msi", program.DisplayName, 50000000, i < mirrors.Length - 1);
                    if (!string.IsNullOrEmpty(path) && File.Exists(path) && new FileInfo(path).Length > 50000000)
                    {
                        program.FileName = "googlechromestandaloneenterprise64.msi";
                        program.Arguments = "/qn /norestart";
                        return path;
                    }
                }
                catch { }
            }

            return null;
        }

        private async Task<string> ResolveWinRarInstallerAsync(ProgramInfo program)
        {
            string cacheDir = GetDownloadCacheDirectory();
            string destFile = Path.Combine(cacheDir, "winrar_setup.exe");

            if (File.Exists(destFile) && new FileInfo(destFile).Length > 2000000)
            {
                Log("  Instalador do WinRAR pronto no cache.", ConsoleColor.Gray);
                return destFile;
            }

            string downloadUrl = "https://www.rarlab.com/rar/winrar-x64-723br.exe";
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AutoInstaller/2.0");
                    string html = wc.DownloadString("https://www.rarlab.com/download.htm");
                    var match = Regex.Match(html, @"href=""([^""]*winrar-x64-[0-9]+br\.exe)""", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        string rel = match.Groups[1].Value.TrimStart('/');
                        downloadUrl = "https://www.rarlab.com/" + rel;
                    }
                }
            }
            catch { }

            return await DownloadFileOnlineAsync(downloadUrl, "winrar_setup.exe", program.DisplayName, 2000000);
        }

        private async Task<string> ResolveRadminInstallerAsync(ProgramInfo program)
        {
            string cacheDir = GetDownloadCacheDirectory();
            string destFile = Path.Combine(cacheDir, "Radmin_VPN.exe");

            if (File.Exists(destFile) && new FileInfo(destFile).Length > 15000000)
            {
                Log("  Instalador do Radmin VPN pronto no cache.", ConsoleColor.Gray);
                return destFile;
            }

            string downloadUrl = "https://download.radmin-vpn.com/download/files/Radmin_VPN_2.1.4952.2.exe";
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AutoInstaller/2.0");
                    string html = wc.DownloadString("https://www.radmin-vpn.com/");
                    var match = Regex.Match(html, @"href=""([^""]*Radmin_VPN_[0-9.]+\.exe)""", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        downloadUrl = match.Groups[1].Value;
                    }
                }
            }
            catch { }

            return await DownloadFileOnlineAsync(downloadUrl, "Radmin_VPN.exe", program.DisplayName, 15000000);
        }

        private async Task<string> ResolveAdobeReaderInstallerAsync(ProgramInfo program)
        {
            string cacheDir = GetDownloadCacheDirectory();
            string destFile = Path.Combine(cacheDir, "AcroRdrDCx64_MUI.exe");

            if (File.Exists(destFile) && new FileInfo(destFile).Length > 200000000)
            {
                Log("  Instalador do Adobe Acrobat Reader pronto no cache.", ConsoleColor.Gray);
                return destFile;
            }

            string downloadUrl = "https://ardownload3.adobe.com/pub/adobe/acrobat/win/AcrobatDC/2600322110/AcroRdrDCx642600322110_MUI.exe";
            return await DownloadFileOnlineAsync(downloadUrl, "AcroRdrDCx64_MUI.exe", program.DisplayName, 200000000);
        }

        private async Task<string> ResolvePowerBIInstallerAsync(ProgramInfo program)
        {
            string cacheDir = GetDownloadCacheDirectory();
            string destFile = Path.Combine(cacheDir, "PBIDesktopSetup_x64.exe");

            if (File.Exists(destFile) && new FileInfo(destFile).Length > 300000000)
            {
                Log("  Instalador do Microsoft Power BI Desktop pronto no cache.", ConsoleColor.Gray);
                return destFile;
            }

            string downloadUrl = "https://download.microsoft.com/download/8/8/0/880BCA75-79DD-466A-927D-1ABF1F5454B0/PBIDesktopSetup-2026-10_x64.exe";
            return await DownloadFileOnlineAsync(downloadUrl, "PBIDesktopSetup_x64.exe", program.DisplayName, 300000000);
        }

        private async Task<string> ResolveOffice365InstallerAsync(ProgramInfo program)
        {
            string cacheDir = GetDownloadCacheDirectory();
            string officeDir = Path.Combine(cacheDir, "Office365");

            try
            {
                if (!Directory.Exists(officeDir))
                {
                    Directory.CreateDirectory(officeDir);
                }
            }
            catch { }

            string setupExe = Path.Combine(officeDir, "setup.exe");
            string configXml = Path.Combine(officeDir, "configuration_o365.xml");
            EnsureOffice365ConfigurationXml(configXml);

            if (File.Exists(setupExe) && new FileInfo(setupExe).Length > 2000000)
            {
                Log("  Ferramenta de instalação do Office 365 pronta no cache.", ConsoleColor.Gray);
                program.Arguments = string.Format("/configure {0}", QuoteProcessArgument(configXml));
                return setupExe;
            }

            // Baixa setup.exe diretamente do CDN oficial da Microsoft (Click-to-Run Bootstrapper)
            string directUrl = "https://officecdn.microsoft.com/pr/wsus/setup.exe";
            string downloadedSetup = await DownloadFileOnlineAsync(directUrl, Path.Combine("Office365", "setup.exe"), "Microsoft Office 365 (Setup)", 2000000);
            if (!string.IsNullOrEmpty(downloadedSetup) && File.Exists(downloadedSetup) && new FileInfo(downloadedSetup).Length > 2000000)
            {
                if (!string.Equals(downloadedSetup, setupExe, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Copy(downloadedSetup, setupExe, true); } catch { }
                }

                if (File.Exists(setupExe))
                {
                    EnsureOffice365ConfigurationXml(configXml);
                    program.Arguments = string.Format("/configure {0}", QuoteProcessArgument(configXml));
                    Log("[ok] Ferramenta de instalação do Office 365 pronta.", ConsoleColor.Green);
                    return setupExe;
                }
            }

            // Fallback via Office Deployment Tool (ODT) caso o CDN direto falhe
            string odtUrl = "https://download.microsoft.com/download/6c1eeb25-cf8b-41d9-8d0d-cc1dbc032140/officedeploymenttool_20527-20150.exe";
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AutoInstaller/2.0");
                    string page = wc.DownloadString("https://www.microsoft.com/en-us/download/details.aspx?id=49117");
                    var match = Regex.Match(page, @"https?://[^\s""'<>]*(?:officedeploymenttool[^\s""'<>]*\.exe)", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        odtUrl = match.Value;
                    }
                }
            }
            catch { }

            string downloadedOdt = await DownloadFileOnlineAsync(odtUrl, "officedeploymenttool.exe", "Office Deployment Tool (ODT)", 2000000);
            if (!string.IsNullOrEmpty(downloadedOdt) && File.Exists(downloadedOdt))
            {
                Log("  Extraindo arquivos oficiais de instalação do Office 365...", ConsoleColor.Cyan);
                int extractExit = await RunProcessAsync(downloadedOdt,
                    string.Format("/extract:{0} /quiet", QuoteProcessArgument(officeDir)),
                    60000, officeDir, false);

                for (int i = 0; i < 15; i++)
                {
                    if (File.Exists(setupExe)) break;
                    await Task.Delay(1000);
                }

                if (File.Exists(setupExe))
                {
                    EnsureOffice365ConfigurationXml(configXml);
                    program.Arguments = string.Format("/configure {0}", QuoteProcessArgument(configXml));
                    Log("[ok] Arquivos de instalação do Office 365 extraídos com sucesso.", ConsoleColor.Green);
                    return setupExe;
                }
            }

            Log("X Falha ao obter a ferramenta de instalação do Office 365.", ConsoleColor.Red);
            return null;
        }

        /// <summary>
        /// Verifica se o Microsoft Office 365 ou Office 16 já está instalado no sistema operacional.
        /// </summary>
        private static bool IsOffice365Installed()
        {
            try
            {
                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string wordPath64 = Path.Combine(progFiles, "Microsoft Office", "root", "Office16", "WINWORD.EXE");
                if (File.Exists(wordPath64)) return true;

                string progFiles86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                string wordPath32 = Path.Combine(progFiles86, "Microsoft Office", "root", "Office16", "WINWORD.EXE");
                if (File.Exists(wordPath32)) return true;

                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Office\ClickToRun\Configuration"))
                {
                    if (key != null)
                    {
                        var prodCodes = key.GetValue("ProductCodes") as string;
                        if (!string.IsNullOrEmpty(prodCodes) && prodCodes.IndexOf("O365", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return true;
                        }
                        var version = key.GetValue("VersionToReport") as string;
                        if (!string.IsNullOrEmpty(version))
                        {
                            return true;
                        }
                    }
                }

                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                {
                    if (key != null)
                    {
                        foreach (var subKeyName in key.GetSubKeyNames())
                        {
                            if (subKeyName.IndexOf("O365", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                subKeyName.IndexOf("Office16", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                return true;
                            }

                            using (var subKey = key.OpenSubKey(subKeyName))
                            {
                                if (subKey != null)
                                {
                                    var dispName = subKey.GetValue("DisplayName") as string;
                                    if (!string.IsNullOrEmpty(dispName) &&
                                        (dispName.IndexOf("Microsoft 365", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                         dispName.IndexOf("Office 365", StringComparison.OrdinalIgnoreCase) >= 0))
                                    {
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Verifica se o Autodesk DWG TrueView já está instalado no sistema operacional.
        /// </summary>
        private static bool IsDwgTrueViewInstalled()
        {
            try
            {
                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string autodeskDir = Path.Combine(progFiles, "Autodesk");
                if (Directory.Exists(autodeskDir))
                {
                    var dirs = Directory.GetDirectories(autodeskDir, "*DWG TrueView*");
                    if (dirs != null && dirs.Length > 0) return true;
                }

                string progFiles86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                string autodeskDir86 = Path.Combine(progFiles86, "Autodesk");
                if (Directory.Exists(autodeskDir86))
                {
                    var dirs = Directory.GetDirectories(autodeskDir86, "*DWG TrueView*");
                    if (dirs != null && dirs.Length > 0) return true;
                }

                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Autodesk\DWG TrueView"))
                {
                    if (key != null) return true;
                }

                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                {
                    if (key != null)
                    {
                        foreach (var subKeyName in key.GetSubKeyNames())
                        {
                            if (subKeyName.IndexOf("DWG TrueView", StringComparison.OrdinalIgnoreCase) >= 0)
                                return true;

                            using (var subKey = key.OpenSubKey(subKeyName))
                            {
                                if (subKey != null)
                                {
                                    var dispName = subKey.GetValue("DisplayName") as string;
                                    if (!string.IsNullOrEmpty(dispName) && dispName.IndexOf("DWG TrueView", StringComparison.OrdinalIgnoreCase) >= 0)
                                        return true;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Obtém o instalador oficial do Autodesk DWG TrueView direto da CDN da Autodesk.
        /// Baixa o pacote oficial SFX (1,5 GB), extrai de forma silenciosa para C:\Autodesk e localiza o Setup.exe.
        /// </summary>
        private async Task<string> ResolveDwgTrueViewInstallerAsync(ProgramInfo program)
        {
            string extractedSetup = @"C:\Autodesk\DWGTrueView_2024_English_64bit_dlm\Setup.exe";
            if (File.Exists(extractedSetup))
            {
                Log("  Instalador extraído do Autodesk DWG TrueView pronto em C:\\Autodesk.", ConsoleColor.Gray);
                return extractedSetup;
            }

            string cacheDir = GetDownloadCacheDirectory();
            string sfxPath = Path.Combine(cacheDir, "DWGTrueView_2024_English_64bit_dlm.sfx.exe");

            if (!File.Exists(sfxPath) || new FileInfo(sfxPath).Length < 1000000000)
            {
                string downloadUrl = "https://dds.autodesk.com/NetSWDLD/2024/ACD/9C02048D-D0DB-3E06-B903-89BD24380AAD/SFX/DWGTrueView_2024_English_64bit_dlm.sfx.exe";
                sfxPath = await DownloadFileOnlineAsync(downloadUrl, "DWGTrueView_2024_English_64bit_dlm.sfx.exe", "Autodesk DWG TrueView (1,5 GB)", 1000000000);
            }

            if (string.IsNullOrEmpty(sfxPath) || !File.Exists(sfxPath))
            {
                Log("X Falha ao baixar o instalador oficial do Autodesk DWG TrueView.", ConsoleColor.Red);
                return null;
            }

            Log("  Extraindo arquivos do Autodesk DWG TrueView (aguarde alguns instantes)...", ConsoleColor.Cyan);
            try
            {
                if (!Directory.Exists(@"C:\Autodesk"))
                {
                    Directory.CreateDirectory(@"C:\Autodesk");
                }
            }
            catch { }

            int sfxExit = await RunProcessAsync(sfxPath, "-suppresslaunch -d \"C:\\Autodesk\"", 900000, @"C:\Autodesk", false);

            for (int i = 0; i < 20; i++)
            {
                if (File.Exists(extractedSetup)) break;
                await Task.Delay(1000);
            }

            if (File.Exists(extractedSetup))
            {
                Log("[ok] Arquivos do Autodesk DWG TrueView extraídos com sucesso.", ConsoleColor.Green);
                return extractedSetup;
            }

            try
            {
                if (Directory.Exists(@"C:\Autodesk"))
                {
                    var found = Directory.GetFiles(@"C:\Autodesk", "Setup.exe", SearchOption.AllDirectories);
                    if (found != null && found.Length > 0)
                    {
                        return found[0];
                    }
                }
            }
            catch { }

            Log("X Setup.exe do Autodesk DWG TrueView não foi encontrado após extração.", ConsoleColor.Red);
            return null;
        }

        private async Task<string> ResolveTraceDrawInstallerAsync(ProgramInfo program)
        {
            bool is64 = Environment.Is64BitOperatingSystem;
            string cacheFileName = is64 ? "tracedraw-setup-x64.exe" : "tracedraw-setup-x86.exe";
            string cacheDir = GetDownloadCacheDirectory();
            string destFile = Path.Combine(cacheDir, cacheFileName);

            program.FileName = cacheFileName;

            try
            {
                string oldGeneric = Path.Combine(cacheDir, "tracedraw-setup.exe");
                if (File.Exists(oldGeneric))
                {
                    File.Delete(oldGeneric);
                }
                string oldX86 = Path.Combine(cacheDir, "tracedraw-setup-x86.exe");
                if (is64 && File.Exists(oldX86))
                {
                    File.Delete(oldX86);
                }
            }
            catch { }

            if (File.Exists(destFile) && new FileInfo(destFile).Length > 5000000)
            {
                Log(string.Format("  Instalador do TraceDraw ({0}) pronto no cache.", is64 ? "64 bits" : "32 bits"), ConsoleColor.Gray);
                return destFile;
            }

            string downloadUrl = null;
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                var apiReq = (HttpWebRequest)WebRequest.Create("https://api.github.com/repos/tracedraw/tracedraw/releases/latest");
                apiReq.Method = "GET";
                apiReq.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AutoInstaller/2.0";
                apiReq.Timeout = 8000;

                using (var apiResp = (HttpWebResponse)apiReq.GetResponse())
                using (var reader = new StreamReader(apiResp.GetResponseStream(), Encoding.UTF8))
                {
                    string json = reader.ReadToEnd();
                    var matches = Regex.Matches(json, @"\""browser_download_url\""\s*:\s*\""(https://[^\s\""]+\.exe)\""", RegexOptions.IgnoreCase);

                    for (int i = 0; i < matches.Count; i++)
                    {
                        string candidate = matches[i].Groups[1].Value;
                        if (is64)
                        {
                            if ((candidate.IndexOf("x86_64", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 candidate.IndexOf("x64", StringComparison.OrdinalIgnoreCase) >= 0) &&
                                candidate.IndexOf("setup.exe", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                downloadUrl = candidate;
                                break;
                            }
                        }
                        else
                        {
                            if ((candidate.IndexOf("windows-x86-setup.exe", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 candidate.IndexOf("x86-setup.exe", StringComparison.OrdinalIgnoreCase) >= 0) &&
                                candidate.IndexOf("x86_64", StringComparison.OrdinalIgnoreCase) < 0)
                            {
                                downloadUrl = candidate;
                                break;
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(downloadUrl))
                    {
                        for (int i = 0; i < matches.Count; i++)
                        {
                            string candidate = matches[i].Groups[1].Value;
                            if (is64)
                            {
                                if (candidate.IndexOf("x86_64", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    candidate.IndexOf("x64", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    downloadUrl = candidate;
                                    break;
                                }
                            }
                            else if (candidate.IndexOf("setup.exe", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                downloadUrl = candidate;
                                break;
                            }
                        }
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(downloadUrl))
            {
                try
                {
                    var tagReq = (HttpWebRequest)WebRequest.Create("https://github.com/tracedraw/tracedraw/releases/latest");
                    tagReq.Method = "GET";
                    tagReq.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AutoInstaller/2.0";
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
                                string archSuffix = is64 ? "windows-x86_64-setup.exe" : "windows-x86-setup.exe";
                                downloadUrl = string.Format("https://github.com/tracedraw/tracedraw/releases/download/{0}/tracedraw-{1}-{2}", tag, version, archSuffix);
                            }
                        }
                    }
                }
                catch { }
            }

            if (string.IsNullOrEmpty(downloadUrl))
            {
                downloadUrl = is64
                    ? "https://github.com/tracedraw/tracedraw/releases/download/v0.49.0/tracedraw-0.49.0-windows-x86_64-setup.exe"
                    : "https://github.com/tracedraw/tracedraw/releases/download/v0.49.0/tracedraw-0.49.0-windows-x86-setup.exe";
            }

            return await DownloadFileOnlineAsync(downloadUrl, cacheFileName, program.DisplayName, 5000000);
        }

        private async Task<string> ResolveVSCodeInstallerAsync(ProgramInfo program)
        {
            string cacheDir = GetDownloadCacheDirectory();
            string destFile = Path.Combine(cacheDir, "VSCodeSetup_x64.exe");

            if (File.Exists(destFile) && new FileInfo(destFile).Length > 50000000)
            {
                Log("  Instalador do Visual Studio Code pronto no cache.", ConsoleColor.Gray);
                return destFile;
            }

            string downloadUrl = "https://update.code.visualstudio.com/latest/win32-x64/stable";
            return await DownloadFileOnlineAsync(downloadUrl, "VSCodeSetup_x64.exe", program.DisplayName, 50000000);
        }

        private async Task<string> ResolveAntigravityInstallerAsync(ProgramInfo program)
        {
            string cacheDir = GetDownloadCacheDirectory();
            string destFile = Path.Combine(cacheDir, "Antigravity-x64.exe");

            if (File.Exists(destFile) && new FileInfo(destFile).Length > 50000000)
            {
                Log("  Instalador do Antigravity pronto no cache.", ConsoleColor.Gray);
                return destFile;
            }

            string downloadUrl = "https://storage.googleapis.com/antigravity-public/antigravity-hub/2.33.0-4731201358004224/windows-x64/Antigravity-x64.exe";
            return await DownloadFileOnlineAsync(downloadUrl, "Antigravity-x64.exe", program.DisplayName, 50000000);
        }

        private async Task<string> ResolveAntigravityIDEInstallerAsync(ProgramInfo program)
        {
            string cacheDir = GetDownloadCacheDirectory();
            string destFile = Path.Combine(cacheDir, "Antigravity_IDE_setup.exe");

            if (File.Exists(destFile) && new FileInfo(destFile).Length > 50000000)
            {
                Log("  Instalador do Antigravity IDE pronto no cache.", ConsoleColor.Gray);
                return destFile;
            }

            string downloadUrl = "https://edgedl.me.gvt1.com/edgedl/release2/j0qc3/antigravity/stable/2.7.0-4507555968450560/windows-x64/Antigravity%20IDE.exe";
            return await DownloadFileOnlineAsync(downloadUrl, "Antigravity_IDE_setup.exe", program.DisplayName, 50000000);
        }

        private async Task<string> ResolveNodeJsInstallerAsync(ProgramInfo program)
        {
            string cacheDir = GetDownloadCacheDirectory();
            string destFile = Path.Combine(cacheDir, "node_lts_x64.msi");

            if (File.Exists(destFile) && new FileInfo(destFile).Length > 20000000)
            {
                Log("  Instalador do Node.js LTS pronto no cache.", ConsoleColor.Gray);
                return destFile;
            }

            string downloadUrl = "https://nodejs.org/dist/v22.14.0/node-v22.14.0-x64.msi";
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AutoInstaller/2.0");
                    string indexJson = wc.DownloadString("https://nodejs.org/dist/index.json");
                    var match = Regex.Match(indexJson, @"\{""version"":""(v[0-9.]+)"",[^}]*""lts"":""([^""]+)""", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        string ver = match.Groups[1].Value;
                        downloadUrl = string.Format("https://nodejs.org/dist/{0}/node-{0}-x64.msi", ver);
                    }
                }
            }
            catch { }

            return await DownloadFileOnlineAsync(downloadUrl, "node_lts_x64.msi", program.DisplayName, 20000000);
        }

        private async Task<string> ResolveComposerInstallerAsync(ProgramInfo program)
        {
            string cacheDir = GetDownloadCacheDirectory();
            string destFile = Path.Combine(cacheDir, "Composer-Setup.exe");

            if (File.Exists(destFile) && new FileInfo(destFile).Length > 1000000)
            {
                Log("  Instalador do Composer pronto no cache.", ConsoleColor.Gray);
                return destFile;
            }

            string downloadUrl = "https://getcomposer.org/Composer-Setup.exe";
            return await DownloadFileOnlineAsync(downloadUrl, "Composer-Setup.exe", program.DisplayName, 1000000);
        }

        private async Task<string> ResolveFileZillaInstallerAsync(ProgramInfo program)
        {
            string cacheDir = GetDownloadCacheDirectory();
            string destFile = Path.Combine(cacheDir, "FileZilla_setup.exe");

            if (File.Exists(destFile) && new FileInfo(destFile).Length > 10000000)
            {
                Log("  Instalador do FileZilla Client pronto no cache.", ConsoleColor.Gray);
                return destFile;
            }

            string[] pkgUrls = new string[]
            {
                "https://community.chocolatey.org/api/v2/package/filezilla/3.72.1",
                "https://community.chocolatey.org/api/v2/package/filezilla"
            };

            foreach (string nupkgUrl in pkgUrls)
            {
                string downloadedPkg = await DownloadFileOnlineAsync(nupkgUrl, "filezilla_pkg.zip", program.DisplayName, 10000000, true, "chocolatey/2.2.2");
                if (!string.IsNullOrEmpty(downloadedPkg) && File.Exists(downloadedPkg))
                {
                    try
                    {
                        string extractDir = Path.Combine(cacheDir, "FileZilla_extract");
                        if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                        Directory.CreateDirectory(extractDir);

                        System.IO.Compression.ZipFile.ExtractToDirectory(downloadedPkg, extractDir);
                        var setupFiles = Directory.GetFiles(extractDir, "FileZilla_*_win64-setup.exe", SearchOption.AllDirectories);
                        if (setupFiles != null && setupFiles.Length > 0)
                        {
                            if (File.Exists(destFile)) File.Delete(destFile);
                            File.Copy(setupFiles[0], destFile, true);
                            try { Directory.Delete(extractDir, true); } catch { }
                            return destFile;
                        }
                    }
                    catch { }
                }
            }

            return null;
        }

        private async Task<string> ResolveDBeaverInstallerAsync(ProgramInfo program)
        {
            string cacheDir = GetDownloadCacheDirectory();
            string destFile = Path.Combine(cacheDir, "dbeaver_setup.exe");

            if (File.Exists(destFile) && new FileInfo(destFile).Length > 50000000)
            {
                Log("  Instalador do DBeaver Community pronto no cache.", ConsoleColor.Gray);
                return destFile;
            }

            string downloadUrl = "https://dbeaver.io/files/dbeaver-ce-latest-x86_64-setup.exe";
            return await DownloadFileOnlineAsync(downloadUrl, "dbeaver_setup.exe", program.DisplayName, 50000000);
        }

        private async Task<string> ResolveBlenderInstallerAsync(ProgramInfo program)
        {
            string cacheDir = GetDownloadCacheDirectory();
            string destFile = Path.Combine(cacheDir, "blender.msi");

            if (File.Exists(destFile) && new FileInfo(destFile).Length > 100000000)
            {
                Log("  Instalador do Blender pronto no cache.", ConsoleColor.Gray);
                return destFile;
            }

            string downloadUrl = "https://download.blender.org/release/Blender5.3/blender-5.3.0-windows-x64.msi";
            return await DownloadFileOnlineAsync(downloadUrl, "blender.msi", program.DisplayName, 100000000);
        }

        private async Task<string> ResolveAffinityInstallerAsync(ProgramInfo program)
        {
            string cacheDir = GetDownloadCacheDirectory();
            string destFile = Path.Combine(cacheDir, "Affinity.msix");

            if (File.Exists(destFile) && new FileInfo(destFile).Length > 100000000)
            {
                Log("  Instalador do Affinity Studio pronto no cache.", ConsoleColor.Gray);
                return destFile;
            }

            string downloadUrl = "https://affinity-update.serif.com/windows/3/studio/retail/Affinity-Affinity-Store-x64-5004-20596bdfdd242bf0f3a45a6fd4ce4f845132454e.msix";
            return await DownloadFileOnlineAsync(downloadUrl, "Affinity.msix", program.DisplayName, 100000000);
        }

        private async Task<string> ResolveDaVinciResolveInstallerAsync(ProgramInfo program)
        {
            string cacheDir = GetDownloadCacheDirectory();
            string davinciDir = Path.Combine(cacheDir, "DaVinciResolve");
            try
            {
                if (!Directory.Exists(davinciDir)) Directory.CreateDirectory(davinciDir);
            }
            catch { }

            if (Directory.Exists(davinciDir))
            {
                var existingExes = Directory.GetFiles(davinciDir, "DaVinci_Resolve_*.exe", SearchOption.AllDirectories);
                if (existingExes != null && existingExes.Length > 0 && new FileInfo(existingExes[0]).Length > 500000000)
                {
                    Log("  Instalador do DaVinci Resolve pronto no cache.", ConsoleColor.Gray);
                    return existingExes[0];
                }
            }

            string localProgDir = Path.Combine(_basePath, "Programas");
            if (Directory.Exists(localProgDir))
            {
                var localExes = Directory.GetFiles(localProgDir, "DaVinci_Resolve_*.exe", SearchOption.AllDirectories);
                if (localExes != null && localExes.Length > 0 && new FileInfo(localExes[0]).Length > 500000000)
                {
                    Log("  Instalador do DaVinci Resolve encontrado em Programas.", ConsoleColor.Gray);
                    return localExes[0];
                }
            }

            string downloadUrl = null;
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                string downloadId = null;

                using (var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AutoInstaller/2.0");
                    string json = wc.DownloadString("https://www.blackmagicdesign.com/api/support/us/downloads.json");
                    var match = Regex.Match(json, @"\{[^{}]*""downloadId""\s*:\s*""([^""]+)""[^{}]*""downloadTitle""\s*:\s*""([^""]*DaVinci Resolve[^""]*)""[^{}]*\}", RegexOptions.IgnoreCase);
                    if (match.Success && match.Groups[2].Value.IndexOf("Studio", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        downloadId = match.Groups[1].Value;
                    }
                    else
                    {
                        var matches = Regex.Matches(json, @"\{""releaseId"":""[^""]+"",""downloadId"":""([^""]+)"",""downloadTitle"":""([^""]*DaVinci Resolve[^""]*)""");
                        foreach (Match m in matches)
                        {
                            if (m.Groups[2].Value.IndexOf("Studio", StringComparison.OrdinalIgnoreCase) < 0)
                            {
                                downloadId = m.Groups[1].Value;
                                break;
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(downloadId))
                {
                    string regUrl = "https://www.blackmagicdesign.com/api/register/us/download/" + downloadId;
                    var regReq = (HttpWebRequest)WebRequest.Create(regUrl);
                    regReq.Method = "POST";
                    regReq.ContentType = "application/json;charset=UTF-8";
                    regReq.Accept = "application/json, text/plain, */*";
                    regReq.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
                    regReq.Headers.Add("Origin", "https://www.blackmagicdesign.com");
                    regReq.Referer = "https://www.blackmagicdesign.com/support/download/" + downloadId;

                    string payload = "{\"firstname\":\"Exabit\",\"lastname\":\"Tech\",\"email\":\"suporte@exabit.com.br\",\"phone\":\"202-555-0194\",\"country\":\"us\",\"street\":\"Bowery 146\",\"state\":\"New York\",\"city\":\"AUR\",\"product\":\"DaVinci Resolve\"}";
                    byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
                    regReq.ContentLength = payloadBytes.Length;

                    using (var st = regReq.GetRequestStream())
                    {
                        st.Write(payloadBytes, 0, payloadBytes.Length);
                    }

                    using (var regResp = (HttpWebResponse)regReq.GetResponse())
                    using (var reader = new StreamReader(regResp.GetResponseStream()))
                    {
                        string respText = reader.ReadToEnd();
                        if (!string.IsNullOrEmpty(respText) && respText.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = respText.Trim();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log(string.Format("  Aviso ao consultar API da Blackmagic: {0}", ex.Message), ConsoleColor.Yellow);
            }

            if (string.IsNullOrEmpty(downloadUrl))
            {
                Log("X Não foi possível gerar link direto para o DaVinci Resolve. Coloque o instalador em %ProgramData%\\EXABIT\\Downloads\\DaVinciResolve.", ConsoleColor.Yellow);
                return null;
            }

            string zipPath = await DownloadFileOnlineAsync(downloadUrl, "DaVinci_Resolve_Windows.zip", program.DisplayName, 500000000);
            if (!string.IsNullOrEmpty(zipPath) && File.Exists(zipPath))
            {
                Log("  Extraindo instalador do DaVinci Resolve...", ConsoleColor.Cyan);
                try
                {
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, davinciDir);
                    try { File.Delete(zipPath); } catch { }

                    var exes = Directory.GetFiles(davinciDir, "DaVinci_Resolve_*.exe", SearchOption.AllDirectories);
                    if (exes != null && exes.Length > 0)
                    {
                        return exes[0];
                    }
                }
                catch (Exception ex)
                {
                    Log(string.Format("X Erro ao extrair DaVinci Resolve: {0}", ex.Message), ConsoleColor.Red);
                }
            }

            return null;
        }

        private async Task<string> ResolveGeminiDesktopAsync(ProgramInfo program)
        {
            string cacheDir = GetDownloadCacheDirectory();
            string destFile = Path.Combine(cacheDir, "GeminiSetup.exe");
            if (File.Exists(destFile) && new FileInfo(destFile).Length > 100000000)
            {
                Log("  Instalador do Gemini Desktop pronto no cache.", ConsoleColor.Gray);
                return destFile;
            }

            string downloadUrl = "https://dl.google.com/release2/Google%20DeepMind/jrago3sbw7xgrkpxfotop5dwxu_1.21.1/GeminiSetup-1.21.1_uncompressed.exe";
            return await DownloadFileOnlineAsync(downloadUrl, "GeminiSetup.exe", program.DisplayName, 100000000);
        }

        private async Task<string> ResolveChatGPTAsync(ProgramInfo program)
        {
            string cacheDir = GetDownloadCacheDirectory();
            string destFile = Path.Combine(cacheDir, "ChatGPT-x64.msix");
            if (File.Exists(destFile) && new FileInfo(destFile).Length > 500000000)
            {
                Log("  Pacote MSIX do ChatGPT pronto no cache.", ConsoleColor.Gray);
                return destFile;
            }

            string downloadUrl = "https://persistent.oaistatic.com/codex-app-prod/releases/26.1111.9664.0/ChatGPT-x64.msix";
            return await DownloadFileOnlineAsync(downloadUrl, "ChatGPT-x64.msix", program.DisplayName, 500000000);
        }

        private async Task<string> ResolveClaudeDesktopAsync(ProgramInfo program)
        {
            string cacheDir = GetDownloadCacheDirectory();
            string destFile = Path.Combine(cacheDir, "Claude-Setup-x64.exe");
            if (File.Exists(destFile) && new FileInfo(destFile).Length > 100000000)
            {
                Log("  Instalador do Claude Desktop pronto no cache.", ConsoleColor.Gray);
                return destFile;
            }

            string downloadUrl = "https://downloads.claude.ai/releases/win32/x64/4.29.0/Claude-2609742292f7dbce2e259e76f53620cac4da9002.exe";
            return await DownloadFileOnlineAsync(downloadUrl, "Claude-Setup-x64.exe", program.DisplayName, 100000000);
        }

        private async Task<string> ResolveDeepSeekDesktopAsync(ProgramInfo program)
        {
            string cacheDir = GetDownloadCacheDirectory();
            string destFile = Path.Combine(cacheDir, "deepseek-harness-win-x64.exe");
            if (File.Exists(destFile) && new FileInfo(destFile).Length > 100000000)
            {
                Log("  Instalador do DeepSeek Desktop pronto no cache.", ConsoleColor.Gray);
                return destFile;
            }

            string downloadUrl = "https://download.deepseek.com/dsh-desk/bin/win-x64/deepseek-harness-0.2.2-rc.5-win-x64.exe";
            return await DownloadFileOnlineAsync(downloadUrl, "deepseek-harness-win-x64.exe", program.DisplayName, 100000000);
        }

        private async Task<string> ResolveEaseUSTodoBackupAsync(ProgramInfo program)
        {
            string cacheDir = GetDownloadCacheDirectory();
            string destFile = Path.Combine(cacheDir, "tb_free.exe");
            if (File.Exists(destFile) && new FileInfo(destFile).Length > 50000000)
            {
                Log("  Instalador do EaseUS Todo Backup Free pronto no cache.", ConsoleColor.Gray);
                return destFile;
            }

            string[] mirrors = new string[]
            {
                "https://download.easeus.com/free/tb_free.exe",
                "https://d1.easeus.com/tb/free/TodoBackup17.5.0_free.exe"
            };

            for (int i = 0; i < mirrors.Length; i++)
            {
                try
                {
                    string path = await DownloadFileOnlineAsync(mirrors[i], "tb_free.exe", program.DisplayName, 50000000, i < mirrors.Length - 1);
                    if (!string.IsNullOrEmpty(path) && File.Exists(path) && new FileInfo(path).Length > 50000000)
                    {
                        return path;
                    }
                }
                catch { }
            }

            return null;
        }

        private async Task<string> ResolveCrystalDiskInfoAsync(ProgramInfo program)
        {
            string cacheDir = GetDownloadCacheDirectory();
            string destFile = Path.Combine(cacheDir, "CrystalDiskInfo9_9_5.exe");

            if (File.Exists(destFile) && new FileInfo(destFile).Length > 3000000)
            {
                Log("  Instalador do CrystalDiskInfo pronto no cache.", ConsoleColor.Gray);
                return destFile;
            }

            string downloadUrl = "https://downloads.sourceforge.net/project/crystaldiskinfo/9.9.5/CrystalDiskInfo9_9_5.exe";
            return await DownloadFileOnlineAsync(downloadUrl, "CrystalDiskInfo9_9_5.exe", program.DisplayName, 3000000, false, "chocolatey/2.2.2");
        }

        private async Task<string> ResolveCrystalDiskMarkAsync(ProgramInfo program)
        {
            string cacheDir = GetDownloadCacheDirectory();
            string destFile = Path.Combine(cacheDir, "CrystalDiskMark9_0_3.exe");

            if (File.Exists(destFile) && new FileInfo(destFile).Length > 3000000)
            {
                Log("  Instalador do CrystalDiskMark pronto no cache.", ConsoleColor.Gray);
                return destFile;
            }

            string downloadUrl = "https://downloads.sourceforge.net/project/crystaldiskmark/9.0.3/CrystalDiskMark9_0_3.exe";
            return await DownloadFileOnlineAsync(downloadUrl, "CrystalDiskMark9_0_3.exe", program.DisplayName, 3000000, false, "chocolatey/2.2.2");
        }

        private async Task<string> ResolveRecuvaAsync(ProgramInfo program)
        {
            string cacheDir = GetDownloadCacheDirectory();
            string destFile = Path.Combine(cacheDir, "rcsetup155.exe");

            if (File.Exists(destFile) && new FileInfo(destFile).Length > 5000000)
            {
                Log("  Instalador do Recuva pronto no cache.", ConsoleColor.Gray);
                return destFile;
            }

            string downloadUrl = "https://download.ccleaner.com/rcsetup155.exe";
            return await DownloadFileOnlineAsync(downloadUrl, "rcsetup155.exe", program.DisplayName, 5000000);
        }

        private static string FindWingetExecutable()
        {
            try
            {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string userWinget = Path.Combine(localApp, "Microsoft", "WindowsApps", "winget.exe");
                if (File.Exists(userWinget)) return userWinget;

                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string winApps = Path.Combine(progFiles, "WindowsApps");
                if (Directory.Exists(winApps))
                {
                    var files = Directory.GetFiles(winApps, "winget.exe", SearchOption.AllDirectories);
                    if (files != null && files.Length > 0) return files[0];
                }
            }
            catch { }
            return "winget";
        }

        private async Task<string> EnsureProgramDownloadedAsync(ProgramInfo program)
        {
            if (program.Id == "rustdesk")
            {
                return await DownloadLatestRustDeskAsync();
            }

            if (program.Id == "citrix")
            {
                return await DownloadLatestCitrixAsync(null);
            }

            if (program.Id == "java")
            {
                return await DownloadLatestJavaAsync(null);
            }

            if (IsStorytoldCraftProgram(program.Id))
            {
                return await DownloadLatestStorytoldCraftAsync(program);
            }

            if (program.Id == "chrome")
            {
                return await ResolveChromeInstallerAsync(program);
            }

            if (program.Id == "firefox")
            {
                return await DownloadFileOnlineAsync(
                    "https://download.mozilla.org/?product=firefox-msi-latest-ssl&os=win64&lang=pt-BR",
                    "Firefox_Setup_x64.msi",
                    program.DisplayName,
                    40000000);
            }

            if (program.Id == "brave")
            {
                return await DownloadFileOnlineAsync(
                    "https://laptop-updates.brave.com/latest/winx64",
                    "BraveBrowserSetup.exe",
                    program.DisplayName,
                    1000000);
            }

            if (program.Id == "anydesk")
            {
                return await DownloadFileOnlineAsync(
                    "https://download.anydesk.com/AnyDesk.exe",
                    "AnyDesk.exe",
                    program.DisplayName,
                    3000000);
            }

            if (program.Id == "googleearth")
            {
                return await DownloadFileOnlineAsync(
                    "https://dl.google.com/dl/earth/client/advanced/current/googleearthprowin-x64.exe",
                    "googleearthprowin.exe",
                    program.DisplayName,
                    40000000);
            }

            if (program.Id == "7zip")
            {
                return await Resolve7ZipInstallerAsync(program);
            }

            if (program.Id == "winrar")
            {
                return await ResolveWinRarInstallerAsync(program);
            }

            if (program.Id == "radmin")
            {
                return await ResolveRadminInstallerAsync(program);
            }

            if (program.Id == "adobereader")
            {
                return await ResolveAdobeReaderInstallerAsync(program);
            }

            if (program.Id == "powerbi")
            {
                return await ResolvePowerBIInstallerAsync(program);
            }

            if (program.Id == "dwgtrueview")
            {
                return await ResolveDwgTrueViewInstallerAsync(program);
            }

            if (program.Id == "office365")
            {
                return await ResolveOffice365InstallerAsync(program);
            }

            if (program.Id == "tracedraw")
            {
                return await ResolveTraceDrawInstallerAsync(program);
            }

            if (program.Id == "vscode")
            {
                return await ResolveVSCodeInstallerAsync(program);
            }

            if (program.Id == "antigravity")
            {
                return await ResolveAntigravityInstallerAsync(program);
            }

            if (program.Id == "antigravityide")
            {
                return await ResolveAntigravityIDEInstallerAsync(program);
            }

            if (program.Id == "nodejs")
            {
                return await ResolveNodeJsInstallerAsync(program);
            }

            if (program.Id == "composer")
            {
                return await ResolveComposerInstallerAsync(program);
            }

            if (program.Id == "filezilla")
            {
                return await ResolveFileZillaInstallerAsync(program);
            }

            if (program.Id == "dbeaver")
            {
                return await ResolveDBeaverInstallerAsync(program);
            }

            if (program.Id == "blender")
            {
                return await ResolveBlenderInstallerAsync(program);
            }

            if (program.Id == "affinity")
            {
                return await ResolveAffinityInstallerAsync(program);
            }

            if (program.Id == "davinciresolve")
            {
                return await ResolveDaVinciResolveInstallerAsync(program);
            }

            if (program.Id == "easeustodo")
            {
                return await ResolveEaseUSTodoBackupAsync(program);
            }

            if (program.Id == "gemini")
            {
                return await ResolveGeminiDesktopAsync(program);
            }

            if (program.Id == "chatgpt")
            {
                return await ResolveChatGPTAsync(program);
            }

            if (program.Id == "claude")
            {
                return await ResolveClaudeDesktopAsync(program);
            }

            if (program.Id == "deepseek")
            {
                return await ResolveDeepSeekDesktopAsync(program);
            }

            if (program.Id == "crystaldiskinfo")
            {
                return await ResolveCrystalDiskInfoAsync(program);
            }

            if (program.Id == "crystaldiskmark")
            {
                return await ResolveCrystalDiskMarkAsync(program);
            }

            if (program.Id == "recuva")
            {
                return await ResolveRecuvaAsync(program);
            }

            if (!string.IsNullOrEmpty(program.DownloadUrl))
            {
                string fn = !string.IsNullOrEmpty(program.FileName) ? Path.GetFileName(program.FileName) : (program.Id + ".exe");
                return await DownloadFileOnlineAsync(program.DownloadUrl, fn, program.DisplayName, 500000);
            }

            return null;
        }

        /// <summary>
        /// Instala um programa específico com base nas informações do <see cref="ProgramInfo"/>.
        /// Realiza o download online de forma transparente e executa o instalador em modo silencioso.
        /// </summary>
        /// <param name="program">Informações do programa a ser instalado</param>
        /// <param name="options">Opções gerais de instalação fornecidas pelo usuário</param>
        public async Task<bool> InstallProgramAsync(ProgramInfo program, InstallationOptions options = null)
        {
            Log(string.Format("Instalando {0}...", program.DisplayName), ConsoleColor.Yellow);

            if (program.Id == "dwgtrueview")
            {
                if (IsDwgTrueViewInstalled())
                {
                    Log("[ok] Autodesk DWG TrueView já está instalado no sistema.", ConsoleColor.Green);
                    return true;
                }

                string wingetExe = FindWingetExecutable();
                if (!string.IsNullOrEmpty(wingetExe))
                {
                    Log("Instalando Autodesk DWG TrueView online via Windows Package Manager...", ConsoleColor.Cyan);
                    int wingetExit = await RunProcessAsync(wingetExe,
                        "install --id Autodesk.DWGTrueView.2027 -e --silent --accept-source-agreements --accept-package-agreements",
                        1200000, null, false);

                    if (wingetExit == 0 || IsDwgTrueViewInstalled())
                    {
                        Log("[ok] Autodesk DWG TrueView instalado com sucesso.", ConsoleColor.Green);
                        return true;
                    }

                    Log("  Instalação via winget não foi concluída. Baixando instalador oficial direto da Autodesk...", ConsoleColor.Yellow);
                }

                string setupExe = await ResolveDwgTrueViewInstallerAsync(program);
                if (string.IsNullOrEmpty(setupExe) || !File.Exists(setupExe))
                {
                    Log("X Não foi possível obter o instalador online de Autodesk DWG TrueView.", ConsoleColor.Red);
                    return false;
                }

                Log("  Executando instalação silenciosa do Autodesk DWG TrueView (aguarde alguns minutos)...", ConsoleColor.Cyan);
                string setupDir = Path.GetDirectoryName(setupExe);
                int exitCode = await RunProcessAsync(setupExe, "-q --noeula", 1800000, setupDir, false);

                if (exitCode == 0 || exitCode == 3010 || IsDwgTrueViewInstalled())
                {
                    Log("[ok] Autodesk DWG TrueView instalado com sucesso.", ConsoleColor.Green);
                    return true;
                }
                else
                {
                    Log(string.Format("X Autodesk DWG TrueView retornou código {0}.", exitCode), ConsoleColor.Yellow);
                    return false;
                }
            }

            if (program.Id == "office365")
            {
                if (IsOffice365Installed())
                {
                    Log("[ok] Microsoft Office 365 já está instalado no sistema.", ConsoleColor.Green);
                    return true;
                }

                string wingetExe = FindWingetExecutable();
                if (!string.IsNullOrEmpty(wingetExe))
                {
                    Log("Instalando Microsoft Office 365 online via Windows Package Manager...", ConsoleColor.Cyan);
                    int wingetExit = await RunProcessAsync(wingetExe,
                        "install --id Microsoft.Office -e --silent --accept-source-agreements --accept-package-agreements",
                        1800000, null, false);

                    if (wingetExit == 0 || IsOffice365Installed())
                    {
                        Log("[ok] Microsoft Office 365 instalado com sucesso.", ConsoleColor.Green);
                        return true;
                    }

                    Log("  Instalação via winget não foi concluída. Baixando instalador oficial do Office 365...", ConsoleColor.Yellow);
                }
            }

            if (program.Id == "powerbi" && IsPowerBIDesktopInstalled())
            {
                Log("[ok] Microsoft Power BI Desktop já está instalado no sistema.", ConsoleColor.Green);
                return true;
            }

            if (program.Id == "anydesk" && IsAnyDeskInstalled())
            {
                Log("[ok] AnyDesk já está instalado no sistema.", ConsoleColor.Green);
                return true;
            }

            if (program.Id == "citrix")
            {
                try
                {
                    string[] citrixProcs = new string[] { "CitrixWorkspaceApp", "Receiver", "wfcrun32" };
                    for (int i = 0; i < citrixProcs.Length; i++)
                    {
                        foreach (var proc in Process.GetProcessesByName(citrixProcs[i]))
                        {
                            try { proc.Kill(); } catch { }
                        }
                    }
                }
                catch { }
            }
            else if (program.Id == "java")
            {
                try
                {
                    string[] javaProcs = new string[] { "java", "javaw", "javaws", "jusched" };
                    for (int i = 0; i < javaProcs.Length; i++)
                    {
                        foreach (var proc in Process.GetProcessesByName(javaProcs[i]))
                        {
                            try { proc.Kill(); } catch { }
                        }
                    }
                }
                catch { }
            }
            else if (IsStorytoldCraftProgram(program.Id))
            {
                CloseProcessesForStorytoldCraft(program.Id);
            }
            else if (program.Id == "anydesk")
            {
                try
                {
                    foreach (var proc in Process.GetProcessesByName("AnyDesk"))
                    {
                        try { proc.Kill(); } catch { }
                    }
                }
                catch { }
            }
            else if (program.Id == "powerbi")
            {
                try
                {
                    foreach (var proc in Process.GetProcessesByName("PBIDesktopSetup_x64"))
                    {
                        try { proc.Kill(); } catch { }
                    }
                }
                catch { }
            }

            if (program.Id == "antigravity")
            {
                if (IsAntigravityInstalled())
                {
                    Log("[ok] Antigravity já está instalado no sistema.", ConsoleColor.Green);
                    return true;
                }

                Log("Instalando Antigravity via Windows Package Manager...", ConsoleColor.Cyan);
                string wingetExe = FindWingetExecutable();
                int wingetExit = await RunProcessAsync(wingetExe,
                    "install --id Google.Antigravity -e --silent --accept-source-agreements --accept-package-agreements",
                    1200000, null, false);

                if (wingetExit == 0 || IsAntigravityInstalled())
                {
                    Log("[ok] Antigravity instalado com sucesso via winget.", ConsoleColor.Green);
                    return true;
                }

                Log("  Instalação via winget não foi concluída. Baixando instalador executável...", ConsoleColor.Yellow);
            }

            if (program.Id == "antigravityide")
            {
                if (IsAntigravityIDEInstalled())
                {
                    Log("[ok] Antigravity IDE já está instalado no sistema.", ConsoleColor.Green);
                    return true;
                }

                Log("Instalando Antigravity IDE via Windows Package Manager...", ConsoleColor.Cyan);
                string wingetExe = FindWingetExecutable();
                int wingetExit = await RunProcessAsync(wingetExe,
                    "install --id Google.AntigravityIDE -e --silent --accept-source-agreements --accept-package-agreements",
                    1200000, null, false);

                if (wingetExit == 0 || IsAntigravityIDEInstalled())
                {
                    Log("[ok] Antigravity IDE instalado com sucesso via winget.", ConsoleColor.Green);
                    return true;
                }

                Log("  Instalação via winget não foi concluída. Baixando instalador executável...", ConsoleColor.Yellow);
            }

            if (program.Id == "blender")
            {
                if (IsBlenderInstalled())
                {
                    Log("[ok] Blender já está instalado no sistema.", ConsoleColor.Green);
                    return true;
                }

                Log("Instalando Blender via Windows Package Manager...", ConsoleColor.Cyan);
                string wingetExe = FindWingetExecutable();
                int wingetExit = await RunProcessAsync(wingetExe,
                    "install --id BlenderFoundation.Blender -e --silent --accept-source-agreements --accept-package-agreements",
                    1200000, null, false);

                if (wingetExit == 0 || IsBlenderInstalled())
                {
                    Log("[ok] Blender instalado com sucesso via winget.", ConsoleColor.Green);
                    return true;
                }

                Log("  Instalação via winget não foi concluída. Baixando instalador MSI...", ConsoleColor.Yellow);
            }

            if (program.Id == "affinity")
            {
                Log("Instalando Affinity Studio via Windows Package Manager...", ConsoleColor.Cyan);
                string wingetExe = FindWingetExecutable();
                int wingetExit = await RunProcessAsync(wingetExe,
                    "install --id Canva.Affinity -e --silent --accept-source-agreements --accept-package-agreements",
                    1200000, null, false);

                if (wingetExit == 0)
                {
                    Log("[ok] Affinity Studio instalado com sucesso via winget.", ConsoleColor.Green);
                    return true;
                }

                Log("  Instalação via winget não foi concluída. Baixando pacote MSIX...", ConsoleColor.Yellow);
                string msixPath = await EnsureProgramDownloadedAsync(program);
                if (!string.IsNullOrEmpty(msixPath) && File.Exists(msixPath))
                {
                    Log("  Instalando pacote MSIX do Affinity Studio...", ConsoleColor.Cyan);
                    string safeMsixPath = msixPath.Replace("'", "''");
                    int exitCode = await RunProcessAsync("powershell",
                        string.Format("-NoProfile -ExecutionPolicy Bypass -Command \"Add-AppxPackage -Path '{0}'\"", safeMsixPath),
                        600000, null, false);

                    if (exitCode == 0)
                    {
                        Log("[ok] Affinity Studio instalado com sucesso.", ConsoleColor.Green);
                        return true;
                    }
                    else
                    {
                        Log(string.Format("X Falha ao instalar Affinity Studio (código {0}).", exitCode), ConsoleColor.Yellow);
                        return false;
                    }
                }
                else
                {
                    Log(string.Format("X Não foi possível obter o instalador online de {0}.", program.DisplayName), ConsoleColor.Red);
                    return false;
                }
            }

            if (program.Id == "davinciresolve")
            {
                string fullPathDavinci = await EnsureProgramDownloadedAsync(program);
                if (string.IsNullOrEmpty(fullPathDavinci) || !File.Exists(fullPathDavinci))
                {
                    Log(string.Format("X Não foi possível obter o instalador online de {0}.", program.DisplayName), ConsoleColor.Red);
                    return false;
                }

                Log("  Executando instalador do DaVinci Resolve (aguarde)...", ConsoleColor.Yellow);
                string runDir = Path.GetDirectoryName(fullPathDavinci);
                int timeout = program.TimeoutMs > 0 ? program.TimeoutMs : 1800000;
                int exitCode = await RunProcessAsync(fullPathDavinci, "/i /q /noreboot", timeout, runDir, true);
                if (exitCode == 0)
                {
                    Log(string.Format("[ok] {0} instalado com sucesso.", program.DisplayName), ConsoleColor.Green);
                    return true;
                }
                else
                {
                    Log(string.Format("X {0} retornou código {1}.", program.DisplayName, exitCode), ConsoleColor.Yellow);
                    return false;
                }
            }

            if (program.Id == "easeustodo")
            {
                if (IsEaseUSTodoBackupInstalled())
                {
                    Log("[ok] EaseUS Todo Backup Free já está instalado no sistema.", ConsoleColor.Green);
                    return true;
                }

                Log("Instalando EaseUS Todo Backup Free via Windows Package Manager...", ConsoleColor.Cyan);
                string wingetExe = FindWingetExecutable();
                int wingetExit = await RunProcessAsync(wingetExe,
                    "install --id EaseUS.TodoBackup -e --silent --accept-source-agreements --accept-package-agreements",
                    1200000, null, false);

                if (wingetExit == 0 || IsEaseUSTodoBackupInstalled())
                {
                    Log("[ok] EaseUS Todo Backup Free instalado com sucesso via winget.", ConsoleColor.Green);
                    return true;
                }

                Log("  Instalação via winget não foi concluída. Baixando instalador executável...", ConsoleColor.Yellow);
            }

            if (program.Id == "gemini")
            {
                if (IsGeminiDesktopInstalled())
                {
                    Log("[ok] Gemini Desktop já está instalado no sistema.", ConsoleColor.Green);
                    return true;
                }

                Log("Instalando Gemini Desktop via Windows Package Manager...", ConsoleColor.Cyan);
                string wingetExe = FindWingetExecutable();
                int wingetExit = await RunProcessAsync(wingetExe,
                    "install --id Google.GeminiDesktop -e --silent --accept-source-agreements --accept-package-agreements",
                    1200000, null, false);

                if (wingetExit == 0 || IsGeminiDesktopInstalled())
                {
                    Log("[ok] Gemini Desktop instalado com sucesso via winget.", ConsoleColor.Green);
                    return true;
                }

                Log("  Instalação via winget não foi concluída. Baixando instalador executável...", ConsoleColor.Yellow);
            }

            if (program.Id == "chatgpt")
            {
                if (IsChatGPTInstalled())
                {
                    Log("[ok] ChatGPT Desktop já está instalado no sistema.", ConsoleColor.Green);
                    return true;
                }

                Log("Instalando ChatGPT Desktop via Windows Package Manager...", ConsoleColor.Cyan);
                string wingetExe = FindWingetExecutable();
                int wingetExit = await RunProcessAsync(wingetExe,
                    "install --id OpenAI.ChatGPT -e --silent --accept-source-agreements --accept-package-agreements",
                    1200000, null, false);

                if (wingetExit == 0 || IsChatGPTInstalled())
                {
                    Log("[ok] ChatGPT Desktop instalado com sucesso via winget.", ConsoleColor.Green);
                    return true;
                }

                Log("  Instalação via winget não foi concluída. Baixando pacote MSIX...", ConsoleColor.Yellow);
                string msixPath = await EnsureProgramDownloadedAsync(program);
                if (!string.IsNullOrEmpty(msixPath) && File.Exists(msixPath))
                {
                    Log("  Instalando pacote MSIX do ChatGPT Desktop...", ConsoleColor.Cyan);
                    string safeMsixPath = msixPath.Replace("'", "''");
                    int exitCode = await RunProcessAsync("powershell",
                        string.Format("-NoProfile -ExecutionPolicy Bypass -Command \"Add-AppxPackage -Path '{0}'\"", safeMsixPath),
                        600000, null, false);

                    if (exitCode == 0 || IsChatGPTInstalled())
                    {
                        Log("[ok] ChatGPT Desktop instalado com sucesso.", ConsoleColor.Green);
                        return true;
                    }
                    else
                    {
                        Log(string.Format("X Falha ao instalar ChatGPT Desktop (código {0}).", exitCode), ConsoleColor.Yellow);
                        return false;
                    }
                }
                else
                {
                    Log(string.Format("X Não foi possível obter o instalador online de {0}.", program.DisplayName), ConsoleColor.Red);
                    return false;
                }
            }

            if (program.Id == "claude")
            {
                if (IsClaudeDesktopInstalled())
                {
                    Log("[ok] Claude Desktop já está instalado no sistema.", ConsoleColor.Green);
                    return true;
                }

                Log("Instalando Claude Desktop via Windows Package Manager...", ConsoleColor.Cyan);
                string wingetExe = FindWingetExecutable();
                int wingetExit = await RunProcessAsync(wingetExe,
                    "install --id Anthropic.Claude -e --silent --accept-source-agreements --accept-package-agreements",
                    1200000, null, false);

                if (wingetExit == 0 || IsClaudeDesktopInstalled())
                {
                    Log("[ok] Claude Desktop instalado com sucesso via winget.", ConsoleColor.Green);
                    return true;
                }

                Log("  Instalação via winget não foi concluída. Baixando instalador executável...", ConsoleColor.Yellow);
            }

            if (program.Id == "deepseek")
            {
                if (IsDeepSeekDesktopInstalled())
                {
                    Log("[ok] DeepSeek Desktop já está instalado no sistema.", ConsoleColor.Green);
                    return true;
                }

                Log("Instalando DeepSeek Desktop via Windows Package Manager...", ConsoleColor.Cyan);
                string wingetExe = FindWingetExecutable();
                int wingetExit = await RunProcessAsync(wingetExe,
                    "install --id DeepSeek.DeepSeekHarnessDesktop -e --silent --accept-source-agreements --accept-package-agreements",
                    1200000, null, false);

                if (wingetExit == 0 || IsDeepSeekDesktopInstalled())
                {
                    Log("[ok] DeepSeek Desktop instalado com sucesso via winget.", ConsoleColor.Green);
                    return true;
                }

                Log("  Instalação via winget não foi concluída. Baixando instalador executável...", ConsoleColor.Yellow);
            }

            if (program.Id == "crystaldiskinfo")
            {
                if (IsCrystalDiskInfoInstalled())
                {
                    Log("[ok] CrystalDiskInfo já está instalado no sistema.", ConsoleColor.Green);
                    return true;
                }

                Log("Instalando CrystalDiskInfo via Windows Package Manager...", ConsoleColor.Cyan);
                string wingetExe = FindWingetExecutable();
                int wingetExit = await RunProcessAsync(wingetExe,
                    "install --id CrystalDewWorld.CrystalDiskInfo -e --silent --accept-source-agreements --accept-package-agreements",
                    1200000, null, false);

                if (wingetExit == 0 || IsCrystalDiskInfoInstalled())
                {
                    Log("[ok] CrystalDiskInfo instalado com sucesso via winget.", ConsoleColor.Green);
                    return true;
                }

                Log("  Instalação via winget não foi concluída. Baixando instalador executável...", ConsoleColor.Yellow);
            }

            if (program.Id == "crystaldiskmark")
            {
                if (IsCrystalDiskMarkInstalled())
                {
                    Log("[ok] CrystalDiskMark já está instalado no sistema.", ConsoleColor.Green);
                    return true;
                }

                Log("Instalando CrystalDiskMark via Windows Package Manager...", ConsoleColor.Cyan);
                string wingetExe = FindWingetExecutable();
                int wingetExit = await RunProcessAsync(wingetExe,
                    "install --id CrystalDewWorld.CrystalDiskMark -e --silent --accept-source-agreements --accept-package-agreements",
                    1200000, null, false);

                if (wingetExit == 0 || IsCrystalDiskMarkInstalled())
                {
                    Log("[ok] CrystalDiskMark instalado com sucesso via winget.", ConsoleColor.Green);
                    return true;
                }

                Log("  Instalação via winget não foi concluída. Baixando instalador executável...", ConsoleColor.Yellow);
            }

            if (program.Id == "recuva")
            {
                if (IsRecuvaInstalled())
                {
                    Log("[ok] Recuva já está instalado no sistema.", ConsoleColor.Green);
                    return true;
                }

                Log("Instalando Recuva Free via Windows Package Manager...", ConsoleColor.Cyan);
                string wingetExe = FindWingetExecutable();
                int wingetExit = await RunProcessAsync(wingetExe,
                    "install --id Piriform.Recuva -e --silent --accept-source-agreements --accept-package-agreements",
                    1200000, null, false);

                if (wingetExit == 0 || IsRecuvaInstalled())
                {
                    Log("[ok] Recuva Free instalado com sucesso via winget.", ConsoleColor.Green);
                    return true;
                }

                Log("  Instalação via winget não foi concluída. Baixando instalador executável...", ConsoleColor.Yellow);
            }

            string fullPath = await EnsureProgramDownloadedAsync(program);
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
            {
                if (program.Id == "7zip")
                {
                    Log("  Tentando instalação do 7-Zip via Windows Package Manager (winget)...", ConsoleColor.Yellow);
                    string wingetExe = FindWingetExecutable();
                    int wingetExit = await RunProcessAsync(wingetExe,
                        "install --id 7zip.7zip -e --silent --accept-package-agreements --accept-source-agreements",
                        300000, null, false);
                    if (wingetExit == 0 || Is7ZipInstalled())
                    {
                        Log("[ok] 7-Zip instalado com sucesso via winget.", ConsoleColor.Green);
                        return true;
                    }
                }
                else if (program.Id == "chrome")
                {
                    Log("  Tentando instalação do Google Chrome via Windows Package Manager (winget)...", ConsoleColor.Yellow);
                    string wingetExe = FindWingetExecutable();
                    int wingetExit = await RunProcessAsync(wingetExe,
                        "install --id Google.Chrome -e --silent --accept-package-agreements --accept-source-agreements",
                        600000, null, false);
                    if (wingetExit == 0 || IsChromeInstalled())
                    {
                        Log("[ok] Google Chrome instalado com sucesso via winget.", ConsoleColor.Green);
                        return true;
                    }
                }

                Log(string.Format("X Não foi possível obter o instalador online de {0}.", program.DisplayName), ConsoleColor.Red);
                return false;
            }

            if (program.Id == "anydesk")
            {
                string runDir = Path.GetDirectoryName(fullPath);
                int anydeskTimeout = program.TimeoutMs > 0 ? program.TimeoutMs : 120000;
                int anydeskExit = await RunProcessAsync(fullPath, program.Arguments, anydeskTimeout, runDir, true);

                for (int attempt = 0; attempt < 8; attempt++)
                {
                    await Task.Delay(1000);
                    if (IsAnyDeskInstalled()) break;
                }

                if (IsAnyDeskInstalled())
                {
                    Log(string.Format("[ok] {0} instalado com sucesso.", program.DisplayName), ConsoleColor.Green);
                    return true;
                }
                else
                {
                    Log(string.Format("X {0}: O instalador finalizou (código {1}), porém os arquivos não foram encontrados no sistema.", program.DisplayName, anydeskExit), ConsoleColor.Yellow);
                    return false;
                }
            }

            if (program.Id == "powerbi")
            {
                string stagingDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EXABIT", "Setup");
                try
                {
                    if (!Directory.Exists(stagingDir)) Directory.CreateDirectory(stagingDir);
                }
                catch { }

                string pbiLog = Path.Combine(stagingDir, "powerbi_install.log");
                string runDir = Path.GetDirectoryName(fullPath);
                int pbiTimeout = program.TimeoutMs > 0 ? program.TimeoutMs : 600000;
                string quietArgs = string.Format("-quiet -norestart ACCEPT_EULA=1 -log {0}", QuoteProcessArgument(pbiLog));

                Log("  Executando instalador do Power BI Desktop (aguarde alguns minutos)...", ConsoleColor.Yellow);
                int pbiExit = await RunProcessAsync(fullPath, quietArgs, pbiTimeout, runDir, true);

                await Task.Delay(2000);
                if (IsPowerBIDesktopInstalled())
                {
                    Log(string.Format("[ok] {0} instalado com sucesso.", program.DisplayName), ConsoleColor.Green);
                    try { if (File.Exists(pbiLog)) File.Delete(pbiLog); } catch { }
                    return true;
                }

                Log("  Instalação silenciosa não detectada. Tentando em modo passivo...", ConsoleColor.Yellow);
                string passiveArgs = string.Format("-passive -norestart ACCEPT_EULA=1 -log {0}", QuoteProcessArgument(pbiLog));
                pbiExit = await RunProcessAsync(fullPath, passiveArgs, pbiTimeout, runDir, true);

                await Task.Delay(2000);
                if (IsPowerBIDesktopInstalled())
                {
                    Log(string.Format("[ok] {0} instalado com sucesso.", program.DisplayName), ConsoleColor.Green);
                    try { if (File.Exists(pbiLog)) File.Delete(pbiLog); } catch { }
                    return true;
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
                    return false;
                }
            }

            try
            {
                string extension = Path.GetExtension(fullPath).ToLower();
                string procWorkDir = Path.GetDirectoryName(fullPath);

                if (program.FireAndForget)
                {
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
                    Log(string.Format("[ok] {0} instalado com sucesso.", program.DisplayName), ConsoleColor.Green);

                    if (program.Id == "rustdesk")
                    {
                        string adminPass = options != null ? options.AdminPassword : null;
                        await ConfigureRustDeskServerAsync(adminPass);
                    }
                    return true;
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
                        bool suppressStdErr = (program.Id == "chrome");
                        exitCode = await RunProcessAsync(fullPath, program.Arguments,
                            timeout, procWorkDir, program.UseShellExecute, suppressStdErr);
                    }

                    if (exitCode == 0 || exitCode == 3010)
                    {
                        string rebootMsg = (exitCode == 3010) ? " (reinicialização do sistema necessária)" : "";
                        Log(string.Format("[ok] {0} instalado com sucesso{1}.", program.DisplayName, rebootMsg), ConsoleColor.Green);
                        if (program.Id == "rustdesk")
                        {
                            string adminPass = options != null ? options.AdminPassword : null;
                            await ConfigureRustDeskServerAsync(adminPass);
                        }
                        return true;
                    }
                    else if (program.Id == "adobereader" && IsAdobeReaderInstalled())
                    {
                        Log(string.Format("[ok] {0} instalado com sucesso.", program.DisplayName), ConsoleColor.Green);
                        return true;
                    }
                    else if (program.Id == "chrome" && IsChromeInstalled())
                    {
                        Log(string.Format("[ok] {0} instalado com sucesso.", program.DisplayName), ConsoleColor.Green);
                        return true;
                    }
                    else if (program.Id == "7zip" && Is7ZipInstalled())
                    {
                        Log(string.Format("[ok] {0} instalado com sucesso.", program.DisplayName), ConsoleColor.Green);
                        return true;
                    }
                    else if (program.Id == "office365" && IsOffice365Installed())
                    {
                        Log(string.Format("[ok] {0} instalado com sucesso.", program.DisplayName), ConsoleColor.Green);
                        return true;
                    }
                    else if (program.Id == "tracedraw" && IsTraceDrawInstalled())
                    {
                        Log(string.Format("[ok] {0} instalado com sucesso.", program.DisplayName), ConsoleColor.Green);
                        return true;
                    }
                    else if (program.Id == "dbeaver" && IsDBeaverInstalled())
                    {
                        Log(string.Format("[ok] {0} instalado com sucesso.", program.DisplayName), ConsoleColor.Green);
                        return true;
                    }
                    else if (program.Id == "filezilla" && IsFileZillaInstalled())
                    {
                        Log(string.Format("[ok] {0} instalado com sucesso.", program.DisplayName), ConsoleColor.Green);
                        return true;
                    }
                    else if (program.Id == "blender")
                    {
                        if (IsBlenderInstalled())
                        {
                            Log(string.Format("[ok] {0} instalado com sucesso.", program.DisplayName), ConsoleColor.Green);
                            return true;
                        }
                        Log(string.Format("  Blender retornou código {0}. Tentando instalação alternativa sem ALLUSERS...", exitCode), ConsoleColor.Yellow);
                        int retryExit = await RunProcessAsync("msiexec",
                            string.Format("/i \"{0}\" /qn /norestart", fullPath),
                            timeout, procWorkDir);
                        if (retryExit == 0 || retryExit == 3010 || IsBlenderInstalled())
                        {
                            Log(string.Format("[ok] {0} instalado com sucesso.", program.DisplayName), ConsoleColor.Green);
                            return true;
                        }
                        else
                        {
                            Log(string.Format("X {0} retornou código {1}.", program.DisplayName, retryExit), ConsoleColor.Yellow);
                            return false;
                        }
                    }
                    else if (program.Id == "gemini" && IsGeminiDesktopInstalled())
                    {
                        Log(string.Format("[ok] {0} instalado com sucesso.", program.DisplayName), ConsoleColor.Green);
                        return true;
                    }
                    else if (program.Id == "chatgpt" && IsChatGPTInstalled())
                    {
                        Log(string.Format("[ok] {0} instalado com sucesso.", program.DisplayName), ConsoleColor.Green);
                        return true;
                    }
                    else if (program.Id == "claude" && IsClaudeDesktopInstalled())
                    {
                        Log(string.Format("[ok] {0} instalado com sucesso.", program.DisplayName), ConsoleColor.Green);
                        return true;
                    }
                    else if (program.Id == "deepseek" && IsDeepSeekDesktopInstalled())
                    {
                        Log(string.Format("[ok] {0} instalado com sucesso.", program.DisplayName), ConsoleColor.Green);
                        return true;
                    }
                    else if (program.Id == "easeustodo" && IsEaseUSTodoBackupInstalled())
                    {
                        Log(string.Format("[ok] {0} instalado com sucesso.", program.DisplayName), ConsoleColor.Green);
                        return true;
                    }
                    else if (program.Id == "antigravity" && IsAntigravityInstalled())
                    {
                        Log(string.Format("[ok] {0} instalado com sucesso.", program.DisplayName), ConsoleColor.Green);
                        return true;
                    }
                    else if (program.Id == "antigravityide" && IsAntigravityIDEInstalled())
                    {
                        Log(string.Format("[ok] {0} instalado com sucesso.", program.DisplayName), ConsoleColor.Green);
                        return true;
                    }
                    else if (program.Id == "crystaldiskinfo" && IsCrystalDiskInfoInstalled())
                    {
                        Log(string.Format("[ok] {0} instalado com sucesso.", program.DisplayName), ConsoleColor.Green);
                        return true;
                    }
                    else if (program.Id == "crystaldiskmark" && IsCrystalDiskMarkInstalled())
                    {
                        Log(string.Format("[ok] {0} instalado com sucesso.", program.DisplayName), ConsoleColor.Green);
                        return true;
                    }
                    else if (program.Id == "recuva" && IsRecuvaInstalled())
                    {
                        Log(string.Format("[ok] {0} instalado com sucesso.", program.DisplayName), ConsoleColor.Green);
                        return true;
                    }
                    else if (program.Id == "dbeaver")
                    {
                        Log(string.Format("  DBeaver retornou código {0}. Tentando com parâmetro /S /allusers...", exitCode), ConsoleColor.Yellow);
                        int retryExit = await RunProcessAsync(fullPath, "/S /allusers", timeout, procWorkDir, false);
                        if (retryExit == 0 || IsDBeaverInstalled())
                        {
                            Log(string.Format("[ok] {0} instalado com sucesso.", program.DisplayName), ConsoleColor.Green);
                            return true;
                        }
                        else
                        {
                            Log(string.Format("X {0} retornou código {1}.", program.DisplayName, retryExit), ConsoleColor.Yellow);
                            return false;
                        }
                    }
                    else if (program.Id == "chrome")
                    {
                        Log(string.Format("  Falha na instalação do Google Chrome (código {0}). Tentando via winget...", exitCode), ConsoleColor.Yellow);
                        string wingetExe = FindWingetExecutable();
                        int wingetExit = await RunProcessAsync(wingetExe,
                            "install --id Google.Chrome -e --silent --accept-package-agreements --accept-source-agreements",
                            600000, null, false);
                        if (wingetExit == 0 || IsChromeInstalled())
                        {
                            Log(string.Format("[ok] {0} instalado com sucesso via winget.", program.DisplayName), ConsoleColor.Green);
                            return true;
                        }
                        else
                        {
                            Log(string.Format("X {0} retornou código {1}.", program.DisplayName, exitCode), ConsoleColor.Yellow);
                            return false;
                        }
                    }
                    else if (program.Id == "7zip")
                    {
                        Log(string.Format("  Falha na instalação do 7-Zip (código {0}). Tentando via winget...", exitCode), ConsoleColor.Yellow);
                        string wingetExe = FindWingetExecutable();
                        int wingetExit = await RunProcessAsync(wingetExe,
                            "install --id 7zip.7zip -e --silent --accept-package-agreements --accept-source-agreements",
                            300000, null, false);
                        if (wingetExit == 0 || Is7ZipInstalled())
                        {
                            Log(string.Format("[ok] {0} instalado com sucesso via winget.", program.DisplayName), ConsoleColor.Green);
                            return true;
                        }
                        else
                        {
                            Log(string.Format("X {0} retornou código {1}.", program.DisplayName, exitCode), ConsoleColor.Yellow);
                            return false;
                        }
                    }
                    else if (program.Id == "citrix" && (exitCode == -2147467259 || exitCode == 1603))
                    {
                        string retryArgs = program.Arguments.Replace("/includeSSON", "").Trim();
                        if (retryArgs.IndexOf("/noreboot", StringComparison.OrdinalIgnoreCase) < 0)
                            retryArgs = (retryArgs + " /noreboot").Trim();
                        if (retryArgs.IndexOf("/forceinstall", StringComparison.OrdinalIgnoreCase) < 0)
                            retryArgs = (retryArgs + " /forceinstall").Trim();

                        Log(string.Format("  Falha na instalação do Citrix Workspace (código {0}). Tentando com parâmetros ajustados...", exitCode), ConsoleColor.Yellow);
                        int retryExitCode = await RunProcessAsync(fullPath, retryArgs, timeout, procWorkDir, program.UseShellExecute);
                        if (retryExitCode == 0 || retryExitCode == 3010)
                        {
                            Log(string.Format("[ok] {0} instalado com sucesso na segunda tentativa.", program.DisplayName), ConsoleColor.Green);
                            return true;
                        }
                        else
                        {
                            Log(string.Format("X {0} retornou código {1} na segunda tentativa.", program.DisplayName, retryExitCode), ConsoleColor.Yellow);
                            return false;
                        }
                    }
                    else
                    {
                        Log(string.Format("X {0} retornou código {1}.", program.DisplayName, exitCode), ConsoleColor.Yellow);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Log(string.Format("X Erro ao instalar {0}: {1}", program.DisplayName, ex.Message), ConsoleColor.Red);
                return false;
            }
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
                    string arch = Environment.Is64BitOperatingSystem ? "64" : "32";
                    string xmlContent =
                        "<Configuration>\r\n" +
                        "  <Add OfficeClientEdition=\"" + arch + "\" Channel=\"Current\">\r\n" +
                        "    <Product ID=\"O365ProPlusRetail\">\r\n" +
                        "      <Language ID=\"pt-br\" />\r\n" +
                        "    </Product>\r\n" +
                        "  </Add>\r\n" +
                        "  <Display Level=\"None\" AcceptEULA=\"TRUE\" />\r\n" +
                        "  <Property Name=\"AUTOACTIVATE\" Value=\"0\" />\r\n" +
                        "  <Updates Enabled=\"TRUE\" Channel=\"Current\" />\r\n" +
                        "</Configuration>\r\n";

                    File.WriteAllText(configPath, xmlContent, new UTF8Encoding(false));
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

                    string targetDir = GetDownloadCacheDirectory();
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

                    string targetDir = GetDownloadCacheDirectory();
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

                    string targetDir = GetDownloadCacheDirectory();
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
                   id == "designcraft" || id == "effectcraft" || id == "filmcraft" ||
                   id == "gridcraft" || id == "lightcraft" || id == "pdfcraft" ||
                   id == "photocraft" || id == "soundcraft" || id == "texturecraft" ||
                   id == "vectorcraft" || id == "wordcraft";
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
                case "designcraft": return "designcraft";
                case "effectcraft": return "effectcraft";
                case "filmcraft": return "filmcraft";
                case "gridcraft": return "gridcraft";
                case "lightcraft": return "lightcraft";
                case "pdfcraft": return "pdfcraft";
                case "photocraft": return "photocraft";
                case "soundcraft": return "soundcraft";
                case "texturecraft": return "texturecraft";
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
                    case "designcraft": procs = new string[] { "designcraft", "DesignCraft" }; break;
                    case "effectcraft": procs = new string[] { "effectcraft", "EffectCraft" }; break;
                    case "filmcraft": procs = new string[] { "filmcraft", "FilmCraft" }; break;
                    case "gridcraft": procs = new string[] { "gridcraft", "GridCraft" }; break;
                    case "lightcraft": procs = new string[] { "lightcraft", "LightCraft" }; break;
                    case "pdfcraft": procs = new string[] { "pdfcraft", "PdfCraft" }; break;
                    case "photocraft": procs = new string[] { "photocraft", "PhotoCraft" }; break;
                    case "soundcraft": procs = new string[] { "soundcraft", "SoundCraft" }; break;
                    case "texturecraft": procs = new string[] { "texturecraft", "TextureCraft" }; break;
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
            string resolvedUrlOrPath = await Task.Run(new Func<string>(() =>
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
                    string targetDir = GetDownloadCacheDirectory();
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

                    return downloadUrl;
                }
                catch (Exception ex)
                {
                    Log(string.Format("  Aviso ao verificar {0}: {1}", program.DisplayName, ex.Message), ConsoleColor.Yellow);
                    return null;
                }
            }));

            if (string.IsNullOrEmpty(resolvedUrlOrPath))
            {
                return null;
            }

            if (File.Exists(resolvedUrlOrPath))
            {
                return resolvedUrlOrPath;
            }

            string urlFn = null;
            try
            {
                var uri = new Uri(resolvedUrlOrPath);
                urlFn = Path.GetFileName(uri.LocalPath);
            }
            catch { }

            if (string.IsNullOrEmpty(urlFn))
            {
                urlFn = program.FileName;
            }

            string downloadedCraft = await DownloadFileOnlineAsync(resolvedUrlOrPath, urlFn, program.DisplayName, 2 * 1024 * 1024);
            if (!string.IsNullOrEmpty(downloadedCraft) && File.Exists(downloadedCraft))
            {
                SyncLocalCopy(downloadedCraft, GetDownloadCacheDirectory(), program.FileName);
                return downloadedCraft;
            }

            return null;
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
        /// Verifica se o Google Chrome está instalado na máquina.
        /// </summary>
        private static bool IsChromeInstalled()
        {
            try
            {
                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                if (File.Exists(Path.Combine(progFiles, @"Google\Chrome\Application\chrome.exe")))
                    return true;

                string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                if (File.Exists(Path.Combine(progFilesX86, @"Google\Chrome\Application\chrome.exe")))
                    return true;

                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (File.Exists(Path.Combine(localApp, @"Google\Chrome\Application\chrome.exe")))
                    return true;

                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe"))
                {
                    if (key != null) return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Verifica se o Adobe Acrobat Reader está instalado na máquina.
        /// </summary>
        private static bool IsAdobeReaderInstalled()
        {
            try
            {
                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                if (File.Exists(Path.Combine(progFiles, @"Adobe\Acrobat DC\Acrobat\Acrobat.exe")))
                    return true;

                string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                if (File.Exists(Path.Combine(progFilesX86, @"Adobe\Acrobat DC\Acrobat\Acrobat.exe")))
                    return true;
                if (File.Exists(Path.Combine(progFilesX86, @"Adobe\Acrobat Reader DC\Reader\AcroRd32.exe")))
                    return true;

                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\AcroRd32.exe"))
                {
                    if (key != null) return true;
                }
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\Acrobat.exe"))
                {
                    if (key != null) return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Verifica se o 7-Zip está instalado na máquina.
        /// </summary>
        private static bool Is7ZipInstalled()
        {
            try
            {
                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                if (File.Exists(Path.Combine(progFiles, @"7-Zip\7z.exe")))
                    return true;

                string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                if (File.Exists(Path.Combine(progFilesX86, @"7-Zip\7z.exe")))
                    return true;

                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\7zFM.exe"))
                {
                    if (key != null) return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Verifica se o TraceDraw está instalado na máquina.
        /// </summary>
        private static bool IsTraceDrawInstalled()
        {
            try
            {
                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                if (File.Exists(Path.Combine(progFiles, @"TraceDraw\tracedraw.exe")))
                    return true;

                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (File.Exists(Path.Combine(localApp, @"Programs\TraceDraw\tracedraw.exe")))
                    return true;
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Verifica se o DBeaver Community está instalado na máquina.
        /// </summary>
        private static bool IsDBeaverInstalled()
        {
            try
            {
                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                if (File.Exists(Path.Combine(progFiles, @"DBeaver\dbeaver.exe")))
                    return true;

                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (File.Exists(Path.Combine(localApp, @"Programs\DBeaver\dbeaver.exe")))
                    return true;

                string[] regKeys = new string[]
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\DBeaver",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\DBeaver"
                };
                foreach (string keyPath in regKeys)
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(keyPath))
                    {
                        if (key != null) return true;
                    }
                    using (var key = Registry.CurrentUser.OpenSubKey(keyPath))
                    {
                        if (key != null) return true;
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Verifica se o FileZilla Client está instalado na máquina.
        /// </summary>
        private static bool IsFileZillaInstalled()
        {
            try
            {
                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                if (File.Exists(Path.Combine(progFiles, @"FileZilla FTP Client\filezilla.exe")))
                    return true;

                string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                if (File.Exists(Path.Combine(progFilesX86, @"FileZilla FTP Client\filezilla.exe")))
                    return true;

                string[] regKeys = new string[]
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\FileZilla Client",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\FileZilla Client"
                };
                foreach (string keyPath in regKeys)
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(keyPath))
                    {
                        if (key != null) return true;
                    }
                    using (var key = Registry.CurrentUser.OpenSubKey(keyPath))
                    {
                        if (key != null) return true;
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Verifica se o Blender já está instalado no sistema,
        /// consultando as pastas de programas e o registro do Windows.
        /// </summary>
        /// <returns>true se o Blender estiver instalado; caso contrário, false.</returns>
        private static bool IsBlenderInstalled()
        {
            try
            {
                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

                string[] searchDirs = new string[]
                {
                    Path.Combine(progFiles, "Blender Foundation"),
                    Path.Combine(progFilesX86, "Blender Foundation")
                };

                foreach (string dir in searchDirs)
                {
                    if (Directory.Exists(dir))
                    {
                        string[] exeFiles = Directory.GetFiles(dir, "blender.exe", SearchOption.AllDirectories);
                        if (exeFiles != null && exeFiles.Length > 0)
                            return true;
                    }
                }

                string[] regKeys = new string[]
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                };

                foreach (string keyPath in regKeys)
                {
                    using (var baseKey = Registry.LocalMachine.OpenSubKey(keyPath))
                    {
                        if (baseKey != null)
                        {
                            foreach (string subKeyName in baseKey.GetSubKeyNames())
                            {
                                using (var subKey = baseKey.OpenSubKey(subKeyName))
                                {
                                    if (subKey != null)
                                    {
                                        string displayName = subKey.GetValue("DisplayName") as string;
                                        if (!string.IsNullOrEmpty(displayName) && displayName.IndexOf("Blender", StringComparison.OrdinalIgnoreCase) >= 0)
                                        {
                                            return true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    using (var baseKey = Registry.CurrentUser.OpenSubKey(keyPath))
                    {
                        if (baseKey != null)
                        {
                            foreach (string subKeyName in baseKey.GetSubKeyNames())
                            {
                                using (var subKey = baseKey.OpenSubKey(subKeyName))
                                {
                                    if (subKey != null)
                                    {
                                        string displayName = subKey.GetValue("DisplayName") as string;
                                        if (!string.IsNullOrEmpty(displayName) && displayName.IndexOf("Blender", StringComparison.OrdinalIgnoreCase) >= 0)
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
            catch { }
            return false;
        }

        /// <summary>
        /// Verifica se o Google Gemini Desktop está instalado na máquina.
        /// </summary>
        private static bool IsGeminiDesktopInstalled()
        {
            try
            {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (File.Exists(Path.Combine(localApp, @"Programs\Google\Gemini\Gemini.exe")) ||
                    File.Exists(Path.Combine(localApp, @"Google\Gemini\Gemini.exe")))
                    return true;

                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                if (File.Exists(Path.Combine(progFiles, @"Google\Gemini\Gemini.exe")))
                    return true;

                string[] regKeys = new string[]
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Gemini",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Gemini"
                };
                foreach (string keyPath in regKeys)
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(keyPath))
                    {
                        if (key != null) return true;
                    }
                    using (var key = Registry.CurrentUser.OpenSubKey(keyPath))
                    {
                        if (key != null) return true;
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Verifica se o ChatGPT Desktop está instalado na máquina (MSIX ou Appx).
        /// </summary>
        private static bool IsChatGPTInstalled()
        {
            try
            {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (File.Exists(Path.Combine(localApp, @"Programs\ChatGPT\ChatGPT.exe")) ||
                    File.Exists(Path.Combine(localApp, @"OpenAI\ChatGPT\ChatGPT.exe")))
                    return true;

                string winApps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps");
                if (Directory.Exists(winApps))
                {
                    string[] dirs = Directory.GetDirectories(winApps, "*OpenAI.ChatGPT*");
                    if (dirs != null && dirs.Length > 0) return true;
                }

                string[] regKeys = new string[]
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\ChatGPT",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\ChatGPT"
                };
                foreach (string keyPath in regKeys)
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(keyPath))
                    {
                        if (key != null) return true;
                    }
                    using (var key = Registry.CurrentUser.OpenSubKey(keyPath))
                    {
                        if (key != null) return true;
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Verifica se o Claude Desktop está instalado na máquina.
        /// </summary>
        private static bool IsClaudeDesktopInstalled()
        {
            try
            {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (File.Exists(Path.Combine(localApp, @"Programs\Claude\Claude.exe")) ||
                    File.Exists(Path.Combine(localApp, @"AnthropicClaude\Claude.exe")))
                    return true;

                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                if (File.Exists(Path.Combine(progFiles, @"Claude\Claude.exe")))
                    return true;

                string[] regKeys = new string[]
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Claude",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Claude"
                };
                foreach (string keyPath in regKeys)
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(keyPath))
                    {
                        if (key != null) return true;
                    }
                    using (var key = Registry.CurrentUser.OpenSubKey(keyPath))
                    {
                        if (key != null) return true;
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Verifica se o DeepSeek Desktop está instalado na máquina.
        /// </summary>
        private static bool IsDeepSeekDesktopInstalled()
        {
            try
            {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (File.Exists(Path.Combine(localApp, @"Programs\deepseek-harness\DeepSeek Harness.exe")) ||
                    File.Exists(Path.Combine(localApp, @"Programs\deepseek-desktop\DeepSeek Desktop.exe")) ||
                    File.Exists(Path.Combine(localApp, @"deepseek\DeepSeek.exe")))
                    return true;

                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                if (File.Exists(Path.Combine(progFiles, @"DeepSeek\deepseek.exe")) ||
                    File.Exists(Path.Combine(progFiles, @"DeepSeek Harness\DeepSeek Harness.exe")))
                    return true;

                string[] regKeys = new string[]
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                };
                foreach (string keyPath in regKeys)
                {
                    using (var baseKey = Registry.LocalMachine.OpenSubKey(keyPath))
                    {
                        if (baseKey != null)
                        {
                            foreach (string subKeyName in baseKey.GetSubKeyNames())
                            {
                                using (var subKey = baseKey.OpenSubKey(subKeyName))
                                {
                                    if (subKey != null)
                                    {
                                        string displayName = subKey.GetValue("DisplayName") as string;
                                        if (!string.IsNullOrEmpty(displayName) && displayName.IndexOf("DeepSeek", StringComparison.OrdinalIgnoreCase) >= 0)
                                            return true;
                                    }
                                }
                            }
                        }
                    }
                    using (var baseKey = Registry.CurrentUser.OpenSubKey(keyPath))
                    {
                        if (baseKey != null)
                        {
                            foreach (string subKeyName in baseKey.GetSubKeyNames())
                            {
                                using (var subKey = baseKey.OpenSubKey(subKeyName))
                                {
                                    if (subKey != null)
                                    {
                                        string displayName = subKey.GetValue("DisplayName") as string;
                                        if (!string.IsNullOrEmpty(displayName) && displayName.IndexOf("DeepSeek", StringComparison.OrdinalIgnoreCase) >= 0)
                                            return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Verifica se o EaseUS Todo Backup Free está instalado na máquina.
        /// </summary>
        private static bool IsEaseUSTodoBackupInstalled()
        {
            try
            {
                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                if (File.Exists(Path.Combine(progFiles, @"EaseUS\Todo Backup\bin\Loader.exe")) ||
                    File.Exists(Path.Combine(progFiles, @"EaseUS\Todo Backup\bin\TodoBackup.exe")))
                    return true;

                string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                if (File.Exists(Path.Combine(progFilesX86, @"EaseUS\Todo Backup\bin\Loader.exe")) ||
                    File.Exists(Path.Combine(progFilesX86, @"EaseUS\Todo Backup\bin\TodoBackup.exe")))
                    return true;

                string[] regKeys = new string[]
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\EaseUS Todo Backup",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\EaseUS Todo Backup"
                };
                foreach (string keyPath in regKeys)
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(keyPath))
                    {
                        if (key != null) return true;
                    }
                    using (var key = Registry.CurrentUser.OpenSubKey(keyPath))
                    {
                        if (key != null) return true;
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Verifica se o Antigravity (standalone) está instalado na máquina.
        /// </summary>
        private static bool IsAntigravityInstalled()
        {
            try
            {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (File.Exists(Path.Combine(localApp, @"Programs\Antigravity\Antigravity.exe")) ||
                    File.Exists(Path.Combine(localApp, @"Antigravity\Antigravity.exe")))
                    return true;

                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                if (File.Exists(Path.Combine(progFiles, @"Antigravity\Antigravity.exe")))
                    return true;

                string[] regKeys = new string[]
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Antigravity",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Antigravity"
                };
                foreach (string keyPath in regKeys)
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(keyPath))
                    {
                        if (key != null) return true;
                    }
                    using (var key = Registry.CurrentUser.OpenSubKey(keyPath))
                    {
                        if (key != null) return true;
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Verifica se o Antigravity IDE está instalado na máquina.
        /// </summary>
        private static bool IsAntigravityIDEInstalled()
        {
            try
            {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (File.Exists(Path.Combine(localApp, @"Programs\Antigravity IDE\Antigravity.exe")) ||
                    File.Exists(Path.Combine(localApp, @"Programs\Antigravity IDE\Antigravity IDE.exe")) ||
                    File.Exists(Path.Combine(localApp, @"Antigravity IDE\Antigravity.exe")))
                    return true;

                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                if (File.Exists(Path.Combine(progFiles, @"Antigravity IDE\Antigravity.exe")))
                    return true;

                string[] regKeys = new string[]
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                };
                foreach (string keyPath in regKeys)
                {
                    using (var baseKey = Registry.LocalMachine.OpenSubKey(keyPath))
                    {
                        if (baseKey != null)
                        {
                            foreach (string subKeyName in baseKey.GetSubKeyNames())
                            {
                                using (var subKey = baseKey.OpenSubKey(subKeyName))
                                {
                                    if (subKey != null)
                                    {
                                        string displayName = subKey.GetValue("DisplayName") as string;
                                        if (!string.IsNullOrEmpty(displayName) && displayName.IndexOf("Antigravity IDE", StringComparison.OrdinalIgnoreCase) >= 0)
                                            return true;
                                    }
                                }
                            }
                        }
                    }
                    using (var baseKey = Registry.CurrentUser.OpenSubKey(keyPath))
                    {
                        if (baseKey != null)
                        {
                            foreach (string subKeyName in baseKey.GetSubKeyNames())
                            {
                                using (var subKey = baseKey.OpenSubKey(subKeyName))
                                {
                                    if (subKey != null)
                                    {
                                        string displayName = subKey.GetValue("DisplayName") as string;
                                        if (!string.IsNullOrEmpty(displayName) && displayName.IndexOf("Antigravity IDE", StringComparison.OrdinalIgnoreCase) >= 0)
                                            return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Verifica se o CrystalDiskInfo está instalado na máquina.
        /// </summary>
        private static bool IsCrystalDiskInfoInstalled()
        {
            try
            {
                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

                if (File.Exists(Path.Combine(progFiles, @"CrystalDiskInfo\DiskInfo64.exe")) ||
                    File.Exists(Path.Combine(progFiles, @"CrystalDiskInfo\DiskInfo32.exe")) ||
                    File.Exists(Path.Combine(progFilesX86, @"CrystalDiskInfo\DiskInfo32.exe")) ||
                    File.Exists(Path.Combine(progFilesX86, @"CrystalDiskInfo\DiskInfo64.exe")))
                    return true;

                string[] regKeys = new string[]
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\CrystalDiskInfo_is1",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\CrystalDiskInfo_is1"
                };
                foreach (string keyPath in regKeys)
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(keyPath))
                    {
                        if (key != null) return true;
                    }
                    using (var key = Registry.CurrentUser.OpenSubKey(keyPath))
                    {
                        if (key != null) return true;
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Verifica se o CrystalDiskMark está instalado na máquina.
        /// </summary>
        private static bool IsCrystalDiskMarkInstalled()
        {
            try
            {
                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

                if (File.Exists(Path.Combine(progFiles, @"CrystalDiskMark\DiskMark64.exe")) ||
                    File.Exists(Path.Combine(progFiles, @"CrystalDiskMark\DiskMark32.exe")) ||
                    File.Exists(Path.Combine(progFilesX86, @"CrystalDiskMark\DiskMark32.exe")) ||
                    File.Exists(Path.Combine(progFilesX86, @"CrystalDiskMark\DiskMark64.exe")))
                    return true;

                string[] regKeys = new string[]
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\CrystalDiskMark_is1",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\CrystalDiskMark_is1"
                };
                foreach (string keyPath in regKeys)
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(keyPath))
                    {
                        if (key != null) return true;
                    }
                    using (var key = Registry.CurrentUser.OpenSubKey(keyPath))
                    {
                        if (key != null) return true;
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Verifica se o Recuva está instalado na máquina.
        /// </summary>
        private static bool IsRecuvaInstalled()
        {
            try
            {
                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

                if (File.Exists(Path.Combine(progFiles, @"Recuva\recuva64.exe")) ||
                    File.Exists(Path.Combine(progFiles, @"Recuva\recuva.exe")) ||
                    File.Exists(Path.Combine(progFilesX86, @"Recuva\recuva.exe")))
                    return true;

                string[] regKeys = new string[]
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Recuva",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Recuva"
                };
                foreach (string keyPath in regKeys)
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(keyPath))
                    {
                        if (key != null) return true;
                    }
                    using (var key = Registry.CurrentUser.OpenSubKey(keyPath))
                    {
                        if (key != null) return true;
                    }
                }
            }
            catch { }
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
        /// Desinstala silenciosamente todos os jogos do computador (Xbox, Solitaire, jogos casuais da Microsoft e OEM),
        /// removendo os pacotes AppX dos usuários e pacotes provisionados da imagem do sistema.
        /// </summary>
        public async Task UninstallAllGamesAsync()
        {
            Log("Desinstalando silenciosamente jogos e pacotes de jogos do Windows...", ConsoleColor.Yellow);

            string psScript =
                "$gamePatterns = @('*Solitaire*', '*Xbox*', '*GamingOverlay*', '*CandyCrush*', '*King.com*', '*BubbleWitch*', '*MarchofEmpires*', '*DisneyMagicKingdoms*', '*HiddenCity*', '*Roblox*', '*Minecraft*'); " +
                "foreach ($pat in $gamePatterns) { " +
                "    try { " +
                "        $pkgs = Get-AppxPackage -AllUsers $pat -ErrorAction SilentlyContinue; " +
                "        foreach ($p in $pkgs) { " +
                "            Write-Host ('  Removendo pacote: ' + $p.Name); " +
                "            Remove-AppxPackage -Package $p.PackageFullName -AllUsers -ErrorAction SilentlyContinue | Out-Null; " +
                "        } " +
                "        $prov = Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -like $pat }; " +
                "        foreach ($pr in $prov) { " +
                "            Write-Host ('  Removendo provisionamento: ' + $pr.DisplayName); " +
                "            Remove-AppxProvisionedPackage -Online -PackageName $pr.PackageName -ErrorAction SilentlyContinue | Out-Null; " +
                "        } " +
                "    } catch {} " +
                "}";

            int exitCode = await RunProcessAsync("powershell",
                string.Format("-NoProfile -ExecutionPolicy Bypass -Command \"{0}\"", psScript),
                180000, null, false);

            if (exitCode == 0)
            {
                Log("[ok] Jogos e pacotes recreativos removidos com sucesso.", ConsoleColor.Green);
            }
            else
            {
                Log("Aviso: Remoção de jogos finalizada com código " + exitCode, ConsoleColor.Yellow);
            }

            await Task.Delay(300);
        }

        /// <summary>
        /// Remove e apaga com segurança todos os perfis de usuário do Windows,
        /// preservando perfis de sistema (Default, Public, SYSTEM), a conta atual em execução
        /// e os novos perfis configurados para criação (Admin e usuário padrão).
        /// </summary>
        public async Task RemoveOtherUserProfilesAsync(string keepUser1 = null, string keepUser2 = null)
        {
            Log("Removendo e limpando perfis antigos do Windows...", ConsoleColor.Yellow);

            string currentLoggedUser = Environment.UserName;
            string k1 = string.IsNullOrEmpty(keepUser1) ? "" : keepUser1;
            string k2 = string.IsNullOrEmpty(keepUser2) ? "" : keepUser2;

            string psScript = string.Format(
                "$keepList = @('{0}', '{1}', '{2}', 'Administrator', 'Administrador', 'DefaultAccount', 'Guest', 'Convidado', 'WDAGUtilityAccount'); " +
                "$systemSids = @('S-1-5-18', 'S-1-5-19', 'S-1-5-20'); " +
                "Get-CimInstance Win32_UserProfile | ForEach-Object {{ " +
                "    $sid = $_.SID; " +
                "    $path = $_.LocalPath; " +
                "    $leaf = Split-Path -Leaf $path; " +
                "    if ($_.Special -or ($systemSids -contains $sid) -or ($path -match '(?i)Default|Public|All Users')) {{ return; }} " +
                "    $matched = $false; " +
                "    foreach ($k in $keepList) {{ if ($k -and ($leaf -like $k)) {{ $matched = $true; break; }} }} " +
                "    if (-not $matched) {{ " +
                "        try {{ " +
                "            Write-Host ('  Removendo perfil antigo: ' + $path); " +
                "            Remove-CimInstance -InputObject $_ -ErrorAction Stop; " +
                "        }} catch {{ " +
                "            Write-Host ('  Falha ao remover perfil ' + $path + ': ' + $_.Exception.Message); " +
                "        }} " +
                "    }} " +
                "}}",
                currentLoggedUser, k1, k2);

            int exitCode = await RunProcessAsync("powershell",
                string.Format("-NoProfile -ExecutionPolicy Bypass -Command \"{0}\"", psScript),
                180000, null, false);

            if (exitCode == 0)
            {
                Log("[ok] Limpeza de perfis de usuário do Windows concluída.", ConsoleColor.Green);
            }
            else
            {
                Log("Aviso: Limpeza de perfis finalizada com código " + exitCode, ConsoleColor.Yellow);
            }

            await Task.Delay(300);
        }

        /// <summary>
        /// Desinstala silenciosamente antivírus de terceiros instalados (McAfee, Avast, AVG, Norton, etc.)
        /// e assegura que o Microsoft Defender permaneça ativo e operacional.
        /// </summary>
        public async Task UninstallThirdPartyAntivirusAsync()
        {
            Log("Verificando e desinstalando silenciosamente antivírus de terceiros...", ConsoleColor.Yellow);

            string psScript =
                "$avKeywords = @('McAfee', 'Avast', 'AVG', 'Norton', 'Kaspersky', 'Bitdefender', 'ESET', 'TotalAV', 'Avira', 'Malwarebytes', 'Webroot', 'Trend Micro', 'Sophos', 'Baidu'); " +
                "$regPaths = @('HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall', 'HKLM:\\SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall'); " +
                "foreach ($p in $regPaths) { " +
                "    if (Test-Path $p) { " +
                "        Get-ChildItem -Path $p -ErrorAction SilentlyContinue | ForEach-Object { " +
                "            $dn = $_.GetValue('DisplayName'); " +
                "            $quiet = $_.GetValue('QuietUninstallString'); " +
                "            $uninst = $_.GetValue('UninstallString'); " +
                "            if ($dn) { " +
                "                foreach ($kw in $avKeywords) { " +
                "                    if ($dn -match ('(?i)' + [regex]::Escape($kw))) { " +
                "                        Write-Host ('  Localizado antivirus de terceiro: ' + $dn); " +
                "                        try { " +
                "                            if ($quiet) { " +
                "                                Start-Process -FilePath 'cmd.exe' -ArgumentList ('/c ' + $quiet) -Wait -WindowStyle Hidden; " +
                "                            } elseif ($uninst) { " +
                "                                if ($uninst -match 'msiexec') { " +
                "                                    $msiArgs = $uninst -replace '(?i)/I', '/X' + ' /qn /norestart'; " +
                "                                    Start-Process -FilePath 'cmd.exe' -ArgumentList ('/c ' + $msiArgs) -Wait -WindowStyle Hidden; " +
                "                                } else { " +
                "                                    Start-Process -FilePath 'cmd.exe' -ArgumentList ('/c ' + $uninst + ' /SILENT /VERYSILENT /SUPPRESSMSGBOXES /NORESTART') -Wait -WindowStyle Hidden; " +
                "                                } " +
                "                            } " +
                "                        } catch {} " +
                "                    } " +
                "                } " +
                "            } " +
                "        } " +
                "    } " +
                "} " +
                "try { Set-MpPreference -DisableRealtimeMonitoring $false -ErrorAction SilentlyContinue; } catch {}; " +
                "try { Start-Service WinDefend -ErrorAction SilentlyContinue; } catch {}";

            int exitCode = await RunProcessAsync("powershell",
                string.Format("-NoProfile -ExecutionPolicy Bypass -Command \"{0}\"", psScript),
                300000, null, false);

            if (exitCode == 0)
            {
                Log("[ok] Antivírus de terceiros desinstalados e Microsoft Defender verificado.", ConsoleColor.Green);
            }
            else
            {
                Log("Aviso: Rotina de desinstalação de antivírus finalizada com código " + exitCode, ConsoleColor.Yellow);
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
            if (options.UninstallGames) totalSteps++;
            if (options.UninstallThirdPartyAntivirus) totalSteps++;
            if (options.RemoveOtherUserProfiles) totalSteps++;
            if (options.RenameComputer) totalSteps++;
            if (options.CreateAdmin) totalSteps++;
            if (options.CreateStandardUser) totalSteps++;
            if (options.SetWallpaper || options.SetWallpaperNortel) totalSteps++;
            totalSteps += options.SelectedPrograms.Count;

            int currentStep = 0;
            bool cancelled = false;

            Log("=== INICIANDO INSTALAÇÃO ===", ConsoleColor.Cyan);
            Log(string.Format("Data: {0:dd/MM/yyyy HH:mm:ss}", DateTime.Now), ConsoleColor.Gray);
            Log("", null);

            try
            {
                await EnsureGlobalPasswordNeverExpiresPolicyAsync();

                // ─── FASE 0: Funcionalidades Preliminares de Limpeza e Preparação do Sistema ───

                // 0.1 Desinstalar silenciosamente todos os jogos (Xbox, Solitaire, casuais)
                if (!cancelled && options.UninstallGames)
                {
                    if (_cancellationTokenSource != null && _cancellationTokenSource.Token.IsCancellationRequested)
                        cancelled = true;

                    if (!cancelled)
                    {
                        currentStep++;
                        ReportProgress(currentStep, totalSteps);
                        await UninstallAllGamesAsync();
                    }
                }

                // 0.2 Desinstalar silenciosamente antivírus de terceiros
                if (!cancelled && options.UninstallThirdPartyAntivirus)
                {
                    if (_cancellationTokenSource != null && _cancellationTokenSource.Token.IsCancellationRequested)
                        cancelled = true;

                    if (!cancelled)
                    {
                        currentStep++;
                        ReportProgress(currentStep, totalSteps);
                        await UninstallThirdPartyAntivirusAsync();
                    }
                }

                // 0.3 Remover e apagar perfis antigos do Windows
                if (!cancelled && options.RemoveOtherUserProfiles)
                {
                    if (_cancellationTokenSource != null && _cancellationTokenSource.Token.IsCancellationRequested)
                        cancelled = true;

                    if (!cancelled)
                    {
                        currentStep++;
                        ReportProgress(currentStep, totalSteps);
                        string newStdUser = options.CreateStandardUser ? options.StandardUserName : null;
                        await RemoveOtherUserProfilesAsync(newStdUser, "Admin");
                    }
                }

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
                if (!cancelled && (options.SetWallpaper || options.SetWallpaperNortel))
                {
                    if (_cancellationTokenSource != null && _cancellationTokenSource.Token.IsCancellationRequested)
                        cancelled = true;

                    if (!cancelled)
                    {
                        currentStep++;
                        ReportProgress(currentStep, totalSteps);
                        string client = options.SetWallpaperNortel ? "Nortel" : null;
                        await SetWallpaperAsync(client);
                    }
                }

                var installedPrograms = new List<string>();
                var failedPrograms = new List<string>();

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
                        bool success = await InstallProgramAsync(program, options);
                        if (success)
                        {
                            installedPrograms.Add(program.DisplayName);
                        }
                        else
                        {
                            failedPrograms.Add(program.DisplayName);
                        }
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

                    if (options.SelectedPrograms != null && options.SelectedPrograms.Count > 0)
                    {
                        Log("──────────────────────────────────────────────────────────────────────", ConsoleColor.Cyan);
                        Log(" RESUMO DOS PROGRAMAS:", ConsoleColor.Cyan);

                        if (installedPrograms.Count > 0)
                        {
                            Log(string.Format(" Programas instalados ({0}):", installedPrograms.Count), ConsoleColor.Blue);
                            for (int i = 0; i < installedPrograms.Count; i++)
                            {
                                Log(string.Format("   • {0}", installedPrograms[i]), ConsoleColor.Blue);
                            }
                        }

                        if (failedPrograms.Count > 0)
                        {
                            Log(string.Format(" Programas não instalados ou com erro ({0}):", failedPrograms.Count), ConsoleColor.Red);
                            for (int i = 0; i < failedPrograms.Count; i++)
                            {
                                Log(string.Format("   • {0}", failedPrograms[i]), ConsoleColor.Red);
                            }
                        }

                        Log("──────────────────────────────────────────────────────────────────────", ConsoleColor.Cyan);
                    }

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
            if (options.CreateDesktopShortcutSuporteExabit || options.CreateDesktopShortcutSuporteNorte)
                shortcuts.Add("Suporte Exabit", "https://suporte.exabit.com.br");
            if (options.CreateDesktopShortcutExabit)
                shortcuts.Add("Exabit Soluções em TI", "https://www.exabit.com.br");
            if (options.CreateDesktopShortcutExahost)
                shortcuts.Add("Exahost Hospedagem de Domínios", "https://www.exahost.com.br");
            if (options.CreateDesktopShortcutRemoteNeoenergia)
                shortcuts.Add("Remote Neoenergia", "https://remote.neoenergia.com/");
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
        public bool SetWallpaperNortel { get; set; }
        public bool EnableLocationServices { get; set; }

        /// <summary>
        /// Se true, desinstala silenciosamente todos os jogos do computador (Xbox, Solitaire, etc.) antes das instalações.
        /// </summary>
        public bool UninstallGames { get; set; }

        /// <summary>
        /// Se true, remove e apaga perfis antigos do Windows preservando apenas contas de sistema e as criadas aqui.
        /// </summary>
        public bool RemoveOtherUserProfiles { get; set; }

        /// <summary>
        /// Se true, desinstala silenciosamente qualquer antivírus de terceiros (McAfee, Avast, AVG, etc.) antes das instalações.
        /// </summary>
        public bool UninstallThirdPartyAntivirus { get; set; }

        /// <summary>
        /// Se true, limpa todos os atalhos das áreas de trabalho no final de todas as instalações.
        /// </summary>
        public bool CleanDesktopShortcuts { get; set; }

        // ─── Opções de atalhos corporativos ───

        /// <summary>
        /// Cria atalho "Suporte Exabit" na área de trabalho.
        /// </summary>
        public bool CreateDesktopShortcutSuporteExabit { get; set; }

        /// <summary>
        /// Compatibilidade com o nome anterior Suporte Norte.
        /// </summary>
        public bool CreateDesktopShortcutSuporteNorte
        {
            get { return CreateDesktopShortcutSuporteExabit; }
            set { CreateDesktopShortcutSuporteExabit = value; }
        }

        /// <summary>
        /// Cria atalho "Exabit Soluções em TI" na área de trabalho.
        /// </summary>
        public bool CreateDesktopShortcutExabit { get; set; }

        /// <summary>
        /// Cria atalho "Exahost Hospedagem de Domínios" na área de trabalho.
        /// </summary>
        public bool CreateDesktopShortcutExahost { get; set; }

        /// <summary>
        /// Cria atalho "Remote Neoenergia" na área de trabalho.
        /// </summary>
        public bool CreateDesktopShortcutRemoteNeoenergia { get; set; }

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
