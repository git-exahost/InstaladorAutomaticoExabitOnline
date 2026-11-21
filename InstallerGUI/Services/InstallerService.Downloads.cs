using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
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
    public partial class InstallerService
    {
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

                    ThrowIfCancellationRequested();

                    var request = (HttpWebRequest)WebRequest.Create(url);
                    request.Method = "GET";
                    request.UserAgent = !string.IsNullOrEmpty(customUserAgent) ? customUserAgent : "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AutoInstaller/2.0";
                    request.Timeout = 30000;
                    request.ReadWriteTimeout = 60000;
                    request.AllowAutoRedirect = true;

                    var cancellationToken = _cancellationTokenSource == null
                        ? System.Threading.CancellationToken.None
                        : _cancellationTokenSource.Token;
                    using (cancellationToken.Register(new Action(request.Abort)))
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
                    if (IsCancellationRequested())
                    {
                        Log(string.Format("X Download de {0} cancelado pelo usuário.", displayName), ConsoleColor.Yellow);
                        return null;
                    }

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

        private static string NormalizeSha256(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool isHex = (c >= '0' && c <= '9') ||
                             (c >= 'a' && c <= 'f') ||
                             (c >= 'A' && c <= 'F');
                if (isHex)
                    sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        private static string ComputeSha256(string path)
        {
            try
            {
                using (var sha = SHA256.Create())
                using (var stream = File.OpenRead(path))
                {
                    byte[] hash = sha.ComputeHash(stream);
                    var sb = new StringBuilder(hash.Length * 2);
                    for (int i = 0; i < hash.Length; i++)
                        sb.Append(hash[i].ToString("x2"));
                    return sb.ToString();
                }
            }
            catch
            {
                return null;
            }
        }

        private async Task<string> EnsureProgramDownloadedAsync(ProgramInfo program)
        {
            if (program == null)
                return null;

            string path = await ResolveProgramInstallerAsync(program);

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return path;

            if (string.IsNullOrEmpty(program.Sha256))
                return path;

            string expected = NormalizeSha256(program.Sha256);
            string actual = ComputeSha256(path);

            if (string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            {
                Log(string.Format("[ok] Hash SHA-256 de {0} validado.", program.DisplayName), ConsoleColor.Green);
                return path;
            }

            Log(string.Format("X Hash SHA-256 de {0} nao confere. Esperado: {1} | Obtido: {2}",
                program.DisplayName, expected, actual ?? "erro ao calcular"), ConsoleColor.Red);

            try { File.Delete(path); } catch { }

            return null;
        }

        private async Task<string> ResolveProgramInstallerAsync(ProgramInfo program)
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

                string commonProg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Programs");
                if (File.Exists(Path.Combine(commonProg, @"Google\Gemini\Gemini.exe")) ||
                    File.Exists(Path.Combine(commonProg, @"Gemini\Gemini.exe")))
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

                string commonProg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Programs");
                if (File.Exists(Path.Combine(commonProg, @"ChatGPT\ChatGPT.exe")) ||
                    File.Exists(Path.Combine(commonProg, @"OpenAI\ChatGPT\ChatGPT.exe")))
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

                string commonProg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Programs");
                if (File.Exists(Path.Combine(commonProg, @"Claude\Claude.exe")) ||
                    File.Exists(Path.Combine(commonProg, @"AnthropicClaude\Claude.exe")))
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

                string commonProg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Programs");
                if (File.Exists(Path.Combine(commonProg, @"deepseek-harness\DeepSeek Harness.exe")) ||
                    File.Exists(Path.Combine(commonProg, @"deepseek-desktop\DeepSeek Desktop.exe")) ||
                    File.Exists(Path.Combine(commonProg, @"deepseek\DeepSeek.exe")))
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


    }
}
