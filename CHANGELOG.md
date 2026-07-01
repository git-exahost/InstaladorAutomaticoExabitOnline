# Changelog

Todas as alterações significativas neste projeto serão documentadas aqui.

O formato é baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/),
e este projeto adere ao [Versionamento Semântico](https://semver.org/lang/pt-BR/).

---

## [2.0.0.3] - 2026-06-30

### Corrigido
- Criação do usuário padrão agora verifica individualmente se os campos "Nome" e "Sobrenome" foram preenchidos com valores diferentes dos placeholders padrão; se ambos estiverem em branco ou com valores padrão, o usuário não é criado e a senha é ignorada

---

## [2.0.0.2] - 2026-06-30

### Alterado
- Painel de configuração expandido de 165px para 225px para acomodar 6 linhas sem cortar o conteúdo
- Label "Senha:" adicionada na coluna 0 da linha 3 (campo de senha do usuário padrão), espelhando o layout dos demais campos
- Checkbox "Usuário padrão não poderá alterar a senha" movido para linha própria (linha 4) com `SetColumnSpan(4)`, separado do campo de senha
- Checkbox "Aplicar papel de parede personalizado" movido para linha 5 com `SetColumnSpan(4)` (válido para 4 colunas)
- `RowCount` do `TableLayoutPanel` de configurações alterado de 5 para 6; loop de `RowStyles` atualizado para 6 iterações

### Corrigido
- Checkbox "Aplicar papel de parede personalizado" não aparecia: `SetColumnSpan(5)` ultrapassava as 4 colunas do `TableLayoutPanel`; corrigido para `SetColumnSpan(4)`
- Altura do container (`leftSide`, linha 0) era insuficiente (165px) para 6 linhas de 32px + padding + header (~220px); aumentado para 225px

---

## [2.0.0.1] - 2026-06-30

### Adicionado
- Logo EXABIT (`logo_exabit.png`) embutida como recurso gerenciado no cabeçalho (lado direito, 300px) com `PictureBoxSizeMode.Zoom`, padding 50px lateral / 3px topo / 12px base, e `BackColor = Transparent`
- Opção "Usuário padrão não pode alterar senha" com checkbox; aplica `/passwordchg:no` no comando `net user` durante a criação e evita que o usuário troque a senha no Windows
- Propriedades `TimeoutMs`, `FireAndForget`, `ProcessWorkingDir`, `UseShellExecute` no modelo `ProgramInfo` para suportar instaladores com comportamento especial (ex: ODiS, RustDesk)
- Atalhos de desktop "Suporte Norte" (`https://suporte.exabit.com.br`) e "Webmail Nortel" (`http://webmail.nortel.srv.br`) nos desktops público, admin e usuário padrão
- Brave Browser à lista de programas (`BraveBrowserSetup.exe`, `/silent /install`)
- Campo de senha para o usuário admin (padrão `EXA@admin#202_`)
- Campo "Sobrenome" separado para o usuário padrão (compõe nome completo com o campo "Nome")
- Limpeza de atalhos `.lnk` do desktop público antes de criar os personalizados
- Classe `InstallationOptions` para agrupar todas as escolhas e configurações do usuário
- Bloco try/catch com tratamento específico para erro de recurso não encontrado ao carregar a logo

### Alterado
- Tema visual: cor de destaque alterada de azul (`0, 180, 255`) para laranja (`#f39200`, `243, 146, 0`) — aplicado em títulos, labels de grupo e destaques
- Cabeçalho redimensionado para 64px de altura com divisão em 2 colunas espelhando o layout de conteúdo (esquerda flexível + direita 300px fixos); título alinhado à esquerda, logo à direita
- Lista de programas reorganizada em ordem alfabética, com Office e Autodesk DWG TrueView posicionados ao final da lista
- Nomes de instaladores encurtados para versões sem espaços: `winrar.exe`, `7z.msi`, `Firefox_Setup.msi`, `jre-windows-x64.exe`, `googleearthprowin.exe`, `AcroRdrDC.exe`
- Silent install Radmin VPN: `/S` → `/VERYSILENT /NORESTART` (compatível com Inno Setup)
- Silent install DWG TrueView: argumentos corrigidos para `-q --noeula`, adicionado `UseShellExecute=true` e `ProcessWorkingDir="DWGTrueView"` (contorna erro `0xc0000043` do instalador ODiS)
- Silent install Office: migrado de `office\Setup64.exe` (sem argumentos, exibia UI) para ODT `setup.exe /configure configuration.xml` com `ProcessWorkingDir="Office"`
- Silent install RustDesk: `--silent-install` com `FireAndForget=true` e delay de 30s (processo não encerra após a instalação)
- Silent install Brave: `/silent /install` (padrão Chromium)
- `RunProcessAsync()` refatorado: agora aceita `workingDirectory`, `UseShellExecute`, `timeoutMs` por programa; leitura de stdout/stderr condicional (desativada quando `UseShellExecute = true`)
- `FlowLayoutPanel` substituído por `Panel` com posicionamento manual (`top += altura + 2`) na lista de programas (corrige item oculto)
- Timeout global de processo aumentado de 2 minutos para 10 minutos (600000 ms)
- Admin e usuário padrão passaram a ser criados automaticamente (removidos checkboxes); admin sempre criado, padrão criado apenas se o campo "Nome" for preenchido e diferente de "Sobrenome"
- `configuration.xml` do Office: `Channel="PerpetualVL2021"` → `Channel="Current"`, `SourcePath="."`, `AllowCdnFallback="False"`, `AUTOACTIVATE="0"` (licença aplicada posteriormente pelo usuário)
- Painel de configuração expandido de 5 para 6 linhas (165px → 195px) para acomodar o checkbox de bloqueio de senha
- `BuildHeader()` reescrito com `TableLayoutPanel` de 2 colunas para alinhar título (esquerda) e logo (direita) com o conteúdo abaixo
- Ícone do executável extraído automaticamente via `Icon.ExtractAssociatedIcon(Application.ExecutablePath)`

### Removido
- Notepad++ da lista de programas
- Checkbox "Criar usuário admin" (admin sempre criado)
- Checkbox "Criar usuário padrão" (criação automática condicionada ao preenchimento do nome)
- Tela de confirmação ("Deseja iniciar a instalação?") — instalação inicia imediatamente ao clicar no botão
- Linha separadora cinza (`Color.FromArgb(80, 80, 80)`) na base do cabeçalho
- Campo "Senha" do administrador substituído por campo editável com valor padrão

### Corrigido
- 7-Zip não aparecia na lista de programas: `FlowLayoutPanel` com `Dock = Fill` escondia o primeiro item; substituído por `Panel` com posicionamento manual e `top = 40`
- DWG TrueView erro `0xc0000043`: faltava `UseShellExecute = true` para o instalador ODiS .NET; leitura de stdout/stderr com processo redirecionado causava falha de inicialização
- Logo EXABIT não aparecia na interface: `Image.FromStream()` exige que o `Stream` permaneça aberto durante toda a vida da imagem; o bloco `using` anterior fechava o stream prematuramente
- Office Product ID inconsistente: XML usava `Channel="PerpetualVL2021"` (canal volume) com `Product ID="O365ProPlusRetail"` (ID retail) — combinação inválida; corrigido para `Channel="Current"` com `Product ID="ProPlusRetail"`
- Mensagens de erro "chcp 65001" inválido exibidas durante a compilação no PowerShell (cosmético, sem impacto no build)

---

## [2.0.0.0] - 2026-06-30

### Adicionado
- Projeto C# WinForms substituindo o antigo sistema baseado em batch + Bat_To_Exe_Converter
- Interface gráfica com tema escuro profissional (fundo `#1E1E1E`, painéis `#282828`, destaque azul `#00B4FF`)
- Painel lateral direito com lista de programas com scroll vertical (300px fixos) e checkboxes individuais
- Botões "Marcar Todos" e "Desmarcar Todos" para agilizar a seleção de programas
- Barra de progresso contínua na lateral direita, abaixo do botão de instalação
- Log colorido em tempo real com console virtual (`RichTextBox`):
  - Verde: operações concluídas com sucesso
  - Vermelho: erros críticos
  - Amarelo: avisos e códigos de saída não-zero
  - Ciano: informações gerais e cabeçalhos de etapa
  - Cinza: dados auxiliares (data, diretório)
- Eventos `OnProgress(int, int)` e `OnLog(string, ConsoleColor?)` para comunicação service → UI com thread safety via `Invoke`
- Modelo `ProgramInfo` com dados do instalador (id, nome, caminho, argumentos, diretório de trabalho)
- Documentação XML (`///`) em todos os métodos públicos e protected
- README.md com guia de uso, compilação e manutenção
- CHANGELOG.md para rastreamento de versões
- AGENTS.md com orientações para agentes de IA
- Tratamento de exceções com try/catch em todas as etapas da instalação
- Execução assíncrona com `async`/`await` (interface não trava durante as instalações)
- Auto-elevação para Administrador via `Process.Start` com `Verb = "runas"` ao detectar que não está elevado
- Verificação de privilégios com `WindowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator)`
- Arquivo `build.bat` para compilação com `csc.exe` do .NET Framework 4.8 (C# 5.0)
- Pasta `Properties/` com `AssemblyInfo.cs` para metadados do executável
- Script `wallpaper.cmd` para aplicar papel de parede personalizado no usuário atual
- Método `RenameComputerAsync()` com PowerShell (`Rename-Computer -NewName`)
- Métodos `CreateAdminUserAsync()` e `CreateStandardUserAsync()` com `net user` e WMIC
- Método `SetWallpaperAsync()` que executa `Scripts/wallpaper.cmd`

### Alterado
- Layout da janela: 1200×700, centralizada na tela, tamanho mínimo 1200×700, maximizável
- `BuildUI()` implementado com `TableLayoutPanel` aninhados para layout responsivo em duas colunas
- `BuildProgramsPanel()` simplificado: lista de checkboxes com scroll vertical, botões de seleção em massa
- Compilação via `csc.exe` do .NET Framework 4.8, sem dependências externas (NuGet, SDK, Visual Studio)
- Tamanho do executável reduzido de ~2MB (gerado pelo Bat_To_Exe_Converter) para ~49KB
- Fonte padrão: Segoe UI 9.5pts (antes: não definida no batch)
- Instalação de MSIs agora usa `msiexec /i` explicitamente

### Removido
- Pasta `src/Build/` com `Bat_To_Exe_Converter.exe`
- Scripts batch antigos movidos para `archive/` (exceto `wallpaper.cmd` que ainda é usado)
- Dependência do Bat_To_Exe_Converter para gerar o executável
- Interface de console (menu de opções numérico) substituída pela GUI

### Corrigido
- `AutoSize = true` nos GroupBoxes causava altura zero (layout quebrado ao redimensionar)
- Barra de rolagem horizontal removida da lista de programas
- Bordas pretas ao redimensionar a janela (layouts agora preenchem corretamente com `Dock = Fill`)
- Instalação sequencial de programas travava a interface (resolvido com async/await)

---

## [1.0.0.0] - 2025

### Adicionado
- Script `Install.bat` para instalação automatizada EXABIT com menu de seleção individual
- Script `build.bat` para instalação EXABIT com menu de 2 opções (completa ou parcial)
- Script `Install_Nortel.bat` para instalação NORTEL ENGENHARIA
- Scripts auxiliares:
  - `program_base.cmd`: instalação de programas base (7-Zip, Chrome, Firefox, Java, etc.)
  - `office.cmd`: instalação do Microsoft Office via `office\Setup64.exe`
  - `dwgtrueview.cmd`: instalação do Autodesk DWG TrueView
  - `radmin.cmd`: instalação do Radmin VPN
  - `wallpaper.cmd`: aplicação de papel de parede personalizado
- Geração de executável único via Bat_To_Exe_Converter
- Instalação silenciosa de 13 programas a partir de `dist/Programas/`
- Criação de usuários admin (com senha fixa) e padrão (com nome definido no script)
- Renomeação de computador via WMIC
- Aplicação de papel de parede personalizado com `wallpaper.cmd`

[2.0.0.2]: https://github.com/git-exahost/InstaladorAutomaticoExabit/releases/tag/v2.0.0.2
[2.0.0.1]: https://github.com/git-exahost/InstaladorAutomaticoExabit/releases/tag/v2.0.0.1
[2.0.0.0]: https://github.com/usuario/auto-install/releases/tag/v2.0.0.0
[1.0.0.0]: https://github.com/usuario/auto-install/releases/tag/v1.0.0.0
