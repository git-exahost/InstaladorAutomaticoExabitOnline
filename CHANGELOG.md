# Changelog

Todas as alterações significativas neste projeto serão documentadas aqui.

O formato é baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/),
e este projeto adere ao [Versionamento Semântico](https://semver.org/lang/pt-BR/).

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
