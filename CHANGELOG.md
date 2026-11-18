# Changelog

Todas as alterações significativas neste projeto serão documentadas aqui.

O formato é baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/),
e este projeto adere ao [Versionamento Semântico](https://semver.org/lang/pt-BR/).

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
