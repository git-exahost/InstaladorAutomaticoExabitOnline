using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows.Forms;
using InstallerGUI.Forms;

namespace InstallerGUI
{
    /// <summary>
    /// Ponto de entrada da aplicação.
    /// Verifica se está executando como Administrador e, se não,
    /// reinicia o processo com privilégios elevados.
    /// </summary>
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // Verifica se está rodando como Administrador
            if (!IsRunningAsAdmin())
            {
                // Reinicia o processo pedindo elevação
                var psi = new ProcessStartInfo
                {
                    FileName = Application.ExecutablePath,
                    UseShellExecute = true,
                    Verb = "runas", // Solicita elevação
                };

                try
                {
                    Process.Start(psi);
                }
                catch
                {
                    // Usuário cancelou ou falhou a elevação
                    MessageBox.Show(
                        "Este programa precisa ser executado como Administrador.\n\n" +
                        "Clique com o botão direito no arquivo e selecione\n" +
                        "\"Executar como administrador\".",
                        "Privilégios necessários",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }

                return; // Sai do processo atual
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        /// <summary>
        /// Verifica se o processo atual tem privilégios de administrador.
        /// </summary>
        private static bool IsRunningAsAdmin()
        {
            try
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }
}
