# PRD: Modo tutorial (gravação de passos)

> Data: 2026-11-16 · Status: rascunho · Versão alvo: Fragmo 0.4.0

## 1. Problema

Quem precisa explicar um procedimento no Windows (suporte de TI, documentação interna, "como faço X" para um colega) hoje tira prints um a um, cola num documento e escreve cada passo à mão. O Gravador de Passos do Windows (`psr.exe`), que automatizava isso, foi descontinuado pela Microsoft e será removido. As ferramentas atuais de "tutorial por prints" funcionam só dentro do navegador. Falta uma ferramenta local, gratuita e para qualquer programa do Windows.

## 2. Objetivo e resultado esperado

- O usuário inicia uma gravação, executa a tarefa em qualquer programa e, ao parar, tem um tutorial com **um passo por ação**: print da janela, elemento clicado destacado e texto já escrito ("Clique no botão “Salvar”").
- Sem digitar nada, um procedimento de 10 cliques vira um tutorial exportável em **menos de 1 minuto** depois de parar a gravação.
- Dados sensíveis visíveis nos prints (CPF, CNPJ, e-mail, IPv4, telefone) saem ocultados por padrão.
- Exporta para **HTML autocontido, Markdown + imagens, PDF e DOCX**, sem nenhum pacote NuGet novo e sem acesso à rede.

## 3. Usuários e cenários de uso

- **Analista de suporte / infra**: grava "como mapear uma unidade de rede" e manda o PDF ou DOCX para o usuário final.
- **Quem documenta processos**: grava o fluxo de um sistema interno e cola o Markdown na wiki, no Obsidian ou no GitHub.
- **Usuário comum**: grava como resolveu algo para mandar a um parente (HTML aberto direto no navegador).

Tudo roda no desktop Windows 10 2004+ / 11, no mesmo processo do Fragmo, iniciado pela bandeja ou por atalho.

## 4. Escopo

### Dentro

- **Gravação** iniciada/parada pelo menu da bandeja e por atalho global, com barra flutuante "Gravando · N passos · Pausar · Parar · Descartar" que **não aparece nos prints**.
- **Eventos que viram passo** (decisão do usuário):
  - clique esquerdo; clique direito e duplo clique como tipos distintos (duplo clique junta os dois cliques num passo só);
  - digitação: vira "Digite no campo X", **sem gravar o conteúdo digitado**; teclas seguidas no mesmo campo viram um único passo; campo de senha vira "Digite a senha no campo X";
  - atalhos de teclado (modificador + tecla, e teclas especiais como Enter, Tab, Esc, F1–F12): "Pressione Ctrl+C";
  - passo manual: um atalho tira um print extra como passo, sem clique.
- **Área do print**: a janela de nível superior onde a ação aconteceu (decisão do usuário). Cai para o monitor inteiro quando não há janela válida (área de trabalho, barra de tarefas, menus soltos).
- **Destaque automático**: retângulo no elemento clicado (via UI Automation) e marcador no ponto do clique. Fica guardado como dado do passo e pode ser ligado ou desligado por passo.
- **Texto automático** em pt-BR ou inglês (idioma do app), gerado a partir do tipo e do nome do elemento (UI Automation) e da janela. Sem IA.
- **Ocultar dados sensíveis**: o OCR + `SensitiveDataDetector` atuais rodam em cada passo, ligado por padrão no modo tutorial.
- **Editor de tutorial** (janela normal, não tela cheia):
  - lista de passos com miniatura;
  - editar título do tutorial e texto de cada passo;
  - subir, descer e excluir passos;
  - inserir nota/título entre passos;
  - abrir o print no editor de anotações atual para desenhar, tarjar ou desfocar;
  - ligar ou desligar o destaque automático do passo;
  - desfazer a última operação estrutural.
- **Salvar e reabrir** o tutorial num arquivo próprio (ZIP com JSON + PNGs) para editar depois.
- **Exportar**: HTML autocontido (imagens em base64), Markdown + pasta de imagens, PDF (A4) e DOCX.
- **Recuperação**: os passos vão para disco durante a gravação; se o app fechar no meio, a sessão pode ser reaberta.
- Configurações novas: atalhos (gravar/parar e passo manual) e "Ocultar dados sensíveis nos tutoriais" (padrão: ligado).

