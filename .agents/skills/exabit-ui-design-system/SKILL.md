---
name: exabit-ui-design-system
description: Sistema oficial de design visual, paleta de cores corporativa, tipografia, espaçamentos, regras de padding e diretrizes de interface gráfica para o Auto Instalador de Programas EXABIT.
---

# Sistema de Design Visual EXABIT (UI/UX Guidelines)

Este documento define os padrões rigorosos de design gráfico, ergonomia visual e arquitetura de interface do **Auto Instalador de Programas** da EXABIT Soluções em TI.
**Todos os agentes e desenvolvedores devem seguir estritamente estas diretrizes ao criar, modificar ou expandir telas.**

---

## 1. Princípios Fundamentais de Design

- **Pixel Perfection**: Alinhamentos milimétricos, sem elementos flutuando desalinhados ou recortados.
- **Hierarquia Visual Clara**: Títulos destacados, seções bem delimitadas por bordas sutis e contraste acessível.
- **Proximidade e Agrupamento (Gestalt)**:
  - Um rótulo (*label*) deve pertencer visualmente ao seu campo de entrada correspondente (*input*), mantendo distância mínima (2 a 4px) entre eles.
  - O espaçamento entre grupos diferentes de campos deve ser maior (8 a 14px) para indicar separação de contexto.
- **Consistência Absoluta entre Telas**: Checkboxes, botões e caixas de texto devem manter rigorosamente a mesma altura de linha, margens e paddings em todas as abas.

---

## 2. Paleta Oficial de Cores (Dark Corporate Theme)

| Elemento | Código Hex / RGB | Uso / Significado |
|---|---|---|
| **Fundo Principal (Janela)** | `#1E1E1E` / `RGB(30, 30, 30)` | Fundo geral da aplicação e área externa. |
| **Painel / Abas / Seções** | `#282828` / `RGB(40, 40, 40)` | Cor de fundo das abas ativas e painéis de agrupamento. |
| **Cards Internos de Dados** | `#202020` / `RGB(32, 32, 32)` | Fundo de cards elevados (ex: Diagnóstico, Contas). |
| **Bordas dos Cards** | `#323232` / `RGB(50, 50, 50)` | Linha de 1px desenhada no evento `Paint` dos cards. |
| **Console de Log / Terminal** | `#141414` / `RGB(20, 20, 20)` | Fundo da área de processos e log de instalação. |
| **Destaque Laranja (Marca)** | `#FF8C00` / `RGB(255, 140, 0)` | Títulos de cards, abas ativas, botões de ação secundária. |
| **Destaque Ciano / Azul** | `#00B4FF` / `RGB(0, 180, 255)` | Diagnóstico compacto, links, botões informativos. |
| **Sucesso / Normal (Verde)** | `#00C850` / `RGB(0, 200, 80)` | Log OK, conexões online excelentes, barras com espaço saudável. |
| **Atenção / Médio (Amarelo)**| `#F0B400` / `RGB(240, 180, 0)` | Discos com < 20% livre, ping moderado. |
| **Alerta / Crítico (Vermelho)** | `#E63232` / `RGB(230, 50, 50)` | Discos com < 10% livre, conexões offline, falhas no log. |
| **Texto Primário** | `#FFFFFF` / `Color.White` | Textos principais, valores de campos e cabeçalhos. |
| **Texto Secundário / Rótulo**| `#AAAAAA` / `RGB(170, 170, 170)` | Labels de dados, descrições, títulos de colunas. |
| **Campos de Entrada (Inputs)**| `#3C3C3C` / `RGB(60, 60, 60)` | Fundo de TextBox com texto branco e borda sutil. |

---

## 3. Tipografia Oficial

- **Fonte Padrão da Aplicação**: `Segoe UI`
- **Fonte do Terminal / Log**: `Consolas`, tamanho `9pt`, Regular
- **Hierarquia de Tamanhos**:
  - **Título da Janela / Marca**: `13.5pt`, Bold
  - **Títulos de Seção / Cards**: `9.5pt`, Bold (`CorDestaqueLaranja` ou `CorDestaqueAzul`)
  - **Abas de Navegação**: `10.0pt`, Bold (ativa) / Regular (inativa)
  - **Itens de Checkbox e Dados**: `9.5pt` ou `8.5pt`, Regular (`Color.White`)
  - **Rótulos (Labels de Identificação)**: `8.5pt`, Regular (`RGB(170, 170, 170)`)
  - **Dicas e Legendas de Rodapé**: `8.0pt` ou `8.5pt`, Italic (`RGB(160, 160, 160)`)

---

## 4. Padronização de Espaçamentos, Margens e Paddings

