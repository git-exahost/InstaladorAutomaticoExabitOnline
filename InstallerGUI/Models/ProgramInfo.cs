namespace InstallerGUI.Models
{
    /// <summary>
    /// Representa um programa que pode ser instalado automaticamente.
    /// Cada instância contém as informações necessárias para localizar
    /// e executar o instalador com os parâmetros corretos.
    /// </summary>
    public class ProgramInfo
    {
        /// <summary>
        /// Identificador único do programa (ex: "winrar", "chrome").
        /// Usado internamente para referência.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Nome exibido na interface do usuário (ex: "WinRAR", "Google Chrome").
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Nome do arquivo instalador dentro da pasta Programas.
        /// Pode incluir subpastas (ex: "Office\\office\\Setup64.exe").
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Argumentos de linha de comando para instalação silenciosa.
        /// Exemplo: "/S" para WinRAR, "/qn /norestart" para MSI.
        /// </summary>
        public string Arguments { get; set; }

        /// <summary>
        /// Pasta relativa à raiz onde o instalador está localizado.
        /// Padrão é "Programas". Pode ser vazio se for na raiz.
        /// </summary>
        public string WorkingDirectory { get; set; }

        /// <summary>
        /// Tempo máximo em ms para aguardar o instalador finalizar.
        /// 0 = usa o padrão global (10 minutos).
        /// </summary>
        public int TimeoutMs { get; set; }

        /// <summary>
        /// Se true, inicia o instalador e não aguarda a finalização.
        /// Útil para programas que mantêm o processo em execução após instalar.
        /// </summary>
        public bool FireAndForget { get; set; }

        /// <summary>
        /// Diretório de trabalho do processo (working directory).
        /// Se vazio, usa o diretório padrão da aplicação.
        /// Útil para instaladores que precisam de arquivos na mesma pasta.
        /// </summary>
        public string ProcessWorkingDir { get; set; }

        /// <summary>
        /// Se true, usa UseShellExecute ao iniciar o processo.
        /// Necessário para alguns instaladores (ex: InstallShield, ODiS).
        /// </summary>
        public bool UseShellExecute { get; set; }

        /// <summary>
        /// Se true, exige conexão ativa com a internet para instalar.
        /// </summary>
        public bool RequiresInternet { get; set; }

        /// <summary>
        /// Define se o programa inicia marcado por padrão na interface.
        /// </summary>
        public bool DefaultChecked { get; set; }

        /// <summary>
        /// URL de download oficial do instalador na internet.
        /// Se vazia, a URL é resolvida dinamicamente pelo instalador.
        /// </summary>
        public string DownloadUrl { get; set; }

        /// <summary>
        /// Categoria ou grupo do programa (ex: "Geral", "ArtCraft", "Desenvolvimento").
        /// Utilizado pela interface para agrupar e exibir divisórias visuais.
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Cria um novo programa para instalação.
        /// </summary>
        /// <param name="id">Identificador único (ex: "winrar")</param>
        /// <param name="displayName">Nome para exibição na interface</param>
        /// <param name="fileName">Nome do arquivo instalador baixado ou local</param>
        /// <param name="arguments">Argumentos para instalação silenciosa</param>
        /// <param name="workingDir">Subpasta opcional (padrão: vazio)</param>
        /// <param name="category">Categoria opcional (padrão: "Geral")</param>
        public ProgramInfo(string id, string displayName, string fileName, string arguments, string workingDir = "", string category = "Geral")
        {
            Id = id;
            DisplayName = displayName;
            FileName = fileName;
            Arguments = arguments;
            WorkingDirectory = workingDir;
            Category = category;
            RequiresInternet = true;
            DefaultChecked = true;
        }

        /// <summary>
        /// Retorna o caminho completo do instalador com base na raiz da aplicação.
        /// </summary>
        /// <param name="basePath">Diretório raiz da aplicação</param>
        /// <returns>Caminho absoluto para o arquivo instalador</returns>
        public string GetFullPath(string basePath)
        {
            if (string.IsNullOrEmpty(WorkingDirectory))
                return System.IO.Path.Combine(basePath, FileName);
            return System.IO.Path.Combine(basePath, WorkingDirectory, FileName);
        }
    }
}