### Fora (não fazer agora)

- IA para escrever ou melhorar textos (decisão do usuário).
- Exportar vídeo, GIF ou narração por voz.
- Replay guiado ("mostrar onde clicar" ao vivo).
- Rolagem do mouse e arrastar como passos.
- Arrastar e soltar para reordenar (usar botões subir/descer).
- Histórico de versões do tutorial, juntar tutoriais, compartilhar em nuvem.
- Leitura de DOM de navegador (o navegador é tratado como qualquer app, via UI Automation).
- Gravar janelas que rodam como administrador quando o Fragmo não roda (ver riscos).

## 5. Restrições

- **Stack:** C# / .NET 10 WinForms, TFM `net10.0-windows10.0.19041.0`, `TreatWarningsAsErrors`, `Nullable`, P/Invoke só em `src/Fragmo/Native/*.cs` com `[LibraryImport]`.
- **Sem pacotes NuGet no app** (regra da `docs/Spec.md`). Referência de framework do próprio .NET (ex.: WPF para UI Automation) é permitida, mas o aumento de tamanho precisa ser medido.
- **Sem rede, sem telemetria.** Tudo local.
- Interface e textos gerados em pt/en, em `Strings.cs`.
- Lógica pura (agrupamento de eventos, texto do passo, layout PDF, montagem HTML/MD/DOCX, serialização) em classes sem UI, com testes xUnit em `tests/Fragmo.Tests`.
- O README e os documentos públicos não citam o programa que inspirou a funcionalidade.
- Custo: zero. Prazo: sem data.

## 6. Contexto técnico encontrado

### Código existente para reutilizar / copiar padrão

- `src/Fragmo/Hotkeys/HotkeyManager.cs`: padrão de gancho `WH_KEYBOARD_LL`. O callback só posta mensagem e a captura roda depois. O timer de 60 s reinstala o gancho, que o Windows remove em silêncio quando ele demora.
- `src/Fragmo/Hotkeys/PrintScreenKeyFilter.cs`: exemplo de lógica de teclas isolada e testável. Copiar o padrão para o agrupador de eventos.
- `src/Fragmo/Capture/ScreenCapturer.cs`: `CaptureVirtualScreen`, `Crop` e `GetForegroundWindowBounds` (DWM `EXTENDED_FRAME_BOUNDS`, ignora `Progman`/`WorkerW`/`Shell_TrayWnd`). Generalizar para "janela de nível superior sob o ponto".
- `src/Fragmo/Capture/CoordinateMapper.cs`: conversão tela ↔ bitmap; tudo em pixels físicos (o app é `PerMonitorV2`).
- `src/Fragmo/Ocr/OcrService.cs` + `SensitiveDataDetector.cs`: OCR do Windows e detecção de CPF/CNPJ/e-mail/IPv4/telefone, que devolve retângulos para `RedactAnnotation`.
- `src/Fragmo/Editor/Annotations.cs`: `RectangleAnnotation`, `StepAnnotation`, `RedactAnnotation` e `BlurAnnotation` desenham o destaque e as tarjas na exportação.
- `src/Fragmo/Editor/ImageComposer.cs`: único caminho de composição (base + anotações).
- `src/Fragmo/Overlay/SelectionOverlay.cs`: editor de anotações atual. O construtor recebe um `CapturedScreen` e uma seleção inicial, então dá para passar o print do passo como "tela" com seleção = imagem inteira. Precisa de um resultado novo ("Concluir") no lugar de copiar/salvar.
- `src/Fragmo/Output/OutputService.cs` / `PrintLayout.Fit`: gravação de imagem e encaixe proporcional na página, reaproveitável no PDF.
- `src/Fragmo/Settings/AppSettings.cs`, `SettingsStore.cs`, `SettingsValidator.cs`, `SettingsForm.cs`: onde entram os atalhos e a opção nova.
- `src/Fragmo/TrayContext.cs` / `CaptureWorkflow.cs`: menu da bandeja, `IsBusy` e o padrão de fluxo; o gravador entra como um fluxo novo.
- `src/Fragmo/Strings.cs`: `T(pt, en)` para todo texto visível, incluindo os modelos de texto dos passos.

### Documentação e APIs (com fonte)

