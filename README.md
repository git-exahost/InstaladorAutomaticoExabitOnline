# Auto Instalador de Programas

![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.8-blue)
![C#](https://img.shields.io/badge/C%23-5.0-green)
![Plataforma](https://img.shields.io/badge/Plataforma-Windows-lightgrey)

## Sumário

- [Sobre o Projeto](#sobre-o-projeto)
- [Estrutura de Pastas](#estrutura-de-pastas)
- [Como Usar](#como-usar)
- [Como Compilar](#como-compilar)
- [Assinatura Digital](#assinatura-digital)
- [Verificação SHA-256](#verificação-sha-256)
- [Adicionar ou Remover Programas](#adicionar-ou-remover-programas)
- [Arquitetura do Código](#arquitetura-do-código)
- [Guia de Modificação](#guia-de-modificação)
- [Resolução de Problemas](#resolução-de-problemas)

---

## Sobre o Projeto

Este programa foi criado para automatizar a instalação de programas e a
configuração inicial de computadores com Windows. Ele é útil para quem
precisa preparar vários notebooks/máquinas com o mesmo conjunto de
aplicativos, sem precisar baixar instaladores manualmente.

A instalação é **100% online**: o próprio instalador baixa cada programa
direto do site oficial do fabricante (ou de repositórios internos da
EXABIT), executa em modo silencioso e guarda o cache em
`C:\ProgramData\EXABIT\Downloads`.

### Funcionalidades

- **Renomear o computador** com um nome personalizado
- **Criar usuários** (admin e/ou padrão) com políticas de senha
  (senha nunca expira / usuário não pode alterar a senha)
- **Aplicar papéis de parede** corporativos (EXABIT e Nortel) em todos os
  temas do Windows, com opção de impedir alteração
- **Instalação online e silenciosa** de dezenas de programas, com
  progresso de download e log em tempo real
- **Diagnóstico do sistema** em tempo real: CPU, RAM, discos, rede, ping,
  antivírus, BitLocker, reinicialização pendente e fonte/bateria
- **Log de sessão em arquivo** em `C:\ProgramData\EXABIT\Logs` (para suporte)
- **Verificação opcional de SHA-256** dos instaladores baixados
- **Limpeza do sistema**: desinstalar jogos, remover antivírus de
  terceiros, apagar perfis antigos e limpar atalhos do Desktop público
- **Atalhos corporativos** (Suporte EXABIT, Exahost, Webmail Nortel, etc.)
- **Reiniciar** o computador ao concluir (opcional)

---

## Estrutura de Pastas

```
Instalador Automatico Exabit Online/
├── InstallerGUI/                        <<< CÓDIGO FONTE (C# WinForms)
│   ├── Forms/
│   │   └── MainForm.cs                  # Janela principal e interface
│   ├── Models/
│   │   └── ProgramInfo.cs               # Modelo de dados de cada programa
│   ├── Services/
│   │   └── InstallerService.cs          # Lógica de instalação e diagnóstico
│   ├── Properties/
│   │   └── AssemblyInfo.cs              # Metadados da versão
│   ├── Image/
│   │   └── logo_exabit.png              # Logo embutido no executável
│   ├── Sound/
│   │   └── R2D2.mp3                     # Som de conclusão
│   ├── Scripts/
│   │   └── apply_wallpapers.ps1         # Script de papéis de parede
│   ├── app.config                       # Configuração de DPI
│   ├── app.manifest                     # Requer Administrador
│   ├── sign.ps1                         # Assinatura Authenticode (opcional)
│   ├── InstallerGUI.csproj              # Projeto MSBuild
│   ├── build.bat                        # Script oficial de compilação
│   ├── Program.cs                       # Ponto de entrada
│   └── install.ico                      # Ícone do executável
│
├── dist/                                <<< ARQUIVOS DE DISTRIBUIÇÃO
│   ├── Instalador Automatico Exabit.exe # Executável compilado
│   ├── Instalador Automatico Exabit.exe.config
│   ├── Wallpapers/                      # Papéis de parede por cliente
│   └── build_info.txt                   # Data da última compilação
│
├── archive/                             # Scripts batch antigos (referência)
├── README.md                            # Este arquivo
├── CHANGELOG.md                         # Registro de versões
└── AGENTS.md                            # Diretrizes para agentes de IA
```

### O que cada arquivo faz

| Arquivo | Função |
|---------|--------|
| `Program.cs` | Ponto de entrada. Verifica privilégios e eleva para Administrador. |
| `Forms/MainForm.cs` | Interface gráfica (abas, checkboxes, cards, log). |
| `Models/ProgramInfo.cs` | Define o que é um "programa" (nome, arquivo, argumentos, flags). |
| `Services/InstallerService.cs` | Executa cada etapa (contas, wallpaper, downloads, instalação, limpeza). |
| `Scripts/apply_wallpapers.ps1` | Aplica os wallpapers em todos os temas e perfis. |
| `sign.ps1` | Assina o executável com um certificado corporativo real. |
| `build.bat` | Compila o projeto usando o `csc.exe` do próprio Windows. |

---

## Como Usar

### Pré-requisitos

- Windows 10/11 (64 bits recomendado)
- .NET Framework 4.8 (já vem instalado no Windows 10/11)
- **Conexão com a internet** (a instalação dos programas é online)
- Executar **como Administrador** (o executável já solicita elevação)

### Execução

1. Copie a pasta `dist/` inteira para um pendrive
2. No computador alvo, execute `dist\Instalador Automatico Exabit.exe`
3. Na tela que abrir, percorra as abas:
   - **Configurações**: nome do computador, senha do admin, usuário padrão
   - **Atalhos**: atalhos corporativos a criar no Desktop
   - **Opções**: localização, política de senha, wallpapers
   - **Funcionalidades**: limpeza de jogos/antivírus/perfis e lista de programas
4. Selecione os programas desejados
5. Clique em **INICIAR INSTALAÇÃO**
6. Acompanhe o progresso e o log na tela
7. Ao final, escolha se deseja reiniciar o computador

> NOTA: A instalação é silenciosa (automática). Os programas são baixados
> e instalados sem abrir janelas de confirmação.

---

## Como Compilar

### Método oficial: `build.bat`

Dê um duplo-clique em `InstallerGUI\build.bat` ou execute no terminal:

```cmd
cd InstallerGUI
build.bat
```

O script vai:

1. Localizar o compilador C# do .NET Framework (`csc.exe`)
2. Compilar todos os arquivos `.cs`
3. Embutir logo, som e o script de wallpaper como recursos
4. Gerar `dist\Instalador Automatico Exabit.exe`
5. Copiar o `app.config` para `dist\`
6. Executar `sign.ps1` (assinatura opcional — veja abaixo)

> O `build.bat` continua sendo o **método oficial** de build (não exige
> Visual Studio). O `InstallerGUI.csproj` está alinhado a ele e pode ser
> usado para editar/compilar no Visual Studio, gerando o mesmo executável
> em `bin\Release\`.

### Compilação manual

```cmd
cd InstallerGUI

"%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" ^
    /target:winexe ^
    /out:"..\dist\Instalador Automatico Exabit.exe" ^
    /win32manifest:app.manifest ^
    /platform:anycpu ^
    /highentropyva+ ^
    /reference:System.dll ^
    /reference:System.Core.dll ^
    /reference:System.Drawing.dll ^
    /reference:System.Windows.Forms.dll ^
    /reference:Microsoft.CSharp.dll ^
    /reference:System.ServiceProcess.dll ^
    /reference:System.Management.dll ^
    /reference:System.IO.Compression.dll ^
    /reference:System.IO.Compression.FileSystem.dll ^
    /resource:Image\logo_exabit.png,InstallerGUI.logo_exabit.png ^
    /resource:Sound\R2D2.mp3,InstallerGUI.R2D2.mp3 ^
    /resource:Scripts\apply_wallpapers.ps1,InstallerGUI.apply_wallpapers.ps1 ^
    /win32icon:install.ico ^
    /optimize+ ^
    /nologo ^
    Program.cs ^
    Forms\MainForm.cs ^
    Models\ProgramInfo.cs ^
    Services\InstallerService.cs ^
    Properties\AssemblyInfo.cs
```

---

## Assinatura Digital

O executável pode ser assinado com **Authenticode** para reduzir avisos do
SmartScreen/antivírus. A assinatura usa **somente um certificado
corporativo real (OV/EV)**:

- Via arquivo PFX:

```cmd
set EXABIT_SIGN_PFX=C:\certs\exabit-codesign.pfx
set EXABIT_SIGN_PFX_PASSWORD=senha-do-pfx
build.bat
```

- Ou com um certificado já instalado no repositório
  `Cert:\CurrentUser\My` / `Cert:\LocalMachine\My` que tenha chave privada
  e seja emitido por uma CA (`Subject` diferente de `Issuer`).

Se nenhum certificado real estiver configurado, o `sign.ps1` apenas avisa
e a compilação continua normalmente (sem assinatura).

> **Importante:** o projeto não cria mais certificados autoassinados nem
> instala certificados em "Autoridades Raiz Confiáveis". Isso evita que
> qualquer binário forjado com o mesmo nome seja aceito como confiável.
> Para uma CA interna, distribua o certificado da CA aos clientes via GPO.

---

## Verificação SHA-256

Cada `ProgramInfo` possui o campo opcional `Sha256`. Quando preenchido, o
instalador baixado (ou reaproveitado do cache) é validado antes de ser
executado:

- hash correto → a instalação prossegue e o log registra
  `[ok] Hash SHA-256 de <programa> validado.`
- hash divergente → o arquivo é apagado do cache e a instalação do
  programa é abortada com mensagem `X`.

```csharp
new ProgramInfo("meuprograma", "Meu Programa", "setup.exe", "/S", "", "Geral")
    { Sha256 = "0123...abcdef" },
```

> Deixe `Sha256` vazio para programas cujo download usa URL "latest"
> (Chrome, Firefox, VSCode, AnyDesk etc.), pois o conteúdo muda a cada
> release. Prefira preencher o hash quando a URL é versionada e o
> fabricante publica o checksum oficial.

---

## Adicionar ou Remover Programas

O catálogo de programas fica em `GetDefaultProgramList()` no
`Forms/MainForm.cs`, e a lógica de download/instalação fica no
`Services/InstallerService.cs`.

### Adicionar um novo programa

1. **Cadastre o programa** em `GetDefaultProgramList()` (`MainForm.cs`):

```csharp
new ProgramInfo(
    "meuprograma",              // ID único (usado na lógica)
    "Meu Programa",             // Nome exibido na interface
    "meuprograma_setup.exe",    // Nome do arquivo no cache
    "/S",                       // Argumentos de instalação silenciosa
    "",                         // Subpasta opcional
    "Geral")                    // Categoria
    { RequiresInternet = true, DefaultChecked = false },
```

2. **Implemente o download** no `InstallerService`:
   - Adicione um branch em `EnsureProgramDownloadedAsync()` **ou**
   - Crie um método `ResolveMeuProgramaInstallerAsync(ProgramInfo)` que
     chame `DownloadFileOnlineAsync(url, arquivo, nome, tamanhoMinimo)`.
   - Use sempre `https://` e a URL oficial do fabricante.

3. **Ajuste a instalação** em `InstallProgramAsync()` se o programa
   precisar de tratamento especial (MSI, MSIX, `winget`, `UseShellExecute`,
   fechar processos antes, etc.). Para instaladores comuns, o fluxo padrão
   já usa `Arguments`.

4. **Opcional — pular se já instalado**: crie um `IsMeuProgramaInstalled()`
   e retorne `true` no início do branch correspondente.

5. **Recompile** com `build.bat`.

### Remover um programa

1. Remova o item em `GetDefaultProgramList()`.
2. Opcional: remova o branch em `EnsureProgramDownloadedAsync()` e o
   tratamento em `InstallProgramAsync()`.
3. Recompile.

### Flags disponíveis em `ProgramInfo`

| Propriedade | Efeito |
|-------------|---------|
| `RequiresInternet` | Indica que o programa precisa de download online |
| `DefaultChecked` | Se aparece marcado por padrão |
| `TimeoutMs` | Tempo máximo de espera (0 = padrão global) |
| `FireAndForget` | Inicia o instalador sem aguardar o término |
| `UseShellExecute` | Usa ShellExecute (necessário em alguns instaladores) |
| `ProcessWorkingDir` | Diretório de trabalho do processo |
| `Category` | Agrupa o programa na interface |

---

## Arquitetura do Código

### Separação em Camadas

```
┌────────────────────────────────────────────────┐
│  Forms/MainForm.cs          (CAMADA DE VISÃO)   │
│  - Interface gráfica                            │
│  - Abas, checkboxes, botões, log                │
│  - Só se comunica com o InstallerService        │
├────────────────────────────────────────────────┤
│  Services/InstallerService.cs (CAMADA DE LÓGICA)│
│  - Executa processos (netsh, powershell, etc.)  │
│  - Downloads online e diagnóstico do sistema    │
│  - Dispara eventos de progresso e log           │
│  - Não referencia System.Windows.Forms          │
├────────────────────────────────────────────────┤
│  Models/ProgramInfo.cs       (CAMADA DE DADOS)  │
│  - Apenas dados (nome, arquivo, argumentos)     │
└────────────────────────────────────────────────┘
```

### Fluxo de Execução

```
Usuário clica "INICIAR INSTALAÇÃO"
        │
        ▼
MainForm.BtnInstall_Click()
    ├── Monta InstallationOptions com as escolhas do usuário
    ├── Chama InstallerService.RunFullInstallationAsync(options)
    │       │
    │       ▼
    │   Fase 0: Limpeza (jogos, antivírus, perfis antigos)
    │   Fase 1: Renomear computador
    │   Fase 2: Criar admin e usuário padrão
    │   Fase 3: Aplicar wallpapers
    │   Fase 4: Baixar e instalar cada programa selecionado
    │   Fase 5: Atalhos corporativos e limpeza do Desktop
    │       │
    │       ▼ (eventos)
    │   OnProgress          → barra de progresso
    │   OnDownloadProgress  → barra de download
    │   OnLog               → console de log
    │   OnInstallationCompleted → tela de conclusão
    │
    ▼
Pergunta se deseja reiniciar
```

### Eventos

O serviço se comunica com a interface exclusivamente por eventos:

```csharp
public event Action<string, ConsoleColor?> OnLog;
public event Action<int, int> OnProgress;
public event Action<string, int, long, long> OnDownloadProgress;
public event Action OnInstallationCompleted;
```

Isso permite que a lógica funcione **sem conhecer a interface**.

---

## Guia de Modificação

### Cenário 1: Alterar a política de senha

A senha do admin **não é mais fixa no código**: ela vem do campo
"Senha do Admin" na aba **Configurações**. A política "senha nunca expira"
fica na aba **Opções** (`_chkPasswordNeverExpiresGlobal`) e é aplicada por
`EnsureGlobalPasswordNeverExpiresPolicyAsync()` e
`ConfigureUserPasswordPolicyAsync()`.

### Cenário 2: Trocar os papéis de parede

Os wallpapers ficam em `dist\Wallpapers\` (e `dist\Wallpapers\Nortel\`).
O script `Scripts\apply_wallpapers.ps1` aplica todos os arquivos em todos
os temas do Windows. Se existir uma cópia em `dist\Scripts\`, ela tem
precedência sobre a versão embutida no executável.

### Cenário 3: Adicionar um atalho corporativo

1. Adicione a propriedade em `InstallationOptions` (`InstallerService.cs`).
2. Adicione o checkbox na aba **Atalhos** (`MainForm.cs`).
3. Preencha a opção no `BtnInstall_Click()`.
4. Crie o atalho no método responsável em `InstallerService.cs`.

### Cenário 4: Mudar o tema de cores

No início do `MainForm.cs` há constantes de cor:

```csharp
private static readonly Color CorFundo           = Color.FromArgb(30, 30, 30);
private static readonly Color CorPainel          = Color.FromArgb(40, 40, 40);
private static readonly Color CorDestaque        = Color.FromArgb(0, 180, 255);
private static readonly Color CorDestaqueLaranja = Color.FromArgb(255, 140, 0);
private static readonly Color CorSucesso         = Color.FromArgb(0, 200, 80);
```

A cor oficial da marca é o **laranja `#FF8C00`**, usada em abas ativas,
checkboxes, barras de progresso e scrollbars.

---

## Resolução de Problemas

### "Este programa precisa ser executado como Administrador"

Execute o `.exe` com botão direito > "Executar como administrador". O
manifesto já pede elevação automaticamente; se o UAC for cancelado, o
programa encerra.

### Onde encontro o log da instalação?

Em `C:\ProgramData\EXABIT\Logs\install-YYYYMMDD-HHMMSS.log` (fallback em
`%TEMP%\EXABIT_Logs`). O caminho também aparece no início da instalação,
no console de log.

### "Não foi possível baixar o programa X"

- Verifique a conexão com a internet e possíveis bloqueios de proxy/firewall
- Confirme acesso aos domínios oficiais dos fabricantes
- Apague o cache e tente novamente:
  `C:\ProgramData\EXABIT\Downloads`

### "Assinatura desconhecida" / aviso do SmartScreen

O executável só é reconhecido como confiável se assinado com um
certificado OV/EV real. Veja a seção [Assinatura Digital](#assinatura-digital).
Não instale certificados autoassinados na raiz do sistema.

### "O programa X não instalou"

- Verifique o log na tela (mensagens com prefixo `X`)
- Confirme se os argumentos de instalação silenciosa estão corretos
- Alguns instaladores exigem reinicialização entre instalações
- Programas via `winget`/MSIX podem depender da versão do Windows

### Erro ao compilar

- Certifique-se de que os arquivos `.cs` estão nas pastas corretas
- O compilador `csc.exe` deve existir em
  `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\`
- Se estiver em um sistema 32 bits, use o caminho `Framework\` (sem `64`)

---

## Tecnologias Utilizadas

- **Linguagem:** C# 5.0
- **Framework:** .NET Framework 4.8
- **Interface:** Windows Forms (controles GDI+ customizados)
- **Compilador:** `csc.exe` (embutido no Windows)
- **Ícone:** `install.ico`

---

## Licença

Este projeto é de uso interno. Sinta-se à vontade para modificar e
adaptar conforme sua necessidade.
