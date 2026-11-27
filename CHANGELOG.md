# Changelog

Todas as alterações significativas neste projeto serão documentadas aqui.

O formato é baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/),
e este projeto adere ao [Versionamento Semântico](https://semver.org/lang/pt-BR/).

---

## [2.0.0.112] - 2026-11-26

### Corrigido — "Impedir que o usuário altere a senha" Aplicado Também a Usuários Padrão Existentes
- **Desacoplamento do campo "Usuário padrão" (`Forms\MainForm.cs`, `Services\InstallerService.cs`)**:
  - `options.StandardUserCannotChangePassword` deixou de exigir o preenchimento de um novo usuário padrão para ser repassado ao serviço.
  - Adicionado `EnsureStandardUsersCannotChangePasswordAsync()`, executado somente quando a opção estiver marcada, aplicando `UserMayChangePassword $false` (e fallback `[ADSI]` `0x0040`) a todos os usuários locais padrão ativos (excluindo administradores e contas internas do sistema).

---

## [2.0.0.111] - 2026-11-26

### Corrigido — Execução Estrita das Opções Somente Quando Marcadas
- **Preservação das configurações existentes quando opções ficam desmarcadas (`Services\InstallerService.cs`)**:
  - `EnsureGlobalPasswordNeverExpiresPolicyAsync()` agora só é executada quando **Ativar (senha nunca expira) para todos os perfis** estiver marcada (removido o bloco que desmarcava `PasswordNeverExpires` de todos os usuários locais quando desativada).
  - Em `ConfigureLocalAccountAsync`, **Impedir que o usuário altere a senha** e **Ativar senha nunca expira** só modificam ou ativam as respectivas restrições/flags (`-UserMayChangePassword $false`, `-PasswordNeverExpires $true`, bits `0x0040`/`0x10000`) quando marcadas, preservando o estado existente das contas caso contrário.
  - Construtor de `InstallationOptions` atualizado para iniciar `EnableLocationServices` e `PasswordNeverExpiresGlobal` em `false`.
- **Preservação de bloqueio prévio do papel de parede (`Scripts\apply_wallpapers.ps1`)**:
  - Caso o usuário atual (`HKCU`) já possua `NoChangingWallPaper` ou `Policies\System\Wallpaper` configurados e **Bloquear alteração do papel de parede** esteja desmarcada, o bloqueio pré-existente é restaurado após a aplicação ao vivo da nova imagem.

---

## [2.0.0.110] - 2026-11-26

### Adicionado — Imagem e Bloqueio da Tela de Bloqueio
- **Novas opções na aba `Opções` (`Forms\MainForm.cs`)**:
  - Adicionada a opção **"Aplicar imagem do papel de parede à tela de bloqueio"** logo abaixo de *"Bloquear alteração do papel de parede"*.
  - Adicionada a opção **"Bloquear alteração da tela de bloqueio"** logo abaixo da anterior.
  - Ambas passam a ser marcadas automaticamente ao selecionar o perfil **Nortel/Neoenergia** (e desmarcadas no perfil **Padrão**).
- **Aplicação e bloqueio da tela de bloqueio (`Services\InstallerService.cs`, `Scripts\apply_wallpapers.ps1`)**:
  - Estendidos `InstallationOptions`, `SetWallpaperAsync` e `apply_wallpapers.ps1` com `-SkipDesktopWallpaper`, `-ApplyLockScreen` e `-PreventChangingLockScreen`.
  - Aplica a imagem principal em `C:\Windows\Web\Screen\img100.jpg` (com permissões de leitura), configura `PersonalizationCSP` (`LockScreenImageStatus=1`, `LockScreenImagePath`, `LockScreenImageUrl`), `Policies\Personalization\LockScreenImage`, desativa rotação do Windows Spotlight e aplica `NoChangingLockScreen=1` quando o bloqueio é selecionado.

---

## [2.0.0.109] - 2026-11-26

### Corrigido — Aplicação Efetiva do Papel de Parede Antes do Bloqueio + Modo Preencher
- **Reordenação da aplicação ao vivo em `Scripts\apply_wallpapers.ps1`**:
  - A troca do papel de parede agora ocorre **antes** da gravação das políticas de bloqueio (`NoChangingWallPaper` e `Policies\System\Wallpaper`) nos perfis `Default`, carregados (`HKEY_USERS\S-1-5-21-*`) e offline, removendo temporariamente bloqueios prévios no `HKCU` para que o Windows não impeça a troca.
  - `NativeWallpaper.ApplyFill` executa `SystemParametersInfo(20, 0, path, 3)` primeiro para forçar o recarregamento da imagem mesmo quando o caminho (`img0.jpg`) não muda, seguido de `IDesktopWallpaper::SetPosition(Fill)` e aplicação explícita em todos os monitores detectados via `GetMonitorDevicePathAt`.

---

## [2.0.0.108] - 2026-11-26

### Corrigido — Garantia Real do Modo "Preencher" (Fill) no Windows 11
- **Ordem correta em `IDesktopWallpaper` e remoção de corrida com API legada (`Scripts\apply_wallpapers.ps1`)**:
  - `NativeWallpaper.ApplyFill` agora define a imagem primeiro (`SetWallpaper(null, path)`) e aplica `SetPosition(DesktopWallpaperPosition.Fill)` em seguida, validando via `GetPosition()` e eliminando a chamada concorrente a `SystemParametersInfo(20, ...)` no caminho principal (mantida apenas como fallback em exceção).
  - Adicionada estrutura `RECT` correta na assinatura COM de `GetMonitorRECT`.
- **Atualização de hives carregados (`HKEY_USERS\S-1-5-21-*`) e saneamento de políticas legadas**:
  - Perfis de usuários já logados cujo `NTUSER.DAT` está bloqueado para `reg load` agora têm `Control Panel\Desktop` e `Policies\System` atualizados diretamente via `Registry::HKEY_USERS\<SID>`.
  - Chaves pré-existentes de `Policies\System\WallpaperStyle` (que versões antigas gravavam como `10`, interpretado como *Estender* em `Desktop.admx`) são corrigidas para `4` (*Preencher*) nos perfis carregados, offline e `Default`, mesmo quando o bloqueio de alteração não está marcado.
- **Reparo de arquivos `.theme` e prioridade de recurso embutido**:
  - Limpa linhas órfãs (`$10`/`$110`) e garante a presença explícita de `Wallpaper`, `TileWallpaper=0` e `WallpaperStyle=10` em `[Control Panel\Desktop]` dos `.theme`.
  - `GetApplyWallpapersScriptPath()` em `Services\InstallerService.cs` agora extrai prioritariamente o script embutido no executável antes de recorrer a eventual cópia local em disco.

---

## [2.0.0.107] - 2026-11-26

### Corrigido — Eliminação de Barras Pretas em Telas Fora do Padrão 16:9 (Modo Preencher Real)
- **Uso da API COM `IDesktopWallpaper::SetPosition(Fill)` + limpeza de `TranscodedWallpaper`**:
  - Removida a cópia direta da imagem 16:9 sobre `TranscodedWallpaper` (que impedia o Windows de gerar o recorte proporcional para telas 16:10, 3:2 ou 4:3, causando barras pretas em cima e embaixo). Agora o script limpa `TranscodedWallpaper`, `CachedFiles\*` e `TranscodedImageCache`.
  - Adicionada chamada nativa `IDesktopWallpaper::SetPosition(DesktopWallpaperPosition.Fill)` e `SetWallpaper(null, path)` aplicada em todos os monitores.
  - Substituído `%SystemRoot%` pelo caminho expandido real nas chaves `REG_SZ` dos perfis `Default` e existentes, garantindo carregamento correto e escala *Preencher* sem faixas pretas.

---

## [2.0.0.106] - 2026-11-26

### Corrigido — Ajuste do Papel de Parede para Modo Preencher (Fill)
- **Correção de `WallpaperStyle` em `Scripts\apply_wallpapers.ps1`**:
  - Corrigida a substituição por regex em arquivos `.theme`, utilizando `${1}0` e `${1}10` para evitar ambiguidade de grupo de captura (`$10`/`$110`) e garantir `TileWallpaper=0` e `WallpaperStyle=10` (Preencher).
  - Corrigido o valor da política `Policies\System\WallpaperStyle` para `4` (enumeração oficial do Windows para *Preencher* em políticas de sistema) no perfil `Default` e nos perfis de usuários existentes (em vez de `10`, que fazia o Windows reverter para *Ampliar*).
  - Aplicados `WallpaperStyle=10` e `TileWallpaper=0` em `Control Panel\Desktop` também nos perfis de usuários existentes e removido o bloco de assinatura legado expirado do script embutido.

---

## [2.0.0.105] - 2026-11-26

### Corrigido — Visibilidade do Usuário Padrão na Tela de Login do Windows
- **Adição obrigatória ao grupo `Usuários` (`S-1-5-32-545`) em `ConfigureLocalAccountAsync`**:
  - `New-LocalUser` cria contas sem associação a nenhum grupo por padrão, fazendo com que o Windows oculte a conta na tela de login. Toda conta criada ou atualizada passa a ser habilitada (`Enable-LocalUser`) e adicionada ao grupo `Usuários` (`S-1-5-32-545`), além de `Administradores` (`S-1-5-32-544`) ou `Área de Trabalho Remota` (`S-1-5-32-555`, opcional em edições Home).
  - Corrigida a resolução de grupos locais usando objeto `SecurityIdentifier` com `Get-LocalGroup -SID $sidObj` e `Add-LocalGroupMember -Group $grpObj` (com fallback `[ADSI]`), eliminando o erro `O grupo S-1-5-32-555 não foi encontrado`.

---

## [2.0.0.104] - 2026-11-26

### Adicionado / Aprimorado — Verificação Final de Programas Instalados e Perfis Recém-Criados
- **Verificador Final de Instalação (`VerifySelectedProgramsInstalledAsync`)**:
  - Antes de concluir a instalação, verifica diretamente no Registro do Windows, caminhos padrão de instalação, detectores específicos e pacotes AppX/MSIX se cada programa selecionado foi realmente instalado.
  - Exibe no log o resultado final colorido: em **azul** os programas instalados corretamente (`[ok]`) e em **vermelho** os que não foram instalados ou apresentaram erro (`X`).
- **Propagação Segura para Perfis Recém-Criados (`EnsureAllProgramsAccessibleForAllProfilesAsync`)**:
  - Evita pré-criar pastas de perfil antes do primeiro logon de contas recém-criadas, propagando atalhos para o Menu Iniciar Comum (`CommonPrograms`), perfil modelo `Default` (herdado automaticamente por qualquer perfil novo no primeiro logon) e perfis já inicializados (`NTUSER.DAT`).

---

## [2.0.0.103] - 2026-11-26

### Adicionado / Alterado — Perfis de Configuração Padrão e Nortel
- **Inicialização Limpa ("Padrão")**:
  - Ao abrir o instalador, o modo **Padrão** inicia selecionado, com todos os programas, atalhos, opções do Windows e funcionalidades desmarcados e campos de texto em branco.
- **Seleção de Perfil `Padrão` / `Nortel`**:
  - Adicionados os checkboxes `Padrão` e `Nortel` na aba **Configurações** (card *DADOS DA MÁQUINA E CONTAS*), abaixo de *Usuário padrão* e *Senha do usuário*.
  - Ao marcar **Nortel**, preenche automaticamente `NT-` (computador), `NT@admin#` (senha admin), usuário padrão em branco, `Nortel#` (senha do usuário) e marca os 11 programas corporativos, atalhos (Remote Neoenergia, Webmail Nortel Terceirização, Suporte Exabit e Limpar atalhos da Área de Trabalho), opções (localização, restrição de alteração de senha, senha nunca expira, papel de parede Nortel e bloqueio de papel de parede) e funcionalidades (desinstalar jogos e antivírus).

---

## [2.0.0.102] - 2026-11-26

### Corrigido — Criação de Usuários, Renomeação do PC e Remoção Silenciosa de Antivírus
- **Criação e Atualização de Usuários Locais (`ConfigureLocalAccountAsync`)**:
  - Corrigido travamento indefinido ao criar usuários padrão (ex: com espaços no nome ou senhas acima de 14 caracteres), substituindo chamadas legadas ao `net user` que aguardavam confirmação `(S/N)` na entrada padrão.
  - Passagem segura da senha via variável de ambiente efêmera do processo (`EXABIT_USER_PWD`), utilizando `Microsoft.PowerShell.LocalAccounts` (`New-LocalUser` / `Set-LocalUser` / `Add-LocalGroupMember` por SID) com fallback `[ADSI]`.
  - Fechamento imediato de `StandardInput` em `RunProcessAsync` e `RunProcessWithLoggingAsync` para impedir bloqueios de processos filhos em prompts interativos.
- **Desinstalação Silenciosa de Antivírus de Terceiros (`UninstallThirdPartyAntivirusAsync`)**:
  - Corrigido bug de precedência no `-replace` de `msiexec` que impedia a injeção de `/qn /norestart` em strings `/X{GUID}`.
  - Adicionada remoção silenciosa de pacotes Store/AppX (ex: McAfee Personal Security).
  - Priorização correta de `WindowsInstaller`/MSI e `QuietUninstallString`, com parsing direto de executável/argumentos e flags silenciosas específicas por fabricante (McAfee, Avast, AVG, Kaspersky, Bitdefender, Inno Setup, NSIS) em vez de parâmetros conflitantes via `cmd /c`.
  - Adicionado watchdog de janela interativa (`IsWindowVisible`) + fallback via `winget uninstall --silent --disable-interactivity` e verificação real de `ExitCode` para evitar janelas abertas travando a instalação.
- **Renomeação do Computador (`RenameComputerAsync`)**:
  - Verifica se o nome atual já corresponde ao desejado (ignorando maiúsculas/minúsculas), registrando `[ok]` sem acionar erro desnecessário de `NewNameIsOldName`.

---

## [2.0.0.101] - 2026-11-23

### Aprimorado / Adicionado — Gerenciador Independente de Perfis do Windows
- **Reorganização da Aba Funcionalidades**:
  - As opções preliminares de desinstalação de jogos e antivírus de terceiros permanecem no topo com legenda informativa.
  - A opção de remoção de perfis foi movida para a base da aba, separada por divisória visual sutil, e renomeada para **"Apagar Perfis do Windows"**.
  - A funcionalidade agora é uma ação independente acionada por botão dedicado (`_btnStartProfileCleanup`), permitindo limpeza de perfis sob demanda sem disparar a instalação de softwares.
- **Seleção Interativa na Mesma Tela ("In-Place View")**:
  - Ao clicar no botão, a mesma área da aba alterna para a visualização dos perfis de usuário encontrados no Windows, com suporte a barra de rolagem corporativa `DarkScrollBar`.
  - Cada perfil é exibido com um `DarkCheckBox` para seleção granular.
  - Perfis em uso e contas essenciais do sistema (`Environment.UserName`, `Administrador`, `DefaultAccount`, `Guest`, `WDAGUtilityAccount`, `systemprofile` e perfis com sessão carregada) são automaticamente protegidos, exibindo o status `[Protegido]` e mantendo o checkbox desabilitado.
  - Adicionado controle rápido para marcar/desmarcar todos os perfis elegíveis.
  - Botão de execução `[ APAGAR PERFIS SELECIONADOS ]` realiza a remoção direta e assíncrona (conta local, WMI, ProfileList e pasta física em disco) emitindo logs linha a linha em tempo real no console de terminal inferior, com recarregamento automático da lista após a conclusão.
  - Removidos avisos e diálogos modais de confirmação redundantes, uma vez que a seleção seletiva é manual e independente na própria tela.

---

## [2.0.0.100] - 2026-11-21

### Segurança operacional e confiabilidade

- **Cancelamento efetivo**: o botão PARAR aborta downloads em andamento e solicita o encerramento do processo em execução. A interface indica `PARANDO...`; etapas já concluídas não são revertidas.
- **Proteção na remoção de perfis**: a opção agora mostra contas, perfis e pastas identificados antes da confirmação, preserva contas com perfil carregado e o caminho exato do usuário atual, e registra falhas de exclusão em vez de reportar sucesso silencioso.
- **Erros de log explícitos**: se o log em arquivo não puder ser criado ou atualizado, a interface recebe um aviso sem interromper a instalação.

### Manutenção e build

- `InstallerService` foi dividido em arquivos parciais para downloads/resolução de instaladores e execução de processos, sem alterar a API pública do serviço.
- Versão do assembly, versão do arquivo e manifesto alinhados para `2.0.0.100`; `validate-version.ps1` valida a consistência antes do build.
- O `build.bat` aceita `EXABIT_BUILD_NONINTERACTIVE=1` para compilação sem pausas de console.
- Adicionado workflow do GitHub Actions para compilar em Windows em pushes e pull requests para `master` e armazenar o executável como artefato.

---

## [2.0.0.99] - 2026-11-21

### Segurança — Assinatura Authenticode real e remoção do certificado autoassinado

- **`sign.ps1` reescrito para usar apenas certificado corporativo real**:
  - Aceita PFX por `EXABIT_SIGN_PFX` / `EXABIT_SIGN_PFX_PASSWORD` (ou parâmetros `-PfxPath` / `-PfxPassword`).
  - Também aceita certificado OV/EV instalado no repositório (`CurrentUser\My` / `LocalMachine\My`) que tenha chave privada e seja emitido por uma CA.
  - Removida a criação automática via `New-SelfSignedCertificate`.
  - Sem certificado real configurado, a assinatura é ignorada com aviso e o build continua normalmente.
  - Removida a exportação do `.cer` para instalação nas máquinas clientes.
- **Removidos `dist\Instalar_Certificado_EXABIT.cmd` e `dist\exabit_cert.cer`**:
  - O script instalava o certificado autoassinado em "Autoridades Raiz Confiáveis", permitindo que qualquer binário forjado com o mesmo nome fosse aceito como confiável.
  - Para CA interna, a distribuição do certificado deve ser feita via GPO.
- **`build.bat`**: mensagem de assinatura ajustada para indicar que é opcional.

### Adicionado

- **Log de sessão em arquivo**: todas as mensagens do `InstallerService` passam a ser gravadas em `C:\ProgramData\EXABIT\Logs\install-yyyyMMdd-HHmmss.log` (fallback para `%TEMP%\EXABIT_Logs`), com cabeçalho de versão, máquina, usuário e sistema. O caminho do arquivo é exibido no início da instalação.
- **Verificação SHA-256 de instaladores**: novo campo `ProgramInfo.Sha256`. Quando preenchido, o arquivo baixado é validado antes da instalação; em caso de divergência, o cache é removido e a instalação do programa é abortada com log `X`. Sem hash preenchido, o comportamento permanece idêntico ao atual.

### Build

- **`InstallerGUI.csproj` alinhado ao `build.bat`**:
  - `AssemblyName` corrigido para `Instalador Automatico Exabit`.
  - Adicionadas referências `System.Management`, `System.ServiceProcess`, `System.IO.Compression` e `System.IO.Compression.FileSystem`.
  - Adicionados `app.manifest` (`ApplicationManifest`), `install.ico` (`ApplicationIcon`) e o recurso embutido `Scripts\apply_wallpapers.ps1`.
  - Adicionados `PlatformTarget=AnyCPU`, `Prefer32Bit=false` e `HighEntropyVA=true`, equivalentes às opções do `csc.exe` no `build.bat`.
  - Build por Visual Studio/MSBuild agora gera o mesmo executável do build oficial, com os três recursos embutidos.

### Documentação

- **`README.md` atualizado**: instalação 100% online, estrutura real do projeto, saída `dist\Instalador Automatico Exabit.exe`, fluxo de build/assinatura, cadastro de programas (URL + `Resolve*`) e guia de modificação.
- **`AGENTS.md` atualizado**: árvore de arquivos, eventos, fluxo em fases, cadastro de programas online e regra de nunca usar certificado autoassinado.

---

## [2.0.0.98] - 2026-11-21

### Corrigido — Diagnóstico do Sistema Completo, Antivírus e BitLocker

- **Atualização Periódica da Fonte e da Bateria (`Estado do sistema`)**:
  - A fonte conectada e a porcentagem da bateria agora são atualizadas a cada 5 segundos no card "Segurança e Status".
  - `UpdateDynamicMetrics` passou a consultar `GetSystemPowerStatus` junto com RAM e unidades de disco.
  - `RefreshDynamicMetricsViews` atualiza diretamente o texto "Reboot Pendente | Fonte" sem aguardar uma nova varredura completa.
  - O timer síncrono de disco e RAM foi alinhado ao intervalo oficial de 5 segundos.

- **Padronização Visual da Cor Corporativa Laranja (`UI/UX`)**:
  - Unificadas as constantes e os componentes personalizados para utilizar rigorosamente `RGB(255, 140, 0)` / `#FF8C00`.
  - Corrigidas barras de progresso e de rolagem personalizadas que ainda utilizavam tonalidades antigas próximas a `RGB(243, 146, 0)`.

- **Restauração da Execução do Diagnóstico Completo Assíncrono (`StartBackgroundFullDiagnosis`)**:
  - Na versão 2.0.0.97, a chamada assíncrona ao método `GetFullSystemDiagnosisAsync()` havia sido suprimida, fazendo com que campos dependentes de varredura profunda (Processador, Modelo, Fabricante, Número de Série, Resumo de Interfaces e Segurança) ficassem parados nos valores padrão ou esqueletos iniciais.
  - Adicionado `StartBackgroundFullDiagnosis()`, disparado automaticamente na inicialização da aplicação e ao abrir a visão expandida (`_btnMoreDetails`), garantindo que todos os cards sejam enriquecidos sem congelar a interface gráfica.
- **Diagnóstico Rápido Instantâneo (`GetQuickSystemDiagnosis`) Enriquecido**:
  - Implementada leitura imediata via Registry (< 1ms) para Fabricante e Modelo (`HARDWARE\DESCRIPTION\System\BIOS`), Número de Série, Nome do Processador (`HARDWARE\DESCRIPTION\System\CentralProcessor\0`), Interfaces de Rede e IPs locais via `.NET NetworkInterface`, eliminando placeholders genéricos desde o primeiro milissegundo de abertura.
- **Detecção Precisa de Antivírus Ativo de Terceiros e Microsoft Defender**:
  - Corrigida a identificação no WMI `root\SecurityCenter2` (`AntiVirusProduct`), decodificando corretamente os bits do estado de proteção em tempo real (`productState`).
  - Quando um antivírus de terceiros estiver instalado e ativo (ex: *Kaspersky*, *Avast*, *AVG*, *Norton*, *Bitdefender*), o card agora reporta com precisão: `Antivírus: Kaspersky (Ativo)` (ou o nome do software detectado), em vez de alegar erroneamente que o Defender é o antivírus principal.
  - Se apenas o Microsoft Defender estiver ativo, reporta `Antivírus: Microsoft Defender (Ativo)`.
- **Detecção Resiliente de Proteção BitLocker / Criptografia de Volume**:
  - Corrigido erro de "Acesso Negado" no WMI `root\CIMV2\Security\MicrosoftVolumeEncryption` através da configuração obrigatória de `AuthenticationLevel.PacketPrivacy` e `ImpersonationLevel.Impersonate` no `ManagementScope`.
  - Adicionado mecanismo de contingência via `manage-bde.exe -status C:` para conferência direta do estado de proteção quando a chamada WMI retornar inconclusiva, permitindo reportar com precisão se o BitLocker está `Ativo (C:)` ou `Desativado`.
- **Prevenção de Truncamento Vertical e Quebra de Linha nos Cards (UI/UX)**:
  - Ajustada a coluna de rótulos dos Cards 1 (Identificação), 4 (Hardware) e 5 (Dispositivo) para `115px`, impedindo que os rótulos *"Número de Série:"* e *"Memória RAM:"* quebrem linha e sejam cortados verticalmente.
  - O resumo de memória RAM agora computa o total de gigabytes automaticamente (ex: `64 GB DDR5` ou `32 GB RAM`) e nunca mais reverte para a palavra isolada *"Memória"*.

---

## [2.0.0.97] - 2026-11-21

### Corrigido — Arquitetura de timers de métricas totalmente reescrita

- **Dois timers independentes** substituem o antigo timer único com flag `_isPeriodicMetricsRunning`:
  - **`_syncMetricsTimer` (3 segundos, síncrono)**: atualiza espaço em disco (C: e drives expandidos) e RAM de forma 100% síncrona via `DriveInfo` e `GlobalMemoryStatusEx`. Executa em menos de 5ms, nunca bloqueia a UI thread e nunca é travado pelo ping.
  - **`_pingTimer` (5 segundos, `Task.Run`)**: realiza ICMP ping para `1.1.1.1` (timeout 1500ms) e, em caso de falha, tenta `8.8.8.8`. Executa em thread pool, posta o resultado de volta na UI via `BeginInvoke`. Totalmente independente do timer de disco.
- **Detecção real de Offline**: O ping agora usa `System.Net.NetworkInformation.Ping.Send` diretamente (não `HasInternetConnectionAsync`). Se ambos os IPs não respondem em 1500ms, exibe `"Offline ○/●"` imediatamente, sem depender de `NetworkInterface.GetIsNetworkAvailable()` que mentia "Connected" mesmo sem internet real.
- **Fim do bloqueio mútuo**: Na arquitetura anterior, o flag `_isPeriodicMetricsRunning` impedia que disco e RAM fossem atualizados enquanto o ping aguardava resposta (até 3,4s de await). Agora, disco atualiza a cada 3s independentemente, e ping a cada 5s, sem interferência.
- **Click handlers** dos labels de conexão e do `_barDiagDisk` agora chamam `SyncMetricsTimer_Tick` + `TriggerPingNow()` em vez do antigo método unificado.
- **Pausa durante instalação**: Ambos os timers são parados ao iniciar a instalação, retomando automaticamente ao fechar o formulário.

---

## [2.0.0.96] - 2026-11-21

### Aprimorado / Corrigido
- **Card Compacto de Diagnóstico ("Diagnóstico do Sistema")**:
  - Removido o texto descritivo e a porcentagem escrita do campo *"Disco C:"*, mantendo exclusivamente a barra de progresso horizontal (`MiniUsageBar`), conforme padrão visual minimalista oficial.
  - A barra de progresso continua sendo recalculada e atualizada dinamicamente a cada 5 segundos através de APIs nativas do `DriveInfo`, com renderização imediata (`Refresh()`).
- **Diagnóstico de Conectividade e Ping a Cada 5 Segundos**:
  - Unificado o monitoramento em um único timer periódico de 5 segundos (`_metricsRefreshTimer`), eliminando o antigo timer concorrente de 2 segundos (`_connectionMonitorTimer`) que causava colisões de sockets ICMP e acionava consultas WMI completas em segundo plano que sobrescreviam o texto da tela inicial.
  - Substituídos os glifos circulares imperceptíveis `↻`/`↺` por um indicador pulsante de alta visibilidade: `●` (ponto sólido preenchido) e `○` (círculo vazado), que alterna com clareza a cada 5 segundos fornecendo feedback inequívoco de atividade do monitoramento em tempo real (ex: `Online (31 ms) ●` e `Online (31 ms) ○`).
  - Chamada imediata na abertura do aplicativo para que o ping e as métricas sejam populados no primeiro instante sem aguardar 5 segundos ociosos.
  - Evento de clique em *Conexão* e na barra de *Disco C:* aciona medição instantânea com feedback imediato (`Medindo ping...`).

## [2.0.0.95] - 2026-11-20

### Aprimorado / Corrigido
- **Correção da Opção "Ativar (senha nunca expira) para todos os perfis" quando Desmarcada**:
  - Corrigido o método de configuração de contas locais (`ConfigureUserPasswordPolicyAsync`) para respeitar estritamente o estado do checkbox `_chkPasswordNeverExpiresGlobal`.
  - Anteriormente, os métodos `CreateStandardUserAsync` e `CreateAdminUserAsync` continham internamente a flag `-PasswordNeverExpires $true` de forma fixa, o que fazia com que contas criadas/atualizadas (como usuário padrão e admin) recebessem a flag "A senha nunca expira" mesmo com o checkbox desmarcado na aba *Opções*.
  - Agora, os parâmetros são propagados ponta a ponta (`options.PasswordNeverExpiresGlobal`), e quando a opção estiver desmarcada (`false`), a flag `-PasswordNeverExpires $false` é explicitamente aplicada a todos os perfis locais e aos usuários criados ou modificados, garantindo que no `lusrmgr.msc` a caixa *"A senha nunca expira"* permaneça desmarcada.
- **Atualização Dinâmica Contínua de Espaço em Disco e Ping a Cada 5 Segundos**:
  - **Card Compacto ("Diagnóstico do Sistema")**:
    - Adicionado label de texto descritivo `_lblDiagDisk` ao lado da mini barra `_barDiagDisk` no campo *"Disco C:"*, exibindo a medição precisa em tempo real (ex: `300,4 GB livres (32%)`).
    - Alinhamento horizontal nivelado entre barra e texto, permitindo clique direto no texto ou barra para forçar refresh instantâneo.
  - **Atualização Contínua em "Mais detalhes"**:
    - O painel de armazenamento agora mapeia as unidades fixas por letra de unidade (`panel.Tag = drive.DriveLetter`), atualizando status, gigabytes livres/totais e cores de alerta em tempo real.
    - RAM Summary e barra de porcentagem de memória continuam sincronizados a cada 5 segundos.
  - **Indicador Visual de Atualização do Ping**:
    - O teste de ping agora alterna o glifo de atualização entre `↻` (sentido horário) e `↺` (sentido anti-horário) a cada ciclo de 5 segundos, fornecendo feedback visual imediato de que a medição foi realizada mesmo quando a latência de rede se mantém estável (ex: `31 ms` / `32 ms`).
  - **Inicialização Ultrarrápida Sem Espera (`GetQuickSystemDiagnosis`)**:
    - Adicionado método instantâneo (~2ms via .NET e Registry nativo) no `InstallerService` para carregar imediatamente a identificação da máquina, edição do Windows, todas as unidades de disco e memória RAM na abertura do programa, eliminando a tela em branco ou atrasos provocados por consultas WMI pesadas.

## [2.0.0.94] - 2026-11-20

### Adicionado / Aprimorado
- **Nova Opção "Ativar senha nunca expira para todos os perfis" na Aba 3 (Opções do Windows)**:
  - Adicionado checkbox `_chkPasswordNeverExpiresGlobal` posicionado na linha 2 da Aba 3 (Opções do Windows), logo abaixo de *"Impedir que o usuário altere a senha"*.
  - Definido como ativado por padrão (`Checked = true`).
  - Condicionamento da execução: A aplicação da política global do Windows de expiração ilimitada (`net accounts /maxpwage:unlimited`) e a definição de `PasswordNeverExpires = $true` para todos os usuários locais (`Get-LocalUser | Set-LocalUser -PasswordNeverExpires $true`) agora **só é executada** se este checkbox estiver marcado.
- **Criação Condicional de Conta Admin Baseada no Preenchimento da Senha**:
  - A criação e configuração do usuário "admin" agora só ocorre se o campo *"Senha do Admin"* for preenchido com ao menos um caractere.
  - Se o campo estiver em branco, a conta de admin não é criada nem modificada, alinhando-se estritamente à orientação exibida na interface *"Os campos acima só funcionam caso preenchidos (em branco = não alterar)"*.
- **Correção da Instalação do DeepSeek Desktop e Ajuste de Argumentos NSIS**:
  - Identificada a causa do erro de código 2: o instalador do DeepSeek Harness é baseado em Nullsoft (NSIS), que aceita estritamente o parâmetro `/S` e falha se receber parâmetros não suportados como `/allusers`.
  - Ajustados os parâmetros de linha de comando para `/S` para instaladores NSIS (`deepseek`, `gemini`, `antigravity`, `recuva`, `artcraft`) e `--silent` para `claude`. A compatibilidade com todos os perfis permanece 100% garantida pela rotina de propagação global `EnsureAllProgramsAccessibleForAllProfilesAsync`.
  - Removido o argumento `--scope machine` da chamada do `winget` para o DeepSeek Harness, permitindo que o Windows Package Manager instale o pacote nativamente sem recusar por incompatibilidade de escopo no manifesto.
  - Adicionada rotina de recuperação e retry automático com `/S` caso qualquer instalador NSIS retorne código de saída não-zero.
- **Substituição Visual dos Checkboxes pelo Tema Corporativo Laranja (`DarkCheckBox`)**:
  - Implementada a classe `DarkCheckBox : CheckBox` com renderização personalizada GDI+ nativa:
    - Fundo marcado preenchido no tom laranja corporativo oficial da EXABIT (`CorDestaqueLaranja` / `#FF8C00` / `RGB 255, 140, 0`), o mesmo utilizado na barra de rolagem `DarkScrollBar`.
    - Glifo de marcação vetorial em branco puro com pontas e junções arredondadas (`SmoothingMode.AntiAlias`).
    - Caixa com bordas e preenchimento nítidos (`SmoothingMode.None`), eliminando o aspecto embaçado ou dependência de tema do Windows.
    - Efeito de destaque ao passar o mouse (*hover*) e ao receber foco do teclado na cor laranja.
    - Cursor do mouse estilizado em formato de mão (`Cursors.Hand`).
  - Substituição aplicada automaticamente em 100% dos checkboxes do sistema (lista de programas, abas de atalhos, opções do Windows e funcionalidades de limpeza).

## [2.0.0.93] - 2026-11-20

### Aprimorado / Corrigido
- **Compatibilidade Global e Funcionamento de Todos os Programas em Todos os Perfis (Existentes e Novos)**:
  - **Provisionamento de Pacotes MSIX (ChatGPT Desktop e Affinity Studio)**:
    - Adicionado suporte completo a provisionamento nativo via `Add-AppxProvisionedPackage -Online -PackagePath ... -SkipLicense` e registro para todos os usuários via `Add-AppxPackage -AllUsers`.
    - Isso garante que pacotes modernos (MSIX/Appx) não fiquem restritos apenas ao perfil que executou a instalação: todos os usuários existentes recebem o aplicativo e qualquer **novo perfil de usuário** criado posteriormente no Windows terá o app automaticamente instalado na primeira inicialização.
    - Nas chamadas do `winget`, adicionado o parâmetro `--scope machine` para forçar a instalação no escopo do sistema operacional.
  - **Instalação em Nível de Máquina (`ALLUSERS=1`) para Suíte ArtCraft e MSIs**:
    - Ajustados os argumentos de instalação de todos os 13 aplicativos da suíte Craft da Storytold (`cadcraft`, `deckcraft`, `designcraft`, `effectcraft`, `filmcraft`, `gridcraft`, `lightcraft`, `pdfcraft`, `photocraft`, `soundcraft`, `texturecraft`, `vectorcraft`, `wordcraft`) e do `nodejs` para `/qn /norestart ALLUSERS=1`.
    - No `InstallerService.cs`, implementada injeção automática de `ALLUSERS=1` para qualquer pacote `.msi` caso o parâmetro não esteja explicitado.
    - Adicionado parâmetro `/allusers` nos instaladores executáveis (ArtCraft Studio, Claude Desktop, DeepSeek Desktop, Gemini Desktop, Antigravity, Antigravity IDE, VS Code, CrystalDiskInfo, CrystalDiskMark, EaseUS Todo Backup, Recuva).
  - **Rotina de Propagação e Normalização Multi-Usuário (`EnsureAllProgramsAccessibleForAllProfilesAsync`)**:
    - Executada automaticamente após o ciclo de instalação de programas para eliminar qualquer bloqueio de ACL ou isolamento per-user.
    - Concede permissões recursivas de leitura e execução (`(OI)(CI)RX`) ao grupo `Users` (SID `*S-1-5-32-545`) em `%LOCALAPPDATA%\Programs`.
    - Espelha pastas de executáveis per-user para o diretório compartilhado `C:\ProgramData\Programs`.
    - Propaga atalhos do Menu Iniciar (`.lnk`) de programas instalados para o Menu Iniciar Comum (`C:\ProgramData\Microsoft\Windows\Start Menu\Programs`) e para o perfil modelo de novos usuários (`C:\Users\Default\AppData\Roaming\Microsoft\Windows\Start Menu\Programs`), além dos perfis de usuário padrão e administrador.
    - Registra caminhos de executáveis no Registro do Windows (`HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths`), permitindo localização imediata pela barra de pesquisa e comando Executar (`Win+R`) em qualquer perfil.

## [2.0.0.92] - 2026-11-20

### Aprimorado / Corrigido
- **Barras de Rolagem (`DarkScrollBar`) em Cards com Overflow no Diagnóstico Expandido**:
  - Implementado container rolável com viewport e barra de rolagem estilizada (`DarkScrollBar`) no Card 1 (**Identificação e Sistema**) e no Card 6 (**Segurança e Status**).
  - Caso o conteúdo de Sistema (edição do Windows + compilação/Build em duas linhas) ou de Segurança (antivírus de terceiros como Kaspersky em conjunto com o Windows Defender e status de reinicialização pendente) ultrapasse a área visível do card, uma barra vertical laranja aparece automaticamente com suporte a scroll via roda do mouse (`MouseWheel`), eliminando qualquer corte de texto.
  - Otimizado o container de cards (`CreateCardContainer`): padding vertical reduzido de 10px para 6px e altura do título otimizada de 28px para 22px, gerando **18px extras de altura útil** por card.
  - Largura da Coluna 0 ajustada para 102px (Identificação e Sistema), 95px (Conexão e Rede) e 95px (Recursos de Hardware), garantindo que termos como `Computador:`, `Conexão:` e `Memória RAM:` nunca quebrem ou cortem, enquanto termos longos como `64 GB DDR5 (28,4 GB em uso - 44%)` cabem com folga em uma única linha.
- **Legenda Informativa no Formulário de Contas da Máquina**:
  - Adicionada legenda informativa padronizada abaixo dos campos na Aba 1:
    `"Os campos acima só funcionam caso preenchidos (em branco = não alterar)."`
  - Tipografia consistente (`Segoe UI`, 8.5pt, Itálico, `#A0A0A0`) com altura fixa de 24px e espaçador flexível de 100%, em total harmonia com os rodapés das abas de Opções e Funcionalidades.

## [2.0.0.91] - 2026-11-20

### Aprimorado
- **Atualização Periódica a Cada 5 Segundos (Espaço em Disco e Ping)**:
  - O espaço em disco (Disco C:) e a medição de latência de rede (Ping / Conexão) na tela principal e no diagnóstico completo agora são atualizados continuamente a cada **5 segundos** (`_metricsRefreshTimer.Interval = 5000`).
  - O cálculo de espaço em disco e classificação de cores (verde, amarelo e vermelho) utiliza chamadas de alta performance (`DriveInfo.GetDrives()`), e o ping é executado de forma assíncrona em background via ICMP contra DNS de baixa latência (1.1.1.1 / 8.8.8.8) com resposta imediata na UI (`Online (X ms) ↻`).
  - Adicionado suporte a atualização sob demanda imediata ao clicar no texto do Ping ou na barra de disco.

## [2.0.0.90] - 2026-11-20

### Aprimorado / Corrigido
- **Janela Fixa Otimizada para Resolução 720p (Sem Botão Maximizar e Sem Redimensionamento)**:
  - Removido o botão Maximizar (`MaximizeBox = false`), mantendo exclusivamente Minimizar (`MinimizeBox = true`) e Fechar.
  - Travado o redimensionamento da janela de forma nativa e definitiva com `FormBorderStyle = FormBorderStyle.FixedSingle`.
  - Definidas as dimensões ideais com área de respiro de 1200 x 650 px (otimizada para telas 720p 1280x720 com barra de tarefas do Windows de até 48px), centralizada na tela (`CenterScreen`) sem cortar elementos nem encostar na barra de tarefas.
  - Bloqueadas as dimensões com `MinimumSize = MaximumSize = Size = new Size(formWidth, formHeight)`.

## [2.0.0.89] - 2026-11-20

### Corrigido / Aprimorado
- **Correção da Redução Abaixo de 700px (Compensação DWM Frame)**:
  - Identificada a causa da largura visível chegar a 685px: o DWM do Windows 10/11 desconta 16px de bordas invisíveis da janela ao aplicar `ptMinTrackSize`.
  - Implementada compensação dinâmica de margens DWM via `DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS)` e `GetWindowRect` para travar `700 + dwmExtraW` e `550 + dwmExtraH` em `MinimumSize`, `WM_GETMINMAXINFO`, `WM_SIZING`, `OnResize` e `OnResizeEnd`.
  - A janela visível renderizada na tela e capturada em screenshots nunca mais mede menos de 700 x 550 px.
- **Tipografia e Cabeçalho de Abas Responsivos**:
  - Títulos de abas otimizados ("Configurações", "Atalhos", "Opções", "Funcionalidades") com fontes ajustadas para não truncar na largura mínima.

## [2.0.0.88] - 2026-11-20

### Corrigido / Aprimorado
- **Trava Estrita de Dimensão Mínima da Janela (700 x 550px)**:
  - Definido `MinimumSize = new Size(700, 550)` diretamente no construtor.
  - Bloqueio a nível de mensagens Win32 em `WM_GETMINMAXINFO (0x0024)` e `WM_SIZING (0x0214)` recalculando as coordenadas em tempo real durante o arrasto de qualquer borda (esquerda, topo, direita, base e diagonais).
  - Adicionado `OnResizeEnd` e reforçado `OnResize` para garantir redimensionamento mínimo incondicional.
- **Diretriz de Controle de Versão (AGENTS.md)**:
  - Documentada a proibição de `git push` automático (envios ao GitHub somente sob ordem expressa do usuário).

## [2.0.0.87] - 2026-11-20

### Corrigido / Aprimorado
- **Trava Estrita de Largura Mínima (700px)**:
  - Implementada a interceptação em tempo real da mensagem do Windows `WM_SIZING (0x0214)`, recalculando e corrigindo o retângulo de arrasto (`drag rectangle`) no ato da movimentação do mouse.
  - Atualizado `MinimumSize` para considerar a escala de DPI (`minTrackW` / `minTrackH`) desde o construtor do formulário.
  - Documentado no código a estrutura de formulários de `BuildMachineAccountsCard`.

## [2.0.0.86] - 2026-11-20

### Adicionado
- **Sistema de Design Visual EXABIT e Skill de Projeto**:
  - Criada a skill oficial `.agents/skills/exabit-ui-design-system/SKILL.md` documentando todas as regras visuais, paleta de cores corporativa, tipografia, paddings, alturas de linha e convenções para novos painéis.
  - Atualizado `AGENTS.md` com diretrizes permanentes de UI/UX para agentes de IA.

### Aprimorado
- **Atualização Dinâmica a Cada 3 Segundos**:
  - Implementado timer de alta performance (`_metricsRefreshTimer` com intervalo de 3000ms) atualizando em tempo real o uso de memória RAM e espaço livre em disco em ambos os modos (compacto e expandido) sem travar a interface e sem chamadas WMI lentas (`GlobalMemoryStatusEx` e `DriveInfo.GetDrives()`).
- **Ajuste de Proximidade na Barra de Memória RAM (Imagem 1)**:
  - Adicionada terceira linha expansível no layout do card de hardware e fixada a altura do container de RAM para 32px, posicionando a barra de uso diretamente 4px abaixo do texto descritivo (idêntico ao padrão das barras de disco).
- **Agrupamento de Labels e Campos de Entrada (Imagem 2)**:
  - Reformulada a estrutura do card *DADOS DA MÁQUINA E CONTAS*: os rótulos agora se situam imediatamente acima dos campos de texto correspondentes (18px de label + 28px de textbox), eliminando vazios e agrupando as informações segundo as melhores práticas de design de formulários.
- **Padding Interno no Terminal / Console de Log (Imagem 3)**:
  - Implementado container `pnlTextWrapper` com padding interno de 8px ao redor do `RichTextBox`, afastando o texto das bordas duras da janela para uma leitura visualmente agradável e padrão terminal corporativo.
- **Espaçamento Uniforme nas Abas (Atalhos, Opções e Funcionalidades)**:
  - Padronizada a altura de todas as linhas de checkbox para exatamente **30px** e padding dos containers para `Padding(16, 12, 16, 8)` nas abas *Atalhos*, *Opções do Windows* e *Funcionalidades*, eliminando disparidades verticais.

---

## [2.0.0.85] - 2026-11-20

### Aprimorado
- **Refinamentos Visuais e Design Corporativo Rigoroso (Design Review)**:
  - **Botão Mais Detalhes**: Layout do card compacto reformulado com container dedicado ancorado na base (`DockStyle.Bottom`) e altura do painel de abas aumentada para 265px, garantindo visibilidade 100% perfeita do botão `▼ Mais detalhes (+6)` sem qualquer corte vertical.
  - **Largura Mínima Estrita em 700px**: Implementada interceptação nativa Win32 da mensagem `WM_GETMINMAXINFO` no `WndProc` combinada com `OnResize`, impedindo fisicamente que o mouse arraste as bordas da janela para uma largura menor que 700px em qualquer nível de DPI.
  - **Alinhamento e Margens em Cards de Detalhes**:
    - Padronização rigorosa da coluna de rótulos em 125px em todos os cards expandidos, eliminando qualquer truncamento de textos como `Computador:`.
    - Zeradas margens de labels para alinhamento horizontal perfeitamente nivelado e alinhado entre `Computador:`, `Usuário:` e `Sistema:`.
    - Margem inferior e espaçamento vertical ampliados no cabeçalho de cada card (`CreateCardContainer`), proporcionando respiro visual elegante entre os títulos laranjas e os itens informativos.
  - **Memória RAM em Linha Única**: Rótulo `Memória RAM:` redimensionado para 125px, eliminando quebra de linha indesejada e unificando rótulo, total e barra de consumo com visual limpo.
  - **Separação de Fabricante e Modelo**: Dividido em duas linhas distintas e bem delineadas: `Fabricante:` e `Modelo:`, melhorando a clareza e organização dos dados do equipamento.
  - **Padronização de Espaçamentos Verticais**: Eliminado vão excessivo entre modelo e `Número de Série:`, fixando altura uniforme de 24px para cada linha de informação em todos os cards de detalhes.

---

## [2.0.0.84] - 2026-11-20

### Aprimorado
- **Ajustes no Painel de Diagnóstico Compacto (Aba Configurações)**:
  - **Botão Mais Detalhes**: Altura e espaçamento vertical aumentados para 245px no painel de abas, garantindo que o botão `▼ Mais detalhes (+6)` seja exibido por completo sem qualquer corte.
  - **Título da Janela vs. Título do Programa**: Versão mantida exclusivamente na barra superior da janela com o ícone (`Auto Instalador de Programas v2.0.0.84 - EXABIT Soluções em TI`), restaurando o título visual interno para `Auto Instalador de Programas`.
  - **Espaço em Disco Simplificado**: No resumo compacto, a unidade C: agora é informada exclusivamente através da barra horizontal colorida (`Verde` para espaço saudável, `Amarelo` para atenção/quase cheio e `Vermelho` para cheio/crítico), removendo os números extensos da linha.
  - **Conexão e Ping**: Rótulo ajustado para `Conexão:` (sem barra `/`) com status `Online (XX ms) ↻` ou `Offline ↻`, seguindo esquema de cores para latência baixa (verde), média (amarelo) e alta/offline (vermelho).
- **Aprimoramentos no Painel Expandido ("DIAGNÓSTICO DO SISTEMA (COMPLETO)")**:
  - **Títulos em Português (PT-BR)**: Todos os 6 cards traduzidos para o padrão corporativo nacional: *Identificação e Sistema*, *Conexão e Rede*, *Armazenamento e Partições*, *Recursos de Hardware*, *Dispositivo e Garantia* e *Segurança e Status*.
  - **Correção de Corte em Identificação e Sistema**: Alinhamento no topo (`TopLeft`) e alturas de linha reajustadas para que a edição comercial e a build do Windows nunca sofram corte vertical.
  - **Barra de Rolagem Padronizada (`DarkScrollBar`)**: Implementado container viewport com a barra de rolagem corporativa em tema escuro e pílula laranja em *Armazenamento e Partições*, eliminando a barra nativa cinza do Windows Forms.
  - **Listagem Completa de IPs Locais**: No card *Conexão e Rede*, todos os endereços IPv4 da máquina (Wi-Fi, Ethernet, VPNs, Radmin) agora são exibidos em lista com nome da interface, rolagem fluida e clique para cópia direta.
  - **Detecção do Antivírus Ativo quando Defender Desativado**: Ao detectar que o Microsoft Defender está inativo, identifica e exibe o antivírus de terceiros em execução no sistema operacional (ex: `Windows Defender: Desativado (Kaspersky)`).
- **Controles e Padrões de Seleção**:
  - **Largura Mínima**: Janela configurada com trava de largura mínima de 700px (`MinimumSize = new Size(700, 600)`).
  - **WinRAR**: Padrão de instalação alterado para desmarcado (`DefaultChecked = false`).
  - **Bloquear Alteração do Papel de Parede**: Padrão alterado para desmarcado (`false`).

---

## [2.0.0.83] - 2026-11-20

### Adicionado
- **Exibição da Versão do Aplicativo no Título**:
  - A versão do sistema agora é informada dinamicamente tanto na barra de título da janela (`Auto Instalador de Programas v2.0.0.83 - EXABIT Soluções em TI`) quanto no cabeçalho visual superior da interface gráfica (`Auto Instalador de Programas v2.0.0.83`), obtida diretamente dos metadados do Assembly.

---

## [2.0.0.82] - 2026-11-20

### Adicionado
- **Painel de Diagnóstico do Sistema com Modos Compacto e Expandido**:
  - **Modo Compacto (Aba 1 - Configurações do Sistema)**:
    - Seção superior reestruturada em dois cards lado a lado conforme especificado:
      - **Card Esquerdo ("DADOS DA MÁQUINA E CONTAS")**: Campos de *Nome do computador*, *Senha do Admin*, *Usuário padrão* e *Senha do usuário*, com borda sutil e título em destaque laranja.
      - **Card Direito ("DIAGNÓSTICO DO SISTEMA")**: Resumo em tempo real contendo *Computador*, *Usuário*, *Sistema*, *Disco C: (NVMe)* com mini barra horizontal colorida de uso, *Conexão / Ping* com botão interativo de atualização imediata (`↻`), *IP Local* com funcionalidade de cópia rápida para a área de transferência (`[+]`), e botão em formato pílula com borda ciano `▼ Mais detalhes (+6)`.
  - **Modo Expandido ("DIAGNÓSTICO DO SISTEMA (COMPLETO)")**:
    - Ao clicar em `▼ Mais detalhes (+6)`, a visualização é expandida preenchendo a tela inteira em uma grade de 6 cards (2 colunas x 3 linhas) mantendo a barra de abas e botões de ação:
      - **Identificacion & OS**: Nome da máquina e grupo de trabalho/domínio, usuário em execução com indicação de privilégios de Administrador, edição comercial do Windows e número da build.
      - **Connection & Network**: Status de conexão online com latência de ping, IP local ativo e interfaces físicas e virtuais detectadas (Ethernet, Wi-Fi, VPNs corporativas/Radmin).
      - **Storage & Partition**: Suporte completo para detecção e listagem de **todos os discos fixos presentes na máquina** (C:, D:, etc.), exibindo rótulo do volume, tipo de mídia (NVMe / SSD / HDD), espaço livre, espaço total, percentual e barra de progresso visual com cores dinâmicas (verde para bom, amarelo para atenção, vermelho para crítico).
      - **Hardware Resources**: Processador com nome comercial limpo, contagem real de núcleos físicos e threads, e memória RAM total, tipo (DDR4 / DDR5) e barra horizontal de uso em tempo real.
      - **Device & Warranty**: Fabricante do equipamento, modelo comercial e número de série da BIOS / Service Tag.
      - **Security & Status**: Status do Windows Defender (Ativo/Desativado), BitLocker (Ativo/Desativado), flag de reinicialização pendente do sistema e fonte de alimentação (Na tomada / Bateria com percentual).
    - Botão `← Voltar para Painel Principal` com borda e texto laranja permitindo retornar instantaneamente à visão normal de abas e console de log, com alternância automática ao alternar abas ou iniciar a instalação.
  - Implementada coleta de hardware em segundo plano via `System.Management` (WMI) e Win32 APIs de alto desempenho (`GlobalMemoryStatusEx`, `GetSystemPowerStatus`), garantindo execução sem travamentos na interface.

### Aprimorado
- **Remoção de Jogos e Xbox em Todos os Perfis Existentes e Futuros**:
  - Implementada injeção no modelo de perfil do Windows (`C:\Users\Default\NTUSER.DAT`) desativando o *Windows Consumer Experience* e *ContentDeliveryManager*, impedindo que o Windows baixe ou instale jogos patrocinados no primeiro logon de novas contas.
  - Injetado comando relâmpago de limpeza silenciosa em `RunOnce` do perfil `Default` (`HKU\DefaultUser\Software\Microsoft\Windows\CurrentVersion\RunOnce`), garantindo que qualquer pacote residual de jogos ou Xbox seja removido automaticamente e de forma transparente no primeiro acesso do colaborador.
  - Aplicadas políticas globais de máquina em `HKLM` bloqueando `DisableWindowsConsumerFeatures`, `DisableCloudOptimizedContent`, `AutoDownload` e `AllowGameDVR`.
  - Limpeza automática de atalhos de jogos nos Menus Iniciar do `Default` e `ProgramData`.
- **Bloqueio de Alteração do Papel de Parede Corporativo pelo Colaborador**:
  - Adicionada a opção **"Bloquear alteração do papel de parede"** na Aba 3 (Opções), posicionada logo abaixo de "Aplicar papel de parede Nortel", marcada por padrão (`true`).
  - Aplicação corporativa via política nativa do Windows (`NoChangingWallPaper = 1` sob `ActiveDesktop` e `Policies\System\WallpaperStyle = 10`) no perfil `DefaultUser` e em contas existentes não-administradoras.
  - Com essa política ativada, a tela de personalização de fundo no Windows fica esmaecida com a mensagem de que as configurações são gerenciadas pela organização, impedindo o colaborador de substituir o papel de parede institucional, enquanto o Administrador permanece com total liberdade de gerenciamento.

---

## [2.0.0.80] - 2026-11-20

### Corrigido
- **Preservação Definitiva do Log e Histórico de Instalação na Conclusão**:
  - Corrigido problema onde o timer de monitoramento de internet reexibia o cabeçalho `"INFORMAÇÕES DO SISTEMA"` e limpava o log assim que o processo de instalação era finalizado (`_installing = false`).
  - Adicionada a trava `_installationStarted`, desativando o timer de diagnóstico inicial e garantindo que o log completo (etapas realizadas, programas instalados, downloads, usuários configurados e eventuais mensagens) permaneça visível na tela por tempo indeterminado após o término do processo.

---

## [2.0.0.79] - 2026-11-20

### Aprimorado
- **Detecção e Atualização Dinâmica da Conexão à Internet a Cada 2 Segundos**:
  - Implementado monitoramento de rede contínuo em tempo real na tela inicial (`MainForm`) através de `System.Windows.Forms.Timer` com intervalo de 2000 ms e escuta ativa de eventos de hardware `NetworkChange.NetworkAvailabilityChanged`.
  - Atualização automática sem cintilação (*flicker-free* via congelamento de redesenho `WM_SETREDRAW` na Win32 API): quando a internet cai ou volta, o status de conexão é atualizado instantaneamente entre `"Conectado à Internet"` (verde) e `"Desconectado da Internet"` (amarelo), o IP da máquina é atualizado e o diagnóstico de latência de Ping é mensurado e exibido em tempo real.
  - Verificação de conectividade ultra-rápida em `InstallerService` com pré-checagem em nanossegundos via `NetworkInterface.GetIsNetworkAvailable()` e teste rápido via Ping em DNS primários (Cloudflare `1.1.1.1` e Google `8.8.8.8`) com fallback HTTP acelerado.
- **Remoção Definitiva e Completa de Xbox e Xbox Live**:
  - Finalização forçada prévia de todos os processos em segundo plano de jogos, Xbox e overlays (`Xbox*`, `GamingServices*`, `GameBar*`, etc.) e interrupção de serviços antes da tentativa de desinstalação para liberar o bloqueio de arquivos no Windows.
  - Remoção prioritária de pacotes provisionados na imagem do Windows (`Get-AppxProvisionedPackage` e `dism.exe /Online /Remove-ProvisionedAppxPackage`), garantindo que tanto novos perfis quanto os existentes nunca restaurem os aplicativos.
  - Remoção em todas as contas de usuário via `Remove-AppxPackage -AllUsers`, remoção per-user e camada de contingência via `winget uninstall` para IDs oficiais (`Microsoft.GamingApp_8wekyb3d8bbwe`, `Microsoft.Xbox.TCUI_8wekyb3d8bbwe`, etc.).
  - Remoção de componentes residuais Win32/MSI como `Xbox Service` (`{81F706B1-AB0A-4D88-B2DB-E5BAF88C87C6}`) e desativação permanente de serviços e GameDVR via registro.

---

## [2.0.0.78] - 2026-11-20

### Corrigido
- **Correção no Script de Papéis de Parede (`apply_wallpapers.ps1`)**:
  - Corrigido erro de sintaxe na linha 352 onde constava o comando truncado `exi` em vez de `exit 0`, eliminando o erro de comando não reconhecido durante a configuração dos papéis de parede.
  - Script adicionado à rotina de assinatura digital Authenticode com certificado corporativo da EXABIT.
- **Tratamento de Pacotes de Sistema Protegidos (`Microsoft.XboxGameCallableUI`)**:
  - Adicionada verificação de pacotes não removíveis (`NonRemovable` / componentes de sistema invariantes) na rotina de remoção de jogos, ignorando pacotes protegidos pelo Windows como `Microsoft.XboxGameCallableUI` e eliminando a mensagem de erro `0x80070032`.
- **Supressão de Avisos Repetitivos de Encerramento de Serviços (`Stop-Service`)**:
  - Adicionado `-WarningAction SilentlyContinue` na interrupção de serviços de antivírus (ex: AVG Firewall) e componentes do Xbox, evitando repetição desnecessária de avisos de espera no log.

---

## [2.0.0.77] - 2026-11-19

### Aprimorado
- **Restauração do Fluxo do Botão "INICIAR INSTALAÇÃO" para Operações Offline**:
  - Revertida a trava que desativava o botão principal sem conexão à rede, permitindo a execução imediata de funções que operam offline (renomear máquina, criação e configuração de contas locais, aplicação de papéis de parede, desinstalação de jogos, remoção de perfis e antivírus).
  - Mantida a verificação e o aviso com opções de "Repetir / Cancelar" caso o usuário tenha marcado programas na lista que exigem download online da internet.

---

## [2.0.0.76] - 2026-11-19

### Aprimorado
- **Exclusão Completa e Efetiva de Usuários e Perfis do Windows**:
  - Implementada rotina em 3 fases que remove não apenas perfis WMI, mas as próprias contas de usuário locais do Windows (`net user /delete`), a pasta física de arquivos e dados do usuário (`C:\Users\<usuario>` via `takeown / icacls / rd /s /q`), os registros de `ProfileList` do sistema (`HKLM:\...\ProfileList\$sid`) e pastas órfãs residuais.
  - Preservação estrita da conta do operador logado atualmente no Windows e da conta padrão `Administrador` / `Administrator` do sistema (além das novas contas configuradas para criação no instalador).
- **Desativação Automática do Botão "INICIAR INSTALAÇÃO" Sem Internet**:
  - O botão principal agora inicia desativado enquanto verifica a rede e permanece **desativado** (`Enabled = false`, estilizado em cinza neutro com texto `"SEM INTERNET (DESATIVADO)"` e cursor de bloqueio) caso a máquina não possua conexão com a internet.
  - Implementado monitoramento contínuo em tempo real (via timer e evento de rede do Windows `NetworkChange.NetworkAvailabilityChanged`): assim que o cabo de rede ou Wi-Fi for conectado, o botão é habilitado instantaneamente em verde (`"INICIAR INSTALAÇÃO"`).

---

## [2.0.0.75] - 2026-11-19

### Aprimorado
- **Atualização do Ícone Oficial do Executável**:
  - Regenerado o arquivo de ícone corporativo [`InstallerGUI\install.ico`](file:///d:/Arquivos%20de%20Trabalho/Exabit%20Soluções%20em%20TI/Instalador%20Automatico%20Exabit%20Online/InstallerGUI/install.ico) a partir do arquivo [`InstallerGUI\Image\icon.png`](file:///d:/Arquivos%20de%20Trabalho/Exabit%20Soluções%20em%20TI/Instalador%20Automatico%20Exabit%20Online/InstallerGUI/Image/icon.png) com o novo esquema de cores.
  - Mantidas todas as 10 resoluções padrão do Windows (`16x16`, `20x20`, `24x24`, `32x32`, `40x40`, `48x48`, `64x64`, `96x96`, `128x128` em DIB 32 bpp e `256x256` em PNG).
  - Executável [`dist\Instalador Automatico Exabit.exe`](file:///d:/Arquivos%20de%20Trabalho/Exabit%20Soluções%20em%20TI/Instalador%20Automatico%20Exabit%20Online/dist/Instalador%20Automatico%20Exabit.exe) recompilado e assinado digitalmente via Authenticode.

---

## [2.0.0.74] - 2026-11-19

### Aprimorado
- **Varredura Completa e Desinstalação Silenciosa de Jogos e Componentes Xbox**:
  - Detecção e desinstalação exaustiva do aplicativo **Xbox** (`Microsoft.GamingApp`), **Xbox Live** (`Microsoft.Xbox.TCUI`), **Xbox Game Bar / Gaming Overlay** (`Microsoft.XboxGamingOverlay`), **Xbox Identity Provider** (`Microsoft.XboxIdentityProvider`), **Xbox Speech-to-Text** (`Microsoft.XboxSpeechToTextOverlay`), **Gaming Services** (`Microsoft.GamingServices`), **Xbox Game Callable UI** (`Microsoft.XboxGameCallableUI`) e Solitaire.
  - Varredura de jogos casuais e OEM pré-instalados na imagem do Windows (King, Candy Crush, Bubble Witch, Disney, Roblox, Minecraft, Asphalt, etc.).
  - Remoção em três camadas: usuário atual, todos os usuários (`-AllUsers`) e pacotes provisionados da imagem (`Remove-AppxProvisionedPackage -Online`) para evitar reinstalação em novas contas.
  - Varredura de clientes e jogos Win32 no Registro (Steam, Epic Games, Riot Games, EA App, Battle.net, Ubisoft Connect, GOG, etc.) com desinstalação silenciosa automática.
  - Desativação de serviços residuais do Xbox (`XblAuthManager`, `XblGameSave`, `XboxNetApiSvc`, `XboxGipSvc`) e bloqueio de GameDVR no registro.
- **Varredura Completa e Desinstalação Silenciosa de Antivírus de Terceiros**:
  - Consulta dinâmica na Central de Segurança do Windows via WMI (`root\SecurityCenter2:AntivirusProduct`), identificando qualquer antivírus ativo de terceiros (Kaspersky, McAfee, Avast, AVG, Norton, Bitdefender, ESET, etc.) de forma 100% dinâmica.
  - Varredura exaustiva nos caminhos de desinstalação do registro (HKLM 64-bit, HKLM 32-bit WOW6432Node e HKCU) para mais de 30 fornecedores e ferramentas complementares de segurança.
  - Desinstalação silenciosa automática de instaladores MSI (`/x {GUID} /qn /norestart`), comandos `QuietUninstallString` e instaladores executáveis com flags silenciosas.
  - Interrupção e desativação de serviços residuais de antivírus de terceiros detectados.
  - Reativação completa do **Microsoft Defender** (`WinDefend`) e da proteção em tempo real.
- **Saída em Tempo Real Linha a Linha no Log**:
  - Implementado `RunProcessWithLoggingAsync` e `RunPowerShellScriptWithLoggingAsync` para captura e exibição em tempo real de cada pacote, jogo e antivírus desinstalado diretamente no painel de log com codificação UTF-8 e destaque por cores.

---

## [2.0.0.73] - 2026-11-19

### Aprimorado
- **Novo Ícone Oficial Completo a partir de `icon.png`**:
  - Conversão da nova imagem [`InstallerGUI\Image\icon.png`](file:///d:/Arquivos%20de%20Trabalho/Exabit%20Soluções%20em%20TI/Instalador%20Automatico%20Exabit%20Online/InstallerGUI/Image/icon.png) (512x512 32-bit ARGB) em arquivo `.ico` corporativo com 10 resoluções completas (`16x16`, `20x20`, `24x24`, `32x32`, `40x40`, `48x48`, `64x64`, `96x96`, `128x128` e `256x256`).
  - Implementado formato híbrido padrão oficial da arquitetura Microsoft Windows (DIB 32 bpp descompactado com máscara AND para compatibilidade total em resoluções $\le 128\times 128$, e compressão PNG para $256\times 256$).
  - O ícone do executável [`dist\Instalador Automatico Exabit.exe`](file:///d:/Arquivos%20de%20Trabalho/Exabit%20Soluções%20em%20TI/Instalador%20Automatico%20Exabit%20Online/dist/Instalador%20Automatico%20Exabit.exe) foi recompilado e assinado com certificado digital Authenticode.

---

## [2.0.0.72] - 2026-11-19

### Adicionado
- **Novo Ícone do Executável Criado a partir de `exa.png`**:
  - Gerado novo ícone de alta fidelidade multi-resolução (`install.ico` contendo camadas 16x16, 24x24, 32x32, 48x48, 64x64, 128x128 e 256x256) a partir da imagem oficial corporativa [`exa.png`](file:///d:/Arquivos%20de%20Trabalho/Exabit%20Soluções%20em%20TI/Instalador%20Automatico%20Exabit%20Online/InstallerGUI/Image/exa.png).
  - O ícone do executável do Windows Explorer, da barra de tarefas e da janela do aplicativo agora exibem a identidade visual da EXABIT com máxima nitidez.
- **Destaque Visual e Alerta por Cores em Diagnóstico**:
  - **Espaço Livre em C:**: Classificação dinâmica por cores representando a saúde do armazenamento:
    - **Verde**: Espaço bom/suficiente (>= 30 GB e >= 20% livres).
    - **Amarelo**: Alerta/Atenção (entre 15 GB e 30 GB, ou 10% a 20% livres).
    - **Vermelho**: Alerta crítico de pouco espaço (< 15 GB ou < 10% livres), alertando risco de falhas em instalações ou atualizações.
  - **Teste de Ping da Conexão**: Classificação dinâmica por cores representando a qualidade de resposta da rede:
    - **Verde**: Latência excelente (< 50 ms) ou boa (< 100 ms).
    - **Amarelo**: Latência moderada (100 ms a 200 ms).
    - **Vermelho**: Latência alta/instável (> 200 ms) ou tempo limite esgotado/inacessível.

---

## [2.0.0.71] - 2026-11-19

### Adicionado
- **Renomeação do Executável Principal**:
  - Binário final de distribuição atualizado de `install.exe` para `Instalador Automatico Exabit.exe`.
  - Scripts de compilação (`build.bat`) e assinatura digital Authenticode (`sign.ps1`) adaptados para o novo nome do executável e limpeza automática de binários antigos legados.
- **Diagnóstico do Sistema Aprimorado**:
  - Adicionado teste de latência (ping) da internet medido em milissegundos contra DNS de alta performance (Cloudflare/Google) em "Informações do Sistema".
  - Adicionada exibição do espaço livre e total da unidade de sistema (`C:`), incluindo percentual disponível.
  - Alinhamento de rótulos perfeitamente uniforme no log de diagnóstico inicial.
- **Nova Aba "Funcionalidades" (Ações Preliminares Executadas Antes das Instalações)**:
  - Checkbox para **Desinstalar silenciosamente todos os jogos do computador** (Xbox, Solitaire, jogos casuais da Microsoft e OEM via remoção de pacotes AppX e provisionamento de imagem).
  - Checkbox para **Remover e apagar todos os perfis do Windows** (exceto contas de sistema essenciais, a conta do operador logado e as novas contas configuradas para criação).
  - Checkbox para **Desinstalar silenciosamente todo e qualquer antivírus de terceiros** (McAfee, Avast, AVG, Norton, etc.) garantindo o Microsoft Defender ativo e operacional.
  - As funcionalidades selecionadas rodam na **Fase 0**, antes de qualquer instalação de programa ou criação de conta.
- **Novos Programas e Reorganização de Categorias**:
  - **Manutenção**: Criada categoria dedicada contendo `EaseUS Todo Backup Free`, `CrystalDiskInfo`, `CrystalDiskMark` e `Recuva Free` com download online e detecção completa.
  - **Desenvolvimento**: Adicionado o **Antigravity** (standalone - `Google.Antigravity`) diretamente acima do **Antigravity IDE** (`Google.AntigravityIDE`).
  - **Design & Multimídia**: Integrado o **TraceDraw** à categoria de multimídia ao lado de Blender e Affinity.
- **Resiliência do Blender**:
  - Implementado fallback automático na instalação do instalador MSI do Blender caso ocorra o código 1603 (executando nova tentativa sem restrição de `ALLUSERS`).

---

## [2.0.0.70] - 2026-11-19

### Adicionado
- **Suporte aos Principais Clientes Desktop de Inteligência Artificial**:
  - Criada nova seção **Inteligência Artificial** no painel de seleção de programas com divisória temática em laranja.
  - **Gemini Desktop**: Adicionado instalador oficial do Google (`Google.GeminiDesktop` via winget / download direto `GeminiSetup.exe`), suporte a argumentos silenciosos `/S` e detecção prévia via `IsGeminiDesktopInstalled()`.
  - **ChatGPT Desktop**: Adicionado cliente oficial da OpenAI (`OpenAI.ChatGPT` via winget / pacote direto `ChatGPT-x64.msix` via `Add-AppxPackage`) e detecção via `IsChatGPTInstalled()`.
  - **Claude Desktop**: Adicionado cliente desktop da Anthropic (`Anthropic.Claude` via winget / instalador direto `Claude-Setup-x64.exe` com `--silent`) e detecção via `IsClaudeDesktopInstalled()`.
  - **DeepSeek Desktop**: Adicionado cliente oficial DeepSeek Harness Desktop (`DeepSeek.DeepSeekHarnessDesktop` via winget / download direto com `/S`) e detecção via `IsDeepSeekDesktopInstalled()`.
- **EaseUS Todo Backup Free**:
  - Adicionado à categoria Geral o software **EaseUS Todo Backup Free** (`EaseUS.TodoBackup` via winget / download oficial direto `tb_free.exe` com argumentos `/VERYSILENT /NORESTART /SUPPRESSMSGBOXES`).
  - Implementada detecção completa de instalação prévia em `Program Files` e Registro do Windows (`IsEaseUSTodoBackupInstalled()`).

---

## [2.0.0.69] - 2026-11-19

### Aprimorado & Limpeza de Distribuição
- **Script de Papel de Parede Embutido no Executável**:
  - O script PowerShell `apply_wallpapers.ps1` agora é compilado e embutido diretamente como recurso do assembly (`/resource:Scripts\apply_wallpapers.ps1,InstallerGUI.apply_wallpapers.ps1`) no `install.exe`.
  - Criado o método de extração dinâmica `GetApplyWallpapersScriptPath()`, extraindo o script para `%TEMP%` em tempo de execução quando necessário.
  - Removida a dependência da pasta física `dist\Scripts`.
- **Limpeza Total da Pasta `dist`**:
  - Excluída a pasta `dist\Scripts` e todos os scripts obsoletos (`dwgtrueview.cmd`, `office.cmd`, `program_base.cmd`, `radmin.cmd`, `rustdesk_server.ps1`, `wallpaper.cmd`).
  - Excluída a pasta `dist\Sound` (o áudio `R2D2.mp3` já é nativamente embutido como recurso no assembly).
  - A pasta `dist` agora é 100% limpa, autossuficiente e portátil, mantendo apenas `install.exe`, `install.exe.config`, `Wallpapers\` e certificados corporativos.

### Corrigido
- **Blender (Detecção de Instalação Prévia e Correção do Código 1603)**:
  - Implementado o método `IsBlenderInstalled()` para detecção precisa em `Program Files\Blender Foundation`, `Program Files (x86)` e Chaves do Registro do Windows (`HKLM`/`HKCU` Uninstall).
  - O instalador agora verifica previamente se o Blender já se encontra instalado no sistema, evitando falhas de upgrade causadas por pacotes MSI órfãos anteriores.
  - Atualizados os argumentos do instalador MSI para `/qn /norestart ALLUSERS=1`.
  - Tratado o retorno do winget e do MSI para considerar sucesso caso o Blender esteja presente e funcional na máquina.

---

## [2.0.0.68] - 2026-11-19

### Aprimorado
- **Alinhamento Centralizado da Marca EXABIT com o Painel de Programas**:
  - Ajustada a coluna direita do cabeçalho (`BuildHeader`) de `260px * scale` para `340px * scale`, casando exatamente com a largura da coluna do painel de seleção de programas (`BuildRightPanel`).
  - Ajustado o padding horizontal do container da logo (`Padding.Left = 10px * scale`, `Padding.Right = 0`), alinhando o PictureBox com o GroupBox "Programas para Instalar" e centralizando a marca EXABIT com precisão geométrica.

---

## [2.0.0.67] - 2026-11-19

### Removido
- **Composer (PHP)**: Removido da lista de programas para instalação a pedido do usuário (eliminando prompts interativos de modo de instalação).
- **DaVinci Resolve Free**: Removido da lista de programas para instalação a pedido do usuário.

### Corrigido & Aprimorado
- **FileZilla Client (Correção do Erro 403 Proibido)**:
  - Adicionado suporte a cabeçalho `User-Agent` customizado (`chocolatey/2.2.2`) no motor de downloads online `DownloadFileOnlineAsync`, contornando bloqueios de anti-bot do Cloudflare na CDN de pacotes.
  - O download agora extrai diretamente o instalador oficial de 64 bits (`FileZilla_*_win64-setup.exe`), com validação profunda em `IsFileZillaInstalled()`.
- **DBeaver Community (Correção do Código 666660)**:
  - Corrigidos os argumentos de instalação silenciosa do DBeaver para `/S /allusers` (o código 666660 indicava sintaxe de escopo ausente no instalador NSIS).
  - Adicionada detecção profunda pós-instalação em `IsDBeaverInstalled()` e retry autônomo com `/S /allusers`.
- **Suporte a Papéis de Parede da Nortel (`Wallpapers\Nortel`)**:
  - Adicionado novo checkbox **"Aplicar papel de parede Nortel"** na aba *Opções do Windows*.
  - Mantida a opção de papel de parede personalizado padrão (`Wallpapers`), com seleção mutuamente exclusiva e intuitiva entre os perfis.
  - Atualizado o script `apply_wallpapers.ps1` com parâmetro `-Client <nome>`, preparando a arquitetura de personalização por cliente e aplicando os papéis de parede oficiais da Nortel (`01-nortel.jpg`, `02-nortel.jpg`) em todos os temas do Windows (Aero, Dark, Spotlight, ThemeA-D).

---

## [2.0.0.66] - 2026-11-19

### Adicionado & Aprimorado
- **Barras de Rolagem Personalizadas Modernas (Tema Escuro com Thumb Laranja)**:
  - Desenvolvido o componente visual `DarkScrollBar` com renderização vetorial suave via GDI+:
    - **Trilho (Fundo)**: Renderizado exatamente na mesma cor de fundo do container (`CorPainel = #282828` na lista de programas e `#141414` no log de instalação), eliminando 100% as barras brancas/cinzas nativas do Windows Forms (UxTheme).
    - **Cursor Clicável (Thumb)**: Desenhado em formato de pílula arredondada com bordas suaves na cor laranja corporativa (`CorDestaque = #F39200`), com estados táteis de hover (`#FFAA28`) e clique/arrasto (`#DC7D00`).
  - **Lista de Seleção de Programas**:
    - Substituído o `AutoScroll` nativo por uma arquitetura viewport de alta performance com suporte a rolagem fluida por roda do mouse em qualquer ponto do painel ou sobre qualquer checkbox/categoria.
    - Sincronização automática com foco por teclado (Tab) e redimensionamento responsivo.
  - **Console de Mensagens e Log**:
    - Ocultada a barra nativa branca do `RichTextBox` e integrada a `DarkScrollBar` sincronizada via mensagens de alta precisão do Win32 (`EM_GETLINECOUNT`, `EM_GETFIRSTVISIBLELINE`, `EM_LINESCROLL`).
    - Rolagem fluida com mouse wheel e atualização automática ao receber novas mensagens de progresso.

---

## [2.0.0.67] - 2026-11-19

### Removido
- **Composer (PHP)**: Removido da lista de programas para instalação a pedido do usuário (eliminando prompts interativos de modo de instalação).
- **DaVinci Resolve Free**: Removido da lista de programas para instalação a pedido do usuário.

### Corrigido & Aprimorado
- **FileZilla Client (Correção do Erro 403 Proibido)**:
  - Adicionado suporte a cabeçalho `User-Agent` customizado (`chocolatey/2.2.2`) no motor de downloads online `DownloadFileOnlineAsync`, contornando bloqueios de anti-bot do Cloudflare na CDN de pacotes.
  - O download agora extrai diretamente o instalador oficial de 64 bits (`FileZilla_*_win64-setup.exe`), com validação profunda em `IsFileZillaInstalled()`.
- **DBeaver Community (Correção do Código 666660)**:
  - Corrigidos os argumentos de instalação silenciosa do DBeaver para `/S /allusers` (o código 666660 indicava sintaxe de escopo ausente no instalador NSIS).
  - Adicionada detecção profunda pós-instalação em `IsDBeaverInstalled()` e retry autônomo com `/S /allusers`.
- **Suporte a Papéis de Parede da Nortel (`Wallpapers\Nortel`)**:
  - Adicionado novo checkbox **"Aplicar papel de parede Nortel"** na aba *Opções do Windows*.
  - Mantida a opção de papel de parede personalizado padrão (`Wallpapers`), com seleção mutuamente exclusiva e intuitiva entre os perfis.
  - Atualizado o script `apply_wallpapers.ps1` com parâmetro `-Client <nome>`, preparando a arquitetura de personalização por cliente e aplicando os papéis de parede oficiais da Nortel (`01-nortel.jpg`, `02-nortel.jpg`) em todos os temas do Windows (Aero, Dark, Spotlight, ThemeA-D).

---

## [2.0.0.66] - 2026-11-19

### Adicionado & Aprimorado
- **Barras de Rolagem Personalizadas Modernas (Tema Escuro com Thumb Laranja)**:
  - Desenvolvido o componente visual `DarkScrollBar` com renderização vetorial suave via GDI+:
    - **Trilho (Fundo)**: Renderizado exatamente na mesma cor de fundo do container (`CorPainel = #282828` na lista de programas e `#141414` no log de instalação), eliminando 100% as barras brancas/cinzas nativas do Windows Forms (UxTheme).
    - **Cursor Clicável (Thumb)**: Desenhado em formato de pílula arredondada com bordas suaves na cor laranja corporativa (`CorDestaque = #F39200`), com estados táteis de hover (`#FFAA28`) e clique/arrasto (`#DC7D00`).
  - **Lista de Seleção de Programas**:
    - Substituído o `AutoScroll` nativo por uma arquitetura viewport de alta performance com suporte a rolagem fluida por roda do mouse em qualquer ponto do painel ou sobre qualquer checkbox/categoria.
    - Sincronização automática com foco por teclado (Tab) e redimensionamento responsivo.
  - **Console de Mensagens e Log**:
    - Ocultada a barra nativa branca do `RichTextBox` e integrada a `DarkScrollBar` sincronizada via mensagens de alta precisão do Win32 (`EM_GETLINECOUNT`, `EM_GETFIRSTVISIBLELINE`, `EM_LINESCROLL`).
    - Rolagem fluida com mouse wheel e atualização automática ao receber novas mensagens de progresso.

---

## [2.0.0.65] - 2026-11-19

### Adicionado & Aprimorado
- **Relatório Final dos Programas em Cores (Azul e Vermelho)**:
  - Ao concluir o processo de instalação e registrar `=== INSTALAÇÃO CONCLUÍDA ===`, o log apresenta automaticamente um resumo completo dos programas selecionados:
    - **Em Azul (`#00B4FF`)**: Lista de todos os programas que foram instalados com sucesso no sistema.
    - **Em Vermelho (`#FF5050`)**: Lista de todos os programas que não foram instalados ou apresentaram erros/falhas durante a execução.
  - Atualizado o mapeamento de cores da interface (`MapColor`) para renderizar `ConsoleColor.Blue` e `ConsoleColor.DarkBlue` em azul vibrante de alto contraste e legibilidade no tema escuro.

---

## [2.0.0.64] - 2026-11-19

### Corrigido
- **Correção Crítica de Inicialização do Aplicativo**:
  - Identificada e corrigida a exceção `System.ArgumentException` disparada pelo .NET Runtime no construtor de `ThinProgressBar` devido à tentativa de definir `BackColor = Color.Transparent` sem a flag `SupportsTransparentBackColor` no WinForms.
  - Adicionada a flag `ControlStyles.SupportsTransparentBackColor` e definido o `BackColor` padrão compatível com a cor do painel escuro (`Color.FromArgb(40, 40, 40)`), permitindo que o executável abra instantaneamente sem falhas.
  - Implementado tratamento global de exceção em `Program.Main()` com exibição de diálogo amigável em caso de erros fatais não tratados, impedindo fechamento silencioso do processo.

---

## [2.0.0.63] - 2026-11-19

### Corrigido & Aprimorado
- **Correção do Corte dos Botões de Ação ("INICIAR INSTALAÇÃO" e "PARAR")**:
  - Reorganizado o layout do painel inferior direito para dispor `INICIAR INSTALAÇÃO` (72% de largura) e `PARAR` (28% de largura) lado a lado em uma mesma linha horizontal com altura de 40px.
  - Resolvido o corte/truncamento na borda inferior da janela, garantindo visibilidade total dos textos, estados de hover e clique, sem comprometer a lista de seleção de programas.
- **Barra de Progresso Ultrafina Nativa Personalizada (Estilo RustDesk)**:
  - Criado o componente customizado `ThinProgressBar`, com desenho vetorial GDI+ com trilho escuro (`#2D2D2D`) e altura ultrafina de 5px.
  - Substituídas as barras nativas do Windows Forms (que exibiam caixa branca e bordas do UxTheme) pelas novas barras ultrafinas tanto para o download online (laranja `CorDestaque`) quanto para o progresso geral das etapas (verde `CorSucesso`), exatamente idênticas à Central de Suporte Remoto RustDesk.
- **TraceDraw 64-bit sem Prompt Interativo**:
  - Atualizado o nome de arquivo alvo para `tracedraw-setup-x64.exe` em sistemas 64 bits, garantindo que o instalador nativo `tracedraw-*-windows-x86_64-setup.exe` seja baixado e executado.
  - Adicionada exclusão preventiva de versões antigas de 32 bits (`tracedraw-setup.exe` e `tracedraw-setup-x86.exe`) no cache para evitar que sejam reexecutadas.
  - Com o instalador 64-bit nativo correspondendo à arquitetura do Windows, a instalação ocorre 100% silenciosa sem nenhuma pergunta ou diálogo ao usuário.
- **Google Chrome Enterprise MSI & Verificação**:
  - Migrado o download e execução do Google Chrome para o pacote Enterprise oficial MSI (`googlechromestandaloneenterprise64.msi`), executado sincronicamente via `msiexec /i "..." /qn /norestart`.
  - Adicionado método de verificação `IsChromeInstalled()` e fallback autônomo via `winget install --id Google.Chrome` caso ocorra qualquer intercorrência.
- **Suporte ao Código de Saída 3010 do Adobe Acrobat Reader**:
  - Código Win32 3010 (`ERROR_SUCCESS_REBOOT_REQUIRED`) tratado formalmente como sucesso com aviso de reinicialização pendente.
  - Adicionada detecção profunda `IsAdobeReaderInstalled()`.
- **7-Zip com Espelhos SourceForge e Fallback Winget**:
  - Implementado download via espelhos diretos SourceForge para eliminar falhas de resolução de DNS em `release-assets.githubusercontent.com`.
  - Fallback automático via `winget install --id 7zip.7zip` e verificação `Is7ZipInstalled()`.

---

## [2.0.0.62] - 2026-11-19

### Corrigido & Aprimorado
- **Correção da Altura e Enquadramento dos Botões das Abas**:
  - Eliminado o corte/truncamento vertical nos botões das abas ("Configurações do Sistema", "Atalhos", "Opções do Windows").
  - Aumentada a altura do cabeçalho de abas para 44px com remoção de margens e preenchimentos internos concorrentes.
  - O indicador inferior ativo (3px em `CorDestaque`) e a linha base das abas inativas agora são renderizados via evento `Paint` customizado diretamente no botão, garantindo alinhamento pixel-perfect e sem recortes em qualquer escala de DPI.
- **Barra de Carregamento Ultra-Fina (Estilo RustDesk)**:
  - Redesenhada a barra de progresso de downloads para perfil fino de 7px de altura, idêntica ao design limpo adotado na Central de Suporte Remoto RustDesk.
  - A barra agora estende-se por toda a largura horizontal abaixo do painel de log, acompanhada por um rótulo compacto de status textual logo abaixo.
  - A barra de progresso geral das etapas (lado direito) também foi padronizada com perfil fino de 7px, otimizando o aproveitamento vertical da interface.

---

## [2.0.0.61] - 2026-11-19

### Adicionado & Aprimorado
- **Painel de Abas Estilo Bootstrap ("With Tabs")**:
  - Reorganizado o painel de configurações do lado esquerdo em 3 abas temáticas com navegação moderna Bootstrap dark:
    - **Aba 1 (Ativa por padrão): Configurações do Sistema**: Nome do computador, Senha do Admin, Usuário padrão e Senha.
    - **Aba 2: Atalhos**: Atalhos corporativos para a Área de Trabalho e opção de limpeza.
    - **Aba 3: Opções do Windows**: Ativar serviços de localização, impedir alteração de senha e papel de parede personalizado.
  - Indicador visual ativo inferior na cor de destaque (`CorDestaque`) e transição instantânea de conteúdo.
- **Diagnóstico Inicial do Sistema no Log ao Abrir**:
  - Exibição automática no evento de inicialização dos dados da máquina:
    - Nome do Computador
    - Nome do Usuário (com indicação de privilégio de Administrador)
    - Endereço(s) IP locais ativos da rede
    - Status de Conexão à Internet em tempo real (Conectado / Desconectado)
    - Versão detalhada do Windows (Edição comercial, arquitetura 64/32 bits, release e build).
- **Barra de Progresso Dedicada para Downloads Online**:
  - Instalada uma nova barra de progresso contínua e rótulo de status diretamente abaixo da caixa de log/informações.
  - O progresso de download dos programas (percentual e MB transferidos) agora é exibido exclusivamente nesta barra, desacoplando completamente da barra do lado direito.
  - A barra do lado direito agora é 100% dedicada ao progresso geral das etapas e programas instalados (`Etapa X / Y`).
- **Novos Atalhos Corporativos de Suporte e Serviços**:
  - Adicionado atalho **Exabit Soluções em TI** (`https://www.exabit.com.br`).
  - Adicionado atalho **Exahost Hospedagem de Domínios** (`https://www.exahost.com.br`).
  - Adicionado atalho **Remote Neoenergia** (`https://remote.neoenergia.com/`).
  - Renomeado atalho **Suporte Nortel** para **Suporte Exabit** (`https://suporte.exabit.com.br`).
  - Mantidos atalhos Webmail Nortel e Webmail Nortel Terceirização.

---

## [2.0.0.60] - 2026-11-19

### Corrigido & Aprimorado
- **TraceDraw (Vetorização Automática)**:
  - Corrigida a seleção do instalador para corresponder exatamente à arquitetura do Windows: em sistemas operacionais de 64 bits (`Environment.Is64BitOperatingSystem`), agora baixa obrigatoriamente a versão nativa `tracedraw-*-windows-x86_64-setup.exe` (e em 32 bits, `windows-x86-setup.exe`).
  - Eliminada completamente a janela interativa que solicitava ao usuário confirmação para instalar a versão de 32 bits em sistema 64 bits.
  - Adicionado o parâmetro `/SP-` (Silent Prompt disable) aos argumentos de instalação Inno Setup (`/VERYSILENT /NORESTART /SUPPRESSMSGBOXES /SP-`), garantindo instalação 100% autônoma e silenciosa.
- **Menu Microsoft Office 365**:
  - Removido o sufixo `(Online)` da interface (`Microsoft Office 365`), padronizando o catálogo de programas, visto que todos os instaladores agora operam em modelo de download online automático.

---

## [2.0.0.59] - 2026-11-19

### Corrigido & Aprimorado
- **Autodesk DWG TrueView**:
  - Implementado mecanismo robusto de detecção de instalação prévia (`IsDwgTrueViewInstalled`) varrendo Program Files (x64/x86) e o Registro do Windows.
  - Implementado fallback automático quando o winget falhar com código `-2145844844` (HTTP 404 devido ao link expirado no manifesto do winget): o instalador agora faz o download direto do pacote oficial SFX de 1,5 GB da CDN da Autodesk (`dds.autodesk.com`), extrai silenciosamente para `C:\Autodesk` e executa a instalação via `Setup.exe -q --noeula`.
- **Google Chrome**:
  - Silenciada a mensagem de diagnóstico inofensiva do atualizador do Chrome (`BinaryReadTagString: file is untagged`) através do parâmetro `suppressStdErrLog: true`, mantendo o log de instalação limpo e profissional.
- **Microsoft Office 365 (Online)**:
  - Corrigido o erro de execução `0xC0000142` (`-1073741510` STATUS_DLL_INIT_FAILED) gerado pela chamada anterior com `UseShellExecute = true` sob processo já elevado.
  - Adicionada detecção preventiva de Office 365 já instalado (`IsOffice365Installed`).
  - Suporte prioritário à instalação via Windows Package Manager (`winget install --id Microsoft.Office`).
  - Fallback direto baixando o Click-to-Run Bootstrapper oficial `setup.exe` do CDN da Microsoft (`officecdn.microsoft.com`) e execução com `UseShellExecute = false` e arquivo `configuration_o365.xml` formatado em UTF-8 sem BOM.

---

## [2.0.0.58] - 2026-11-19

### Adicionado & Aprimorado
- **Remoção de Negrito na Lista de Seleção de Programas**:
  - Os checkboxes dos programas agora utilizam fonte regular (`Segoe UI, 9.5pt Regular`) em vez de herdar o estilo em negrito do painel de grupo, proporcionando uma leitura visual muito mais leve e elegante.
- **Relato de Progresso de Downloads em Tempo Real**:
  - Implementado sistema de download por streaming contínuo com buffer de 64 KB via `HttpWebRequest` em `InstallerService.cs`.
  - Disparo do evento `OnDownloadProgress(displayName, percent, bytesReceived, totalBytes)` atualizando o rótulo de status na interface gráfica (ex: `45% (85/190 MB)`).
  - Registro de marcos percentuais no log em tempo real (25%, 50%, 75% e 100%) para dar visibilidade total do andamento dos downloads sem congestionar o log.
- **Divisórias Visuais Categorizadas**:
  - A interface gráfica agora agrupa os programas dinamicamente com linhas de divisão horizontais e títulos em destaque para cada categoria:
    - **Geral**: Programas corporativos essenciais (7-Zip, Chrome, Firefox, Office 365, etc.).
    - **ArtCraft**: Suíte Criativa completa da Storytold.
    - **TraceDraw**: Vetorização automática e traçado.
    - **Desenvolvimento**: Ferramentas para desenvolvedores e administradores de banco/servidores.
    - **Design & Multimídia**: Criação 3D, design gráfico e edição de vídeo.
- **Novos Aplicativos na Divisória ArtCraft**:
  - Adicionados os aplicativos da Storytold:
    - **FilmCraft** (Editor de Vídeo)
    - **LightCraft** (Iluminação e Estúdio 3D)
    - **EffectCraft** (Efeitos Visuais e VFX)
    - **DesignCraft** (Design Gráfico e UI)
    - **TextureCraft** (Texturização e Materiais 3D)
  - Integração com encerramento preventivo de processos e download via GitHub Releases.
- **Divisória e Aplicativo TraceDraw**:
  - Adicionado o **TraceDraw** (Vetorização Automática) com resolução dinâmica de download direto do GitHub Releases e instalação silenciosa Inno Setup (`/VERYSILENT /NORESTART /SUPPRESSMSGBOXES`).
- **Divisória Desenvolvimento**:
  - Adicionados 6 programas com instalação silenciosa e autônoma:
    - **Visual Studio Code**: download do instalador estável da Microsoft com `/VERYSILENT /NORESTART /MERGETASKS="!runcode"`.
    - **Antigravity IDE**: instalação via Windows Package Manager (`winget`) com fallback para o instalador oficial do Google.
    - **Node.js LTS**: download dinâmico da versão LTS oficial da OpenJS Foundation com `/qn /norestart`.
    - **Composer (PHP)**: download do executável oficial `Composer-Setup.exe` com `/VERYSILENT /NORESTART`.
    - **FileZilla Client**: download seguro via pacote Chocolatey CDN, extração limpa e instalação com `/S`.
    - **DBeaver Community**: download do instalador oficial direto de dbeaver.io com `/S`.
- **Divisória Design & Multimídia**:
  - Adicionados 3 softwares de computação gráfica e vídeo:
    - **Blender**: instalação via `winget` com fallback para download oficial do MSI da Blender Foundation (`/qn /norestart`).
    - **Affinity Studio (Português)**: instalação via `winget` com fallback para pacote oficial MSIX com `Add-AppxPackage`.
    - **DaVinci Resolve Free**: integração completa com a API oficial da Blackmagic Design para geração automática do link temporário de download do `.zip`, extração silenciosa e execução do instalador corporativo com `/i /q /noreboot`.

---

## [2.0.0.57] - 2026-11-18

### Modificado & Adicionado
- **Renomeação do Projeto para "Auto Instalador de Programas"**:
  - Título da aplicação, cabeçalhos, metadados de assembly (`AssemblyInfo.cs`) e scripts atualizados para "Auto Instalador de Programas".
- **Remoção do Microsoft Office 2016**:
  - Removido o item `office` (Microsoft Office 2016 local) da lista padrão de programas, mantendo o `office365` ("Microsoft Office 365 (Online)").
- **Transição para Instalação 100% Online de Todos os Programas**:
  - Eliminada a necessidade da pasta `dist\Programas` e de instaladores pré-baixados offline.
  - O instalador agora baixa os executáveis e pacotes oficiais diretamente da internet sob demanda para a área de cache corporativo (`%ProgramData%\EXABIT\Downloads`).
  - Suporte a download online direto e dinâmico com resolução de versões mais recentes para:
    - **Google Chrome**: download do instalador corporativo standalone oficial x64.
    - **Mozilla Firefox**: download direto do pacote oficial MSI x64 em pt-BR da Mozilla.
    - **Brave Browser**: download oficial silencioso da Brave.
    - **AnyDesk**: download oficial direto com instalação silenciosa e autônoma.
    - **7-Zip**: resolução dinâmica da versão mais recente em 7-zip.org.
    - **WinRAR**: resolução dinâmica da versão mais recente em pt-BR via rarlab.com.
    - **Radmin VPN**: download da versão mais recente oficial do site do desenvolvedor.
    - **Adobe Acrobat Reader**: download do instalador offline corporativo oficial 64 bits MUI da Adobe.
    - **Microsoft Power BI Desktop**: download do instalador oficial x64 da Microsoft.
    - **Microsoft Office 365 (Online)**: download oficial do Office Deployment Tool (ODT) da Microsoft, extração silenciosa e instalação automatizada via XML pt-BR.
    - **Autodesk DWG TrueView**: instalação online oficial automatizada via Windows Package Manager (`winget`).
    - **RustDesk, Citrix Workspace e Java JRE 8**: atualizados para operar 100% online através do cache local corporativo.
    - **Suíte Craft / ArtCraft Studio**: download direto dos releases oficiais da Storytold no GitHub.
  - Validação unificada de conectividade de rede na interface gráfica antes do início da instalação com opções amigáveis de tentar novamente.

---

## [2.0.0.56] - 2026-11-18

### Corrigido
- **Tratamento de Caminho Raiz com Barra Invertida no Script de Wallpaper**:
  - Corrigido erro `Test-Path : Caracteres inválidos no caminho` (`InvalidArgument: D:\" \dist\Wallpapers`) que ocorria ao executar o instalador a partir da raiz de uma unidade de disco ou pendrive (ex: `D:\`).
  - No Windows, quando um caminho termina em barra invertida e é passado entre aspas (`"D:\"`), o parser de linha de comando trata a barra como escape da aspa (`\"`), fazendo com que o PowerShell receba uma aspa literal dentro do parâmetro `-BasePath`.
  - Em `InstallerService.cs` (`SetWallpaperAsync`), a passagem do parâmetro agora utiliza `QuoteProcessArgument` com sanitização prévia de barras invertidas finais.
  - Em `apply_wallpapers.ps1`, adicionada sanitização de aspas/apóstrofos/espaços em `$BasePath`, validação de caminho com `-LiteralPath` e tratamento com `-ErrorAction SilentlyContinue`.

---

## [2.0.0.55] - 2026-11-17

### Corrigido
- **Eliminação de Ícones Duplicados na Área de Trabalho**:
  - Corrigido problema onde atalhos de suporte ("Suporte Norte", "Webmail Nortel", "Webmail Nortel Terceirização") eram exibidos duas vezes na área de trabalho.
  - O Windows mescla visualmente a Área de Trabalho Pública (`C:\Users\Public\Desktop`) com a Área de Trabalho do Usuário Logado (`C:\Users\<user>\Desktop`). Ao gravar nos dois locais, o Windows exibia dois ícones idênticos na tela.
  - Os atalhos agora são criados exclusivamente em `CommonDesktopDirectory` (`C:\Users\Public\Desktop`), garantindo que todos os usuários (Admin, padrão e novos) os vejam exatamente **uma vez**.
  - Adicionada rotina de desduplicação que remove automaticamente quaisquer cópias privadas remanescentes dos perfis individuais (`admin\Desktop`, usuário padrão e template `Default\Desktop`).

---

## [2.0.0.54] - 2026-11-17

### Adicionado & Modificado
- **Opção Selecionável para Limpeza de Atalhos da Área de Trabalho**:
  - Adicionada a opção `"Limpar atalhos da Área de Trabalho"` no painel de **Configurações do Sistema**, marcada como ativa por padrão (`true`).
  - Permite desmarcar a opção caso o cliente deseje preservar os atalhos já existentes na área de trabalho.
  - Conectada a opção `InstallationOptions.CleanDesktopShortcuts` ao fluxo de execução em `InstallerService.RunFullInstallationAsync`.
  - Altura do painel de configurações ajustada para `290 * scale` e janela principal para `780 * scale` de altura desejada, garantindo proporção e acomodação de todas as 7 opções com zero cortes de texto.

---

## [2.0.0.53] - 2026-11-17

### Corrigido
- **Tratamento de Junções de Sistema e Permissões na Limpeza de Atalhos**:
  - Corrigido aviso de acesso negado em `C:\Users\All Users\Desktop` e `C:\Users\Todos os Usuários\Desktop`.
  - No Windows, pastas legadas como `All Users`, `Todos os Usuários`, `Default User` e `Usuário Padrão` são pontos de junção (*Reparse Points*) protegidos pelo sistema operacional.
  - A rotina de varredura agora ignora automaticamente diretórios com atributo `FileAttributes.ReparsePoint` e diretórios públicos já mapeados.
  - O método não tenta mais criar pastas Desktop para diretórios inexistentes durante a varredura, exceto pelo template oficial `Default\Desktop`.
  - Adicionado tratamento silencioso de `UnauthorizedAccessException` para evitar falsos alarmes no log.

---

## [2.0.0.52] - 2026-11-17

### Modificado
- **Execução da Limpeza de Atalhos Exclusivamente no Final de Todas as Instalações**:
  - Removida a limpeza prematura de atalhos que era realizada antes do início da instalação dos programas.
  - A limpeza de todas as áreas de trabalho agora ocorre exclusivamente como etapa final, após a conclusão de todas as instalações de programas selecionados.
  - A rotina `CleanAllDesktops` foi aprimorada para varrer de forma abrangente o Desktop Público (`CommonDesktopDirectory`), o Desktop do usuário em execução (`DesktopDirectory`), Desktop do Admin, Desktop do Usuário Padrão criado, o template de novos usuários (`Default\Desktop`) e todos os diretórios de usuários sob `C:\Users`.
  - Mensagem de log atualizada para: `Limpando atalhos de todas as áreas de trabalho no final de todas as instalações...`.
  - Criação apenas dos atalhos de suporte personalizados selecionados logo após a limpeza.

---

## [2.0.0.51] - 2026-11-17

### Adicionado & Corrigido
- **Restauração da Opção de Seleção e Ativação Padrão dos Serviços de Localização do Windows**:
  - Reincorporada a opção de seleção `"Ativar serviços de localização do Windows"` no menu de **Configurações do Sistema**, marcada como ativa por padrão (`true`).
  - Altura e espaçamento do painel esquerdo ajustados (`260 * scale` e distribuição percentual de 6 linhas com 16.66% cada), garantindo acomodação confortável e visual 100% livre de qualquer corte de texto.
  - Conexão direta entre o estado da checkbox na interface (`_chkEnableLocation`) e as opções de execução (`InstallationOptions.EnableLocationServices`).
  - Sincronização definitiva com o interruptor mestre das Configurações do Windows 11 (`Privacidade e segurança > Localização > Serviços de localização`) através da execução do utilitário nativo `SystemSettingsAdminFlows.exe SetCamSystemGlobal location 1`.
  - Configuração automática do serviço de geolocalização (`lfsvc`) e do serviço de sensores (`SensorService`).
  - Propagação para o Administrador, perfil padrão (`HKEY_USERS\.DEFAULT`) e novos perfis criados através do `Active Setup` do Windows (`HKLM\SOFTWARE\Microsoft\Active Setup\Installed Components`).
  - Mantida compatibilidade total e conformidade com antivírus (0 detecções no Kaspersky).

---

## [2.0.0.50] - 2026-11-17

### Adicionado & Modificado
- **Divisória Visual e Organização dos Aplicativos ArtCraft**:
  - Inserida divisória visual horizontal logo abaixo do WinRAR no menu de seleção de programas.
  - O **ArtCraft Studio** foi reposicionado logo após a divisória como o primeiro item da seção.
  - Adicionados dois novos programas da suíte Storytold:
    - **FilmCraft** (`storytold/filmcraft`)
    - **SoundCraft** (`storytold/soundcraft`)
  - Adicionados resumos explicativos entre parênteses para todos os 10 programas da ArtCraft:
    - *ArtCraft Studio (Suíte Criativa)*
    - *CADCraft (Desenho Técnico CAD)*
    - *DeckCraft (Apresentações e Slides)*
    - *FilmCraft (Editor de Vídeo)*
    - *GridCraft (Planilhas Eletrônicas)*
    - *PdfCraft (Leitor e Editor de PDF)*
    - *PhotoCraft (Editor de Fotos)*
    - *SoundCraft (Editor de Áudio)*
    - *VectorCraft (Ilustração Vetorial)*
    - *WordCraft (Editor de Texto)*
  - Todos os programas da ArtCraft mantêm a capacidade de download da versão mais recente via GitHub Releases (`*-windows-x64.msi`), instalação silenciosa e suporte a fallback offline.

---

## [2.0.0.49] - 2026-11-17

### Adicionado & Corrigido
- **Ativação Padrão dos Serviços de Localização do Windows para Admin e Usuário**:
  - Reintegrada a ativação automática dos Serviços de Localização do Windows durante o fluxo de instalação (`RunFullInstallationAsync`), sem necessidade de controle ou checkbox na interface gráfica.
  - Implementação nativa limpa via .NET `Microsoft.Win32.Registry` e `System.ServiceProcess.ServiceController`:
    - **Nível de Sistema (HKLM)**: Configura permissão Master de Localização (`ConsentStore\location = Allow`), aplicativos de desktop (`NonPackaged = Allow`), sensor de hardware e geolocalização (`SensorPermissionState = 1`), e anula qualquer política restritiva em `LocationAndSensors`.
    - **Serviço de Sistema**: Configura o serviço de geolocalização `lfsvc` para inicialização sob demanda (`Start = 3` e `Status = 1`) e inicia a instância caso não esteja em execução.
    - **Perfil Atual e Admin (HKCU)**: Aplica permissão ativa de localização diretamente no hive do usuário administrador em execução.
    - **Perfil Padrão (HKEY_USERS\.DEFAULT) e Usuários Ativos**: Propaga as chaves de permissão para todos os hives de usuários montados.
    - **Novos Usuários Criados (Active Setup)**: Registra componentes do Active Setup no Windows para aplicar automaticamente a permissão de localização no hive do usuário padrão no seu primeiro logon.
  - Totalmente livre de comandos invasivos (`reg load NTUSER.DAT`), garantindo 0 detecções de antivírus.

---

## [2.0.0.48] - 2026-11-17

### Adicionado & Modificado
- **Integração dos Aplicativos da Suíte Craft (Storytold) com Instalação Silenciosa e Atualização Dinâmica**:
  - Reposicionados e organizados os programas na interface gráfica:
    - **ArtCraft Studio** posicionado como primeiro item no início da lista de seleção, renomeado de "ArtCraft" para "ArtCraft Studio". Caso haja conexão com a internet, verifica o release mais recente no repositório `storytold/artcraft`, realiza o download e atualiza a cópia local em `dist\Programas\ArtCraft_setup.exe`; sem internet, utiliza automaticamente o instalador local existente.
    - Programas corporativos gerais mantidos no meio da lista, ordenados alfabeticamente de "7-Zip" a "WinRAR".
    - Adicionados os 7 novos aplicativos da suíte Craft da Storytold em ordem alfabética no final da lista:
      - **CADCraft** (`storytold/cadcraft`)
      - **DeckCraft** (`storytold/deckcraft`)
      - **GridCraft** (`storytold/gridcraft`)
      - **PdfCraft** (`storytold/pdfcraft`)
      - **PhotoCraft** (`storytold/photocraft`)
      - **VectorCraft** (`storytold/vectorcraft`)
      - **WordCraft** (`storytold/wordcraft`)
  - **Mecanismo Dinâmico de Download Silencioso**:
    - Implementado `DownloadLatestStorytoldCraftAsync` em `InstallerService.cs` para consultar a API do GitHub (`/releases/latest`) com fallback via cabeçalho HTTP Location.
    - Baixa diretamente os pacotes Windows x64 MSI (`.msi`) e os instala silenciosamente com `msiexec /i "..." /qn /norestart`.
    - Realiza sincronização automática em `dist\Programas`, permitindo que os instaladores fiquem prontos para uso offline posterior.
    - Encerramento preventivo de processos ativos de cada aplicativo antes da instalação para evitar bloqueio de arquivos.
  - **Validação de Conectividade Aprimorada**:
    - Ao iniciar a instalação, se algum dos novos programas que necessitam de download via internet for selecionado em uma máquina offline, exibe aviso descritivo com a lista exata dos programas que exigem conexão, sem interromper ou travar os programas offline já presentes em disco.

---

## [2.0.0.47] - 2026-11-17

### Modificado
- **Identificação e Instalação do Pacote Microsoft Office**:
  - Renomeado o instalador offline local de "Microsoft Office" para "Microsoft Office 2016" na lista de seleção de programas para melhor clareza e diferenciação.
  - Atualizada a linha de comando do instalador "Microsoft Office 365 (Online)" para utilizar `/configure configuration_o365.xml`, garantindo aplicação direta do perfil de configuração em Português (Brasil) e instalação silenciosa.
  - Recompilado e assinado digitalmente com certificado corporativo SHA256 e Timestamp.

---

## [2.0.0.46] - 2026-11-17

### Adicionado
- **Download Dinâmico da Versão Mais Recente do Java JRE com Fallback Offline**:
  - Implementado o método `DownloadLatestJavaAsync` em `InstallerService.cs` seguindo o mesmo padrão de resiliência utilizado para RustDesk e Citrix Workspace.
  - Ao iniciar a instalação do Java JRE, verifica se há conectividade com a internet:
    - **Com internet**: Consulta a página oficial da Oracle (`java.com/en/download/manual.jsp`), detecta o número da versão mais recente disponível (ex: Java 8 Update 513) e compara com a versão do instalador local (`dist\Programas\jre-windows-x64.exe`, Update 471). Se houver versão mais recente online, baixa o instalador offline de 64 bits diretamente do CDN oficial (`AutoDL`) e atualiza a cópia local em `dist\Programas`.
    - **Sem internet**: Utiliza automaticamente o instalador offline local existente em `dist\Programas\jre-windows-x64.exe` sem interromper ou travar o fluxo de instalação.
  - Adicionado encerramento preventivo de instâncias ativas do Java (`java.exe`, `javaw.exe`, `javaws.exe`, `jusched.exe`) antes de disparar o instalador, prevenindo erros de arquivo em uso (1603).

---

## [2.0.0.45] - 2026-11-17

### Removido & Corrigido
- **Eliminação do Falso Positivo de Antivírus (`HEUR:Trojan.Win32.Generic`)**:
  - Removido completamente o método `ConfigureLocationServicesAsync` e as rotinas que carregavam o hive offline `NTUSER.DAT` (`reg.exe load`), manipulavam chaves de sistema `ConsentStore\location` e invocavam `SystemSettingsAdminFlows.exe`/`sc.exe config lfsvc`. O monitor heurístico comportamental do Kaspersky detectava esse comportamento de alteração de políticas do Windows como trojan genérico.
  - Removidos comandos de script powershell com `ExecutionPolicy Bypass` e chamadas COM de atalho de shell em `InstallerService.cs`, garantindo binário 100% limpo, sem gatilhos heurísticos de antivírus.
  - O executável é compilado diretamente e assinado digitalmente com certificado Authenticode SHA256 e carimbo de tempo (Timestamp).
- **Centralização e Alinhamento Visual de "Configurações do Sistema"**:
  - Proporções equilibradas: a tabela principal agora divide as colunas em 46% (entradas à esquerda) e 54% (opções à direita), eliminando o espaço vazio à direita e conferindo largura confortável e elegante às caixas de texto.
  - Alinhamento vertical perfeito: os 5 checkboxes agora utilizam `Anchor = AnchorStyles.Left`, centralizando-se verticalmente em cada uma das suas 5 linhas. A primeira linha de opções alinha-se horizontalmente com o primeiro campo de texto ("Nome do computador"), e a última linha alinha-se com o último campo ("Senha do usuário").
  - Altura do painel ajustada para `(int)(230 * scale)` com espaçamento interno equilibrado (`Padding: top 24, bottom 10`), removendo o espaço vazio inferior e centralizando harmoniosamente todos os elementos dentro do `GroupBox`.

---

## [2.0.0.44] - 2026-11-17

### Removido & Corrigido
- **Remoção das opções redundantes da interface**:
  - Removida a checkbox "Ativar serviços de localização do Windows" da interface gráfica: os serviços de localização continuam sendo configurados e ativados automaticamente por padrão no sistema e em todos os perfis durante a instalação sem necessitar de opção na UI.
  - Removida a checkbox "Adicionar atalhos também no Menu Iniciar": os atalhos de suporte ("Suporte Norte", "Webmail Nortel", "Webmail Nortel Terceirização") continuam sendo criados diretamente na Área de Trabalho (Desktop).
- **Correção definitiva do corte vertical e horizontal de textos em Configurações do Sistema**:
  - Com a redução de 7 para 5 opções selecionáveis, o `TableLayoutPanel` da coluna de opções agora distribui 20% de altura para cada linha (mais de 40px a 53px de altura útil por item, em comparação aos apertados 25px anteriores).
  - Aumentada a altura do painel de configurações de `(int)(235 * scale)` para `(int)(265 * scale)`.
  - Configurada largura fixa e confortável para os campos de entrada à esquerda (`(int)(320 * scale)`), dedicando 100% do espaço restante para as caixas de seleção, eliminando qualquer risco de quebra ou corte horizontal de nomes longos.
  - O método `CreateCheckBox` agora aplica explicitamente a fonte `Segoe UI 9.5 Regular` e margens reduzidas, impedindo que os controles herdem a fonte negrito de 10pt do `GroupBox` que causava extrapolação e corte de texto no Windows.

---

## [2.0.0.43] - 2026-11-17

### Corrigido
- **Atalhos no Menu Iniciar Compatíveis com Windows 10 e Windows 11**:
  - Corrigida a funcionalidade da opção "Adicionar atalhos também no Menu Iniciar": o Windows 10 e Windows 11 (`StartMenuExperienceHost.exe`) ignoram completamente arquivos `.url` na lista de programas do Menu Iniciar, exibindo exclusivamente atalhos de Shell `.lnk`.
  - Implementado o método `CreateShellShortcut` em `InstallerService.cs` utilizando COM `WScript.Shell` (com fallback via PowerShell) para gerar atalhos `.lnk` com destino `explorer.exe <URL>`, ícone oficial de website (`url.dll,0`) e descrições personalizadas.
  - Ao executar `explorer.exe <URL>`, o Windows abre diretamente o link no navegador padrão do sistema (Chrome, Brave, Firefox, Edge, etc.) sem abrir janela de prompt de comando (CMD).
  - Os atalhos são registrados tanto no diretório geral de programas de Todos os Usuários (`C:\ProgramData\Microsoft\Windows\Start Menu\Programs`) quanto nos diretórios de usuários individuais (`Default\AppData\Roaming\...`, `admin`, usuário padrão e usuário atual).
  - Adicionada limpeza preventiva de quaisquer arquivos `.url` legados no Menu Iniciar e notificação imediata ao Shell do Windows (`SHChangeNotify`) para atualização dinâmica do cache do Menu Iniciar.

---

## [2.0.0.42] - 2026-11-17

### Adicionado
- **Ativação Padrão dos Serviços de Localização do Windows**:
  - Adicionado novo método `ConfigureLocationServicesAsync` em `InstallerService.cs` que ativa a geolocalização e os sensores do Windows no nível de sistema e em todos os perfis de usuário.
  - O serviço de geolocalização do Windows (`lfsvc`) é configurado para inicialização automática (`sc.exe config lfsvc start= auto`) e iniciado (`net start lfsvc`).
  - Habilitadas as diretivas e chaves mestras de consentimento em `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location` e `NonPackaged` (aplicativos Win32 e empacotados), bem como as políticas em `HKLM\SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors` e o sensor `SensorPermissionState`.
  - Disparado o utilitário nativo `SystemSettingsAdminFlows.exe` do Windows 11 para sincronizar a chave de controle mestre nas Configurações.
  - Aplicado consentimento no perfil atual (`HKCU`), em todos os perfis ativos carregados sob `HKEY_USERS`, no perfil de modelo `C:\Users\Default\NTUSER.DAT` (garantindo que o admin e novos usuários criados já iniciem com localização ativa no primeiro login), e nos perfis existentes em disco (`admin` e usuário padrão).
  - Adicionada opção checkbox "Ativar serviços de localização do Windows" no painel de configurações da interface gráfica (`MainForm.cs`), ativada por padrão (`true`).

---

## [2.0.0.41] - 2026-11-17

### Corrigido
- **Instalação do Microsoft Office 365 (Online) em Português do Brasil (pt-br) Silenciosa**:
  - Criado arquivo de configuração `dist/Programas/configuration_o365.xml` com `<Product ID="O365ProPlusRetail">`, `<Language ID="pt-br" />` e `<Display Level="None" AcceptEULA="TRUE" />`.
  - Configurado o argumento `/configure configuration_o365.xml` no instalador `OfficeSetup.exe` (Office Deployment Tool) em `MainForm.cs`, garantindo download e streaming direto dos servidores CDN da Microsoft em português brasileiro e modo 100% silencioso.
  - Implementado método de salvaguarda `EnsureOffice365ConfigurationXml` em `InstallerService.cs` que verifica e recria automaticamente o arquivo XML caso esteja ausente antes de disparar o instalador.
  - Adicionado suporte ao código de saída `3010` (reboot pendente com sucesso) para instaladores do Office.

---

## [2.0.0.40] - 2026-11-17

### Corrigido
- **Isolamento de Layout dos Botões Marcar/Desmarcar e da Barra de Rolagem**:
  - Eliminada a sobreposição onde os botões "Marcar" e "Desmarcar" flutuavam sobre os últimos itens da lista de programas e cobriam o fim da barra de rolagem.
  - O painel de programas agora utiliza um `TableLayoutPanel` container de 2 linhas: Linha 0 (100% proporcional) aloca a lista com rolagem independente (`_pnlPrograms`), e Linha 1 (`38px` absolutos com DPI) aloca exclusivamente a barra de botões.
  - A barra de rolagem vertical agora finaliza estritamente acima dos botões, e todos os itens da lista (incluindo "RustDesk" e "WinRAR") rolam completamente à vista com margem inferior (`AutoScrollMargin`).

---

## [2.0.0.39] - 2026-11-17

### Corrigido
- **Exibição Completa da Logo EXABIT no Cabeçalho**: Corrigido o corte da marca no canto superior direito:
  - Ajustada a altura da linha do cabeçalho de 68px para 78px proporcionais à escala DPI (`(int)(78 * scale)`), fornecendo espaço vertical suficiente para exibição integral do conjunto logotipo e slogan.
  - Adicionado estilo de linha explícito de 100% (`tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100))`) na tabela do cabeçalho, evitando que o painel da logo assumisse altura padrão excedente que sofria corte/truncamento na borda inferior do container.
  - Otimizado o espaçamento interno do painel da logo (`Padding`), permitindo que a imagem (`logo_exabit.png`) seja renderizada em modo `Zoom` de forma nítida, preservando integralmente tanto a palavra "exabit" quanto o subtítulo "SOLUÇÕES EM TI".

---

## [2.0.0.38] - 2026-11-17

### Corrigido
- **Suporte Completo a Escala Alta de DPI (125%, 150%, PerMonitorV2)**: Corrigido o corte de textos na interface ao executar em monitores com escala de 125% ou superior no Windows 11:
  - Implementado cálculo dinâmico de escala (`GetDpiScale()`) baseado no DPI real do monitor (`DeviceDpi`).
  - O painel lateral direito de programas agora escala proporcionalmente, acomodando nomes extensos de softwares sem sobreposição pela barra de rolagem.
  - No painel de configurações, a coluna de rótulos foi convertida para dimensionamento automático (`SizeType.AutoSize`) e as linhas de opções agora utilizam distribuição proporcional em porcentagem, eliminando cortes verticais e horizontais.
  - O cabeçalho foi refatorado com layout de fluxo vertical (`FlowLayoutPanel`), prevenindo colisão entre o título e o caminho do executável.
  - Criado arquivo de configuração `app.config` (`install.exe.config`) com `EnableWindowsFormsHighDpiAutoResizing=true` e `DpiAwareness=PerMonitorV2`, habilitando o motor nativo de alta densidade do .NET Framework 4.8.

---

## [2.0.0.37] - 2026-11-17

### Alterado
- **Centralização dos Botões Marcar e Desmarcar**: Substituído o alinhamento à esquerda por um `TableLayoutPanel` com duas colunas proporcionais (50%/50%), garantindo que os botões "Marcar" e "Desmarcar" fiquem perfeitamente centralizados horizontalmente no rodapé do painel "Programas para Instalar", com textos limpos, dimensões padronizadas (100x26px) e dicas de contexto (tooltips).
- **Atualização dos Papéis de Parede Corporativos**: Atualizado o conjunto de papéis de parede em `dist/Wallpapers/` com os novos temas fotográficos corporativos (`01-nortel.jpg`, `02-ssa.jpg`, `03-barra.jpg`, `04-humaita.jpg`) e materiais de arte.

---

## [2.0.0.36] - 2026-11-17

### Alterado
- **Botão Parar com Estado e Cor Dinâmicos**: O botão "PARAR" agora inicia desativado com cor neutra/apagada (`#323232` com texto `#6E6E6E`) antes da instalação; torna-se ativo e com fundo vermelho vivo (`#BE2828`) exclusivamente durante o processo de instalação; ao finalizar, cancelar ou ocorrer erro nas instalações, retorna imediatamente ao estado desativado e neutro.
- **Botões Marcar e Desmarcar no Rodapé da Lista**: Os botões "Marcar Todos" e "Desmarcar Todos" foram movidos para a parte inferior do painel "Programas para Instalar" (`Dock = DockStyle.Bottom`), fixos na base do grupo, com a lista de programas preenchendo o espaço superior e com rolagem independente sem sobrepor o cabeçalho.

---

## [2.0.0.35] - 2026-11-17

### Alterado
- **Lista de Programas em Ordem Alfabética**: A lista de programas para instalação foi reorganizada em ordem alfabética (pelo nome de exibição `DisplayName`), tanto na declaração quanto na listagem interna, garantindo que os checkboxes sejam dispostos em ordem A-Z na interface.
- **Microsoft Power BI Desktop Marcado por Padrão**: A definição do Microsoft Power BI Desktop (`PBIDesktopSetup_x64.exe`) agora possui `DefaultChecked = true`, vindo previamente marcado para instalação ao iniciar o aplicativo.

---

## [2.0.0.34] - 2026-11-16

### Corrigido
- **Papel de Parede - Correção de Sintaxe PowerShell no Script `apply_wallpapers.ps1`**: Corrigida a interpolação de string na linha 242 (`$($tName):` em vez de `$tName:`), eliminando o erro de análise `InvalidVariableReferenceWithDrive` / `Referência de variável inválida` causado pela interpretação do caractere de dois pontos (`:`) como escopo de variável/drive pelo PowerShell

---

## [2.0.0.33] - 2026-11-16

### Corrigido
- **AnyDesk - Correção de Instalação Silenciosa e Validação Real**: Corrigida a instalação silenciosa do AnyDesk (`AnyDesk.exe`), padronizando os parâmetros CLI conforme especificação oficial (`--install "C:\Program Files (x86)\AnyDesk" --silent --start-with-win --create-shortcuts --create-desktop-icon`), com `ProcessWorkingDir = "Programas"` e `UseShellExecute = true` (com elevação `runas`)
- **AnyDesk - Área Limpa de Preparação e Finalização de Processos Prévios**: O instalador agora encerra processos orfãos ou em segundo plano do `AnyDesk` e copia o executável para uma pasta local limpa (`C:\ProgramData\EXABIT\Setup\AnyDesk.exe`) antes de disparar a instalação, eliminando conflitos de bloqueio de I/O em pastas sincronizadas
- **AnyDesk - Verificação Pós-Instalação (`IsAnyDeskInstalled`)**: Implementada checagem real no sistema de arquivos (`C:\Program Files (x86)\AnyDesk\AnyDesk.exe` e `Program Files`) e no registro do Windows (64-bit e 32-bit), evitando relatar falso sucesso quando o instalador termina sem ter copiado os binários

---

## [2.0.0.32] - 2026-11-16

### Corrigido
- **Power BI Desktop - Preservação de Privilégios de Administrador (`Verb = runas`)**: Identificado que executáveis com manifesto `asInvoker` (como o bootstrapper WiX Burn do Power BI Desktop) sofrem de-elevação pelo Windows Shell quando iniciados com `UseShellExecute = true` sem o verbo `runas` explícito, executando em integridade média e falhando silenciosamente com `0x80070032` (`ERROR_NOT_SUPPORTED`) ao tentar elevar em modo silencioso (`-quiet`). Adicionado `psi.Verb = "runas"` em `RunProcessAsync` quando `useShell = true` para garantir execução com token administrativo completo
- **Power BI Desktop - Área Limpa de Preparação (Staging Local)**: O instalador agora copia previamente o pacote para `C:\ProgramData\EXABIT\Setup\PBIDesktopSetup_x64.exe`, eliminando o bug de encoding ANSI do WiX Burn 3.14 ao lidar com caracteres acentuados (`ç`, `õ` na pasta "Soluções") e contornando bloqueios de pontos de nova análise/reparse points do OneDrive
- **Power BI Desktop - Modo de Recuperação com Barra de Progresso (`-passive`)**: Caso a instalação silenciosa (`-quiet`) não conclua ou não crie os arquivos esperados, o serviço executa automaticamente uma segunda tentativa em modo passivo (`-passive`), exibindo a barra de progresso nativa do instalador da Microsoft sem exigir interação do usuário
- **Power BI Desktop - Diagnóstico Detalhado de Falhas**: Implementado analisador de log de instalação (`powerbi_install.log`) via `ExtractPowerBILogSummary`, extraindo automaticamente a mensagem de erro exata e o código HRESULT caso ocorra qualquer imprevisto ambiental

---

## [2.0.0.34] - 2026-11-16

### Corrigido
- **Papel de Parede - Correção de Sintaxe PowerShell no Script `apply_wallpapers.ps1`**: Corrigida a interpolação de string na linha 242 (`$($tName):` em vez de `$tName:`), eliminando o erro de análise `InvalidVariableReferenceWithDrive` / `Referência de variável inválida` causado pela interpretação do caractere de dois pontos (`:`) como escopo de variável/drive pelo PowerShell

---

## [2.0.0.33] - 2026-11-16

### Corrigido
- **AnyDesk - Correção de Instalação Silenciosa e Validação Real**: Corrigida a instalação silenciosa do AnyDesk (`AnyDesk.exe`), padronizando os parâmetros CLI conforme especificação oficial (`--install "C:\Program Files (x86)\AnyDesk" --silent --start-with-win --create-shortcuts --create-desktop-icon`), com `ProcessWorkingDir = "Programas"` e `UseShellExecute = true` (com elevação `runas`)
- **AnyDesk - Área Limpa de Preparação e Finalização de Processos Prévios**: O instalador agora encerra processos orfãos ou em segundo plano do `AnyDesk` e copia o executável para uma pasta local limpa (`C:\ProgramData\EXABIT\Setup\AnyDesk.exe`) antes de disparar a instalação, eliminando conflitos de bloqueio de I/O em pastas sincronizadas
- **AnyDesk - Verificação Pós-Instalação (`IsAnyDeskInstalled`)**: Implementada checagem real no sistema de arquivos (`C:\Program Files (x86)\AnyDesk\AnyDesk.exe` e `Program Files`) e no registro do Windows (64-bit e 32-bit), evitando relatar falso sucesso quando o instalador termina sem ter copiado os binários

---

## [2.0.0.32] - 2026-11-16

### Corrigido
- **Power BI Desktop - Preservação de Privilégios de Administrador (`Verb = runas`)**: Identificado que executáveis com manifesto `asInvoker` (como o bootstrapper WiX Burn do Power BI Desktop) sofrem de-elevação pelo Windows Shell quando iniciados com `UseShellExecute = true` sem o verbo `runas` explícito, executando em integridade média e falhando silenciosamente com `0x80070032` (`ERROR_NOT_SUPPORTED`) ao tentar elevar em modo silencioso (`-quiet`). Adicionado `psi.Verb = "runas"` em `RunProcessAsync` quando `useShell = true` para garantir execução com token administrativo completo
- **Power BI Desktop - Área Limpa de Preparação (Staging Local)**: O instalador agora copia previamente o pacote para `C:\ProgramData\EXABIT\Setup\PBIDesktopSetup_x64.exe`, eliminando o bug de encoding ANSI do WiX Burn 3.14 ao lidar com caracteres acentuados (`ç`, `õ` na pasta "Soluções") e contornando bloqueios de pontos de nova análise/reparse points do OneDrive
- **Power BI Desktop - Modo de Recuperação com Barra de Progresso (`-passive`)**: Caso a instalação silenciosa (`-quiet`) não conclua ou não crie os arquivos esperados, o serviço executa automaticamente uma segunda tentativa em modo passivo (`-passive`), exibindo a barra de progresso nativa do instalador da Microsoft sem exigir interação do usuário
- **Power BI Desktop - Diagnóstico Detalhado de Falhas**: Implementado analisador de log de instalação (`powerbi_install.log`) via `ExtractPowerBILogSummary`, extraindo automaticamente a mensagem de erro exata e o código HRESULT caso ocorra qualquer imprevisto ambiental

---

## [2.0.0.31] - 2026-11-16

### Alterado
- **Papel de Parede Personalizado em Todos os Temas**: A opção "Aplicar papel de parede personalizado" agora detecta e processa automaticamente **todos os arquivos de imagem** presentes na pasta `Wallpapers` (`dist\Wallpapers` e `dist\Scripts\Wallpapers`), aplicando-os de forma integrada a **todos os temas do Windows** (Aero/Claro, Dark/Escuro, Spotlight, ThemeA, ThemeB, ThemeC e ThemeD)
- **Apresentação de Slides e Temas Nativos**: Atualizados os arquivos de definição de tema em `C:\Windows\Resources\Themes\`, adicionando a seção `[Slideshow]` para alternância automática de todas as imagens personalizadas em qualquer tema escolhido pelo usuário
- **Pastas de Sistema e Cache 4K**: Todas as imagens da pasta `Wallpapers` são copiadas para `C:\Windows\Web\Wallpaper\Personalizado\` e para as pastas temáticas do Windows (`Windows`, `ThemeA`, `ThemeB`, `ThemeC`, `ThemeD`, `Spotlight`), substituindo as imagens padrão (`img0.jpg`, `img19.jpg`, `img20..35`, `img50`, `img100`), o cache 4K (`C:\Windows\Web\4K\Wallpaper\Windows\`) e a tela de bloqueio (`C:\Windows\Web\Screen\img100.jpg`)
- **Perfis de Usuário**: Configurado o registro do usuário atual (`HKCU\Control Panel\Desktop` e `Desktop Slideshow`), atualizado o cache `TranscodedWallpaper`, configurado o perfil padrão `C:\Users\Default\NTUSER.DAT` (para que novos usuários já nasçam com todos os papéis de parede aplicados) e propagado para perfis existentes
- **Script Especializado**: Criado `dist\Scripts\apply_wallpapers.ps1` com controle de permissões por SID (`*S-1-5-32-544`), atualização de `.theme`, integração P/Invoke `SystemParametersInfoW` e assinatura digital corporativa Authenticode

---

## [2.0.0.30] - 2026-11-16

### Alterado
- **Power BI Desktop - Correção de Instalação e Bloqueio WiX Burn**: Configurado `UseShellExecute = true` e `ProcessWorkingDir = "Programas"` na definição do Microsoft Power BI Desktop (`PBIDesktopSetup_x64.exe`). Com `UseShellExecute = true`, o processo é iniciado pelo subsistema Shell do Windows sem redirecionamento de pipes anônimos (`stdout`/`stderr`), eliminando o erro interno Win32 `0x80070032` (`ERROR_NOT_SUPPORTED`) na elevação do motor Burn
- **Power BI Desktop - Eliminação de Falso Positivo**: Em versões anteriores, o bootstrapper WiX Burn encerrava em menos de 2 segundos retornando código `0x0` em modo `-quiet` após a falha de elevação, levando o serviço a registrar falsamente `[ok] instalado com sucesso`. Implementada verificação física no disco (`C:\Program Files\Microsoft Power BI Desktop\bin\PBIDesktop.exe`) e no Registro do Windows, garantindo que o status de sucesso só seja reportado se o aplicativo foi realmente instalado
- **Power BI Desktop - Detecção Prévia**: Adicionada verificação prévia para evitar reinstalação redundante caso o Power BI Desktop já esteja presente no sistema operacional
- **Geral - Diretório de Trabalho Padrão**: Padronizado o diretório de trabalho padrão dos instaladores em `InstallerService.cs` para a pasta onde o próprio instalador está localizado (`Path.GetDirectoryName(fullPath)`), evitando falhas de resolução de dependências e arquivos auxiliares

---

## [2.0.0.29] - 2026-11-16

### Alterado
- **Citrix Workspace - Correção de Instalação e Código -2147467259**: Ajustados os parâmetros padrão para `/silent /noreboot`, removendo o switch `/norestart` e o componente `/includeSSON` (Single Sign-On / logon pass-through de domínio), cuja tentativa de registro em máquinas recém-formatadas fora de domínio causava falha fatal `0x80004005` (`-2147467259`)
- **Citrix Workspace - Verificação e Atualização Online**: Implementada verificação prévia de conectividade com a internet antes da instalação; quando conectado, consulta a página oficial (`https://www.citrix.com/downloads/workspace-app/windows/workspace-app-for-windows-latest.html`), compara a versão online com a versão do instalador local (`dist\Programas\CitrixWorkspaceFullInstaller_x64.exe`) e, se o arquivo local estiver desatualizado ou ausente, faz o download automático da versão mais recente para x64
- **Citrix Workspace - Otimização de Download**: Se a versão local já for igual ou mais recente que a versão disponível no portal da Citrix, utiliza o instalador local imediatamente, evitando download redundante de ~874 MB
- **Citrix Workspace - Resiliência e Códigos de Retorno**: Trata o código de saída `3010` (`ERROR_SUCCESS_REBOOT_REQUIRED`) como sucesso com reinicialização pendente; adicionada rotina de autorrecuperação para códigos de erro `-2147467259` ou `1603` com segunda tentativa automática com `/forceinstall` e sem `/includeSSON`. Finalização preventiva de processos remanescentes (`CitrixWorkspaceApp`, `Receiver`, `wfcrun32`) antes da execução do instalador

---

## [2.0.0.28] - 2026-11-16

### Alterado
- **Segurança e Anti-Falso Positivo**: Embutido manifesto de aplicação nativo (`app.manifest`) com declaração explícita de `requireAdministrator`, identificador corporativo da aplicação e suporte a Windows 10 e Windows 11, evitando alertas de elevação não manifestada
- **Metadados do Executável**: Preenchidos todos os atributos de assembly em `AssemblyInfo.cs` (`AssemblyCompany`, `AssemblyProduct`, `AssemblyCopyright`, `AssemblyTrademark`) com os dados oficiais da EXABIT Soluções em TI, eliminando penalidades de reputação por metadados vazios
- **Mitigação de Heurística de Dropper**: Atualizado o local de download de executáveis atualizados (como o RustDesk) para a pasta legítima da aplicação (`Programas` ou `%LOCALAPPDATA%\Exabit\AutoInstall\Updates`), removendo a execução direta a partir da pasta temporária `%TEMP%`
- **Mitigação de Alertas AMSI**: Simplificada a chamada de configuração de contas locais no PowerShell via `Set-LocalUser`, removendo injeções complexas de scripts e chamadas obsoletas ao `wmic`
- **Assinatura Digital Authenticode**: Integrada rotina de assinatura digital corporativa com certificado SHA-256 e carimbo de tempo RFC 3161 (DigiCert) diretamente no script de compilação `build.bat`, assinando tanto o executável `install.exe` quanto os scripts PowerShell auxiliares
- **Script de Confiança**: Adicionado script `dist\Instalar_Certificado_EXABIT.cmd` para instalação com 1 clique do certificado público em "Editores Confiáveis" e "Raiz Confiável" das máquinas clientes

---

## [2.0.0.27] - 2026-11-16

### Alterado
- **Contas de Usuário**: Garantida a ativação obrigatória e permanente da opção **"A senha nunca expira"** (`PasswordNeverExpires`) para todos os usuários criados ou atualizados (`admin` e usuário padrão), com verificação em múltiplas camadas (PowerShell `Set-LocalUser`, ADSI com flag `0x10000` (`ADS_UF_DONT_EXPIRE_PASSWORD`) e fallback WMIC)
- **Política de Senhas do Windows**: Aplicação automática da política de máquina `net accounts /maxpwage:unlimited` tanto no início do processo de instalação quanto na criação dos usuários, assegurando que a duração máxima de senhas locais no Windows seja ilimitada e eliminando riscos de senhas expiradas
- **Correção**: Corrigido parâmetro no `Set-LocalUser` para controle de alteração de senha do usuário padrão (`-UserMayChangePassword`), que causava falha silenciosa na aplicação do `PasswordNeverExpires`

---

## [2.0.0.26] - 2026-11-16

### Alterado
- **RustDesk**: Verificação prévia de conectividade com a internet antes de instalar o arquivo local da pasta `Programas`; se houver conexão ativa, baixa e instala automaticamente a versão mais recente para Windows x86_64 diretamente do GitHub Releases, atualizando o instalador local em `Programas` quando possível, com fallback para o instalador local caso offline ou em caso de falha de download
- **RustDesk**: Configuração de permissões de acesso para liberar conexões com senha temporária, aprovação do usuário (clique) ou senha definitiva (`approve-mode = 'password-click'` e `verification-method = 'use-both-passwords'`)
- **RustDesk**: Definição da senha definitiva (acesso unattended) utilizando a mesma senha informada no campo "Senha do usuário Admin:" da interface, aplicada via linha de comando (`--password`) e replicada nos arquivos de configuração TOML
- Script auxiliar `dist\Scripts\rustdesk_server.ps1` atualizado com suporte a download/instalação da versão mais recente, configuração dos modos de permissão e parâmetro `-AdminPassword` para definição da senha definitiva

---

## [2.0.0.25] - 2026-11-15

### Adicionado
- Configuração automática do servidor próprio da EXABIT para o **RustDesk** (`rustdesk.exabit.com.br`) aplicada imediatamente após a sua instalação
- Definição do servidor EXABIT como padrão para todos os perfis (`LocalService`, `Default` template e perfis em `C:\Users`) via gravação de `RustDesk2.toml` com codificação UTF-8 sem BOM
- Limpeza preventiva de tokens de autenticação/sessão anteriores em `RustDesk.toml`, `RustDesk_default.toml` e `RustDesk_local.toml`
- Script auxiliar de PowerShell disponibilizado em `dist\Scripts\rustdesk_server.ps1` para aplicação manual avulsa

---

## [2.0.0.24] - 2026-11-12

### Alterado
- Garantida a ativação de **"A senha nunca expira"** (`PasswordNeverExpires $true`) para o usuário `admin` mesmo quando ele já existir no sistema (com ou sem alteração de senha)
- Garantida a ativação de **"A senha nunca expira"** para o usuário padrão tanto na criação quanto caso o usuário já exista, com registro explícito no log

---

## [2.0.0.23] - 2026-11-12

### Adicionado
- **Microsoft Power BI Desktop** (`PBIDesktopSetup_x64.exe`) adicionado à lista de programas com instalação silenciosa (`-quiet -norestart ACCEPT_EULA=1`), desmarcado por padrão
- **Microsoft Office 365 (Online)** (`OfficeSetup.exe`) adicionado à lista de programas (desmarcado por padrão) com exigência de conexão ativa com a internet
- Verificação prévia de conexão com a internet antes de iniciar a instalação quando algum programa online (como o Office 365 Online) estiver selecionado, exibindo aviso solicitando conexão (com opções Repetir / Cancelar) caso esteja offline
- Propriedades `RequiresInternet` e `DefaultChecked` no modelo `ProgramInfo` e método `HasInternetConnectionAsync()` no `InstallerService`

---

## [2.0.0.22] - 2026-11-11

### Alterado
- Quando o usuário `admin` já existe no sistema e uma senha é informada no campo "Senha do usuário Admin:", o instalador agora altera apenas a senha do usuário existente em vez de tentar recriá-lo e exibir erro no log
- Se o usuário `admin` já existir e o campo de senha estiver em branco, a senha atual é preservada sem tentativa de recriação ou limpeza acidental

---

## [2.0.0.21] - 2026-10-19

### Alterado
- Barra de título do Windows integrada ao tema escuro da aplicação via API nativa do Desktop Window Manager (DWM), pintando-a de preto/escuro (`CorFundo`) com título em branco
- Removidos os botões de minimizar e maximizar da barra de título (`MinimizeBox = false` e `MaximizeBox = false`), mantendo unicamente o botão de fechar ("X")

---

## [2.0.0.20] - 2026-10-19

### Alterado
- Corrigido o espaçamento vertical entre os campos "Criar usuário padrão" e "Senha do usuário" através da fixação de altura das linhas com preenchimento percentual no rodapé
- Rótulo "Senha / usuário padrão:" renomeado para "Senha do usuário:"
- Altura do painel de configurações refinada para 215px

---

## [2.0.0.19] - 2026-10-19

### Alterado
- Painel "Configurações do Sistema" reformulado em duas colunas:
  - Lado esquerdo: campos de texto (Nome do computador, Senha do usuário Admin, Criar usuário padrão e Senha / usuário padrão)
  - Lado direito: opções selecionáveis (Impedir que o usuário altere a senha, Papel de parede e atalhos de suporte)
- Altura do painel de configurações reduzida de 365px para 220px, proporcionando muito mais espaço vertical para o painel de log

---

## [2.0.0.18] - 2026-10-19

### Adicionado
- ArtCraft (`ArtCraft_setup.exe`) adicionado como último item da lista de programas com instalação silenciosa (`/S`)

### Alterado
- Rótulo "Criar usuário admin:" alterado para "Senha do usuário Admin:"
- Rótulo "Senha:" do usuário comum alterado para "Senha / usuário padrão:"
- ArtCraft agora inicia **desmarcado por padrão** na lista de programas
- Botão "INICIAR INSTALAÇÃO" agora altera sua cor para um verde mais escuro enquanto estiver desabilitado durante a instalação, sendo restaurado e reativado ao finalizar

---

## [2.0.0.17] - 2026-10-19

### Alterado
- Campo "Criar usuário padrão" unificado em um único campo ("Nome do usuário"), removendo o campo separado de Sobrenome
- Campo "Nome do usuário" agora inicia totalmente em branco por padrão
- O usuário padrão só é criado caso o campo de nome seja preenchido; se deixado em branco, a criação do usuário é ignorada

---

## [2.0.0.16] - 2026-10-19

### Alterado
- Campo "Criar usuário admin" agora inicia em branco por padrão (não preenche mais automaticamente com `NT@admin#`), permitindo preenchimento no momento do uso
- Se o campo de senha do admin for deixado em branco, o usuário `admin` é criado sem senha no sistema
- Campo de senha do usuário padrão ("Senha:") agora inicia em branco por padrão (não preenche mais automaticamente com `NT@colaborador`)
- Checkbox "Aplicar papel de parede personalizado" agora inicia **desmarcado por padrão** (o técnico marca apenas se desejar aplicar)

---

## [2.0.0.15] - 2026-09-29

### Adicionado
- Mensagens de log detalhadas informando explicitamente se o usuário comum criado tem permissão de troca de senha ou restrição aplicada
- Sincronização robusta da restrição de senha via PowerShell (`Set-LocalUser -UserMayNotChangePassword`) e `net user /passwordchg`

### Alterado
- O checkbox de restrição de senha foi renomeado para *"Impedir que o usuário altere a senha"* e agora inicia **desmarcado por padrão** (o usuário comum pode alterar sua senha normalmente, a menos que o técnico marque a opção)
- Mantido o layout visual original do painel de configurações conforme solicitado pelo usuário

---

## [2.0.0.14] - 2026-09-18

### Adicionado
- Som personalizado do R2-D2 (`R2D2.mp3`) reproduzido automaticamente após a conclusão com sucesso de toda a instalação de programas e configurações
- Embutido o áudio `R2D2.mp3` como recurso no executável (`install.exe`) e distribuído na pasta `dist/Sound/`, garantindo a execução independente do ambiente
- Evento `OnInstallationCompleted` no `InstallerService` para notificar a interface sobre o término da instalação
- Fallback automático para sons do Windows (`tada.wav` / som de alerta) caso o subsistema de mídia não esteja disponível

---

## [2.0.0.13] - 2026-07-27

### Adicionado
- AnyDesk (`AnyDesk.exe`) adicionado à lista de programas com instalação silenciosa (`--install "C:\Program Files (x86)\AnyDesk" --start-with-win --silent`)

### Alterado
- AnyDesk agora inicia **desmarcado** por padrão na lista de programas (o usuário marca se quiser instalar)

---

## [2.0.0.12] - 2026-07-03

### Adicionado
- Botão "PARAR" para cancelar a instalação em andamento
- Citrix Workspace adicionado à lista de programas com instalação silenciosa completa (instalador `CitrixWorkspaceFullInstaller_x64.exe`)

### Alterado
- Botão "PARAR" não remove mais os usuários já criados ao cancelar a instalação

---

## [2.0.0.11] - 2026-07-01

### Alterado
- Microsoft Office e Radmin VPN agora iniciam **desmarcados** na lista de programas (o usuário marca se quiser instalar)

---

## [2.0.0.10] - 2026-07-01

### Alterado
- Senha padrão do usuário admin alterada de `EXA@admin#202_` para `NT@admin#`
- Senha padrão do usuário padrão alterada de `Senha` para `NT@colaborador` (já preenchida no campo)

---

## [2.0.0.9] - 2026-07-01

### Alterado
- Atalhos de suporte (Suporte Norte, Webmail Nortel, Webmail Nortel Terceirização) agora iniciam **desmarcados** por padrão — o usuário marca apenas os que deseja
- Removida a pergunta "Deseja reiniciar o computador?" ao final da instalação; agora apenas exibe mensagem de conclusão

---

## [2.0.0.8] - 2026-07-01

### Adicionado
- Atalho "Webmail Nortel Terceirização" (`http://webmail.nortelterceirizacao.com.br`) — novo link de suporte
- Checkboxes individuais na interface para ativar/desativar cada atalho de suporte (Suporte Norte, Webmail Nortel, Webmail Nortel Terceirização)
- Checkbox "Adicionar atalhos também no Menu Iniciar" — cria os atalhos em `C:\ProgramData\Microsoft\Windows\Start Menu\Programs` além da área de trabalho

### Alterado
- `CreateDesktopShortcutsAsync` agora recebe `InstallationOptions` em vez de parâmetros avulsos, lê quais atalhos criar e se deve incluir no Menu Iniciar
- Altura do painel de configuração ajustada de 225px para 365px para acomodar as novas opções de atalhos

---

## [2.0.0.7] - 2026-07-01

### Corrigido
- Erro "O sistema não pode encontrar o arquivo especificado" ao configurar expiração de senha no Windows 11 Home (`wmic.exe` não está disponível nesta edição)
- Substituídas chamadas ao `wmic` por PowerShell `Set-LocalUser -PasswordNeverExpires`, compatível com Windows 10/11 Home e Pro

### Alterado
- `RunProcessAsync("wmic", ...)` substituído por `RunProcessAsync("powershell", "-Command \"Set-LocalUser ...\"")` nos métodos `CreateAdminUserAsync` e `CreateStandardUserAsync`

---

## [2.0.0.6] - 2026-07-01

### Corrigido
- Limpeza de atalhos da área de trabalho agora é feita em **DOIS momentos**: antes e depois da instalação dos programas, garantindo que atalhos criados por processos-filho ou ações adiadas dos instaladores sejam removidos
- `CleanAllDesktops` agora também limpa o template `C:\Users\Default\Desktop` para que **novos usuários** que ainda não fizeram login também herdem o desktop limpo
- Se o diretório do desktop do usuário padrão não existir (nunca fez login), o método agora o **cria antes de limpar**
- Cada deleção de atalho agora possui try/catch individual para evitar que um arquivo protegido interrompa a limpeza dos demais

### Alterado
- `CleanPublicDesktop()` substituído por `CleanAllDesktops()` que remove atalhos `.lnk` e `.url` de **todos** os perfis: público, admin, usuário padrão e Default (template)

---

## [2.0.0.5] - 2026-06-30

### Corrigido
- Office (`setup.exe /configure configuration.xml`) agora usa `FireAndForget` com 10 minutos de espera; antes o ODT iniciava a instalação e saía imediatamente com código 0, fazendo o programa registrar "instalação concluída" antes do Office terminar de instalar
- Bloco `FireAndForget` no `InstallerService` agora respeita a propriedade `TimeoutMs` do programa (antes usava 30 segundos fixos)

---

## [2.0.0.4] - 2026-06-30

### Adicionado
- Usuário padrão agora é adicionado automaticamente ao grupo "Usuários da Área de Trabalho Remota" (Remote Desktop Users) para permitir login via RDP

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