- **Gravador de Passos descontinuado** (contexto do problema): "Steps Recorder is no longer being updated and will be removed in a future release of Windows", anunciado em novembro de 2023. https://learn.microsoft.com/windows/whats-new/deprecated-features

- **`WH_MOUSE_LL` / `LowLevelMouseProc`**: o gancho é chamado no contexto da thread que o instalou, por mensagem, e por isso **a thread precisa de loop de mensagens**. Se passar do `LowLevelHooksTimeout` (no máximo 1000 ms desde o Windows 10 1709), o gancho é **removido em silêncio**. A Microsoft recomenda rodar o gancho numa thread dedicada que repassa o trabalho a outra thread e retorna na hora. `wParam` traz `WM_LBUTTONDOWN`, `WM_RBUTTONDOWN` etc. https://learn.microsoft.com/windows/win32/winmsg/lowlevelmouseproc
  ```cpp
  typedef struct tagMSLLHOOKSTRUCT {
    POINT pt;          // coordenadas per-monitor-aware (físicas)
    DWORD mouseData; DWORD flags; DWORD time; ULONG_PTR dwExtraInfo;
  } MSLLHOOKSTRUCT;    // flags bit 0 = LLMHF_INJECTED
  ```
  https://learn.microsoft.com/windows/win32/api/winuser/ns-winuser-msllhookstruct

- **UI Automation, elemento sob o ponto.** API gerenciada: `System.Windows.Automation.AutomationElement.FromPoint(System.Windows.Point)` recebe **coordenadas físicas**. Pela documentação, **se o cliente puder consultar a própria UI, todas as chamadas de UI Automation devem rodar numa thread separada**. https://learn.microsoft.com/dotnet/api/system.windows.automation.automationelement.frompoint
  ```csharp
  var element = AutomationElement.FromPoint(new System.Windows.Point(x, y));
  var info = element.Current; // Name, ControlType, BoundingRectangle, IsPassword, ProcessId
  ```
  - `ElementFromPoint` pode devolver `UIA_E_ELEMENTNOTAVAILABLE` se o elemento sumir; tratar o erro e seguir sem texto. https://learn.microsoft.com/windows/win32/api/uiautomationclient/nf-uiautomationclient-iuiautomation-elementfrompoint
  - Por padrão, UI Automation trabalha com coordenadas físicas; o processo precisa ser DPI-aware (o Fragmo já é `PerMonitorV2`). https://learn.microsoft.com/windows/win32/winauto/uiauto-screenscaling
  - `IsPassword` existe para leitores de tela decidirem se leem o conteúdo; é o mesmo sinal que o Fragmo usa para "Digite a senha". https://learn.microsoft.com/windows/win32/api/uiautomationclient/nf-uiautomationclient-iuiautomationelement-get_currentispassword
  - Num projeto WinForms .NET, `System.Windows.Automation` (UIAutomationClient/UIAutomationTypes) vem com `<UseWPF>true</UseWPF>` ao lado de `<UseWindowsForms>`. É referência de framework, não NuGet. https://learn.microsoft.com/dotnet/core/project-sdk/msbuild-props-desktop
  - **Alternativa** se o tamanho do EXE ou conflitos de tipo (`Point`) incomodarem: COM `IUIAutomation` (`CUIAutomation`) declarado à mão com `[GeneratedComInterface]`, usando `ElementFromPoint` + `GetCurrentPropertyValue`.

- **Excluir a barra de gravação dos prints**: `SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE)`, disponível desde o Windows 10 2004 (bate com o TFM 19041). Confirmar o comportamento com `CopyFromScreen` na Spec.

- **DOCX sem OpenXML SDK**: um `.docx` é um ZIP com `[Content_Types].xml`, `_rels/.rels`, `word/document.xml`, `word/_rels/document.xml.rels` e `word/media/*.png`. A imagem entra num `<w:r><w:drawing><wp:inline>` com `<wp:extent cx cy>` em EMU e um relacionamento por imagem. Qualquer referência quebrada faz o Word recusar o arquivo. Gerar com `System.IO.Compression.ZipArchive` (já está no .NET) e validar abrindo no Word e no LibreOffice.
  - https://en.wikipedia.org/wiki/Office_Open_XML_file_formats
  - https://www.toptal.com/developers/xml/an-informal-introduction-to-docx
  - https://python-docx.readthedocs.io/en/stable/dev/analysis/features/shapes/picture.html

