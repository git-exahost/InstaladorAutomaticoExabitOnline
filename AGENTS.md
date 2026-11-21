# Orientações para Agentes de IA

Este arquivo contém diretrizes para agentes de IA (Claude, opencode, etc.)
que forem auxiliar na manutenção e evolução deste projeto.

## Visão Geral do Projeto

**Nome:** Auto Instalador de Programas
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
  - `OnInstallationCompleted()` → disparado quando a instalação é concluída com sucesso
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
CorFundo          = Color.FromArgb(30, 30, 30)    // fundo escuro (#1E1E1E)
CorPainel         = Color.FromArgb(40, 40, 40)    // painéis internos e abas (#282828)
CorDestaque       = Color.FromArgb(0, 180, 255)   // azul ciano (diagnóstico compacto, links)
CorDestaqueLaranja= Color.FromArgb(255, 140, 0)   // laranja corporativo (títulos de cards, abas ativas)
CorSucesso        = Color.FromArgb(0, 200, 80)    // verde (log OK, saudável)
CorTexto          = Color.White                    // texto principal
CorInputBg        = Color.FromArgb(60, 60, 60)    // campos de texto
```

## Sistema de Design Visual e Padronização de Interface (UI/UX)

O usuário deste projeto é **Designer Gráfico**. A interface gráfica deve seguir padrões rigorosos de design profissional, tipografia e espaçamento. Consulte também a skill `.agents/skills/exabit-ui-design-system/SKILL.md`.

### 1. Espaçamento Padronizado de Abas (Atalhos, Opções, Funcionalidades)
- **Padding do Painel da Aba**: `new Padding((int)(16 * scale), (int)(12 * scale), (int)(16 * scale), (int)(8 * scale))`
- **Altura de Linha de Checkbox**: Rigorosamente **`30px`** por item (`new RowStyle(SizeType.Absolute, (int)(30 * scale))`). Nunca use porcentagens desiguais (como 25% ou 28%) que afastam os itens aleatoriamente.
- **Linha de Dica / Legenda**: Altura fixa de **`24px`** (`new RowStyle(SizeType.Absolute, (int)(24 * scale))`).
- **Espaçador Inferior**: A última linha da tabela deve ser sempre `new RowStyle(SizeType.Percent, 100f)` para absorver o espaço residual sem deformar os itens superiores.

### 2. Formulários (Agrupamento Label + Input)
- O rótulo (*label*) deve ficar **imediatamente acima** do seu campo de texto (*TextBox*), nunca separado por grandes vazios.
- Padrão: Label de 18px (`Dock = Fill`, `Margin = Empty`), seguido diretamente do TextBox de 28px (`Dock = Fill`, `Margin = Empty`).
- Distância inter-campos: Linhas de 50px no `TableLayoutPanel` com respiro sutil.

### 3. Console de Log e Janelas de Processo (Terminal)
- O `RichTextBox` deve possuir **padding interno de 8px** em relação às bordas (usando `pnlTextWrapper` com `Padding(8, 8, 4, 8)`), garantindo que o texto nunca encoste nas paredes da janela.

### 4. Barras de Consumo / MiniUsageBar
- Altura de 8px. Deve estar posicionada logo abaixo do texto descritivo (distância de 4px a 6px), nunca no rodapé de um painel esticado.

### 5. Atualizações Periódicas e Timers (5 Segundos)
- O espaço em disco (Disco C:) e a latência de rede (Ping / Conexão) na tela principal e no diagnóstico completo são atualizados dinamicamente a cada **5 segundos** (`_metricsRefreshTimer.Interval = 5000`), utilizando APIs leves de alta performance (`DriveInfo.GetDrives()`, `GlobalMemoryStatusEx` e `MeasureInternetPingAsync` assíncrono em background), sem chamadas WMI lentas na thread de UI.

### 6. Restrição de Janela (FixedSingle, Sem Maximizar, Otimizada para 720p)
- **Bloqueio de Redimensionamento**: `FormBorderStyle = FormBorderStyle.FixedSingle` (borda fixa, cursor nunca vira seta de redimensionamento).
- **Sem Botão Maximizar**: `MaximizeBox = false` e `MinimizeBox = true` (apenas botões Minimizar e Fechar na barra de título).
- **Dimensões Travadas (Compatível com 720p 1280x720)**: Dimensão ideal calculada em `1200 x 650px` (com clamping a `workingArea - 16px` para garantir folga com barra de tarefas do Windows de 48px), travada com `MinimumSize = MaximumSize = Size = new Size(formWidth, formHeight)`.

### 7. Rolagem em Cards de Diagnóstico e Legendas Informativas
- **Prevenção de Truncamento Vertical**: Em cards cujo texto pode variar de tamanho (ex: compilação do Windows, antivírus de terceiros como Kaspersky, status de reinicialização pendente), utilizar container com viewport e barra de rolagem `DarkScrollBar` personalizada (cor laranja, evento `MouseWheel`), garantindo que nenhum texto seja cortado pela base.
- **Largura da Coluna de Rótulos (Coluna 0)**: Ajustada entre 95px e 102px conforme o tamanho do maior rótulo (`Computador:`, `Número de Série:`, `Memória RAM:`), garantindo que termos de valores longos (como RAM com uso e porcentagem) tenham espaço suficiente na Coluna 1 sem quebrar linhas ou cortar com reticências.
- **Legendas de Orientações**: Linhas informativas e de advertência (ex: "Os campos acima só funcionam caso preenchidos...") devem seguir rigorosamente o padrão `Segoe UI`, 8.5pt, Itálico, `#A0A0A0`, com altura de 24px e espaçador flexível abaixo.

### 8. Customização de Checkboxes Corporativos (DarkCheckBox em Laranja)
- **Eliminação do Tema Padrão do Windows**: Checkboxes utilizam a classe customizada `DarkCheckBox : CheckBox` com desenho GDI+ próprio (`ControlStyles.UserPaint`).
- **Cor Corporativa Oficial**: Quando marcado (`Checked = true`), o preenchimento da caixa utiliza rigorosamente o laranja corporativo da EXABIT (`CorDestaqueLaranja` = `Color.FromArgb(255, 140, 0)` / `#FF8C00`), em perfeita sintonia visual com o `DarkScrollBar` e as abas ativas.
- **Geometria e Glifo**: Caixa com bordas nítidas sem blur (`SmoothingMode.None`), glifo de seleção vetorial em branco puro com cantos arredondados (`SmoothingMode.AntiAlias`), borda com iluminação laranja em hover/focus e cursor em formato de mão (`Cursors.Hand`).

## Regras Críticas de Operação

- **NUNCA fazer `git push` sem ordem do usuário**: Qualquer alteração, commit ou build deve permanecer estritamente local. O envio de atualizações para o repositório remoto (GitHub) **só pode ser feito quando o usuário mandar explicitamente**.
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
