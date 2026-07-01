# Orientações para Agentes de IA

Este arquivo contém diretrizes para agentes de IA (Claude, opencode, etc.)
que forem auxiliar na manutenção e evolução deste projeto.

## Visão Geral do Projeto

**Nome:** Instalador Automático de Programas
**Linguagem:** C# 5.0 (.NET Framework 4.8)
**Interface:** Windows Forms
**Propósito:** Automatizar a instalação de programas e configuração inicial
do Windows em notebooks/máquinas recém-formatadas.

## Estrutura do Projeto

```
Auto Install/
├── InstallerGUI/             → CÓDIGO FONTE
│   ├── Forms/
│   │   └── MainForm.cs       → Interface gráfica
│   ├── Models/
│   │   └── ProgramInfo.cs    → Modelo de dados dos programas
│   ├── Services/
│   │   └── InstallerService.cs → Lógica de instalação
│   ├── Program.cs            → Entry point
│   ├── build.bat             → Script de compilação
│   └── install.ico           → Ícone do executável
├── dist/                     → ARQUIVOS DE DISTRIBUIÇÃO
│   ├── install.exe           → Executável compilado
│   ├── Programas/            → Instaladores dos programas
│   └── Scripts/              → Scripts auxiliares
├── archive/                  → Scripts batch antigos (referência)
├── README.md                 → Documentação principal
├── CHANGELOG.md              → Registro de versões
└── AGENTS.md                 → Este arquivo
```

## Arquitetura (Camadas)

```
Forms/MainForm.cs         → VISÃO (UI) - Só se comunica com Services
Services/InstallerService → LÓGICA - Executa processos, dispara eventos
Models/ProgramInfo.cs     → DADOS - Estrutura dos programas
```

### Regras da separação

- `MainForm.cs` **não pode** conter lógica de instalação (Process.Start, etc.)
- `InstallerService` **não pode** conter referência a `System.Windows.Forms`
- A comunicação entre service e UI é feita **exclusivamente por eventos**:
  - `OnLog(string, ConsoleColor?)` → para mensagens no log
  - `OnProgress(int, int)` → para atualizar a barra de progresso
- Para acessar a UI com segurança de thread: `Invoke(new Action(...))`

## Convenções de Código

### C# 5.0 (limitações importantes)

O compilador disponível é o `csc.exe` do .NET Framework 4.8,
que suporta **C# 5.0**. Atenção:

| Funcionalidade | Suporte | Alternativa |
|---------------|---------|-------------|
| String interpolation `$"..."` | ❌ Não | `string.Format("...", ...)` |
| Pattern matching `x is Type y` | ❌ Não | `var y = x as Type; if (y != null)` |
| Null-conditional `x?.Y` | ❌ Não | `if (x != null) x.Y` |
| `async`/`await` | ✅ Sim | - |
| LINQ | ✅ Sim | - |
| `var` | ✅ Sim | - |
| Nullable types `int?` | ✅ Sim | - |
| Expression-bodied members | ❌ Não | Usar blocos `{ }` |

### Nomenclatura

- **Classes:** `PascalCase` (ex: `InstallerService`, `ProgramInfo`)
- **Métodos:** `PascalCase` (ex: `BuildUI`, `RunProcessAsync`)
- **Campos privados:** `_camelCase` (ex: `_basePath`, `_installing`)
- **Constantes:** `PascalCase` (ex: `CorFundo`, `CorDestaque`)
- **Eventos:** Prefixo `On` (ex: `OnLog`, `OnProgress`)
- **Métodos async:** Sufixo `Async` (ex: `RenameComputerAsync`)

### Estilo

- **Sem comentários** em código de produção (exceto XML docs públicos)
- XML documentation (`///`) em todos os métodos públicos e protected
- Usar `var` quando o tipo for óbvio pelo nome
- Evitar `#region` - usar comentários com bordas (`// ───────`)
- Cores do tema sempre via constantes estáticas no início da classe

## Como Compilar

```cmd
cd InstallerGUI
build.bat
```

Compila com `csc.exe` (vem no Windows via .NET Framework).
Não requer Visual Studio, NuGet, SDKs ou qualquer instalação adicional.
O .NET Framework 4.8 já está presente no Windows 10/11.

## Como Adicionar um Novo Programa

1. Colocar o instalador em `dist/Programas/`
2. Descobrir os argumentos de instalação silenciosa (pesquisar no Google)
3. Adicionar na lista `GetDefaultProgramList()` em `MainForm.cs`
4. Executar `build.bat`

Formato do item:
```csharp
new ProgramInfo("id", "Nome Exibido", "arquivo.exe", "/S")
```

## Instalação Silenciosa (Argumentos Comuns)

| Tipo | Formato |
|------|---------|
| MSI | `msiexec /i "arquivo.msi" /qn /norestart` |
| Inno Setup | `setup.exe /SILENT /VERYSILENT /SUPPRESSMSGBOXES` |
| NSIS | `setup.exe /S` |
| Wise | `setup.exe /s` |
| InstallShield | `setup.exe /s /v"/qn"` |

## Fluxo de Instalação

```
BtnInstall_Click()
  → Cria InstallationOptions (opções do usuário)
  → RunFullInstallationAsync(options)
      ├── RenameComputerAsync()
      ├── CreateAdminUserAsync()
      ├── CreateStandardUserAsync()
      ├── SetWallpaperAsync()
      ├── InstallProgramAsync() [para cada programa]
      └── CleanPublicDesktop()
  → Pergunta se quer reiniciar
  → Se sim: shutdown /r /t 10
```

## Cores do Tema (constantes em MainForm.cs)

```csharp
CorFundo    = Color.FromArgb(30, 30, 30)    // fundo escuro
CorPainel   = Color.FromArgb(40, 40, 40)    // painéis internos
CorDestaque = Color.FromArgb(0, 180, 255)   // azul (títulos, links)
CorSucesso  = Color.FromArgb(0, 200, 80)    // verde (log OK)
CorTexto    = Color.White                    // texto principal
CorInputBg  = Color.FromArgb(60, 60, 60)    // campos de texto
```

## Considerações ao Modificar

- **Testar compilação** sempre após alterações: `build.bat`
- **Testar execução** abrindo o .exe (verificar se a janela aparece)
- **Layout**: usar `Dock = DockStyle.Fill` para preenchimento automático
- **Evitar medidas fixas** de altura/largura (usar `Percent` no TableLayoutPanel)
- **Threading**: toda atualização de UI vinda de evento deve usar `Invoke`
- **Tratamento de erros**: toda etapa de instalação deve ter try/catch
- **Log**: mensagens em português com prefixo `[ok]`, `X`, `⚠`

## Para Agentes de IA

Ao receber tarefas sobre este projeto:
1. Leia o `README.md` para contexto completo
2. Verifique o `CHANGELOG.md` para entender o histórico
3. Consulte `AGENTS.md` (este arquivo) para regras e convenções
4. Para entender o layout: leia `Forms/MainForm.cs` → método `BuildUI()`
5. Para entender a lógica: leia `Services/InstallerService.cs`
6. Compile e teste após qualquer alteração
7. Se adicionar funcionalidade nova, atualize o `CHANGELOG.md`
8. Não adicione dependências externas (NuGet, DLLs)
9. C# 5.0 não suporta string interpolation, pattern matching, null-conditional
10. O projeto usa .NET Framework 4.8 (Windows Forms), não .NET Core/6+
