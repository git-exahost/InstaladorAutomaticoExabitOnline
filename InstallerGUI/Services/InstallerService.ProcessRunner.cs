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
            ThrowIfCancellationRequested();

            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = useShell,
                CreateNoWindow = !useShell,
                RedirectStandardOutput = false,
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

                if (!useShell)
                {
                    process.ErrorDataReceived += (sender, args) =>
                    {
                        if (!suppressStdErrLog && !string.IsNullOrWhiteSpace(args.Data))
                            Log(string.Format("  {0}", args.Data.Trim()), ConsoleColor.Yellow);
                    };
                }

                process.Start();

                if (!useShell)
                {
                    process.BeginErrorReadLine();
                }

                var cancellationToken = _cancellationTokenSource == null
                    ? System.Threading.CancellationToken.None
                    : _cancellationTokenSource.Token;
                using (cancellationToken.Register(delegate
                {
                    try
                    {
                        if (!process.HasExited)
                            process.Kill();
                    }
                    catch (Exception ex)
                    {
                        Log(string.Format("X Não foi possível encerrar {0} após o cancelamento: {1}",
                            fileName, ex.Message), ConsoleColor.Red);
                    }
                }))
                {
                    await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));

                    ThrowIfCancellationRequested();

                    if (!process.HasExited)
                    {
                        process.Kill();
                        Log(string.Format("  Processo {0} excedeu o tempo limite.", fileName),
                            ConsoleColor.Yellow);
                        return -1;
                    }
                }

                return process.ExitCode;
            }
        }

        /// <summary>
        /// Executa um processo redirecionando e capturando a saída em tempo real linha a linha para o log da aplicação.
        /// </summary>
        private async Task<int> RunProcessWithLoggingAsync(string fileName, string arguments,
            int timeoutMs = 600000, string workingDirectory = null)
        {
            ThrowIfCancellationRequested();

            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = workingDirectory ?? string.Empty,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            var tcs = new TaskCompletionSource<int>();

            using (var process = new Process { StartInfo = psi })
            {
                process.EnableRaisingEvents = true;
                process.Exited += (sender, args) =>
                {
                    tcs.TrySetResult(process.ExitCode);
                };

                process.OutputDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrWhiteSpace(args.Data))
                    {
                        string line = args.Data.Trim();
                        if (line.StartsWith("[ok]"))
                            Log(line, ConsoleColor.Green);
                        else if (line.StartsWith("X ") || line.StartsWith("[erro]"))
                            Log(line, ConsoleColor.Red);
                        else if (line.StartsWith("[Alerta]") || line.StartsWith("[!]"))
                            Log(line, ConsoleColor.Yellow);
                        else
                            Log(line, ConsoleColor.Yellow);
                    }
                };

                process.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrWhiteSpace(args.Data))
                    {
                        Log("  " + args.Data.Trim(), ConsoleColor.Yellow);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                var cancellationToken = _cancellationTokenSource == null
                    ? System.Threading.CancellationToken.None
                    : _cancellationTokenSource.Token;
                using (cancellationToken.Register(delegate
                {
                    try
                    {
                        if (!process.HasExited)
                            process.Kill();
                    }
                    catch (Exception ex)
                    {
                        Log(string.Format("X Não foi possível encerrar {0} após o cancelamento: {1}",
                            fileName, ex.Message), ConsoleColor.Red);
                    }
                }))
                {
                    await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));

                    ThrowIfCancellationRequested();

                    if (!process.HasExited)
                    {
                        try { process.Kill(); }
                        catch (Exception ex)
                        {
                            Log(string.Format("X Não foi possível encerrar {0} após o timeout: {1}",
                                fileName, ex.Message), ConsoleColor.Red);
                        }
                        Log(string.Format("  Processo {0} excedeu o tempo limite.", fileName), ConsoleColor.Yellow);
                        return -1;
                    }
                }

                return process.ExitCode;
            }
        }

        /// <summary>
        /// Salva um script PowerShell temporário codificado em UTF-8 e o executa com captura de saída em tempo real.
        /// </summary>
        private async Task<int> RunPowerShellScriptWithLoggingAsync(string scriptContent, int timeoutMs = 600000)
        {
            string tempScript = Path.Combine(Path.GetTempPath(), string.Format("exabit_ps_{0}.ps1", Guid.NewGuid().ToString("N")));
            try
            {
                File.WriteAllText(tempScript, scriptContent, Encoding.UTF8);
                string args = string.Format("-NoProfile -ExecutionPolicy Bypass -File \"{0}\"", tempScript);
                return await RunProcessWithLoggingAsync("powershell", args, timeoutMs);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempScript))
                        File.Delete(tempScript);
                }
                catch { }
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


    }
}