### 4.1. Conteúdo de Abas (Tab Panels)
Todas as abas (Configurações, Atalhos, Opções do Windows, Funcionalidades, ou futuras telas) **devem** obrigatoriamente usar:
- **Padding Externo do Container**:
  ```csharp
  Padding = new Padding((int)(16 * scale), (int)(12 * scale), (int)(16 * scale), (int)(8 * scale));
  ```
- **Altura de Linha de Checkboxes**:
  - Exatamente **`30px`** por item (`RowStyle(SizeType.Absolute, (int)(30 * scale))`).
  - Margem do CheckBox: `Padding(2, 0, 2, 0)`, alinhado com `Anchor = AnchorStyles.Left`.
- **Linha de Dica / Legenda de Rodapé**:
  - Altura fixa de **`24px`** (`RowStyle(SizeType.Absolute, (int)(24 * scale))`).
- **Espaçador Inferior Flexível**:
  - Última linha da tabela deve usar `SizeType.Percent, 100f` para absorver qualquer altura excedente, impedindo que os checkboxes se afastem uns dos outros de forma irregular.

### 4.2. Formulários e Caixas de Texto (Label + Input)
- **Regra de Ouro**: A etiqueta (*label*) deve ficar **imediatamente acima** do campo de texto (*TextBox*), nunca separada por grandes vazios.
- **Estrutura Padrão**:
  - Linha do Label: altura **`18px`**, alinhamento `MiddleLeft`, `Margin = Padding.Empty`.
  - Linha do TextBox: altura **`28px`**, `Dock = DockStyle.Fill`, `Margin = Padding.Empty`.
  - Distância entre campos sucessivos: **`6px` a `10px`**.

### 4.3. Cards de Diagnóstico e Informação
- **Container**: `Padding = Padding((int)(12 * scale), (int)(10 * scale), (int)(12 * scale), (int)(10 * scale))`.
- **Linha do Título do Card**: altura **`28px`**, com margem inferior de **`6px`** separando o título em destaque do primeiro dado.
- **Coluna de Rótulos (Coluna 0)**: fixada em **`125px`** (`SizeType.Absolute, (int)(125 * scale)`), garantindo que termos como `Computador:`, `Processador:` ou `Memória RAM:` nunca quebrem linha ou sofram corte horizontal.
- **Alinhamento dos Rótulos**: Todos os labels devem usar `Margin = Padding.Empty` para alinhamento horizontal 100% nivelado.
- **Linhas de Dados**: altura padronizada de **`24px`** por linha.

### 4.4. Barras de Consumo / MiniUsageBar
- Altura padrão da barra: **`8px`**.
- Posição: Deve ser posicionada **imediatamente abaixo** do texto de medição (distância máxima de 4px a 6px), nunca ancorada no fundo de um painel esticado.

### 4.5. Console de Log e Janelas de Processo (Terminal)
- **Padding Interno Obrigatório**:
  - O controle `RichTextBox` deve ficar envolto em um container (`pnlTextWrapper`) com padding interno de **`8px`**:
  ```csharp
  Padding = new Padding((int)(8 * scale), (int)(8 * scale), (int)(4 * scale), (int)(8 * scale));
  ```
  - Isso garante que o texto jamais toque nas bordas duras da janela, criando a sensação de terminal profissional (estilo VS Code / JetBrains).

---

## 5. Atualizações Periódicas e Timers em Background (5 Segundos)

- **Métricas e Conexão em Tempo Real**:
  - Espaço livre em disco (Disco C: e demais unidades) e o teste de latência de rede (Ping / Conexão) devem ser atualizados a cada **5 segundos** (`Interval = 5000`).
  - Utilizar APIs rápidas do sistema (`DriveInfo.GetDrives()`, `GlobalMemoryStatusEx` e `MeasureInternetPingAsync` assíncrono em background), **nunca** chamadas WMI completas ou operações bloqueantes na thread da UI.
  - Desativar timers no início da instalação (`_btnInstall.Click`) e na destruição da janela (`OnFormClosing`).

---

## 6. Restrições Físicas da Janela (FixedSingle, Sem Maximizar, Otimizada para 720p)

- **Bloqueio de Redimensionamento**: `FormBorderStyle = FormBorderStyle.FixedSingle` (borda fixa única, cursor nunca vira seta de redimensionamento).
- **Sem Botão Maximizar**: `MaximizeBox = false` e `MinimizeBox = true` (apenas botões Minimizar e Fechar na barra de título).
- **Dimensões Travadas (Compatível com 720p 1280x720)**: Dimensão ideal calculada em `1200 x 650px` (com clamping a `workingArea - 16px` para garantir folga com barra de tarefas do Windows de até 48px), travada com `MinimumSize = MaximumSize = Size = new Size(formWidth, formHeight)`.