- **PDF sem biblioteca**: escritor mínimo próprio, A4 (595×842 pt):
  - imagens como JPEG (`/Filter /DCTDecode`);
  - texto em Helvetica com `WinAnsiEncoding`, que cobre acentos do português;
  - tabela `xref` calculada.

  Caracteres fora do WinAnsi viram `?` e isso fica documentado. Alternativa descartada: "Microsoft Print to PDF" via `PrintDocument`, porque depende da impressora virtual instalada e não é testável.

### Repositórios de referência

- Uma extensão de navegador open source (MIT) com a mesma ideia serviu de inspiração para o fluxo: gravar, um print por ação com elemento destacado, editor de passos e exportações. **Não citar o nome em material público.** O que aproveitar da ideia: juntar cliques rápidos próximos; mascarar dados sensíveis durante a gravação; avisar que o mascaramento automático não é garantia e o usuário deve revisar.

## 7. Requisitos

### Funcionais

- **RF1:** Iniciar a gravação pelo menu da bandeja "Gravar tutorial" e por atalho global configurável. Parar pelo mesmo atalho ou pela barra flutuante. Enquanto grava, o clique esquerdo no ícone da bandeja não abre captura de área.
- **RF2:** Barra flutuante sempre visível durante a gravação, com contador de passos e os botões Pausar/Continuar, Parar e Descartar. Ela fica excluída dos prints e cliques nela não viram passo.
- **RF3:** Cada clique esquerdo ou direito fora das janelas do Fragmo gera um passo com print da janela de nível superior sob o ponto, coordenadas do clique e, quando houver, nome/tipo/retângulo do elemento via UI Automation.
- **RF4:** Dois cliques esquerdos dentro do tempo e da distância de duplo clique do sistema (`GetDoubleClickTime`, `SM_CXDOUBLECLK`/`SM_CYDOUBLECLK`) viram **um** passo "Clique duas vezes em X".
- **RF5:** Teclas imprimíveis seguidas, sem clique no meio, viram **um** passo "Digite no campo X". O print é tirado depois da última tecla do grupo. O texto digitado nunca é armazenado em memória persistente, disco ou exportação. Se o elemento focado tiver `IsPassword`, o texto do passo é "Digite a senha no campo X".
- **RF6:** Combinações com Ctrl, Alt ou Win, e as teclas Enter, Tab, Esc, Delete e F1–F12, viram passo "Pressione <atalho>". Shift sozinho com tecla imprimível conta como digitação. Os atalhos do próprio Fragmo não viram passo.
- **RF7:** O atalho de passo manual adiciona um passo só com o print da janela ativa e o texto "Observe a tela".
- **RF8:** O texto do passo é gerado por uma função pura a partir de (tipo de ação, tipo de controle, nome do elemento, título da janela, estado de alternância, idioma), com modelos por tipo de controle (botão, menu, aba, link, caixa de seleção, campo, item de lista, genérico). Sem nome de elemento, usa "Clique em <área> da janela “Título”".
- **RF9:** Com "Ocultar dados sensíveis nos tutoriais" ligado, cada passo recebe tarjas (`RedactAnnotation`) nas áreas detectadas pelo OCR. O texto do passo e o título da janela passam pelo mesmo detector; trechos sensíveis são trocados por "•••".
- **RF10:** Os passos são gravados em disco (`%LOCALAPPDATA%\Fragmo\Tutorials\<id>\`) assim que capturados. Ao iniciar, se houver sessão não finalizada, o Fragmo oferece reabri-la ou descartá-la.
- **RF11:** Ao parar, abre o editor de tutorial com todos os passos. Ele suporta todas as operações da seção 4.
- **RF12:** "Editar imagem" abre o editor de anotações atual sobre o print do passo e devolve a imagem anotada. A original fica guardada para "Restaurar original".
- **RF13:** Salvar e abrir o tutorial em arquivo próprio (ZIP: `tutorial.json` + `images/*.png`), com campo de versão de formato.
- **RF14:** Exportar HTML único (CSS embutido, imagens base64, legível impresso e em tema claro), Markdown (`nome.md` + `nome_imagens/passo-01.png`, links relativos), PDF A4 (título, cada passo com número, texto e imagem encaixada; quebra de página quando não couber) e DOCX (título, parágrafos numerados, imagem inline limitada à largura útil da página).
- **RF15:** As exportações usam a imagem final de cada passo, já com destaque, tarjas e anotações, gerada pelo `ImageComposer`.
- **RF16:** Nas Configurações: atalho "Gravar/parar tutorial", atalho "Passo manual" (registrado só durante a gravação) e a caixa "Ocultar dados sensíveis nos tutoriais".

### Não funcionais

- **RNF1 (desempenho):** os callbacks dos ganchos retornam em menos de 5 ms. Captura, UI Automation e OCR rodam fora da thread do gancho. Uma consulta de UI Automation tem limite de 500 ms; passado o limite, o passo segue sem texto do elemento.
- **RNF2 (confiabilidade):** ganchos numa thread dedicada com loop de mensagens e reinstalados periodicamente, como já é feito no `HotkeyManager`. Ganchos só ficam instalados **durante** a gravação.
- **RNF3 (privacidade):**
  - nenhum conteúdo digitado é guardado;
  - eventos injetados (`LLMHF_INJECTED`) são ignorados;
  - a pasta temporária da sessão é apagada ao descartar ou ao salvar/exportar e fechar;
  - o README avisa que a ocultação automática não substitui a revisão;
  - o desfoque continua não sendo apresentado como proteção.
- **RNF4 (memória):** os prints ficam em disco, não em RAM. O editor carrega miniaturas sob demanda. A meta é tutoriais de até 200 passos sem passar de ~300 MB de RAM.
- **RNF5 (DPI e monitores):** coordenadas físicas de ponta a ponta. O destaque precisa bater com o elemento em monitores de 100% e 150% ao mesmo tempo.
- **RNF6 (tamanho):** medir o EXE single-file antes e depois de `UseWPF`. Se crescer mais de ~25 MB, usar a alternativa COM.
- **RNF7 (testes):** lógica pura com testes xUnit: agrupador de eventos, gerador de texto, serialização, HTML/MD/DOCX/PDF (estrutura e referências) e layout PDF. `dotnet test` verde e build sem warnings.

## 8. Riscos e dúvidas em aberto

- [ ] **Antivírus / SmartScreen:** ganchos globais de teclado e mouse num EXE sem assinatura parecem keylogger para heurísticas. Mitigações: ganchos só durante a gravação, indicador sempre visível, conteúdo nunca gravado. Validar com o Defender antes do release.
- [ ] **Janelas elevadas (UIPI):** confirmar num spike se o gancho de baixo nível recebe cliques em janelas de administrador e se UI Automation consegue lê-las a partir do Fragmo sem elevação. Se não der, o passo sai só com print e texto genérico, ou não sai, e isso fica documentado.
- [ ] **Momento do print:** a captura fica fora do gancho, então pode sair alguns milissegundos depois do clique, com um menu já aberto, por exemplo. Medir no spike se é aceitável ou se é preciso capturar dentro do gancho com orçamento de tempo.
- [ ] **Tamanho e conflito de tipos com `UseWPF`:** `System.Windows.Point` versus `System.Drawing.Point`. Isolar o uso de UI Automation num único arquivo com alias.
- [ ] **Qualidade dos nomes UI Automation** em apps Electron, Java e jogos: há fallback para texto genérico. Testar em Explorer, Configurações, Edge/Chrome, Office e um app Electron.
- [ ] **Atalhos padrão:** proposta `Ctrl+Shift+T` (gravar/parar) e `Ctrl+Shift+P` (passo manual). Confirmar que não colidem com atalhos comuns. `Ctrl+Shift+T` reabre aba no navegador, mas como atalho global registrado ele vence; avaliar `Ctrl+Shift+G`.
- [ ] **Extensão do arquivo de tutorial:** proposta `.fragmotut`. Decidir na Spec.
- [ ] **Título da janela** pode trazer dados pessoais (assunto de e-mail, nome de arquivo). Hoje passa só pelo detector do RF9; avaliar trocar pelo nome do aplicativo (`FileDescription` do processo) por padrão.
- [ ] **PDF com caracteres fora do WinAnsi** (nomes de elementos em outros alfabetos ou emoji): aceitar `?` na 0.4.0 ou embutir fonte TrueType depois.
