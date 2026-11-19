# Auto Instalador de Programas

![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.8-blue)
![C#](https://img.shields.io/badge/C%23-5.0-green)
![Plataforma](https://img.shields.io/badge/Plataforma-Windows-lightgrey)

## Sumário

- [Sobre o Projeto](#sobre-o-projeto)
- [Estrutura de Pastas](#estrutura-de-pastas)
- [Como Usar](#como-usar)
- [Como Compilar](#como-compilar)
- [Adicionar ou Remover Programas](#adicionar-ou-remover-programas)
- [Arquitetura do Código](#arquitetura-do-código)
- [Guia de Modificação](#guia-de-modificação)
- [Resolução de Problemas](#resolução-de-problemas)

---

## Sobre o Projeto

Este programa foi criado para automatizar a instalação de programas e a
configuração inicial de computadores com Windows. Ele é útil para quem
precisa preparar vários notebooks/máquinas com o mesmo conjunto de
aplicativos.

### Funcionalidades

- **Renomear o computador** com um nome personalizado
- **Criar usuários** (admin e/ou padrão) automaticamente
- **Aplicar papel de parede** personalizado da empresa
- **Instalação 100% online de programas** em modo silencioso (sem necessidade de arquivos pré-baixados em pasta local)
- **Limpar atalhos** do Desktop público ao final
- **Reiniciar** o computador ao concluir (opcional)

---

## Estrutura de Pastas

```
Auto Install/
├── InstallerGUI/                  <<< CÓDIGO FONTE (C# WinForms)
│   ├── Forms/
│   │   └── MainForm.cs            # Janela principal da interface
│   ├── Models/
│   │   └── ProgramInfo.cs         # Modelo de dados de cada programa
│   ├── Services/
│   │   └── InstallerService.cs    # Lógica de instalação (coração)
│   ├── Properties/
│   │   └── AssemblyInfo.cs        # Metadados da versão
│   ├── install.ico                # Ícone do executável
│   ├── Program.cs                 # Ponto de entrada
│   ├── InstallerGUI.csproj        # Projeto MSBuild
│   └── build.bat                  # Script para compilar
│
├── dist/                          <<< ARQUIVOS DE DISTRIBUIÇÃO
│   ├── install.exe                # Executável compilado
│   ├── Programas/                 # Instaladores (Adobe, Chrome, etc.)
│   ├── Scripts/                   # Scripts auxiliares (wallpaper, etc.)
│   └── build_info.txt             # Data da última compilação
│
├── src/                           # (projeto antigo - removido)
├── archive/                       # Scripts batch antigos (referência)
└── README.md                      # Este arquivo
```

### O que cada arquivo faz

| Arquivo | Função |
|---------|--------|
| `Program.cs` | Ponto de entrada. Inicia o Windows Forms. |
| `Forms/MainForm.cs` | Interface gráfica (checkboxes, botões, log). |
| `Models/ProgramInfo.cs` | Define o que é um "programa" (nome, caminho, args). |
| `Services/InstallerService.cs` | Executa cada etapa da instalação (renomear, criar usuário, instalar). |
| `build.bat` | Compila o projeto usando o compilador do próprio Windows. |

---

## Como Usar

### Pré-requisitos

- Windows 7 ou superior (64 bits recomendado)
- .NET Framework 4.8 (já vem instalado no Windows 10/11)
- Executar **como Administrador** (necessário para criar usuários, renomear PC)

### Execução

1. Copie a pasta `dist/` inteira para um pendrive
2. No computador alvo, execute `dist\install.exe` como Administrador
3. Na tela que abrir:
   - Marque as opções desejadas (nome do PC, criar usuários, wallpaper)
   - Selecione os programas que quer instalar
   - Clique em **INICIAR INSTALAÇÃO**
4. Acompanhe o progresso pelo log na tela
5. Ao final, escolha se deseja reiniciar o computador

> NOTA: A instalação é silenciosa (automática). Os programas serão instalados
> sem abrir janelas de confirmação.

---

## Como Compilar

### Método 1: Via build.bat (recomendado)

Dê um duplo-clique em `InstallerGUI\build.bat` ou execute no terminal:

```cmd
cd InstallerGUI
build.bat
```

O script vai:
1. Localizar o compilador C# do .NET Framework
2. Compilar todos os arquivos .cs
3. Gerar `dist\install.exe`

### Método 2: Via linha de comando manual

```cmd
cd InstallerGUI

"%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" ^
    /target:winexe ^
    /out:bin\Release\install.exe ^
    /reference:System.dll ^
    /reference:System.Core.dll ^
    /reference:System.Drawing.dll ^
    /reference:System.Windows.Forms.dll ^
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

## Adicionar ou Remover Programas

### Adicionar um novo programa

1. **Copie o instalador** para a pasta `dist\Programas\`
2. **Abra o arquivo** `InstallerGUI\Forms\MainForm.cs`
3. **Localize o método** `GetDefaultProgramList()` (linha ~340)
4. **Adicione um novo item** no formato:

```csharp
new ProgramInfo(
    "identificador",     // ID único (ex: "vlc", "teamviewer")
    "Nome do Programa",  // Nome exibido na interface
    "instalador.exe",    // Nome do arquivo na pasta Programas
    "/S"                 // Argumentos para instalação silenciosa
)
```

> **Dica**: Para descobrir os argumentos de instalação silenciosa de um
> programa, pesquise por `"nome_do_programa" silent install` no Google.

5. **Recompile** o projeto (rode `build.bat`)

### Remover um programa

1. No mesmo método `GetDefaultProgramList()`, remova ou comente a linha
2. Recompile
3. Opcional: remova o instalador da pasta `dist\Programas\`

### Exemplo completo

```csharp
// Antes de adicionar, pesquise os argumentos silent do instalador
new ProgramInfo("teamviewer", "TeamViewer", "TeamViewer_Setup.exe", "/S"),
```

---

## Arquitetura do Código

### Separação em Camadas

```
┌────────────────────────────────────────────────┐
│  Forms/MainForm.cs          (CAMADA DE VISÃO)   │
│  - Interface gráfica                            │
│  - Checkboxes, botões, log                      │
│  - Só se comunica com o InstallerService        │
├────────────────────────────────────────────────┤
│  Services/InstallerService.cs (CAMADA DE LÓGICA)│
│  - Executa processos (netsh, powershell, etc.)  │
│  - Dispara eventos de progresso e log           │
│  - Não sabe nada sobre a interface              │
├────────────────────────────────────────────────┤
│  Models/ProgramInfo.cs       (CAMADA DE DADOS)  │
│  - Apenas dados (nome, caminho, argumentos)     │
│  - Sem lógica                                   │
└────────────────────────────────────────────────┘
```

### Fluxo de Execução

```
Usuário clica "INICIAR INSTALAÇÃO"
        │
        ▼
MainForm.BtnInstall_Click()
    ├── Cria InstallationOptions com as escolhas do usuário
    ├── Chama InstallerService.RunFullInstallationAsync()
    │       │
    │       ▼ (sequencial)
    │   Renomear Computador  ──── RunProcessAsync("powershell", ...)
    │   Criar Admin          ──── RunProcessAsync("net", ...)
    │   Criar Usuário Padrão ──── RunProcessAsync("net", ...)
    │   Aplicar Wallpaper    ──── RunProcessAsync("wallpaper.cmd", ...)
    │   Instalar Programa 1  ──── RunProcessAsync("chrome.exe", "/silent")
    │   Instalar Programa 2  ──── RunProcessAsync("firefox.msi", "/qn")
    │   ...                  ──── RunProcessAsync(...)
    │   Limpar Desktop
    │       │
    │       ▼ (eventos)
    │   OnProgress → MainForm.UpdateProgress()  (atualiza barra)
    │   OnLog      → MainForm.AppendLog()       (escreve no log)
    │
    ▼
Pergunta se deseja reiniciar
```

### Eventos

O serviço se comunica com a interface através de eventos:

```csharp
// No InstallerService:
public event Action<string, ConsoleColor> OnLog;
public event Action<int, int> OnProgress;

// No MainForm (conexão no construtor):
_service.OnLog += AppendLog;
_service.OnProgress += UpdateProgress;
```

Isso permite que a lógica de instalação funcione **sem conhecer a interface**.
Se um dia você quiser criar uma interface diferente (ex: web, console),
basta conectar os eventos de outra forma.

---

## Guia de Modificação

### Cenário 1: Mudar a senha do admin

No arquivo `Services\InstallerService.cs`, localize o método
`CreateAdminUserAsync()` e altere a linha:

```csharp
// Linha ~105: altere a senha entre aspas
await RunProcessAsync("net",
    "user admin \"EXA@admin#202_\" /add", 10000);
```

### Cenário 2: Mudar o nome do wallpaper

No arquivo `Services\InstallerService.cs`, localize `SetWallpaperAsync()`:

```csharp
string scriptPath = Path.Combine(_basePath, "Scripts", "wallpaper.cmd");
```

O script `wallpaper.cmd` é quem define qual imagem será usada. Edite
diretamente o arquivo `dist\Scripts\wallpaper.cmd`.

### Cenário 3: Adicionar uma nova empresa (ex: "NORTEL")

1. Crie um novo método em `InstallerService.cs` (ex: `CreateNortelUserAsync()`)
2. Adicione um checkbox em `MainForm.cs` no `BuildConfigPanel()`
3. No `BtnInstall_Click()`, inclua a chamada ao novo método
4. Recompile

### Cenário 4: Mudar o tema de cores

No início do `MainForm.cs`, há constantes de cor:

```csharp
private static readonly Color CorFundo = Color.FromArgb(30, 30, 30);
private static readonly Color CorPainel = Color.FromArgb(40, 40, 40);
private static readonly Color CorDestaque = Color.FromArgb(0, 180, 255);
private static readonly Color CorSucesso = Color.FromArgb(0, 200, 80);
```

Altere os valores RGB para personalizar as cores.

---

## Resolução de Problemas

### "O sistema não reconhece o comando"

Certifique-se de estar executando o `install.exe` como Administrador.
Clique com o botão direito > "Executar como administrador".

### "Falha ao renomear o computador"

- O nome do computador não pode conter espaços ou caracteres especiais
- Execute como Administrador
- No Windows 10/11, a renomeação via PowerShell é mais confiável

### "O programa X não instalou"

- Verifique se o instalador está em `dist\Programas\`
- Verifique se os argumentos de instalação silenciosa estão corretos
- Alguns instaladores podem exigir reinicialização entre instalações

### Erro ao compilar

- Certifique-se de que os arquivos .cs estão nas pastas corretas
- O compilador `csc.exe` deve existir em `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\`
- Se estiver em um sistema 32 bits, use o caminho `Framework\` (sem `64`)

---

## Tecnologias Utilizadas

- **Linguagem:** C# 5.0
- **Framework:** .NET Framework 4.8
- **Interface:** Windows Forms
- **Compilador:** csc.exe (embutido no Windows)
- **Ícone:** `install.ico`

---

## Licença

Este projeto é de uso interno. Sinta-se à vontade para modificar e
adaptar conforme sua necessidade.
