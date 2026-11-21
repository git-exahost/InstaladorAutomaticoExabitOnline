using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
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
    public partial class InstallerService
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
        /// Caminho do arquivo de log da sessão atual (criado sob demanda).
        /// Retorna null caso não seja possível criar o diretório de logs.
        /// </summary>
        public string LogFilePath
        {
            get { return GetLogFilePath(); }
        }

        /// <summary>
        /// Token para cancelar a instalação.
        /// </summary>
        private System.Threading.CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// Protege a escrita concorrente no arquivo de log.
        /// </summary>
        private readonly object _logFileLock = new object();

        /// <summary>
        /// Caminho do arquivo de log da sessão atual.
        /// </summary>
        private string _logFilePath;

        /// <summary>
        /// Indica se o cabeçalho do arquivo de log já foi escrito.
        /// </summary>
        private bool _logFileHeaderWritten;
        private bool _logFileWarningShown;

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
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Dispose();
            }
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

        private bool IsCancellationRequested()
        {
            return _cancellationTokenSource != null &&
                _cancellationTokenSource.IsCancellationRequested;
        }

        private void ThrowIfCancellationRequested()
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Token.ThrowIfCancellationRequested();
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
        /// <summary>
        /// Cria o usuário administrador "admin" com a senha fornecida e o adiciona
        /// ao grupo de administradores locais, ou apenas altera sua senha caso já exista.
        /// </summary>
        /// <param name="password">Senha do usuário admin</param>
        /// <param name="passwordNeverExpires">Se true, ativa 'senha nunca expira'; se false, desmarca</param>
        public async Task CreateAdminUserAsync(string password, bool passwordNeverExpires = true)
        {
            try
            {
                bool exists = await UserExistsAsync("admin");
                if (exists)
                    await UpdateExistingAdminPasswordAsync(password, passwordNeverExpires);
                else
                    await CreateNewAdminUserAsync(password, passwordNeverExpires);
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

        private async Task UpdateExistingAdminPasswordAsync(string password, bool passwordNeverExpires)
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

            await ConfigureUserPasswordPolicyAsync("admin", false, passwordNeverExpires);
        }

        private async Task CreateNewAdminUserAsync(string password, bool passwordNeverExpires)
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

            await ConfigureUserPasswordPolicyAsync("admin", false, passwordNeverExpires);
        }

        /// <summary>
        /// Define a política global de segurança do Windows para a duração máxima de senhas.
        /// Se neverExpires for true: maxpwage:unlimited e PasswordNeverExpires $true para todos os perfis.
        /// Se neverExpires for false: PasswordNeverExpires $false para todos os perfis locais existentes.
        /// </summary>
        private async Task EnsureGlobalPasswordNeverExpiresPolicyAsync(bool neverExpires)
        {
            try
            {
                if (neverExpires)
                {
                    int code = await RunProcessAsync("net", "accounts /maxpwage:unlimited", 10000, null, false, true);
                    if (code == 0)
                    {
                        Log("[ok] Política global do Windows: expiração de senhas definida como NUNCA (ilimitada).", ConsoleColor.Green);
                    }

                    string psCmd = "Get-LocalUser -ErrorAction SilentlyContinue | Set-LocalUser -PasswordNeverExpires $true -ErrorAction SilentlyContinue";
                    int psCode = await RunPowerShellScriptWithLoggingAsync(psCmd, 15000);
                    if (psCode == 0)
                    {
                        Log("[ok] Senha definida como 'nunca expira' para todos os perfis locais.", ConsoleColor.Green);
                    }
                }
                else
                {
                    string psCmd = "Get-LocalUser -ErrorAction SilentlyContinue | Set-LocalUser -PasswordNeverExpires $false -ErrorAction SilentlyContinue";
                    int psCode = await RunPowerShellScriptWithLoggingAsync(psCmd, 15000);
                    if (psCode == 0)
                    {
                        Log("[ok] Opção 'A senha nunca expira' desmarcada para todos os perfis locais.", ConsoleColor.Yellow);
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Configura a política da conta de usuário informada: se a senha nunca expira e se pode alterar a senha,
        /// utilizando o cmdlet nativo Set-LocalUser do Windows PowerShell.
        /// </summary>
        /// <param name="username">Nome da conta local de usuário</param>
        /// <param name="cannotChangePassword">Se true, impede alteração da senha pelo usuário</param>
        /// <param name="passwordNeverExpires">Se true, define PasswordNeverExpires como $true; se false, define como $false</param>
        private async Task ConfigureUserPasswordPolicyAsync(string username, bool cannotChangePassword = false, bool passwordNeverExpires = true)
        {
            if (string.IsNullOrWhiteSpace(username)) return;

            string safeUser = username.Replace("'", "''");
            bool mayChangePassword = !cannotChangePassword;
            string psMayChange = mayChangePassword ? "true" : "false";
            string psNeverExpires = passwordNeverExpires ? "true" : "false";

            int psCode = await RunProcessAsync("powershell",
                string.Format("-NoProfile -ExecutionPolicy Bypass -Command \"Set-LocalUser -Name '{0}' -PasswordNeverExpires ${1} -UserMayChangePassword ${2}\"",
                safeUser, psNeverExpires, psMayChange), 15000);

            if (psCode == 0)
            {
                if (passwordNeverExpires)
                    Log(string.Format("[ok] Senha do usuário \"{0}\" configurada para NUNCA expirar.", username), ConsoleColor.Green);
                else
                    Log(string.Format("[ok] Senha do usuário \"{0}\": expiração padrão (nunca expirar: desmarcado).", username), ConsoleColor.Yellow);

                if (cannotChangePassword)
                    Log("[ok] Restrição aplicada: usuário impedido de alterar a senha.", ConsoleColor.Green);
                else if (!string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase))
                    Log("[ok] Permissão de senha: usuário poderá alterar a própria senha.", ConsoleColor.Green);
            }
            else
            {
                if (passwordNeverExpires)
                    Log(string.Format("[ok] Senha do usuário \"{0}\" protegida contra expiração via política do sistema.", username), ConsoleColor.Green);
                else
                    Log(string.Format("Aviso: Não foi possível atualizar política de expiração para \"{0}\".", username), ConsoleColor.Yellow);
            }
        }

        /// <summary>
        /// Cria um usuário padrão com nome e senha fornecidos (ou atualiza caso já exista),
        /// aplicando a política de expiração e alteração de senha conforme configurado.
        /// </summary>
        /// <param name="username">Nome do usuário</param>
        /// <param name="password">Senha do usuário (opcional)</param>
        /// <param name="cannotChangePassword">Impede o usuário de alterar a senha</param>
        /// <param name="passwordNeverExpires">Se true, ativa 'senha nunca expira'; se false, desmarca</param>
        public async Task CreateStandardUserAsync(string username, string password, bool cannotChangePassword = false, bool passwordNeverExpires = true)
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
                    await UpdateExistingStandardUserAsync(username, password, cannotChangePassword, passwordNeverExpires);
                else
                    await CreateNewStandardUserAsync(username, password, cannotChangePassword, passwordNeverExpires);
            }
            catch (Exception ex)
            {
                Log(string.Format("X Erro ao configurar \"{0}\": {1}", username, ex.Message),
                    ConsoleColor.Red);
            }

            await Task.Delay(500);
        }

        private async Task CreateNewStandardUserAsync(string username, string password, bool cannotChangePassword, bool passwordNeverExpires)
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

            await ConfigureStandardUserSettingsAsync(username, cannotChangePassword, passwordNeverExpires);

            Log(string.Format("[ok] Usuário \"{0}\" criado.", username), ConsoleColor.Green);
        }

        private async Task UpdateExistingStandardUserAsync(string username, string password, bool cannotChangePassword, bool passwordNeverExpires)
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

            await ConfigureStandardUserSettingsAsync(username, cannotChangePassword, passwordNeverExpires);
        }

        private async Task ConfigureStandardUserSettingsAsync(string username, bool cannotChangePassword, bool passwordNeverExpires)
        {
            await ConfigureUserPasswordPolicyAsync(username, cannotChangePassword, passwordNeverExpires);

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
        /// e configurando para novos usuários e usuário atual, com opção de bloquear alteração por colaboradores.
        /// </summary>
        public async Task SetWallpaperAsync(string client = null, bool preventChangingWallpaper = false)
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
                string preventArg = preventChangingWallpaper ? " -PreventChangingWallpaper" : "";

                int exitCode = await RunProcessAsync("powershell",
                    string.Format("-NoProfile -ExecutionPolicy Bypass -File {0} -BasePath {1}{2}{3}", quotedPsScript, quotedBasePath, clientArg, preventArg),
                    120000, null, false);

                if (exitCode == 0)
                {
                    Log(string.Format("[ok] Papéis de parede {0}configurados em todos os temas do Windows.",
                        !string.IsNullOrEmpty(client) ? "(" + client + ") " : ""), ConsoleColor.Green);
                    if (preventChangingWallpaper)
                    {
                        Log("[ok] Restrição de papel de parede ativada: colaboradores impedidos de alterar a tela de fundo.", ConsoleColor.Green);
                    }
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

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;

            public MEMORYSTATUSEX()
            {
                this.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;
            public uint BatteryLifeTime;
            public uint BatteryFullLifeTime;
        }

        [DllImport("kernel32.dll")]
        private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS sps);

        /// <summary>
        /// Realiza teste de latência (ping) da conexão com a internet contra servidores DNS rápidos e confiáveis.
        /// </summary>
        /// <returns>Diagnóstico formatado com latência, provedor DNS e cor representativa de qualidade.</returns>
        public async Task<PingDiagnosis> MeasureInternetPingAsync()
        {
            return await Task.Run(new Func<PingDiagnosis>(() => MeasureInternetPingInternal()));
        }

        private static PingDiagnosis MeasureInternetPingInternal()
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
                            long ms = reply.RoundtripTime;
                            if (ms < 50)
                            {
                                return new PingDiagnosis
                                {
                                    Success = true,
                                    LatencyMs = ms,
                                    Text = string.Format("{0} ms ({1} - Excelente)", ms, hostLabel),
                                    Color = ConsoleColor.Green
                                };
                            }
                            else if (ms < 100)
                            {
                                return new PingDiagnosis
                                {
                                    Success = true,
                                    LatencyMs = ms,
                                    Text = string.Format("{0} ms ({1} - Bom)", ms, hostLabel),
                                    Color = ConsoleColor.Green
                                };
                            }
                            else if (ms < 200)
                            {
                                return new PingDiagnosis
                                {
                                    Success = true,
                                    LatencyMs = ms,
                                    Text = string.Format("{0} ms ({1} - Moderado)", ms, hostLabel),
                                    Color = ConsoleColor.Yellow
                                };
                            }
                            else
                            {
                                return new PingDiagnosis
                                {
                                    Success = true,
                                    LatencyMs = ms,
                                    Text = string.Format("{0} ms ({1} - Alto / Instável)", ms, hostLabel),
                                    Color = ConsoleColor.Red
                                };
                            }
                        }
                    }
                    catch { }
                }
            }

            return new PingDiagnosis
            {
                Success = false,
                LatencyMs = -1,
                Text = "Tempo limite esgotado / Inacessível",
                Color = ConsoleColor.Red
            };
        }

        /// <summary>
        /// Obtém o diagnóstico completo do sistema: identificação, SO, conexões, discos, hardware, garantia e segurança.
        /// </summary>
        public async Task<FullSystemDiagnosticInfo> GetFullSystemDiagnosisAsync()
        {
            return await Task.Run(new Func<FullSystemDiagnosticInfo>(() =>
            {
                var diag = new FullSystemDiagnosticInfo();

                // 1. Identificação da Máquina e Usuário
                try
                {
                    diag.ComputerName = Environment.MachineName;
                    diag.UserName = Environment.UserName;

                    try
                    {
                        var principal = new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent());
                        diag.IsAdmin = principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
                    }
                    catch { diag.IsAdmin = false; }
                }
                catch { }

                // 2. Sistema Operacional (Edição comercial e build)
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

                    diag.WindowsEdition = !string.IsNullOrEmpty(displayVer)
                        ? string.Format("{0} {1}", prodName, displayVer)
                        : prodName;
                    diag.WindowsBuild = !string.IsNullOrEmpty(build)
                        ? string.Format("(Build {0})", build)
                        : "";
                }
                catch { }

                // 3. Fabricante, Modelo e Domínio/Workgroup (Win32_ComputerSystem)
                try
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Model, Domain FROM Win32_ComputerSystem"))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            diag.Manufacturer = (obj["Manufacturer"] ?? "").ToString().Trim();
                            diag.Model = (obj["Model"] ?? "").ToString().Trim();
                            diag.DomainOrWorkgroup = (obj["Domain"] ?? "WORKGROUP").ToString().Trim();
                            break;
                        }
                    }
                }
                catch { }

                if (string.IsNullOrEmpty(diag.Manufacturer) || diag.Manufacturer.IndexOf("System manufacturer", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    try
                    {
                        using (var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS"))
                        {
                            if (key != null)
                            {
                                diag.Manufacturer = (key.GetValue("SystemManufacturer") as string) ?? diag.Manufacturer;
                                diag.Model = (key.GetValue("SystemProductName") as string) ?? diag.Model;
                            }
                        }
                    }
                    catch { }
                }

                // 4. Número de Série / Service Tag (Win32_BIOS)
                try
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS"))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            diag.SerialNumber = (obj["SerialNumber"] ?? "").ToString().Trim();
                            break;
                        }
                    }
                }
                catch { }

                if (string.IsNullOrEmpty(diag.SerialNumber) || diag.SerialNumber.IndexOf("Default", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    try
                    {
                        using (var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS"))
                        {
                            if (key != null)
                            {
                                string sn = (key.GetValue("BaseBoardSerialNumber") as string) ?? (key.GetValue("SystemSerialNumber") as string);
                                if (!string.IsNullOrEmpty(sn)) diag.SerialNumber = sn.Trim();
                            }
                        }
                    }
                    catch { }
                }
                if (string.IsNullOrEmpty(diag.SerialNumber)) diag.SerialNumber = "Não informado";

                // 5. Processador (Win32_Processor)
                try
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor"))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            string rawName = (obj["Name"] ?? "").ToString().Trim();
                            rawName = Regex.Replace(rawName, @"\((R|TM)\)", "", RegexOptions.IgnoreCase);
                            rawName = Regex.Replace(rawName, @"@.*$", "").Trim();
                            rawName = Regex.Replace(rawName, @"\s+", " ");
                            diag.CpuName = rawName;

                            int cores = 0;
                            int threads = 0;
                            if (obj["NumberOfCores"] != null) int.TryParse(obj["NumberOfCores"].ToString(), out cores);
                            if (obj["NumberOfLogicalProcessors"] != null) int.TryParse(obj["NumberOfLogicalProcessors"].ToString(), out threads);
                            diag.CpuCores = cores;
                            diag.CpuThreads = threads;
                            break;
                        }
                    }
                }
                catch { }

                if (string.IsNullOrEmpty(diag.CpuName) || diag.CpuName == "Processador")
                {
                    try
                    {
                        using (var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                        {
                            if (key != null)
                            {
                                string rawName = (key.GetValue("ProcessorNameString") as string) ?? Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Processador";
                                rawName = Regex.Replace(rawName, @"\((R|TM)\)", "", RegexOptions.IgnoreCase);
                                rawName = Regex.Replace(rawName, @"@.*$", "").Trim();
                                rawName = Regex.Replace(rawName, @"\s+", " ");
                                diag.CpuName = rawName;
                            }
                        }
                    }
                    catch { }
                }
                if (diag.CpuCores <= 0) diag.CpuCores = Environment.ProcessorCount > 1 ? Environment.ProcessorCount / 2 : 1;
                if (diag.CpuThreads <= 0) diag.CpuThreads = Environment.ProcessorCount;
                diag.CpuSummary = string.Format("({0} Cores / {1} Threads)", diag.CpuCores, diag.CpuThreads);

                // 6. Memória RAM e Tipo (GlobalMemoryStatusEx + Win32_PhysicalMemory)
                try
                {
                    var memStatus = new MEMORYSTATUSEX();
                    if (GlobalMemoryStatusEx(memStatus))
                    {
                        diag.TotalRamGb = memStatus.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
                        diag.FreeRamGb = memStatus.ullAvailPhys / (1024.0 * 1024.0 * 1024.0);
                        diag.UsedRamGb = Math.Max(0, diag.TotalRamGb - diag.FreeRamGb);
                        diag.RamUsagePercent = (double)memStatus.dwMemoryLoad;
                    }

                    string ramType = "";
                    try
                    {
                        using (var searcher = new ManagementObjectSearcher("SELECT SMBIOSMemoryType FROM Win32_PhysicalMemory"))
                        {
                            foreach (ManagementObject obj in searcher.Get())
                            {
                                int smbiosType = 0;
                                if (obj["SMBIOSMemoryType"] != null) int.TryParse(obj["SMBIOSMemoryType"].ToString(), out smbiosType);
                                if (smbiosType == 24) ramType = "DDR3";
                                else if (smbiosType == 26) ramType = "DDR4";
                                else if (smbiosType == 30 || smbiosType == 34 || smbiosType == 35) ramType = "DDR5";
                                if (!string.IsNullOrEmpty(ramType)) break;
                            }
                        }
                    }
                    catch { }
                    if (string.IsNullOrEmpty(ramType)) ramType = "DDR4";
                    diag.RamType = ramType;
                    diag.RamSummary = string.Format("{0:N0} GB {1}", Math.Round(diag.TotalRamGb), ramType);
                }
                catch { }

                // 7. Discos e Partições (Todos os discos fixos da máquina)
                try
                {
                    var driveMediaMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    try
                    {
                        using (var searcher = new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage", "SELECT FriendlyName, BusType, MediaType FROM MSFT_PhysicalDisk"))
                        {
                            foreach (ManagementObject disk in searcher.Get())
                            {
                                string fname = (disk["FriendlyName"] ?? "").ToString();
                                int busType = 0;
                                if (disk["BusType"] != null) int.TryParse(disk["BusType"].ToString(), out busType);
                                int mType = 0;
                                if (disk["MediaType"] != null) int.TryParse(disk["MediaType"].ToString(), out mType);

                                string type = "SSD";
                                if (busType == 17 || fname.IndexOf("NVMe", StringComparison.OrdinalIgnoreCase) >= 0) type = "NVMe";
                                else if (mType == 4 || fname.IndexOf("SSD", StringComparison.OrdinalIgnoreCase) >= 0) type = "SSD";
                                else if (mType == 3 || fname.IndexOf("HDD", StringComparison.OrdinalIgnoreCase) >= 0) type = "HDD";

                                if (!driveMediaMap.ContainsKey("C:")) driveMediaMap["C:"] = type;
                            }
                        }
                    }
                    catch { }

                    var allDrives = DriveInfo.GetDrives();
                    foreach (var drive in allDrives)
                    {
                        if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;

                        string letter = drive.Name.TrimEnd('\\');
                        string volLabel = "";
                        try { volLabel = drive.VolumeLabel; } catch { }

                        double totalGb = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                        double freeGb = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                        double usedGb = Math.Max(0, totalGb - freeGb);
                        double pctFree = totalGb > 0 ? (freeGb / totalGb * 100.0) : 0.0;
                        double pctUsed = totalGb > 0 ? (usedGb / totalGb * 100.0) : 0.0;

                        string mediaType = "NVMe";
                        if (driveMediaMap.ContainsKey(letter)) mediaType = driveMediaMap[letter];

                        ConsoleColor color = ConsoleColor.Green;
                        if (pctFree < 10.0 || freeGb < 15.0) color = ConsoleColor.Red;
                        else if (pctFree < 20.0 || freeGb < 30.0) color = ConsoleColor.Yellow;

                        diag.Drives.Add(new DiskDriveDiagnosis
                        {
                            DriveLetter = letter,
                            VolumeLabel = volLabel,
                            MediaType = mediaType,
                            FreeGb = freeGb,
                            TotalGb = totalGb,
                            UsedGb = usedGb,
                            PercentFree = pctFree,
                            PercentUsed = pctUsed,
                            StatusText = string.Format("{0:N1} / {1:N1} GB ({2:N0}% livre)", freeGb, totalGb, pctFree),
                            StatusColor = color
                        });
                    }
                }
                catch { }

                // 8. Segurança (Antivírus e BitLocker)
                try
                {
                    DetectAntivirus(diag);
                    DetectBitLocker(diag);
                }
                catch { }

                // 9. Estado do Sistema (Reboot Pendente e Fonte de Energia)
                try
                {
                    bool rebootPending = false;
                    try
                    {
                        using (var k1 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"))
                            if (k1 != null) rebootPending = true;
                        if (!rebootPending)
                        {
                            using (var k2 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired"))
                                if (k2 != null) rebootPending = true;
                        }
                        if (!rebootPending)
                        {
                            using (var k3 = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager"))
                            {
                                if (k3 != null && k3.GetValue("PendingFileRenameOperations") != null) rebootPending = true;
                            }
                        }
                    }
                    catch { }
                    diag.PendingReboot = rebootPending;
                    diag.PendingRebootText = rebootPending ? "Sim" : "Não";

                    SYSTEM_POWER_STATUS power;
                    if (GetSystemPowerStatus(out power))
                    {
                        if (power.ACLineStatus == 1)
                            diag.PowerSourceText = "Na tomada";
                        else if (power.ACLineStatus == 0)
                            if (power.BatteryLifePercent <= 100)
                                diag.PowerSourceText = string.Format("Bateria ({0}%)", power.BatteryLifePercent);
                            else
                                diag.PowerSourceText = "Bateria";
                        else
                            diag.PowerSourceText = "Na tomada";
                    }
                    else
                    {
                        diag.PowerSourceText = "Na tomada";
                    }
                }
                catch { }

                // 10. Conexão de Rede, IPs e Interfaces
                DetectNetworkInterfacesAndIps(diag);

                // 11. Status de Internet e Ping
                try
                {
                    bool online = CheckInternetConnectivity();
                    diag.HasInternet = online;
                    if (online)
                    {
                        var ping = MeasureInternetPingInternal();
                        if (ping != null && ping.Success)
                        {
                            diag.ConnectionStatusText = string.Format("Online ({0} ms)", ping.LatencyMs);
                            diag.PingLatencyText = string.Format("{0} ms", ping.LatencyMs);
                            if (ping.LatencyMs < 80)
                                diag.ConnectionStatusColor = ConsoleColor.Green;
                            else if (ping.LatencyMs < 180)
                                diag.ConnectionStatusColor = ConsoleColor.Yellow;
                            else
                                diag.ConnectionStatusColor = ConsoleColor.Red;
                        }
                        else
                        {
                            diag.ConnectionStatusText = "Online";
                            diag.ConnectionStatusColor = ConsoleColor.Green;
                        }
                    }
                    else
                    {
                        diag.ConnectionStatusText = "Offline";
                        diag.ConnectionStatusColor = ConsoleColor.Red;
                    }
                }
                catch { }

                return diag;
            }));
        }

        /// <summary>
        /// Detecta antivírus de terceiros e status do Microsoft Defender no sistema operacional.
        /// </summary>
        private void DetectAntivirus(FullSystemDiagnosticInfo diag)
        {
            if (diag == null) return;
            string activeThirdPartyAv = null;
            string inactiveThirdPartyAv = null;
            bool defenderActive = false;
            bool defenderFound = false;

            try
            {
                using (var searcher = new ManagementObjectSearcher(@"root\SecurityCenter2", "SELECT displayName, productState FROM AntiVirusProduct"))
                {
                    foreach (ManagementObject av in searcher.Get())
                    {
                        string name = av["displayName"] != null ? av["displayName"].ToString().Trim() : "";
                        if (string.IsNullOrEmpty(name)) continue;

                        int pState = 0;
                        if (av["productState"] != null) int.TryParse(av["productState"].ToString(), out pState);

                        // No Windows SecurityCenter2, os bits 12 a 15 (nibble alto do byte intermediário)
                        // indicam se a proteção em tempo real está ativa: 1 = Ativo / Habilitado, 0 = Desativado
                        bool isEnabled = ((pState >> 12) & 0x0F) == 1;

                        if (name.IndexOf("Windows Defender", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("Microsoft Defender", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            defenderFound = true;
                            if (isEnabled) defenderActive = true;
                        }
                        else
                        {
                            if (isEnabled)
                            {
                                if (string.IsNullOrEmpty(activeThirdPartyAv))
                                    activeThirdPartyAv = name;
                                else if (activeThirdPartyAv.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0)
                                    activeThirdPartyAv += ", " + name;
                            }
                            else
                            {
                                if (string.IsNullOrEmpty(inactiveThirdPartyAv))
                                    inactiveThirdPartyAv = name;
                            }
                        }
                    }
                }
            }
            catch { }

            if (!defenderFound)
            {
                bool defServiceRunning = false;
                try
                {
                    using (var sc = new System.ServiceProcess.ServiceController("WinDefend"))
                    {
                        defServiceRunning = (sc.Status == System.ServiceProcess.ServiceControllerStatus.Running);
                    }
                }
                catch { }

                bool regDisabled = false;
                try
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Defender"))
                    {
                        if (key != null)
                        {
                            var dis = key.GetValue("DisableAntiSpyware");
                            if (dis != null && (int)dis == 1) regDisabled = true;
                        }
                    }
                }
                catch { }

                if (defServiceRunning && !regDisabled)
                {
                    defenderActive = true;
                    defenderFound = true;
                }
            }

            diag.ThirdPartyAntivirus = activeThirdPartyAv ?? inactiveThirdPartyAv;
            diag.DefenderActive = defenderActive;

            if (!string.IsNullOrEmpty(activeThirdPartyAv))
            {
                diag.AntivirusSummary = string.Format("{0} (Ativo)", activeThirdPartyAv);
                diag.DefenderStatusText = string.Format("Desativado ({0})", activeThirdPartyAv);
            }
            else if (defenderActive)
            {
                diag.AntivirusSummary = "Microsoft Defender (Ativo)";
                diag.DefenderStatusText = "Ativo";
            }
            else if (!string.IsNullOrEmpty(inactiveThirdPartyAv))
            {
                diag.AntivirusSummary = string.Format("{0} (Desativado)", inactiveThirdPartyAv);
                diag.DefenderStatusText = "Desativado";
            }
            else
            {
                diag.AntivirusSummary = "Desativado / Não detectado";
                diag.DefenderStatusText = "Desativado";
            }
        }

        /// <summary>
        /// Detecta proteção BitLocker e status de criptografia de unidade de forma resiliente.
        /// Utiliza PacketPrivacy obrigatório na consulta WMI e fallback via manage-bde.exe.
        /// </summary>
        private void DetectBitLocker(FullSystemDiagnosticInfo diag)
        {
            if (diag == null) return;
            bool bitlocker = false;
            string bitlockerDrive = null;

            // 1. Consulta WMI Win32_EncryptableVolume com PacketPrivacy (obrigatório pelo subsistema WMI do BitLocker)
            try
            {
                var scope = new ManagementScope(@"\\.\root\CIMV2\Security\MicrosoftVolumeEncryption");
                scope.Options.Authentication = AuthenticationLevel.PacketPrivacy;
                scope.Options.Impersonation = ImpersonationLevel.Impersonate;
                scope.Options.Timeout = TimeSpan.FromSeconds(2);
                scope.Connect();

                using (var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT DriveLetter, ProtectionStatus, ConversionStatus FROM Win32_EncryptableVolume")))
                {
                    foreach (ManagementObject vol in searcher.Get())
                    {
                        int status = 0;
                        if (vol["ProtectionStatus"] != null) int.TryParse(vol["ProtectionStatus"].ToString(), out status);
                        int convStatus = 0;
                        if (vol["ConversionStatus"] != null) int.TryParse(vol["ConversionStatus"].ToString(), out convStatus);
                        string letter = (vol["DriveLetter"] ?? "").ToString();

                        if (status == 1 || convStatus == 1 || convStatus == 2)
                        {
                            bitlocker = true;
                            if (!string.IsNullOrEmpty(letter))
                            {
                                bitlockerDrive = letter;
                            }
                            break;
                        }
                    }
                }
            }
            catch { }

            // 2. Fallback via manage-bde.exe se WMI não obteve proteção ativa
            if (!bitlocker)
            {
                try
                {
                    string sysDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
                    string bdePath = Path.Combine(sysDir, "manage-bde.exe");
                    if (File.Exists(bdePath))
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = bdePath,
                            Arguments = "-status C:",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        };
                        using (var p = Process.Start(psi))
                        {
                            if (p.WaitForExit(1500))
                            {
                                string outText = p.StandardOutput.ReadToEnd();
                                if (!string.IsNullOrEmpty(outText))
                                {
                                    if (outText.IndexOf("Proteção Ativada", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        outText.IndexOf("Protection On", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        outText.IndexOf("Totalmente Criptografado", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        outText.IndexOf("Fully Encrypted", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        (outText.IndexOf("100%", StringComparison.OrdinalIgnoreCase) >= 0 && outText.IndexOf("Criptografado", StringComparison.OrdinalIgnoreCase) >= 0))
                                    {
                                        bitlocker = true;
                                        bitlockerDrive = "C:";
                                    }
                                }
                            }
                            else
                            {
                                try { p.Kill(); } catch { }
                            }
                        }
                    }
                }
                catch { }
            }

            diag.BitLockerActive = bitlocker;
            if (bitlocker)
            {
                diag.BitLockerStatusText = !string.IsNullOrEmpty(bitlockerDrive)
                    ? string.Format("Ativo ({0})", bitlockerDrive)
                    : "Ativo";
            }
            else
            {
                diag.BitLockerStatusText = "Desativado";
            }
        }

        /// <summary>
        /// Detecta interfaces ativas de rede e endereços IPv4 locais de forma nativa e instantânea via .NET (~1ms).
        /// </summary>
        private void DetectNetworkInterfacesAndIps(FullSystemDiagnosticInfo diag)
        {
            if (diag == null) return;
            try
            {
                var ips = new List<string>();
                var ipsWithAdapters = new List<string>();
                var activeNics = new List<string>();
                bool hasVpn = false;

                foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
                        continue;
                    if (nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                        continue;

                    string desc = nic.Description ?? "";
                    string name = nic.Name ?? "";
                    string nicLabel = name;

                    if (desc.IndexOf("Radmin", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        desc.IndexOf("VPN", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        desc.IndexOf("TAP", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        desc.IndexOf("WireGuard", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Radmin", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("VPN", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        hasVpn = true;
                        nicLabel = (name.IndexOf("Radmin", StringComparison.OrdinalIgnoreCase) >= 0 || desc.IndexOf("Radmin", StringComparison.OrdinalIgnoreCase) >= 0) ? "Radmin VPN" : "VPN";
                    }
                    else
                    {
                        if (nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211)
                        {
                            activeNics.Add("Wi-Fi");
                            nicLabel = "Wi-Fi";
                        }
                        else if (nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Ethernet)
                        {
                            activeNics.Add("Ethernet");
                            nicLabel = "Ethernet";
                        }
                    }

                    var ipProps = nic.GetIPProperties();
                    foreach (var addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            string ipStr = addr.Address.ToString();
                            if (!ipStr.StartsWith("127.") && !ipStr.StartsWith("169.254.") && !ips.Contains(ipStr))
                            {
                                ips.Add(ipStr);
                                ipsWithAdapters.Add(string.Format("{0} ({1})", ipStr, nicLabel));
                            }
                        }
                    }
                }

                diag.AllLocalIps = ipsWithAdapters.Count > 0 ? ipsWithAdapters : ips;
                string preferredIp = ips.FirstOrDefault(ip => !ip.StartsWith("26.")) ?? (ips.Count > 0 ? ips[0] : "Indisponível");
                diag.LocalIp = preferredIp;

                if (hasVpn)
                    diag.NetworkInterfacesSummary = "Radmin / VPN detectadas";
                else if (activeNics.Count > 0)
                    diag.NetworkInterfacesSummary = string.Format("{0} ativo(s)", string.Join(", ", activeNics.Distinct().ToArray()));
                else
                    diag.NetworkInterfacesSummary = "Rede local ativa";
            }
            catch { }
        }

        /// <summary>
        /// Obtém diagnóstico rápido e instantâneo do sistema (execução em ~2ms via .NET e Registry, sem WMI pesado).
        /// Preenche de imediato dados do computador, SO, unidades de armazenamento, memória RAM, processador, fabricante e rede.
        /// </summary>
        public FullSystemDiagnosticInfo GetQuickSystemDiagnosis()
        {
            var diag = new FullSystemDiagnosticInfo();
            try
            {
                diag.ComputerName = Environment.MachineName;
                diag.UserName = Environment.UserName;
                try
                {
                    diag.DomainOrWorkgroup = Environment.UserDomainName;
                }
                catch { }

                try
                {
                    var principal = new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent());
                    diag.IsAdmin = principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
                }
                catch { diag.IsAdmin = false; }

                // Identificação rápida do SO via Registry
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    if (key != null)
                    {
                        var prod = (key.GetValue("ProductName") as string) ?? "";
                        var display = (key.GetValue("DisplayVersion") as string) ?? (key.GetValue("ReleaseId") as string) ?? "";
                        var build = (key.GetValue("CurrentBuild") as string) ?? (key.GetValue("CurrentBuildNumber") as string) ?? "";
                        string ubr = key.GetValue("UBR") != null ? key.GetValue("UBR").ToString() : "";
                        if (!string.IsNullOrEmpty(ubr) && !string.IsNullOrEmpty(build))
                        {
                            build = build + "." + ubr;
                        }

                        int bNum = 0;
                        if (!string.IsNullOrEmpty(build))
                        {
                            string firstPart = build.Split('.')[0];
                            int.TryParse(firstPart, out bNum);
                        }
                        if (bNum >= 22000 && prod.IndexOf("Windows 10", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            prod = prod.Replace("Windows 10", "Windows 11");
                        }

                        if (!string.IsNullOrEmpty(prod))
                        {
                            diag.WindowsEdition = !string.IsNullOrEmpty(display) ? string.Format("{0} {1}", prod, display) : prod;
                        }
                        if (!string.IsNullOrEmpty(build))
                        {
                            diag.WindowsBuild = string.Format("(Build {0})", build);
                        }
                    }
                }

                // Identificação rápida de Fabricante, Modelo e Serial via Registry BIOS (0ms)
                try
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS"))
                    {
                        if (key != null)
                        {
                            string mf = (key.GetValue("SystemManufacturer") as string) ?? "";
                            string md = (key.GetValue("SystemProductName") as string) ?? "";
                            string sn = (key.GetValue("SystemSerialNumber") as string) ?? (key.GetValue("BaseBoardSerialNumber") as string) ?? "";

                            if (!string.IsNullOrEmpty(mf) && mf.IndexOf("System manufacturer", StringComparison.OrdinalIgnoreCase) < 0)
                                diag.Manufacturer = mf.Trim();
                            if (!string.IsNullOrEmpty(md) && md.IndexOf("System Product Name", StringComparison.OrdinalIgnoreCase) < 0)
                                diag.Model = md.Trim();
                            if (!string.IsNullOrEmpty(sn) && sn.IndexOf("Default", StringComparison.OrdinalIgnoreCase) < 0)
                                diag.SerialNumber = sn.Trim();
                        }
                    }
                }
                catch { }

                // Identificação rápida do Processador via Registry CentralProcessor (0ms)
                try
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                    {
                        if (key != null)
                        {
                            string rawCpu = (key.GetValue("ProcessorNameString") as string) ?? "";
                            if (!string.IsNullOrEmpty(rawCpu))
                            {
                                rawCpu = Regex.Replace(rawCpu, @"\((R|TM)\)", "", RegexOptions.IgnoreCase);
                                rawCpu = Regex.Replace(rawCpu, @"@.*$", "").Trim();
                                rawCpu = Regex.Replace(rawCpu, @"\s+", " ");
                                diag.CpuName = rawCpu;
                            }
                        }
                    }
                }
                catch { }
                int procCount = Environment.ProcessorCount;
                diag.CpuCores = procCount > 1 ? procCount / 2 : 1;
                diag.CpuThreads = procCount;
                diag.CpuSummary = string.Format("({0} Threads)", procCount);

                // Carrega memória RAM e discos fixos instantaneamente
                UpdateDynamicMetrics(diag);
                diag.RamSummary = string.Format("{0:N0} GB RAM", Math.Round(diag.TotalRamGb));

                // Interfaces de rede e IPs locais instantâneos via .NET NetworkInterface (1ms)
                DetectNetworkInterfacesAndIps(diag);

                // Estado de reinicialização pendente e fonte de energia (0ms)
                try
                {
                    bool rebootPending = false;
                    using (var k1 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"))
                        if (k1 != null) rebootPending = true;
                    if (!rebootPending)
                    {
                        using (var k2 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired"))
                            if (k2 != null) rebootPending = true;
                    }
                    diag.PendingReboot = rebootPending;
                    diag.PendingRebootText = rebootPending ? "Sim" : "Não";

                    SYSTEM_POWER_STATUS power;
                    if (GetSystemPowerStatus(out power))
                    {
                        if (power.ACLineStatus == 1)
                            diag.PowerSourceText = "Na tomada";
                        else if (power.ACLineStatus == 0)
                            if (power.BatteryLifePercent <= 100)
                                diag.PowerSourceText = string.Format("Bateria ({0}%)", power.BatteryLifePercent);
                            else
                                diag.PowerSourceText = "Bateria";
                        else
                            diag.PowerSourceText = "Na tomada";
                    }
                }
                catch { }

                // Antivírus rápido (< 5ms via SecurityCenter2)
                DetectAntivirus(diag);

                // BitLocker rápido
                DetectBitLocker(diag);
            }
            catch { }

            return diag;
        }

        /// <summary>
        /// Atualiza rapidamente os dados dinâmicos de memória RAM e unidades de disco (execução instantânea sem WMI lento).
        /// Ideal para timers de alta frequência periódicos (ex: a cada 3 ou 5 segundos).
        /// </summary>
        public void UpdateDynamicMetrics(FullSystemDiagnosticInfo diag)
        {
            if (diag == null) return;

            // 1. Atualiza dados de memória RAM instantaneamente via GlobalMemoryStatusEx (Win32)
            try
            {
                var memStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memStatus))
                {
                    diag.TotalRamGb = memStatus.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
                    diag.FreeRamGb = memStatus.ullAvailPhys / (1024.0 * 1024.0 * 1024.0);
                    diag.UsedRamGb = Math.Max(0, diag.TotalRamGb - diag.FreeRamGb);
                    diag.RamUsagePercent = (double)memStatus.dwMemoryLoad;
                }
            }
            catch { }

            // 2. Atualiza fonte e porcentagem da bateria via GetSystemPowerStatus (Win32)
            try
            {
                SYSTEM_POWER_STATUS power;
                if (GetSystemPowerStatus(out power))
                {
                    if (power.ACLineStatus == 1)
                        diag.PowerSourceText = "Na tomada";
                    else if (power.ACLineStatus == 0)
                        if (power.BatteryLifePercent <= 100)
                            diag.PowerSourceText = string.Format("Bateria ({0}%)", power.BatteryLifePercent);
                        else
                            diag.PowerSourceText = "Bateria";
                    else
                        diag.PowerSourceText = "Na tomada";
                }
            }
            catch { }

            // 3. Atualiza dados de espaço em disco de todas as unidades fixas via DriveInfo (.NET nativo)
            try
            {
                if (diag.Drives == null)
                {
                    diag.Drives = new List<DiskDriveDiagnosis>();
                }

                var allDrives = DriveInfo.GetDrives();
                foreach (var drive in allDrives)
                {
                    if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;

                    string letter = drive.Name.TrimEnd('\\');
                    var existing = diag.Drives.FirstOrDefault(d => d.DriveLetter.Equals(letter, StringComparison.OrdinalIgnoreCase));
                    double totalGb = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                    double freeGb = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                    double usedGb = Math.Max(0, totalGb - freeGb);
                    double pctFree = totalGb > 0 ? (freeGb / totalGb * 100.0) : 0.0;
                    double pctUsed = totalGb > 0 ? (usedGb / totalGb * 100.0) : 0.0;

                    ConsoleColor color = ConsoleColor.Green;
                    if (pctFree < 10.0 || freeGb < 15.0) color = ConsoleColor.Red;
                    else if (pctFree < 20.0 || freeGb < 30.0) color = ConsoleColor.Yellow;

                    if (existing != null)
                    {
                        existing.FreeGb = freeGb;
                        existing.TotalGb = totalGb;
                        existing.UsedGb = usedGb;
                        existing.PercentFree = pctFree;
                        existing.PercentUsed = pctUsed;
                        existing.StatusText = string.Format("{0:N1} / {1:N1} GB ({2:N0}% livre)", freeGb, totalGb, pctFree);
                        existing.StatusColor = color;
                    }
                    else
                    {
                        diag.Drives.Add(new DiskDriveDiagnosis
                        {
                            DriveLetter = letter,
                            VolumeLabel = drive.VolumeLabel,
                            MediaType = "Disco",
                            FreeGb = freeGb,
                            TotalGb = totalGb,
                            UsedGb = usedGb,
                            PercentFree = pctFree,
                            PercentUsed = pctUsed,
                            StatusText = string.Format("{0:N1} / {1:N1} GB ({2:N0}% livre)", freeGb, totalGb, pctFree),
                            StatusColor = color
                        });
                    }
                }
            }
            catch { }
        }

        private static bool CheckInternetConnectivity()
        {
            try
            {
                if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
                    return false;
            }
            catch { }

            // 1. Teste rápido via Ping no Cloudflare DNS (1.1.1.1) - responde em ~15-30ms
            try
            {
                using (var ping = new System.Net.NetworkInformation.Ping())
                {
                    var reply = ping.Send("1.1.1.1", 1000);
                    if (reply != null && reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                        return true;
                }
            }
            catch { }

            // 2. Teste rápido via Ping no Google DNS (8.8.8.8)
            try
            {
                using (var ping = new System.Net.NetworkInformation.Ping())
                {
                    var reply = ping.Send("8.8.8.8", 1000);
                    if (reply != null && reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                        return true;
                }
            }
            catch { }

            // 3. Fallback HTTP rápido
            if (TryHttpGetContains("http://www.msftconnecttest.com/connecttest.txt", "Microsoft Connect Test", 1500))
                return true;

            return TryHttpHead("https://www.microsoft.com", 1500);
        }

        private static bool TryHttpGetContains(string url, string expectedText, int timeoutMs = 2500)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.Timeout = timeoutMs;
                request.ReadWriteTimeout = timeoutMs;
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

        private static bool TryHttpHead(string url, int timeoutMs = 2500)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "HEAD";
                request.Timeout = timeoutMs;
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
                    "install --id Canva.Affinity --scope machine -e --silent --accept-source-agreements --accept-package-agreements",
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
                    Log("  Instalando e provisionando pacote MSIX do Affinity Studio para todos os perfis...", ConsoleColor.Cyan);
                    string safeMsixPath = msixPath.Replace("'", "''");
                    int exitCode = await RunProcessAsync("powershell",
                        string.Format("-NoProfile -ExecutionPolicy Bypass -Command \"Add-AppxProvisionedPackage -Online -PackagePath '{0}' -SkipLicense; Add-AppxPackage -Path '{0}' -AllUsers; if (-not (Get-AppxPackage -AllUsers *Affinity*)) {{ Add-AppxPackage -Path '{0}' }}\"", safeMsixPath),
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
                    "install --id Google.GeminiDesktop --scope machine -e --silent --accept-source-agreements --accept-package-agreements",
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
                    "install --id OpenAI.ChatGPT --scope machine -e --silent --accept-source-agreements --accept-package-agreements",
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
                    Log("  Instalando e provisionando pacote MSIX do ChatGPT Desktop para todos os perfis...", ConsoleColor.Cyan);
                    string safeMsixPath = msixPath.Replace("'", "''");
                    int exitCode = await RunProcessAsync("powershell",
                        string.Format("-NoProfile -ExecutionPolicy Bypass -Command \"Add-AppxProvisionedPackage -Online -PackagePath '{0}' -SkipLicense; Add-AppxPackage -Path '{0}' -AllUsers; if (-not (Get-AppxPackage -AllUsers *OpenAI.ChatGPT*)) {{ Add-AppxPackage -Path '{0}' }}\"", safeMsixPath),
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
                    "install --id Anthropic.Claude --scope machine -e --silent --accept-source-agreements --accept-package-agreements",
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
                    "install --id CrystalDewWorld.CrystalDiskInfo --scope machine -e --silent --accept-source-agreements --accept-package-agreements",
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
                    "install --id CrystalDewWorld.CrystalDiskMark --scope machine -e --silent --accept-source-agreements --accept-package-agreements",
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
                    "install --id Piriform.Recuva --scope machine -e --silent --accept-source-agreements --accept-package-agreements",
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
                        "install --id 7zip.7zip --scope machine -e --silent --accept-package-agreements --accept-source-agreements",
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
                        "install --id Google.Chrome --scope machine -e --silent --accept-package-agreements --accept-source-agreements",
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
                        string msiArgs = program.Arguments ?? "";
                        if (msiArgs.IndexOf("ALLUSERS=", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            msiArgs = (msiArgs + " ALLUSERS=1").Trim();
                        }
                        exitCode = await RunProcessAsync("msiexec",
                            string.Format("/i \"{0}\" {1}", fullPath, msiArgs),
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
                    else if (program.Id == "deepseek")
                    {
                        if (IsDeepSeekDesktopInstalled())
                        {
                            Log(string.Format("[ok] {0} instalado com sucesso.", program.DisplayName), ConsoleColor.Green);
                            return true;
                        }

                        if (!string.Equals(program.Arguments, "/S", StringComparison.OrdinalIgnoreCase))
                        {
                            Log(string.Format("  DeepSeek retornou código {0}. Tentando com parâmetro /S silencioso padrão...", exitCode), ConsoleColor.Yellow);
                            int retryExit = await RunProcessAsync(fullPath, "/S", timeout, procWorkDir, false);
                            if (retryExit == 0 || IsDeepSeekDesktopInstalled())
                            {
                                Log(string.Format("[ok] {0} instalado com sucesso.", program.DisplayName), ConsoleColor.Green);
                                return true;
                            }
                        }

                        Log(string.Format("X {0} retornou código {1}.", program.DisplayName, exitCode), ConsoleColor.Yellow);
                        return false;
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
        /// Realiza uma varredura completa no Windows e desinstala silenciosamente todos os jogos encontrados,
        /// incluindo o aplicativo Xbox (Microsoft.GamingApp), Xbox Live (Microsoft.Xbox.TCUI), Xbox Game Bar,
        /// Serviços de Jogos, Solitaire, jogos casuais da Microsoft e de fabricantes OEM (King, Candy Crush, etc.),
        /// além de jogos e clientes Win32 (Steam, Epic, EA App, Riot, Roblox, etc.), desativando serviços residuais.
        /// </summary>
        public async Task UninstallAllGamesAsync()
        {
            Log("Iniciando varredura profunda no Windows por jogos e componentes Xbox...", ConsoleColor.Yellow);

            var sb = new StringBuilder();
            sb.AppendLine("[Console]::OutputEncoding = [System.Text.Encoding]::UTF8");
            sb.AppendLine("$removedCount = 0");

            // 1. Finaliza processos de jogos, Xbox e overlays para desbloquear remoção de pacotes
            sb.AppendLine("$gameProcesses = @('Xbox*', 'GamingServices*', 'GameBar*', 'GameBarFTServer*', 'bcastdvr*')");
            sb.AppendLine("foreach ($pname in $gameProcesses) {");
            sb.AppendLine("    try {");
            sb.AppendLine("        Get-Process -Name $pname -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue");
            sb.AppendLine("    } catch {}");
            sb.AppendLine("}");
            sb.AppendLine("& taskkill.exe /F /IM Xbox* /IM GamingServices* /IM GameBar* /IM GameBarFTServer* /IM bcastdvr* /T 2>$null | Out-Null");

            // 2. Para servicos do Xbox e Gaming Services para liberar arquivos bloqueados
            sb.AppendLine("$xboxSvcs = @('GamingServices', 'GamingServicesNet', 'XblAuthManager', 'XblGameSave', 'XboxNetApiSvc', 'XboxGipSvc')");
            sb.AppendLine("foreach ($s in $xboxSvcs) {");
            sb.AppendLine("    try {");
            sb.AppendLine("        Stop-Service -Name $s -Force -ErrorAction SilentlyContinue -WarningAction SilentlyContinue");
            sb.AppendLine("    } catch {}");
            sb.AppendLine("}");

            sb.AppendLine("$gamePatterns = @(");
            sb.AppendLine("    '*GamingApp*',");
            sb.AppendLine("    '*Xbox.TCUI*',");
            sb.AppendLine("    '*Xbox*',");
            sb.AppendLine("    '*GamingServices*',");
            sb.AppendLine("    '*Solitaire*',");
            sb.AppendLine("    '*CandyCrush*',");
            sb.AppendLine("    '*King.com*',");
            sb.AppendLine("    '*BubbleWitch*',");
            sb.AppendLine("    '*MarchofEmpires*',");
            sb.AppendLine("    '*Disney*',");
            sb.AppendLine("    '*HiddenCity*',");
            sb.AppendLine("    '*Roblox*',");
            sb.AppendLine("    '*Minecraft*',");
            sb.AppendLine("    '*Asphalt*',");
            sb.AppendLine("    '*FarmVille*',");
            sb.AppendLine("    '*CookingFever*',");
            sb.AppendLine("    '*CaesarsSlots*',");
            sb.AppendLine("    '*RoyalRevolt*',");
            sb.AppendLine("    '*SeekersNotes*',");
            sb.AppendLine("    '*Gardenscapes*',");
            sb.AppendLine("    '*Fishdom*',");
            sb.AppendLine("    '*Homescapes*',");
            sb.AppendLine("    '*Township*',");
            sb.AppendLine("    '*JewelQuest*',");
            sb.AppendLine("    '*Mahjong*',");
            sb.AppendLine("    '*Minesweeper*',");
            sb.AppendLine("    '*Hearts*',");
            sb.AppendLine("    '*Spades*',");
            sb.AppendLine("    '*Sudoku*'");
            sb.AppendLine(")");

            // 3. Remove pacotes provisionados da imagem do Windows (DISM) para que novas contas ou instalacoes nao restaurem
            sb.AppendLine("try {");
            sb.AppendLine("    $allProv = Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue");
            sb.AppendLine("    if ($allProv) {");
            sb.AppendLine("        foreach ($pr in $allProv) {");
            sb.AppendLine("            $pName = $pr.PackageName");
            sb.AppendLine("            if (-not $pName -or ($pName -like '*XboxGameCallableUI*')) { continue }");
            sb.AppendLine("            $match = $false");
            sb.AppendLine("            foreach ($pat in $gamePatterns) {");
            sb.AppendLine("                if ($pName -like $pat -or ($pr.DisplayName -and ($pr.DisplayName -like $pat))) {");
            sb.AppendLine("                    $match = $true");
            sb.AppendLine("                    break");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("            if ($match) {");
            sb.AppendLine("                $disp = if ($pr.DisplayName) { $pr.DisplayName } else { $pName }");
            sb.AppendLine("                Write-Host ('  Removendo provisionamento da imagem: ' + $disp + '...')");
            sb.AppendLine("                try {");
            sb.AppendLine("                    Remove-AppxProvisionedPackage -Online -PackageName $pName -ErrorAction SilentlyContinue | Out-Null");
            sb.AppendLine("                    Write-Host ('[ok] Provisionamento removido da imagem: ' + $disp)");
            sb.AppendLine("                    $removedCount++");
            sb.AppendLine("                } catch {}");
            sb.AppendLine("                & dism.exe /Online /Remove-ProvisionedAppxPackage /PackageName:$pName /Quiet /NoRestart 2>$null | Out-Null");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("} catch {}");

            // 4. Remove pacotes Appx instalados para todos os usuarios
            sb.AppendLine("foreach ($pat in $gamePatterns) {");
            sb.AppendLine("    try {");
            sb.AppendLine("        $pkgs = Get-AppxPackage -Name $pat -AllUsers -ErrorAction SilentlyContinue");
            sb.AppendLine("        if (-not $pkgs) { $pkgs = Get-AppxPackage -Name $pat -ErrorAction SilentlyContinue }");
            sb.AppendLine("        foreach ($p in $pkgs) {");
            sb.AppendLine("            if ($p.NonRemovable -or $p.IsFramework -or ($p.Name -like '*XboxGameCallableUI*')) { continue }");
            sb.AppendLine("            Write-Host ('  Desinstalando pacote de jogo: ' + $p.Name + ' (' + $p.Version + ')...')");
            sb.AppendLine("            $pkgRemoved = $false");
            sb.AppendLine("            try {");
            sb.AppendLine("                Remove-AppxPackage -Package $p.PackageFullName -AllUsers -ErrorAction Stop | Out-Null");
            sb.AppendLine("                $pkgRemoved = $true");
            sb.AppendLine("            } catch {");
            sb.AppendLine("                try {");
            sb.AppendLine("                    Remove-AppxPackage -Package $p.PackageFullName -ErrorAction Stop | Out-Null");
            sb.AppendLine("                    $pkgRemoved = $true");
            sb.AppendLine("                } catch {}");
            sb.AppendLine("            }");
            sb.AppendLine("            if (-not $pkgRemoved) {");
            sb.AppendLine("                try {");
            sb.AppendLine("                    & winget.exe uninstall --id $p.PackageFamilyName --silent --accept-source-agreements 2>$null | Out-Null");
            sb.AppendLine("                } catch {}");
            sb.AppendLine("            }");
            sb.AppendLine("            Write-Host ('[ok] Pacote de jogo desinstalado: ' + $p.Name)");
            sb.AppendLine("            $removedCount++");
            sb.AppendLine("        }");
            sb.AppendLine("    } catch {}");
            sb.AppendLine("}");

            // 5. Desinstalacao complementar dos pacotes Xbox principais via Winget
            sb.AppendLine("$targetXboxIds = @(");
            sb.AppendLine("    'Microsoft.GamingApp_8wekyb3d8bbwe',");
            sb.AppendLine("    'Microsoft.Xbox.TCUI_8wekyb3d8bbwe',");
            sb.AppendLine("    'Microsoft.XboxGamingOverlay_8wekyb3d8bbwe',");
            sb.AppendLine("    'Microsoft.XboxIdentityProvider_8wekyb3d8bbwe',");
            sb.AppendLine("    'Microsoft.XboxSpeechToTextOverlay_8wekyb3d8bbwe',");
            sb.AppendLine("    'Microsoft.GamingServices_8wekyb3d8bbwe'");
            sb.AppendLine(")");
            sb.AppendLine("foreach ($id in $targetXboxIds) {");
            sb.AppendLine("    try {");
            sb.AppendLine("        & winget.exe uninstall --id $id --silent --accept-source-agreements 2>$null | Out-Null");
            sb.AppendLine("    } catch {}");
            sb.AppendLine("}");

            // 6. Desinstalacao de jogos e clientes Win32 / MSI residuais
            sb.AppendLine("$regPaths = @(");
            sb.AppendLine("    'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\*',");
            sb.AppendLine("    'HKLM:\\SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\*',");
            sb.AppendLine("    'HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\*'");
            sb.AppendLine(")");
            sb.AppendLine("$gameWin32Keywords = @(");
            sb.AppendLine("    'Steam', 'Epic Games', 'Riot Games', 'Valorant', 'Battle.net',");
            sb.AppendLine("    'Ubisoft Connect', 'GOG Galaxy', 'EA app', 'Origin', 'Roblox Player',");
            sb.AppendLine("    'Minecraft Launcher', 'Rockstar Games', 'Xbox Service'");
            sb.AppendLine(")");
            sb.AppendLine("try {");
            sb.AppendLine("    $items = Get-ItemProperty $regPaths -ErrorAction SilentlyContinue");
            sb.AppendLine("    foreach ($item in $items) {");
            sb.AppendLine("        $dn = $item.DisplayName");
            sb.AppendLine("        if ($dn) {");
            sb.AppendLine("            foreach ($kw in $gameWin32Keywords) {");
            sb.AppendLine("                if ($dn -match [regex]::Escape($kw)) {");
            sb.AppendLine("                    Write-Host ('  Localizado jogo/componente Win32: ' + $dn)");
            sb.AppendLine("                    $quiet = $item.QuietUninstallString");
            sb.AppendLine("                    $uninst = $item.UninstallString");
            sb.AppendLine("                    try {");
            sb.AppendLine("                        if ($uninst -match '\\{[A-Fa-f0-9\\-]+\\}') {");
            sb.AppendLine("                            $guid = $matches[0]");
            sb.AppendLine("                            Start-Process -FilePath 'msiexec.exe' -ArgumentList ('/x ' + $guid + ' /qn /norestart') -Wait -WindowStyle Hidden");
            sb.AppendLine("                            Write-Host ('[ok] Componente desinstalado: ' + $dn)");
            sb.AppendLine("                            $removedCount++");
            sb.AppendLine("                        } elseif ($quiet) {");
            sb.AppendLine("                            Start-Process -FilePath 'cmd.exe' -ArgumentList ('/c ' + $quiet) -Wait -WindowStyle Hidden");
            sb.AppendLine("                            Write-Host ('[ok] Jogo Win32 desinstalado: ' + $dn)");
            sb.AppendLine("                            $removedCount++");
            sb.AppendLine("                        } elseif ($uninst) {");
            sb.AppendLine("                            Start-Process -FilePath 'cmd.exe' -ArgumentList ('/c ' + $uninst + ' /S /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /quiet /qn') -Wait -WindowStyle Hidden");
            sb.AppendLine("                            Write-Host ('[ok] Jogo Win32 desinstalado: ' + $dn)");
            sb.AppendLine("                            $removedCount++");
            sb.AppendLine("                        }");
            sb.AppendLine("                    } catch {");
            sb.AppendLine("                        Write-Host ('  [!] Falha ao desinstalar ' + $dn + ': ' + $_.Exception.Message)");
            sb.AppendLine("                    }");
            sb.AppendLine("                    break");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("} catch {}");

            // 7. Desativacao definitiva de servicos do Xbox e politicas GameDVR
            sb.AppendLine("try {");
            sb.AppendLine("    foreach ($s in $xboxSvcs) {");
            sb.AppendLine("        $svc = Get-Service -Name $s -ErrorAction SilentlyContinue");
            sb.AppendLine("        if ($svc) {");
            sb.AppendLine("            Stop-Service -Name $s -Force -ErrorAction SilentlyContinue -WarningAction SilentlyContinue");
            sb.AppendLine("            Set-Service -Name $s -StartupType Disabled -ErrorAction SilentlyContinue -WarningAction SilentlyContinue");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("    if (-not (Test-Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\GameDVR')) {");
            sb.AppendLine("        New-Item -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\GameDVR' -Force -ErrorAction SilentlyContinue | Out-Null");
            sb.AppendLine("    }");
            sb.AppendLine("    Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\GameDVR' -Name 'AllowGameDVR' -Value 0 -Type DWord -ErrorAction SilentlyContinue");
            sb.AppendLine("    Set-ItemProperty -Path 'HKCU:\\System\\GameConfigStore' -Name 'GameDVR_Enabled' -Value 0 -Type DWord -ErrorAction SilentlyContinue");
            sb.AppendLine("    if (-not (Test-Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\GameDVR')) {");
            sb.AppendLine("        New-Item -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\GameDVR' -Force -ErrorAction SilentlyContinue | Out-Null");
            sb.AppendLine("    }");
            sb.AppendLine("    Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\GameDVR' -Name 'AppCaptureEnabled' -Value 0 -Type DWord -ErrorAction SilentlyContinue");
            sb.AppendLine("} catch {}");

            // 8. Configura o perfil Default (C:\Users\Default\NTUSER.DAT) para bloquear jogos e Xbox em todos os novos perfis
            sb.AppendLine("try {");
            sb.AppendLine("    $defaultNtUser = \"$env:SystemDrive\\Users\\Default\\NTUSER.DAT\"");
            sb.AppendLine("    if (Test-Path $defaultNtUser) {");
            sb.AppendLine("        reg load \"HKU\\DefaultUser\" \"$defaultNtUser\" >$null 2>&1");
            sb.AppendLine("        if ($LASTEXITCODE -eq 0) {");
            sb.AppendLine("            reg add \"HKU\\DefaultUser\\Software\\Policies\\Microsoft\\Windows\\CloudContent\" /v DisableWindowsConsumerFeatures /t REG_DWORD /d 1 /f >$null 2>&1");
            sb.AppendLine("            reg add \"HKU\\DefaultUser\\Software\\Policies\\Microsoft\\Windows\\GameDVR\" /v AllowGameDVR /t REG_DWORD /d 0 /f >$null 2>&1");
            sb.AppendLine("            reg add \"HKU\\DefaultUser\\System\\GameConfigStore\" /v GameDVR_Enabled /t REG_DWORD /d 0 /f >$null 2>&1");
            sb.AppendLine("            reg add \"HKU\\DefaultUser\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager\" /v ContentDeliveryAllowed /t REG_DWORD /d 0 /f >$null 2>&1");
            sb.AppendLine("            reg add \"HKU\\DefaultUser\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager\" /v SilentInstalledAppsEnabled /t REG_DWORD /d 0 /f >$null 2>&1");
            sb.AppendLine("            reg add \"HKU\\DefaultUser\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager\" /v OemPreInstalledAppsEnabled /t REG_DWORD /d 0 /f >$null 2>&1");
            sb.AppendLine("            reg add \"HKU\\DefaultUser\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager\" /v PreInstalledAppsEnabled /t REG_DWORD /d 0 /f >$null 2>&1");
            sb.AppendLine("            reg add \"HKU\\DefaultUser\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager\" /v PreInstalledAppsEverEnabled /t REG_DWORD /d 0 /f >$null 2>&1");
            sb.AppendLine("            reg add \"HKU\\DefaultUser\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager\" /v SystemPaneSuggestionsEnabled /t REG_DWORD /d 0 /f >$null 2>&1");
            sb.AppendLine("            reg add \"HKU\\DefaultUser\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager\" /v SubscribedContent-314563Enabled /t REG_DWORD /d 0 /f >$null 2>&1");
            sb.AppendLine("            reg add \"HKU\\DefaultUser\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager\" /v SubscribedContent-338387Enabled /t REG_DWORD /d 0 /f >$null 2>&1");
            sb.AppendLine("            reg add \"HKU\\DefaultUser\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager\" /v SubscribedContent-338388Enabled /t REG_DWORD /d 0 /f >$null 2>&1");
            sb.AppendLine("            reg add \"HKU\\DefaultUser\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager\" /v SubscribedContent-338389Enabled /t REG_DWORD /d 0 /f >$null 2>&1");
            sb.AppendLine("            $cleanCmd = 'powershell.exe -WindowStyle Hidden -NoProfile -ExecutionPolicy Bypass -Command \"Get-AppxPackage *Xbox*, *GamingApp*, *TCUI*, *GamingServices*, *Solitaire* -ErrorAction SilentlyContinue | Remove-AppxPackage -ErrorAction SilentlyContinue\"'");
            sb.AppendLine("            reg add \"HKU\\DefaultUser\\Software\\Microsoft\\Windows\\CurrentVersion\\RunOnce\" /v \"PurgeXboxAndGames\" /t REG_SZ /d $cleanCmd /f >$null 2>&1");
            sb.AppendLine("            reg unload \"HKU\\DefaultUser\" >$null 2>&1");
            sb.AppendLine("            Write-Host '[ok] Perfil Default configurado: novos usuarios nunca receberao jogos ou Xbox.'");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("} catch {}");

            // 9. Politicas globais de maquina para barrar download de jogos de consumidor em qualquer usuario
            sb.AppendLine("try {");
            sb.AppendLine("    if (-not (Test-Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent')) {");
            sb.AppendLine("        New-Item -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent' -Force -ErrorAction SilentlyContinue | Out-Null");
            sb.AppendLine("    }");
            sb.AppendLine("    Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent' -Name 'DisableWindowsConsumerFeatures' -Value 1 -Type DWord -ErrorAction SilentlyContinue");
            sb.AppendLine("    Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent' -Name 'DisableCloudOptimizedContent' -Value 1 -Type DWord -ErrorAction SilentlyContinue");
            sb.AppendLine("    if (-not (Test-Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\WindowsStore')) {");
            sb.AppendLine("        New-Item -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\WindowsStore' -Force -ErrorAction SilentlyContinue | Out-Null");
            sb.AppendLine("    }");
            sb.AppendLine("    Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\WindowsStore' -Name 'AutoDownload' -Value 2 -Type DWord -ErrorAction SilentlyContinue");
            sb.AppendLine("} catch {}");

            // 10. Limpeza de atalhos residuais de jogos no Menu Iniciar Default e Public
            sb.AppendLine("try {");
            sb.AppendLine("    $startMenuPaths = @(");
            sb.AppendLine("        \"$env:SystemDrive\\Users\\Default\\AppData\\Roaming\\Microsoft\\Windows\\Start Menu\\Programs\",");
            sb.AppendLine("        \"$env:ProgramData\\Microsoft\\Windows\\Start Menu\\Programs\"");
            sb.AppendLine("    )");
            sb.AppendLine("    foreach ($sm in $startMenuPaths) {");
            sb.AppendLine("        if (Test-Path $sm) {");
            sb.AppendLine("            Get-ChildItem -Path $sm -Recurse -Include *Xbox*, *Gaming*, *Solitaire* -ErrorAction SilentlyContinue | Remove-Item -Force -Recurse -ErrorAction SilentlyContinue");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("} catch {}");

            sb.AppendLine("if ($removedCount -gt 0) {");
            sb.AppendLine("    Write-Host ('[ok] Varredura concluida: ' + $removedCount + ' jogos e componentes removidos com sucesso.')");
            sb.AppendLine("} else {");
            sb.AppendLine("    Write-Host '[ok] Varredura concluida: Nenhum jogo ou componente pendente encontrado.'");
            sb.AppendLine("}");

            int exitCode = await RunPowerShellScriptWithLoggingAsync(sb.ToString(), 300000);
            if (exitCode == 0)
                Log("[ok] Processamento de desinstalação de jogos concluído.", ConsoleColor.Green);
            else
                Log("Aviso: Varredura de jogos finalizada com código " + exitCode, ConsoleColor.Yellow);

            await Task.Delay(300);
        }

        /// <summary>
        /// Gera uma prévia das contas, perfis e diretórios que serão removidos ou preservados.
        /// </summary>
        /// <param name="keepUser1">Primeiro usuário adicional que deve ser preservado.</param>
        /// <param name="keepUser2">Segundo usuário adicional que deve ser preservado.</param>
        /// <returns>Resumo de contas e perfis que serão removidos ou preservados.</returns>
        public string GetUserProfileCleanupPreview(string keepUser1 = null, string keepUser2 = null)
        {
            var keepNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Environment.UserName,
                "Administrator",
                "Administrador",
                "DefaultAccount",
                "Guest",
                "Convidado",
                "WDAGUtilityAccount"
            };
            if (!string.IsNullOrWhiteSpace(keepUser1))
                keepNames.Add(keepUser1);
            if (!string.IsNullOrWhiteSpace(keepUser2))
                keepNames.Add(keepUser2);

            var remove = new List<string>();
            var preserve = new List<string>();
            var knownProfilePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var loadedProfileSids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string currentProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            using (var searcher = new ManagementObjectSearcher(
                "SELECT SID, Loaded FROM Win32_UserProfile WHERE Loaded = True"))
            using (var profiles = searcher.Get())
            {
                foreach (ManagementObject profile in profiles)
                {
                    string sid = Convert.ToString(profile["SID"]);
                    if (!string.IsNullOrWhiteSpace(sid))
                        loadedProfileSids.Add(sid);
                }
            }

            using (var searcher = new ManagementObjectSearcher(
                "SELECT Name, SID FROM Win32_UserAccount WHERE LocalAccount = True"))
            using (var users = searcher.Get())
            {
                foreach (ManagementObject user in users)
                {
                    string name = Convert.ToString(user["Name"]);
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    string sid = Convert.ToString(user["SID"]);
                    if (keepNames.Contains(name) || loadedProfileSids.Contains(sid))
                        preserve.Add("Conta local: " + name);
                    else
                        remove.Add("Conta local: " + name);
                }
            }

            using (var searcher = new ManagementObjectSearcher(
                "SELECT LocalPath, SID, Special, Loaded FROM Win32_UserProfile"))
            using (var profiles = searcher.Get())
            {
                foreach (ManagementObject profile in profiles)
                {
                    string path = Convert.ToString(profile["LocalPath"]);
                    string sid = Convert.ToString(profile["SID"]);
                    if (string.IsNullOrWhiteSpace(path))
                        continue;

                    knownProfilePaths.Add(path);
                    string leaf = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
                    bool isSystemProfile = Convert.ToBoolean(profile["Special"]) ||
                        sid == "S-1-5-18" || sid == "S-1-5-19" || sid == "S-1-5-20" ||
                        Regex.IsMatch(path, @"(?i)\\(Default|Public|All Users|ServiceProfiles|systemprofile)(\\|$)");
                    bool isPreserved = keepNames.Contains(leaf) ||
                        string.Equals(path, currentProfilePath, StringComparison.OrdinalIgnoreCase);

                    if (isSystemProfile || Convert.ToBoolean(profile["Loaded"]) || isPreserved)
                        preserve.Add("Perfil: " + path);
                    else
                        remove.Add("Perfil: " + path);
                }
            }

            string usersDirectory = Path.Combine(
                Path.GetPathRoot(Environment.SystemDirectory), "Users");
            if (Directory.Exists(usersDirectory))
            {
                foreach (string path in Directory.GetDirectories(usersDirectory))
                {
                    var directory = new DirectoryInfo(path);
                    string name = directory.Name;
                    if ((directory.Attributes & FileAttributes.ReparsePoint) != 0 ||
                        Regex.IsMatch(name, @"(?i)^(Public|Default|Default User|All Users)$"))
                    {
                        continue;
                    }

                    if (knownProfilePaths.Contains(path))
                        continue;

                    if (keepNames.Contains(name) ||
                        string.Equals(path, currentProfilePath, StringComparison.OrdinalIgnoreCase))
                        preserve.Add("Pasta de perfil: " + path);
                    else
                        remove.Add("Pasta de perfil órfã: " + path);
                }
            }

            var preview = new StringBuilder();
            preview.AppendLine("Serão removidos:");
            if (remove.Count == 0)
                preview.AppendLine("  Nenhuma conta, perfil ou pasta órfã encontrada.");
            else
            {
                for (int i = 0; i < remove.Count; i++)
                    preview.AppendLine("  " + remove[i]);
            }

            preview.AppendLine();
            preview.AppendLine("Serão preservados:");
            preview.AppendLine("  Conta atual e contas essenciais do Windows.");
            for (int i = 0; i < preserve.Count; i++)
                preview.AppendLine("  " + preserve[i]);
            return preview.ToString();
        }

        /// <summary>
        /// Remove contas e perfis que não estejam protegidos pela prévia confirmada.
        /// </summary>
        /// <param name="keepUser1">Primeiro usuário adicional que deve ser preservado.</param>
        /// <param name="keepUser2">Segundo usuário adicional que deve ser preservado.</param>
        public async Task RemoveOtherUserProfilesAsync(string keepUser1 = null, string keepUser2 = null)
        {
            Log("Iniciando varredura e exclusão completa de usuários e perfis do Windows...", ConsoleColor.Yellow);

            string currentLoggedUser = Environment.UserName;
            string k1 = string.IsNullOrEmpty(keepUser1) ? "" : keepUser1.Replace("'", "''");
            string k2 = string.IsNullOrEmpty(keepUser2) ? "" : keepUser2.Replace("'", "''");
            string currentProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).Replace("'", "''");

            var sb = new StringBuilder();
            sb.AppendLine("[Console]::OutputEncoding = [System.Text.Encoding]::UTF8");
            sb.AppendLine(string.Format("$currentLoggedUser = '{0}'", currentLoggedUser.Replace("'", "''")));
            sb.AppendLine(string.Format("$currentProfilePath = '{0}'", currentProfilePath));
            sb.AppendLine(string.Format("$keepList = @('{0}', '{1}', '{2}', 'Administrator', 'Administrador', 'DefaultAccount', 'Guest', 'Convidado', 'WDAGUtilityAccount')",
                currentLoggedUser.Replace("'", "''"), k1, k2));
            sb.AppendLine("$systemSids = @('S-1-5-18', 'S-1-5-19', 'S-1-5-20')");
            sb.AppendLine("$removedCount = 0");
            sb.AppendLine("$cleanupFailed = $false");
            sb.AppendLine("try {");
            sb.AppendLine("    $allProfiles = @(Get-CimInstance Win32_UserProfile -ErrorAction Stop)");
            sb.AppendLine("    $loadedProfileSids = @($allProfiles | Where-Object { $_.Loaded } | ForEach-Object { $_.SID })");
            sb.AppendLine("    $loadedProfilePaths = @($allProfiles | Where-Object { $_.Loaded } | ForEach-Object { $_.LocalPath })");
            sb.AppendLine("} catch {");
            sb.AppendLine("    Write-Host ('  [!] Nao foi possivel verificar os perfis carregados; limpeza abortada: ' + $_.Exception.Message)");
            sb.AppendLine("    exit 1");
            sb.AppendLine("}");
            sb.AppendLine("Write-Host ('  Conta em execucao preservada: ' + $currentLoggedUser)");
            sb.AppendLine("Write-Host '  Contas protegidas: Administrador e servicos essenciais do sistema'");

            // FASE 1: Varredura e exclusão de contas locais de usuário no Windows (Win32_UserAccount)
            sb.AppendLine("try {");
            sb.AppendLine("    $localUsers = Get-CimInstance Win32_UserAccount -Filter 'LocalAccount = True' -ErrorAction Stop");
            sb.AppendLine("    foreach ($user in $localUsers) {");
            sb.AppendLine("        $name = $user.Name");
            sb.AppendLine("        $sid = $user.SID");
            sb.AppendLine("        $isKeep = $loadedProfileSids -contains $sid");
            sb.AppendLine("        foreach ($k in $keepList) {");
            sb.AppendLine("            if ($k -and ($name -ieq $k)) { $isKeep = $true; break }");
            sb.AppendLine("        }");
            sb.AppendLine("        if (-not $isKeep) {");
            sb.AppendLine("            Write-Host ('  Localizada conta para remocao: ' + $name)");
            sb.AppendLine("            try {");
            sb.AppendLine("                & net user \"$name\" /delete | Out-Null");
            sb.AppendLine("                if ($LASTEXITCODE -eq 0) {");
            sb.AppendLine("                    Write-Host ('[ok] Conta de usuario removida do Windows: ' + $name)");
            sb.AppendLine("                    $removedCount++");
            sb.AppendLine("                } else {");
            sb.AppendLine("                    $cleanupFailed = $true");
            sb.AppendLine("                    Write-Host ('  [!] net user retornou codigo ' + $LASTEXITCODE + ' ao remover ' + $name)");
            sb.AppendLine("                }");
            sb.AppendLine("            } catch {");
            sb.AppendLine("                $cleanupFailed = $true");
            sb.AppendLine("                Write-Host ('  [!] Erro ao remover conta ' + $name + ': ' + $_.Exception.Message)");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("} catch { $cleanupFailed = $true; Write-Host ('  [!] Erro ao consultar contas locais: ' + $_.Exception.Message) }");

            // FASE 2: Varredura e exclusão de perfis em disco e WMI (Win32_UserProfile)
            sb.AppendLine("try {");
            sb.AppendLine("    $profiles = $allProfiles");
            sb.AppendLine("    foreach ($p in $profiles) {");
            sb.AppendLine("        $path = $p.LocalPath");
            sb.AppendLine("        $sid = $p.SID");
            sb.AppendLine("        if (-not $path) { continue }");
            sb.AppendLine("        $leaf = Split-Path -Leaf $path");
            sb.AppendLine("        if ($p.Special -or $p.Loaded -or ($systemSids -contains $sid) -or ($path -match '(?i)\\\\(Default|Public|All Users|ServiceProfiles|systemprofile)(\\\\|$)') -or ($path -ieq $currentProfilePath)) {");
            sb.AppendLine("            continue");
            sb.AppendLine("        }");
            sb.AppendLine("        $isKeep = $false");
            sb.AppendLine("        foreach ($k in $keepList) {");
            sb.AppendLine("            if ($k -and (($leaf -ieq $k) -or ($path -match ('(?i)\\\\' + [regex]::Escape($k) + '$')))) {");
            sb.AppendLine("                $isKeep = $true; break");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("        if (-not $isKeep) {");
            sb.AppendLine("            Write-Host ('  Apagando perfil e dados de usuario: ' + $path)");
            sb.AppendLine("            try { Remove-CimInstance -InputObject $p -ErrorAction Stop | Out-Null } catch { $cleanupFailed = $true; Write-Host ('  [!] Erro ao remover registro WMI de ' + $path + ': ' + $_.Exception.Message) }");
            sb.AppendLine("            try {");
            sb.AppendLine("                $regKey = \"HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\ProfileList\\$sid\"");
            sb.AppendLine("                if (Test-Path $regKey) { Remove-Item -Path $regKey -Recurse -Force -ErrorAction Stop | Out-Null }");
            sb.AppendLine("                $regBak = \"HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\ProfileList\\$sid.bak\"");
            sb.AppendLine("                if (Test-Path $regBak) { Remove-Item -Path $regBak -Recurse -Force -ErrorAction Stop | Out-Null }");
            sb.AppendLine("            } catch { $cleanupFailed = $true; Write-Host ('  [!] Erro ao limpar registro do perfil ' + $sid + ': ' + $_.Exception.Message) }");
            sb.AppendLine("            try {");
            sb.AppendLine("                if (Test-Path $path) {");
            sb.AppendLine("                    & cmd.exe /c \"takeown /f `\"$path`\" /r /d y >nul 2>&1\"");
            sb.AppendLine("                    & cmd.exe /c \"icacls `\"$path`\" /grant administrators:F /t /c /q >nul 2>&1\"");
            sb.AppendLine("                    & cmd.exe /c \"rd /s /q `\"$path`\" >nul 2>&1\"");
            sb.AppendLine("                    if (Test-Path $path) { Remove-Item -Path $path -Recurse -Force -ErrorAction SilentlyContinue | Out-Null }");
            sb.AppendLine("                    if (-not (Test-Path $path)) {");
            sb.AppendLine("                        Write-Host ('[ok] Pasta de dados e arquivos excluida do disco: ' + $path)");
            sb.AppendLine("                    } else {");
            sb.AppendLine("                        $cleanupFailed = $true");
            sb.AppendLine("                        Write-Host ('  [!] Nao foi possivel excluir completamente a pasta: ' + $path)");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            sb.AppendLine("            } catch { $cleanupFailed = $true; Write-Host ('  [!] Erro ao excluir pasta ' + $path + ': ' + $_.Exception.Message) }");
            sb.AppendLine("            $removedCount++");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("} catch { $cleanupFailed = $true; Write-Host ('  [!] Erro ao consultar perfis WMI: ' + $_.Exception.Message) }");

            // FASE 3: Varredura de pastas órfãs em C:\Users
            sb.AppendLine("try {");
            sb.AppendLine("    $usersBaseDir = Split-Path $env:USERPROFILE");
            sb.AppendLine("    if (Test-Path $usersBaseDir) {");
            sb.AppendLine("        $userDirs = Get-ChildItem -Path $usersBaseDir -Directory -ErrorAction Stop");
            sb.AppendLine("        foreach ($d in $userDirs) {");
            sb.AppendLine("            $folderName = $d.Name");
            sb.AppendLine("            $fullPath = $d.FullName");
            sb.AppendLine("            if ($folderName -match '(?i)^(Public|Default|Default User|All Users)$' -or ($fullPath -ieq $currentProfilePath) -or ($loadedProfilePaths -contains $fullPath)) { continue }");
            sb.AppendLine("            $isKeep = $false");
            sb.AppendLine("            foreach ($k in $keepList) {");
            sb.AppendLine("            if ($k -and ($folderName -ieq $k)) { $isKeep = $true; break }");
            sb.AppendLine("            }");
            sb.AppendLine("            if (-not $isKeep) {");
            sb.AppendLine("                Write-Host ('  Removendo pasta residual orfa: ' + $fullPath)");
            sb.AppendLine("                try {");
            sb.AppendLine("                    & cmd.exe /c \"takeown /f `\"$fullPath`\" /r /d y >nul 2>&1\"");
            sb.AppendLine("                    & cmd.exe /c \"icacls `\"$fullPath`\" /grant administrators:F /t /c /q >nul 2>&1\"");
            sb.AppendLine("                    & cmd.exe /c \"rd /s /q `\"$fullPath`\" >nul 2>&1\"");
            sb.AppendLine("                    if (Test-Path $fullPath) { Remove-Item -Path $fullPath -Recurse -Force -ErrorAction SilentlyContinue | Out-Null }");
            sb.AppendLine("                    if (-not (Test-Path $fullPath)) {");
            sb.AppendLine("                        Write-Host ('[ok] Pasta residual excluida com sucesso: ' + $fullPath)");
            sb.AppendLine("                        $removedCount++");
            sb.AppendLine("                    } else {");
            sb.AppendLine("                        $cleanupFailed = $true");
            sb.AppendLine("                        Write-Host ('  [!] Nao foi possivel excluir a pasta residual: ' + $fullPath)");
            sb.AppendLine("                    }");
            sb.AppendLine("                } catch { $cleanupFailed = $true; Write-Host ('  [!] Erro ao excluir pasta residual ' + $fullPath + ': ' + $_.Exception.Message) }");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("} catch { $cleanupFailed = $true; Write-Host ('  [!] Erro ao consultar pastas de usuarios: ' + $_.Exception.Message) }");

            sb.AppendLine("if ($removedCount -gt 0) {");
            sb.AppendLine("    Write-Host ('[ok] Exclusao concluida: ' + $removedCount + ' contas/perfis foram completamente removidos.')");
            sb.AppendLine("} else {");
            sb.AppendLine("    Write-Host '[ok] Nenhum perfil ou conta adicional encontrada para remocao.'");
            sb.AppendLine("}");
            sb.AppendLine("if ($cleanupFailed) { exit 1 }");

            int exitCode = await RunPowerShellScriptWithLoggingAsync(sb.ToString(), 180000);
            if (exitCode == 0)
                Log("[ok] Limpeza e exclusão de perfis do Windows concluída com êxito.", ConsoleColor.Green);
            else
                Log("Aviso: Limpeza de perfis finalizada com código " + exitCode, ConsoleColor.Yellow);

            await Task.Delay(300);
        }

        /// <summary>
        /// Realiza uma varredura profunda no sistema (via Central de Segurança do Windows WMI SecurityCenter2,
        /// registro HKLM/HKCU de 32 e 64 bits e serviços do sistema) e desinstala silenciosamente todos os antivírus
        /// de terceiros encontrados (McAfee, Avast, AVG, Norton, Kaspersky, Bitdefender, ESET, etc.),
        /// reativando e assegurando a operação do Microsoft Defender com proteção em tempo real.
        /// </summary>
        public async Task UninstallThirdPartyAntivirusAsync()
        {
            Log("Iniciando varredura no Windows por antivírus de terceiros...", ConsoleColor.Yellow);

            var sb = new StringBuilder();
            sb.AppendLine("[Console]::OutputEncoding = [System.Text.Encoding]::UTF8");
            sb.AppendLine("$detectedAVs = @()");
            sb.AppendLine("try {");
            sb.AppendLine("    $avCim = Get-CimInstance -Namespace root\\SecurityCenter2 -ClassName AntivirusProduct -ErrorAction SilentlyContinue |");
            sb.AppendLine("             Where-Object { $_.displayName -notmatch '(?i)Windows Defender' }");
            sb.AppendLine("    if ($avCim) {");
            sb.AppendLine("        foreach ($av in $avCim) {");
            sb.AppendLine("            $detectedAVs += $av.displayName");
            sb.AppendLine("            Write-Host ('  [Alerta] Antivirus ativo detectado no sistema: ' + $av.displayName)");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("} catch {");
            sb.AppendLine("    Write-Host ('  Aviso ao consultar SecurityCenter2: ' + $_.Exception.Message)");
            sb.AppendLine("}");
            sb.AppendLine("$regPaths = @(");
            sb.AppendLine("    'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\*',");
            sb.AppendLine("    'HKLM:\\SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\*',");
            sb.AppendLine("    'HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\*'");
            sb.AppendLine(")");
            sb.AppendLine("$avKeywords = @(");
            sb.AppendLine("    'McAfee', 'Avast', 'AVG', 'Norton', 'Symantec', 'Kaspersky', 'Bitdefender',");
            sb.AppendLine("    'ESET', 'TotalAV', 'Avira', 'Malwarebytes', 'Webroot', 'Trend Micro',");
            sb.AppendLine("    'Sophos', 'Baidu', 'Panda', 'BullGuard', 'F-Secure', 'Comodo', 'Segurazo',");
            sb.AppendLine("    'RAV Endpoint', 'ByteFence', 'CCleaner Cloud', 'SuperAntiSpyware', 'Quick Heal',");
            sb.AppendLine("    'Adaware', '360 Total Security', 'Dr.Web', 'G Data', 'VIPRE', 'ZoneAlarm'");
            sb.AppendLine(")");
            sb.AppendLine("$uninstalledCount = 0");
            sb.AppendLine("try {");
            sb.AppendLine("    $items = Get-ItemProperty $regPaths -ErrorAction SilentlyContinue");
            sb.AppendLine("    foreach ($item in $items) {");
            sb.AppendLine("        $dn = $item.DisplayName");
            sb.AppendLine("        if ($dn) {");
            sb.AppendLine("            $isTarget = $false");
            sb.AppendLine("            foreach ($kw in $avKeywords) {");
            sb.AppendLine("                if ($dn -match ('(?i)' + [regex]::Escape($kw))) { $isTarget = $true; break }");
            sb.AppendLine("            }");
            sb.AppendLine("            if (-not $isTarget) {");
            sb.AppendLine("                foreach ($det in $detectedAVs) {");
            sb.AppendLine("                    if ($dn -match ('(?i)' + [regex]::Escape($det))) { $isTarget = $true; break }");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("            if ($isTarget) {");
            sb.AppendLine("                Write-Host ('  Localizado software de seguranca: ' + $dn)");
            sb.AppendLine("                $quiet = $item.QuietUninstallString");
            sb.AppendLine("                $uninst = $item.UninstallString");
            sb.AppendLine("                $child = $item.PSChildName");
            sb.AppendLine("                try {");
            sb.AppendLine("                    if ($child -match '^\\{[0-9a-fA-F\\-]{36}\\}$') {");
            sb.AppendLine("                        Write-Host ('  Executando desinstalacao silenciosa MSI: ' + $child)");
            sb.AppendLine("                        Start-Process -FilePath 'msiexec.exe' -ArgumentList ('/x ' + $child + ' /qn /norestart') -Wait -WindowStyle Hidden");
            sb.AppendLine("                        Write-Host ('[ok] Desinstalado com sucesso: ' + $dn)");
            sb.AppendLine("                        $uninstalledCount++");
            sb.AppendLine("                    } elseif ($quiet) {");
            sb.AppendLine("                        Write-Host ('  Executando desinstalacao silenciosa: ' + $dn)");
            sb.AppendLine("                        Start-Process -FilePath 'cmd.exe' -ArgumentList ('/c ' + $quiet) -Wait -WindowStyle Hidden");
            sb.AppendLine("                        Write-Host ('[ok] Desinstalado com sucesso: ' + $dn)");
            sb.AppendLine("                        $uninstalledCount++");
            sb.AppendLine("                    } elseif ($uninst) {");
            sb.AppendLine("                        Write-Host ('  Executando desinstalacao automatizada: ' + $dn)");
            sb.AppendLine("                        if ($uninst -match 'msiexec') {");
            sb.AppendLine("                            $msiArgs = $uninst -replace '(?i)/I', '/X' + ' /qn /norestart'");
            sb.AppendLine("                            Start-Process -FilePath 'cmd.exe' -ArgumentList ('/c ' + $msiArgs) -Wait -WindowStyle Hidden");
            sb.AppendLine("                        } else {");
            sb.AppendLine("                            Start-Process -FilePath 'cmd.exe' -ArgumentList ('/c ' + $uninst + ' /SILENT /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /quiet /qn /s /S') -Wait -WindowStyle Hidden");
            sb.AppendLine("                        }");
            sb.AppendLine("                        Write-Host ('[ok] Desinstalado com sucesso: ' + $dn)");
            sb.AppendLine("                        $uninstalledCount++");
            sb.AppendLine("                    }");
            sb.AppendLine("                } catch {");
            sb.AppendLine("                    Write-Host ('  [!] Falha ao desinstalar ' + $dn + ': ' + $_.Exception.Message)");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("} catch {}");
            sb.AppendLine("try {");
            sb.AppendLine("    Get-Service | Where-Object {");
            sb.AppendLine("        $sn = $_.Name; $sd = $_.DisplayName");
            sb.AppendLine("        foreach ($kw in $avKeywords) {");
            sb.AppendLine("            if ($sn -match ('(?i)' + [regex]::Escape($kw)) -or $sd -match ('(?i)' + [regex]::Escape($kw))) { return $true }");
            sb.AppendLine("        }");
            sb.AppendLine("        return $false");
            sb.AppendLine("    } | ForEach-Object {");
            sb.AppendLine("        try {");
            sb.AppendLine("            Write-Host ('  Finalizando servico residual: ' + $_.DisplayName)");
            sb.AppendLine("            Stop-Service -Name $_.Name -Force -ErrorAction SilentlyContinue -WarningAction SilentlyContinue");
            sb.AppendLine("            Set-Service -Name $_.Name -StartupType Disabled -ErrorAction SilentlyContinue -WarningAction SilentlyContinue");
            sb.AppendLine("        } catch {}");
            sb.AppendLine("    }");
            sb.AppendLine("} catch {}");
            sb.AppendLine("try {");
            sb.AppendLine("    Write-Host 'Verificando e reativando Microsoft Defender...'");
            sb.AppendLine("    Set-MpPreference -DisableRealtimeMonitoring $false -ErrorAction SilentlyContinue");
            sb.AppendLine("    Set-MpPreference -DisableIntrusionPreventionSystem $false -ErrorAction SilentlyContinue");
            sb.AppendLine("    Set-MpPreference -DisableIOAVProtection $false -ErrorAction SilentlyContinue");
            sb.AppendLine("    Set-MpPreference -DisableScriptScanning $false -ErrorAction SilentlyContinue");
            sb.AppendLine("    $defPol = 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows Defender'");
            sb.AppendLine("    if (Test-Path $defPol) {");
            sb.AppendLine("        Set-ItemProperty -Path $defPol -Name 'DisableAntiSpyware' -Value 0 -Type DWord -ErrorAction SilentlyContinue");
            sb.AppendLine("        Set-ItemProperty -Path $defPol -Name 'DisableRealtimeMonitoring' -Value 0 -Type DWord -ErrorAction SilentlyContinue");
            sb.AppendLine("    }");
            sb.AppendLine("    Start-Service WinDefend -ErrorAction SilentlyContinue");
            sb.AppendLine("    Write-Host '[ok] Microsoft Defender ativo e com protecao em tempo real habilitada.'");
            sb.AppendLine("} catch {}");
            sb.AppendLine("if ($uninstalledCount -gt 0 -or $detectedAVs.Count -gt 0) {");
            sb.AppendLine("    Write-Host ('[ok] Varredura concluida: Antivirus de terceiros tratados e removidos.')");
            sb.AppendLine("} else {");
            sb.AppendLine("    Write-Host '[ok] Varredura concluida: Nenhum antivirus de terceiros pendente no sistema.'");
            sb.AppendLine("}");

            int exitCode = await RunPowerShellScriptWithLoggingAsync(sb.ToString(), 300000);
            if (exitCode == 0)
                Log("[ok] Antivírus de terceiros desinstalados e Microsoft Defender verificado.", ConsoleColor.Green);
            else
                Log("Aviso: Rotina de desinstalação de antivírus finalizada com código " + exitCode, ConsoleColor.Yellow);

            await Task.Delay(300);
        }

        /// <summary>
        /// Executa a instalação completa em sequência: renomear PC, criar usuários,
        /// wallpaper e instalar todos os programas selecionados.
        /// </summary>
        /// <param name="options">Opções configuradas pelo usuário na interface</param>
        public async Task RunFullInstallationAsync(InstallationOptions options)
        {
            if (options == null)
                throw new ArgumentNullException("options");
            if (options.SelectedPrograms == null)
                throw new ArgumentException("A lista de programas selecionados não pode ser nula.", "options");

            // Calcula o total de etapas para a barra de progresso
            int totalSteps = 0;
            if (options.UninstallGames) totalSteps++;
            if (options.UninstallThirdPartyAntivirus) totalSteps++;
            if (options.RemoveOtherUserProfiles) totalSteps++;
            if (options.RenameComputer) totalSteps++;
            if (options.CreateAdmin) totalSteps++;
            if (options.CreateStandardUser) totalSteps++;
            if (options.SetWallpaper || options.SetWallpaperNortel || options.PreventWallpaperChange) totalSteps++;
            totalSteps += options.SelectedPrograms.Count;

            int currentStep = 0;
            bool cancelled = false;

            Log("=== INICIANDO INSTALAÇÃO ===", ConsoleColor.Cyan);
            Log(string.Format("Data: {0:dd/MM/yyyy HH:mm:ss}", DateTime.Now), ConsoleColor.Gray);

            string logPath = GetLogFilePath();
            if (!string.IsNullOrEmpty(logPath))
                Log("Arquivo de log: " + logPath, ConsoleColor.Gray);

            Log("", null);

            try
            {
                // Configura política de 'senha nunca expira' (ativa para todos se marcado, ou desmarca para todos se desmarcado)
                await EnsureGlobalPasswordNeverExpiresPolicyAsync(options.PasswordNeverExpiresGlobal);
                if (IsCancellationRequested())
                    cancelled = true;

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
                        string newAdminUser = options.CreateAdmin ? "Admin" : null;
                        await RemoveOtherUserProfilesAsync(newStdUser, newAdminUser);
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
                        await CreateAdminUserAsync(options.AdminPassword, options.PasswordNeverExpiresGlobal);
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
                        await CreateStandardUserAsync(options.StandardUserName, options.StandardUserPassword, options.StandardUserCannotChangePassword, options.PasswordNeverExpiresGlobal);
                    }
                }

                // Configura e ativa os Serviços de Localização do Windows para o Admin e o Usuário
                if (!cancelled && options.EnableLocationServices)
                {
                    await ConfigureLocationServicesAsync();
                }

                // 4. Wallpaper e Bloqueio de Alteração
                if (!cancelled && (options.SetWallpaper || options.SetWallpaperNortel || options.PreventWallpaperChange))
                {
                    if (_cancellationTokenSource != null && _cancellationTokenSource.Token.IsCancellationRequested)
                        cancelled = true;

                    if (!cancelled)
                    {
                        currentStep++;
                        ReportProgress(currentStep, totalSteps);
                        string client = options.SetWallpaperNortel ? "Nortel" : null;
                        await SetWallpaperAsync(client, options.PreventWallpaperChange);
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
                        if (IsCancellationRequested())
                        {
                            cancelled = true;
                            break;
                        }

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
                    // Garante que todos os programas instalados fiquem acessíveis para todos os perfis do Windows (atuais e novos)
                    await EnsureAllProgramsAccessibleForAllProfilesAsync(options);
                    if (IsCancellationRequested())
                        cancelled = true;

                    // Limpa atalhos de todas as áreas de trabalho no final de todas as instalações (se selecionado)
                    if (!cancelled && options.CleanDesktopShortcuts)
                    {
                        string stdUserName = options.CreateStandardUser ? options.StandardUserName : null;
                        CleanAllDesktops(stdUserName);
                    }

                    // Recria apenas os atalhos personalizados (conforme opções do usuário)
                    if (!cancelled)
                        await CreateDesktopShortcutsAsync(options);

                    if (!cancelled)
                        Log("=== INSTALAÇÃO CONCLUÍDA ===", ConsoleColor.Green);

                    if (!cancelled && options.SelectedPrograms.Count > 0)
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
                    if (!cancelled && handler != null)
                    {
                        handler();
                    }

                    if (cancelled)
                        Log("=== INSTALAÇÃO CANCELADA ===", ConsoleColor.Yellow);
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
            finally
            {
                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = null;
                }
            }
        }

        /// <summary>
        /// Garante que todos os programas instalados (especialmente suíte ArtCraft, ferramentas de IA como ChatGPT,
        /// Claude, DeepSeek, Gemini, etc.) fiquem acessíveis e funcionais para todos os perfis de usuário do Windows,
        /// incluindo o usuário atual, o administrador, o usuário padrão e quaisquer novos perfis criados no futuro.
        /// </summary>
        private async Task EnsureAllProgramsAccessibleForAllProfilesAsync(InstallationOptions options)
        {
            Log("Garantindo compatibilidade de todos os programas para todos os perfis de usuário (atuais e novos)...", ConsoleColor.Cyan);

            var sb = new StringBuilder();
            sb.AppendLine("# Script de Compatibilidade e Acesso Global de Programas para Todos os Perfis (EXABIT)");
            sb.AppendLine("$ErrorActionPreference = 'SilentlyContinue'");
            sb.AppendLine();
            sb.AppendLine("# 1. Concede permissao de Leitura e Execucao em %LOCALAPPDATA%\\Programs para o grupo Users (SID *S-1-5-32-545)");
            sb.AppendLine("$localPrograms = \"$env:LOCALAPPDATA\\Programs\"");
            sb.AppendLine("if (Test-Path $localPrograms) {");
            sb.AppendLine("    & icacls.exe $localPrograms /grant \"*S-1-5-32-545:(OI)(CI)RX\" /T /C /Q 2>$null | Out-Null");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("# 2. Configura diretorio compartilhado do sistema C:\\ProgramData\\Programs");
            sb.AppendLine("$commonProgramsDir = \"$env:ProgramData\\Programs\"");
            sb.AppendLine("if (-not (Test-Path $commonProgramsDir)) {");
            sb.AppendLine("    New-Item -ItemType Directory -Path $commonProgramsDir -Force | Out-Null");
            sb.AppendLine("}");
            sb.AppendLine("& icacls.exe $commonProgramsDir /grant \"*S-1-5-32-545:(OI)(CI)RX\" /T /C /Q 2>$null | Out-Null");
            sb.AppendLine();
            sb.AppendLine("# 3. Espelha pastas de aplicativos de LocalAppData\\Programs para ProgramData\\Programs");
            sb.AppendLine("if (Test-Path $localPrograms) {");
            sb.AppendLine("    Get-ChildItem -Path $localPrograms -Directory -ErrorAction SilentlyContinue | ForEach-Object {");
            sb.AppendLine("        $target = Join-Path $commonProgramsDir $_.Name");
            sb.AppendLine("        if (-not (Test-Path $target)) {");
            sb.AppendLine("            Copy-Item -Path $_.FullName -Destination $target -Recurse -Force -ErrorAction SilentlyContinue | Out-Null");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("# 4. Provisiona pacotes MSIX/Appx para todos os perfis existentes e futuros (ChatGPT, Affinity, etc.)");
            sb.AppendLine("$cacheDirs = @(\"$env:ProgramData\\EXABIT\\Downloads\", \"$env:ProgramData\\EXABIT\\Setup\", \"$PSScriptRoot\\..\\dist\\Programas\")");
            sb.AppendLine("foreach ($cDir in $cacheDirs) {");
            sb.AppendLine("    if (Test-Path $cDir) {");
            sb.AppendLine("        Get-ChildItem -Path $cDir -Filter \"*.msix\" -Recurse -ErrorAction SilentlyContinue | ForEach-Object {");
            sb.AppendLine("            Add-AppxProvisionedPackage -Online -PackagePath $_.FullName -SkipLicense -ErrorAction SilentlyContinue | Out-Null");
            sb.AppendLine("            Add-AppxPackage -Path $_.FullName -AllUsers -ErrorAction SilentlyContinue | Out-Null");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("# 5. Propagacao de atalhos do Menu Iniciar para CommonStartMenu e Perfil Default");
            sb.AppendLine("$wscript = New-Object -ComObject WScript.Shell");
            sb.AppendLine("$userStartMenu = [Environment]::GetFolderPath('Programs')");
            sb.AppendLine("$commonStartMenu = [Environment]::GetFolderPath('CommonPrograms')");
            sb.AppendLine("$defaultStartMenu = \"$env:SystemDrive\\Users\\Default\\AppData\\Roaming\\Microsoft\\Windows\\Start Menu\\Programs\"");
            sb.AppendLine("if (-not (Test-Path $defaultStartMenu)) {");
            sb.AppendLine("    New-Item -ItemType Directory -Path $defaultStartMenu -Force | Out-Null");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("$destStartMenus = @($commonStartMenu, $defaultStartMenu)");
            if (options != null && options.CreateStandardUser && !string.IsNullOrWhiteSpace(options.StandardUserName))
            {
                sb.AppendLine(string.Format("$destStartMenus += \"$env:SystemDrive\\Users\\{0}\\AppData\\Roaming\\Microsoft\\Windows\\Start Menu\\Programs\"", options.StandardUserName.Trim()));
            }
            if (options != null && options.CreateAdmin)
            {
                sb.AppendLine("$destStartMenus += \"$env:SystemDrive\\Users\\admin\\AppData\\Roaming\\Microsoft\\Windows\\Start Menu\\Programs\"");
            }
            sb.AppendLine();
            sb.AppendLine("if (Test-Path $userStartMenu) {");
            sb.AppendLine("    $ignoreNames = @('System Tools', 'Administrative Tools', 'Accessibility', 'Accessories', 'Windows PowerShell')");
            sb.AppendLine("    Get-ChildItem -Path $userStartMenu -Filter '*.lnk' -Recurse -ErrorAction SilentlyContinue | ForEach-Object {");
            sb.AppendLine("        $rel = $_.FullName.Substring($userStartMenu.Length).TrimStart('\\', '/')");
            sb.AppendLine("        $shouldSkip = $false");
            sb.AppendLine("        foreach ($ign in $ignoreNames) { if ($rel -like \"$ign*\") { $shouldSkip = $true; break } }");
            sb.AppendLine("        if (-not $shouldSkip) {");
            sb.AppendLine("            $srcSh = $wscript.CreateShortcut($_.FullName)");
            sb.AppendLine("            $tPath = $srcSh.TargetPath");
            sb.AppendLine("            $tArgs = $srcSh.Arguments");
            sb.AppendLine("            $tWork = $srcSh.WorkingDirectory");
            sb.AppendLine("            $tIcon = $srcSh.IconLocation");
            sb.AppendLine();
            sb.AppendLine("            if ($tPath -like \"$localPrograms\\*\") {");
            sb.AppendLine("                $subRel = $tPath.Substring($localPrograms.Length).TrimStart('\\', '/')");
            sb.AppendLine("                $alt = Join-Path $commonProgramsDir $subRel");
            sb.AppendLine("                if (Test-Path $alt) {");
            sb.AppendLine("                    $tPath = $alt");
            sb.AppendLine("                    $tWork = Split-Path $alt -Parent");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            foreach ($dstMenu in $destStartMenus) {");
            sb.AppendLine("                $dstPath = Join-Path $dstMenu $rel");
            sb.AppendLine("                $dstDir = Split-Path $dstPath -Parent");
            sb.AppendLine("                if (-not (Test-Path $dstDir)) { New-Item -ItemType Directory -Path $dstDir -Force | Out-Null }");
            sb.AppendLine("                $newSh = $wscript.CreateShortcut($dstPath)");
            sb.AppendLine("                $newSh.TargetPath = $tPath");
            sb.AppendLine("                $newSh.Arguments = $tArgs");
            sb.AppendLine("                $newSh.WorkingDirectory = $tWork");
            sb.AppendLine("                if ($tIcon) { $newSh.IconLocation = $tIcon }");
            sb.AppendLine("                $newSh.Save()");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("# 6. Registra executaveis em HKLM App Paths para busca global e comando Executar");
            sb.AppendLine("$appPaths = \"HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\App Paths\"");
            sb.AppendLine("$scanFolders = @($commonProgramsDir, \"$env:ProgramFiles\", \"${env:ProgramFiles(x86)}\")");
            sb.AppendLine("foreach ($sF in $scanFolders) {");
            sb.AppendLine("    if (Test-Path $sF) {");
            sb.AppendLine("        Get-ChildItem -Path $sF -Filter \"*.exe\" -Recurse -Depth 3 -ErrorAction SilentlyContinue | ForEach-Object {");
            sb.AppendLine("            $name = $_.Name");
            sb.AppendLine("            if ($name -notmatch 'unins|setup|update|helper|crash|elevate|patch|install') {");
            sb.AppendLine("                $keyPath = Join-Path $appPaths $name");
            sb.AppendLine("                if (-not (Test-Path $keyPath)) {");
            sb.AppendLine("                    New-Item -Path $keyPath -Force -ErrorAction SilentlyContinue | Out-Null");
            sb.AppendLine("                    Set-ItemProperty -Path $keyPath -Name '(default)' -Value $_.FullName -ErrorAction SilentlyContinue | Out-Null");
            sb.AppendLine("                    Set-ItemProperty -Path $keyPath -Name 'Path' -Value $_.DirectoryName -ErrorAction SilentlyContinue | Out-Null");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            try
            {
                await RunPowerShellScriptWithLoggingAsync(sb.ToString(), 300000);
                Log("[ok] Programas e atalhos configurados com sucesso para todos os perfis de usuário.", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                Log(string.Format("  Aviso ao propagar programas para todos os perfis: {0}", ex.Message), ConsoleColor.Yellow);
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
        /// Retorna o caminho do arquivo de log da sessão, criando o diretório
        /// em C:\ProgramData\EXABIT\Logs (ou na pasta temporária como fallback).
        /// </summary>
        private string GetLogFilePath()
        {
            if (!string.IsNullOrEmpty(_logFilePath))
                return _logFilePath;

            lock (_logFileLock)
            {
                if (!string.IsNullOrEmpty(_logFilePath))
                    return _logFilePath;

                string directory = null;

                try
                {
                    directory = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "EXABIT", "Logs");
                    Directory.CreateDirectory(directory);
                }
                catch
                {
                    try
                    {
                        directory = Path.Combine(Path.GetTempPath(), "EXABIT_Logs");
                        Directory.CreateDirectory(directory);
                    }
                    catch
                    {
                        return null;
                    }
                }

                _logFilePath = Path.Combine(directory,
                    string.Format("install-{0:yyyyMMdd-HHmmss}.log", DateTime.Now));
                return _logFilePath;
            }
        }

        /// <summary>
        /// Grava a mensagem no arquivo de log da sessão, com data e hora.
        /// Falhas de escrita são ignoradas para nunca interromper a instalação.
        /// </summary>
        private void WriteLogFile(string message)
        {
            try
            {
                string path = GetLogFilePath();
                if (string.IsNullOrEmpty(path))
                {
                    ReportLogFileFailure("não foi possível criar o diretório de logs");
                    return;
                }

                var buffer = new StringBuilder();

                lock (_logFileLock)
                {
                    if (!_logFileHeaderWritten)
                    {
                        _logFileHeaderWritten = true;

                        string version = "desconhecida";
                        try
                        {
                            version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                        }
                        catch { }

                        buffer.AppendLine("==================================================");
                        buffer.AppendLine(" Auto Instalador de Programas - EXABIT");
                        buffer.AppendLine(" Versao: " + version);
                        buffer.AppendLine(" Inicio: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        buffer.AppendLine(" Maquina: " + Environment.MachineName);
                        buffer.AppendLine(" Usuario: " + Environment.UserName);
                        buffer.AppendLine(" Sistema: " + Environment.OSVersion.VersionString);
                        buffer.AppendLine("==================================================");
                    }

                    buffer.AppendLine(string.Format("[{0:yyyy-MM-dd HH:mm:ss}] {1}",
                        DateTime.Now, message));
                    File.AppendAllText(path, buffer.ToString(), Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                ReportLogFileFailure(ex.Message);
            }
        }

        private void ReportLogFileFailure(string detail)
        {
            lock (_logFileLock)
            {
                if (_logFileWarningShown)
                    return;
                _logFileWarningShown = true;
            }

            var handler = OnLog;
            if (handler != null)
            {
                handler("⚠ O log em arquivo não está disponível: " + detail, ConsoleColor.Yellow);
            }
        }

        /// <summary>
        /// Dispara o evento de log para a interface tratar e grava no arquivo da sessão.
        /// </summary>
        private void Log(string message, ConsoleColor? color)
        {
            WriteLogFile(message);

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

        /// <summary>
        /// Se true, bloqueia a alteração do papel de parede pelo usuário padrão (colaborador),
        /// impedindo a substituição da tela de fundo corporativa da empresa.
        /// </summary>
        public bool PreventWallpaperChange { get; set; }

        public bool EnableLocationServices { get; set; }

        /// <summary>
        /// Se true, define a expiração de senha como NUNCA (ilimitada) na política global do Windows
        /// e ativa PasswordNeverExpires para todos os perfis locais existentes.
        /// </summary>
        public bool PasswordNeverExpiresGlobal { get; set; }

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
            PasswordNeverExpiresGlobal = true;
            SelectedPrograms = new List<ProgramInfo>();
        }
    }

    /// <summary>
    /// Representa o resultado detalhado de diagnóstico de latência de internet.
    /// </summary>
    public class PingDiagnosis
    {
        public bool Success { get; set; }
        public long LatencyMs { get; set; }
        public string Text { get; set; }
        public ConsoleColor Color { get; set; }
    }

    /// <summary>
    /// Diagnóstico detalhado de uma unidade de disco fixo.
    /// </summary>
    public class DiskDriveDiagnosis
    {
        public string DriveLetter { get; set; }
        public string VolumeLabel { get; set; }
        public string MediaType { get; set; }
        public double FreeGb { get; set; }
        public double TotalGb { get; set; }
        public double UsedGb { get; set; }
        public double PercentFree { get; set; }
        public double PercentUsed { get; set; }
        public string StatusText { get; set; }
        public ConsoleColor StatusColor { get; set; }

        public DiskDriveDiagnosis()
        {
            DriveLetter = "C:";
            VolumeLabel = "";
            MediaType = "SSD";
            StatusText = "";
            StatusColor = ConsoleColor.Green;
        }
    }

    /// <summary>
    /// Informações completas de diagnóstico de hardware, sistema, rede e segurança.
    /// </summary>
    public class FullSystemDiagnosticInfo
    {
        // Identificação & SO
        public string ComputerName { get; set; }
        public string DomainOrWorkgroup { get; set; }
        public string UserName { get; set; }
        public bool IsAdmin { get; set; }
        public string WindowsEdition { get; set; }
        public string WindowsBuild { get; set; }

        // Conexão & Rede
        public bool HasInternet { get; set; }
        public string ConnectionStatusText { get; set; }
        public string PingLatencyText { get; set; }
        public ConsoleColor ConnectionStatusColor { get; set; }
        public string LocalIp { get; set; }
        public List<string> AllLocalIps { get; set; }
        public string NetworkInterfacesSummary { get; set; }

        // Storage & Partition
        public List<DiskDriveDiagnosis> Drives { get; set; }

        // Recursos de Hardware
        public string CpuName { get; set; }
        public int CpuCores { get; set; }
        public int CpuThreads { get; set; }
        public string CpuSummary { get; set; }
        public double TotalRamGb { get; set; }
        public double UsedRamGb { get; set; }
        public double FreeRamGb { get; set; }
        public double RamUsagePercent { get; set; }
        public string RamType { get; set; }
        public string RamSummary { get; set; }

        // Dispositivo & Garantia
        public string Manufacturer { get; set; }
        public string Model { get; set; }
        public string SerialNumber { get; set; }

        // Segurança & Estado
        public bool DefenderActive { get; set; }
        public string DefenderStatusText { get; set; }
        public string ThirdPartyAntivirus { get; set; }
        public string AntivirusSummary { get; set; }
        public bool BitLockerActive { get; set; }
        public string BitLockerStatusText { get; set; }
        public bool PendingReboot { get; set; }
        public string PendingRebootText { get; set; }
        public string PowerSourceText { get; set; }

        public FullSystemDiagnosticInfo()
        {
            Drives = new List<DiskDriveDiagnosis>();
            AllLocalIps = new List<string>();
            ComputerName = Environment.MachineName;
            DomainOrWorkgroup = "WORKGROUP";
            UserName = Environment.UserName;
            WindowsEdition = "Windows";
            WindowsBuild = "";
            ConnectionStatusText = "Verificando...";
            ConnectionStatusColor = ConsoleColor.Yellow;
            LocalIp = "Indisponível";
            NetworkInterfacesSummary = "Detectando interfaces...";
            CpuName = "Identificando...";
            RamSummary = "Memória RAM";
            Manufacturer = "Identificando...";
            Model = "Identificando...";
            SerialNumber = "Identificando...";
            AntivirusSummary = "Identificando...";
            DefenderStatusText = "Identificando...";
            BitLockerStatusText = "Verificando...";
            PendingRebootText = "Não";
            PowerSourceText = "Na tomada";
        }
    }
}
