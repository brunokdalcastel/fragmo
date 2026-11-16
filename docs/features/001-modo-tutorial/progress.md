# Progresso: Modo tutorial

## 2026-11-16 · Fase 1 (fundação nativa e spike): código pronto, spike manual pendente

### Feito
- `Native/User32.cs`: constantes e funções dos ganchos de mouse, laço de mensagens, timer de thread, `WindowFromPoint`/`GetAncestor`, `SetWindowDisplayAffinity`. Também entrou `GetWindowText` e `GetCurrentThreadId` (a Spec não citava, mas são necessários para o título e para o `PostThreadMessage`).
- `Capture/ScreenCapturer.cs`: `GetWindowFrameBounds(hwnd)` extraído e `CaptureRect(rect)`.
- `Tutorial/Input/RawInputEvent.cs`, `Tutorial/Input/InputHookThread.cs`.
- `Tutorial/Capture/WindowLocator.cs`, `ElementInfo.cs`, `ElementInspector.cs` (UIA gerenciada, thread MTA, limite de 500 ms; pedido que estourou o limite esperando na fila é descartado sem chamar a UIA).
- `Tutorial/RecordingBar.cs` (casca: status + Parar, `WDA_EXCLUDEFROMCAPTURE`, abre sem roubar o foco).
- `Tutorial/Model/TutorialModel.cs` criado já com `ElementKind` (o `ElementInfo` precisa dele); a Fase 2 completa o arquivo.
- `Fragmo.csproj`: `<UseWPF>true</UseWPF>` + `<Using Include="System.IO" />` (com WPF o SDK tira `System.IO` dos usings implícitos por causa de `System.Windows.Shapes.Path`). Nenhum arquivo fora de `ElementInspector.cs` importa `System.Windows` (exceto `System.Windows.Forms`).
- Spike: `--tutorial-spike [segundos]` em `Program.cs`, lógica em `Tutorial/TutorialSpike.cs` (sai na Fase 6). Grava em `%TEMP%\fragmo-spike\`: `NNNN.png` (print), `NNNN-hl.png` (print + retângulo do elemento + ponto do clique, para conferir alinhamento/DPI) e `log.txt` (teclas só como grupo: texto/atalho/especial).

### Medições
| Item | Valor |
|---|---|
| EXE single-file antes do `UseWPF` | 58 104 582 bytes (58,1 MB) |
| EXE single-file depois do `UseWPF` | 81 877 134 bytes (81,9 MB) |
| Diferença | +23,8 MB (limite do D5: 25 MB) → **fica a UIA gerenciada** |
| Build Release | 0 warnings |
| `dotnet test` | 164/164 |

Smoke test do spike (5 s, monitor 1920×1080 a 100%): ganchos instalados, 2 cliques gravados, gancho→fila ~0 ms, fila→print (inclui o próprio `CopyFromScreen`) ~108 ms, UIA 107–316 ms. Saída limpa.

### Pendente (manual, para o Bruno)
Rodar `src\Fragmo\bin\Release\net10.0-windows10.0.19041.0\Fragmo.exe --tutorial-spike 120` e clicar em botões/menus de Explorer, Configurações, Edge, Office e um app Electron. Depois conferir:
- [ ] ≥ 80% dos cliques em botões/menus com nome e retângulo corretos (tabela abaixo).
- [ ] A barra de gravação não aparece em nenhum PNG.
- [ ] Retângulo dos `-hl.png` bate com o elemento a 100% e a 150%.
- [ ] Atraso médio clique→print e quantos prints já mostram a reação ao clique (se > 1 em 10, reabrir D6).
- [ ] Janela elevada (Gerenciador de Tarefas): gancho recebe? UIA lê? → regra para a seção Notas da Spec.

| App | Cliques | Com nome e retângulo certos | Observações |
|---|---|---|---|
| Explorer | | | |
| Configurações | | | |
| Edge | | | |
| Office | | | |
| Electron | | | |

## 2026-11-16 · Fase 2 (modelo e lógica pura): CONCLUÍDA

### Feito (build Release: 0 warnings, 0 erros)
- `Tutorial/Model/TutorialModel.cs` completo (+ `TutorialItem.NewId()`).
- `Tutorial/Input/InputEventGrouper.cs`. **Desvio da Spec:** o construtor ficou `(doubleClickMs, doubleClickArea, ignored, typingIdleMs = 1200)`, porque C# não aceita um parâmetro opcional antes de um obrigatório. Teclas ABNT2 0xC1/0xC2 contam como imprimíveis. Atalho ignorado (do Fragmo) também fecha a digitação aberta, para manter a ordem dos passos.
- `Tutorial/Text/StepTextGenerator.cs` + modelos em `Strings.StepTemplateText(StepTemplate, UiLanguage)`. **Acréscimos:** modelos "sem nome" também para clique direito e duplo clique; `Manual` não recebe o sufixo "na janela".
- `Ocr/SensitiveDataDetector.MaskText`. **Desvio:** a linha sintética com `Rectangle.Empty` sugerida na Spec não funciona (o `Find` descarta trecho sem retângulo); a busca por texto virou o método interno `FindInText`, usado pelo `Find` e pelo `MaskText`.
- `Ocr/SensitiveRedactor.cs`; `SelectionOverlay.AutoRedactAsync` já usa esse método.
- `Tutorial/Model/TutorialSerializer.cs` (JSON, pacote, anti zip-slip, só `images/*.png`, limites de tamanho) e `TutorialEditHistory.cs` (as edições sem mudança devolvem o mesmo objeto).

### Testes
- Novos: `InputEventGrouperTests`, `StepTextGeneratorTests` (inclui todo `StepTemplate` com pt ≠ en), `SensitiveRedactorTests`, `TutorialSerializerTests` (JSON, versão futura, caminhos ruins, pacote ida e volta, zip-slip, PNG ausente, arquivo que não é ZIP), `TutorialEditHistoryTests`; `SensitiveDataDetectorTests` ganhou os casos de `MaskText`.
- `dotnet test`: **259/259** (eram 164). Build Release: 0 warnings.
- A Spec foi corrigida nos dois desvios (assinatura do construtor e `MaskText`).

### Próximo
- Fase 3 (gravação). Antes, de preferência, rodar o spike manual da Fase 1 (decide o D6 e a regra das janelas elevadas).

## 2026-11-16 · Fase 3 (gravação): código pronto, testes manuais pendentes

### Feito (build Release: 0 warnings, 0 erros)
- `Tutorial/Recording/RecordingSession.cs`: pasta `%LOCALAPPDATA%\Fragmo\Tutorials\<yyyyMMdd-HHmmss>-<guid8>\`, PNG e JSON gravados de forma atômica (`.tmp` + `File.Move`), `AddStep`/`UpdateItem`/`MarkFinished`/`Delete`, `FindUnfinished(root)`/`Resume(folder)` (a numeração continua depois da maior imagem existente). A raiz é parâmetro para os testes; o app usa `DefaultRoot`.
- `Tutorial/Recording/TutorialRecorder.cs`: fila única (`Channel`) consumida por `Task.Run`; tick de 250 ms por `System.Threading.Timer`; o clique é verificado (D10) e capturado **antes** de ir ao agrupador; UIA depois do print; digitação guarda `FocusedAsync()` no início e cria `Type`/`TypePassword` no fim; atalho e passo manual capturam a janela em primeiro plano. Nome e título passam por `MaskText` antes do texto (RF9); OCR de tarjas numa segunda fila, um por vez, e entra por `UpdateItem`. Erro de disco/pasta → `Failed` e os ganchos saem; erro de captura (tela bloqueada, UAC) só perde aquele passo.
- Duplo clique: o gravador guarda nome/título (já mascarados) do último clique e reescreve o texto só se esse clique ainda é o último passo.
- `Tutorial/RecordingBar.cs`: status (gravando/pausado/processando), Pausar/Continuar, Parar, Descartar; arrastável pelo rótulo; `WS_EX_NOACTIVATE` (clicar na barra não tira o foco da janela gravada). O spike usa `showControls: false`.
- `Tutorial/TutorialWorkflow.cs`: iniciar/parar/descartar (confirma se N > 0), 0 passos → apaga a sessão, balão "Tutorial gravado…"; `CheckUnfinishedOnStartup(quiet)` com Sim/Não/Cancelar (no `--tray` só balão; sessão sem passos é apagada sem perguntar).
- `HotkeyManager`: `ToggleTutorial`, `TutorialManualStep`, `TryUnregister`. `AppSettings`/`SettingsStore`/`SettingsValidator`/`SettingsForm`: os dois atalhos e "Ocultar dados sensíveis nos tutoriais".
- `TrayContext`: item "Gravar tutorial"/"Parar gravação", capturas bloqueadas durante a gravação (balão), clique esquerdo no ícone não faz nada gravando, instalador não fecha o app gravando, sair do app tira os ganchos (a sessão fica para recuperar).
- `SelectionOverlay` já usava `SensitiveRedactor` desde a Fase 2.

### Desvios (Spec corrigida)
- `Discard()` virou `Task DiscardAsync()`.
- O atalho de passo manual é registrado pelo `TrayContext` no evento `RecordingChanged`, não pelo workflow.

### Testes
- Novos: `RecordingSessionTests` (9), `TutorialRecordingTests` (conversão de retângulo, atalhos ignorados, padrões, validação), `SettingsStoreTests` +2 (arquivo antigo sem os campos; ida e volta).
- `dotnet test`: **280/280** (eram 259).

### Limitações conhecidas
- Clicar no ícone da bandeja (ex.: botão direito para parar pelo menu) vira passo, porque a área de notificação é do Explorer. Parar pela barra ou pelo atalho evita.
- Com a UIA lenta (até 500 ms), um segundo clique rápido em outro lugar espera o anterior terminar antes do print.

### Pendente (manual, para o Bruno)
O Fragmo instalado estava aberto (instância única), então o build não foi aberto aqui. Fechar o Fragmo da bandeja e rodar `src\Fragmo\bin\Release\net10.0-windows10.0.19041.0\Fragmo.exe`, depois:
- [ ] 10 cliques no Explorer → 10 PNGs + `tutorial.json` com textos corretos; a barra não aparece nos prints.
- [ ] Duplo clique numa pasta → 1 passo "Clique duas vezes em …".
- [ ] Digitar no Bloco de Notas, depois Ctrl+S → "Digite no campo …" + "Pressione Ctrl+S"; procurar o texto digitado na pasta da sessão não acha nada.
- [ ] Campo de senha → "Digite a senha…".
- [ ] Clique na barra e Ctrl+Shift+G não viram passo.
- [ ] CPF válido visível → tarja em `Redactions`.
- [ ] Matar o processo no meio e reabrir → pergunta de sessão não finalizada.
- [ ] Gravador parado: CPU ~0 e nenhuma thread `InputHookThread`.
- [ ] (Fase 1, ainda pendente) spike manual: `--tutorial-spike 120`.

## 2026-11-16 · Fase 4 (composição e exportadores): CONCLUÍDA (falta abrir no Word)

### Feito (build Release: 0 warnings, 0 erros)
- `Tutorial/Export/StepImageRenderer.cs`: `Render`, `RenderThumbnail` (160 px), `Compose` (sem disco) e `Annotations` (tarjas → retângulo → marcador). Caminho validado por `IsValidImagePath`; PNG lido por bytes (não trava o arquivo).
- `Tutorial/Export/ExportDocument.cs`: `ExportDocument`/`ExportNote`/`ExportStep` + `Build` (um passo por vez, numeração só de passos) e `ExportText` (linhas, título em uma linha, gravação atômica).
- `HtmlExporter.cs`, `MarkdownExporter.cs`, `PdfLayout.cs`, `HelveticaMetrics.cs`, `PdfWriter.cs`, `DocxWriter.cs`. Nenhum pacote NuGet novo.

### Desvios (Spec corrigida)
- Passo `Edited` não recebe **nada** por cima, nem destaque (o texto da Spec só citava as tarjas; o D8 manda os dois).
- `PdfLayout.Plan` recebe `PdfTextMeasure (texto, negrito, tamanho)` em vez de `Func<string,int,float>`: título e notas são em negrito.
- Tabela de larguras em `HelveticaMetrics.cs` (arquivo novo): AFM de ASCII para as duas fontes; acentuadas = letra base.
- Emoji vira um `?` só (o cp1252 sozinho gerava `??`, um por metade do par UTF-16).
- DOCX também limita a altura da imagem (8 in). Markdown apaga só os `passo-NN.png` de uma exportação anterior.
- `Write(doc, Stream)` no PDF e no DOCX (testes sem disco) e `HtmlExporter.Write(doc, path)`.

### Testes
- Novos: `StepImageRendererTests` (inclui `ExportDocument.Build`), `HtmlExporterTests`, `MarkdownExporterTests`, `PdfLayoutTests`, `PdfWriterTests`, `DocxWriterTests` + `ExportTestData`.
- `dotnet test`: **328/328** (eram 280).

### Verificação manual (feita aqui, com um tutorial sintético a partir de `docs/images`)
- PDF do Fragmo renderizado pela API de PDF do Windows: título, notas, acentos, aspas curvas, `\` e parênteses, quebra de página, tarja, destaque e marcador corretos; emoji → `?` (D4).
- DOCX convertido pelo LibreOffice (headless) sem erro; o PDF resultante mostra título, passos e imagens certos.
- **Word não verificado:** a automação COM do Word falhou nesta máquina até com um documento criado pelo próprio Word (`RPC_E_SERVERFAULT`), então o problema é a automação, não o arquivo.

### Pendente (manual, para o Bruno)
- [ ] Abrir um `.docx` exportado no Word e confirmar que abre sem aviso de reparo.
- [ ] Abrir HTML no Edge, MD no VS Code/Obsidian e PDF no Acrobat/SumatraPDF (só a API do Windows e o LibreOffice foram usados aqui).
- [ ] Os checklists manuais das Fases 1 e 3 continuam pendentes.

## 2026-11-16 · Fase 5 (editor de tutorial): código pronto, testes manuais pendentes

### Feito (build Release: 0 warnings, 0 erros)
- `Tutorial/Editor/TutorialEditorForm.cs`: janela redimensionável (mín. 900×600), tema, `AutoScaleMode.Dpi`. Topo: título, Salvar (`Ctrl+S`), Exportar ▾ (HTML, Markdown, PDF, Word), Desfazer (`Ctrl+Z`). Esquerda: `ListBox` owner-draw (número, miniatura, texto; nota em itálico). Direita: imagem composta, texto (Enter/Leave confirma, Shift+Enter quebra linha), "Destacar elemento", Subir, Descer, Excluir (`Del` na lista), Inserir nota, Editar imagem, Restaurar original (só com `Edited`). `Ctrl+Z` com texto ainda não confirmado usa o desfazer da própria caixa.
- `Tutorial/Editor/ThumbnailCache.cs`: LRU de 64 miniaturas; chave = item (record imutável) + número; falha guardada como `null`.
- `Tutorial/Editor/StepImageEditing.cs`: overlay em modo tutorial; grava `images/NNNN-e<k>.png`, `OriginalImageFile` só na 1ª edição.
- `SelectionOverlay`: `OverlayResult.Done` e `tutorialMode`. `EditorToolbar`: modo tutorial (sem copiar/salvar/fixar/imprimir/copiar texto; botão Concluir com ✓). `ThemedColorTable` virou `internal` (menu Exportar).
- `TutorialWorkflow`: abre o editor ao parar, no "Reabrir" da sessão não finalizada e no novo item "Abrir tutorial…" da bandeja; vários editores ao mesmo tempo; `HasOpenEditors` (o instalador não fecha o app com editor aberto).
- `FileNameBuilder.Sanitize` (título → nome de arquivo; tira inválidos, junta espaços, corta em 80, nomes reservados → padrão). `RecordingSession.NewFolderPath`.
- Removidos os textos `TutorialRecorded`/`TutorialRecovered` (o balão e a pasta aberta da Fase 3 deram lugar ao editor).

### Desvios (Spec corrigida)
- **Overlay do "Editar imagem":** fundo do tamanho do monitor com a imagem centralizada, em vez de uma janela do tamanho da imagem. Com imagem pequena a barra (≈ 700 px) não caberia.
- **Cópia de trabalho:** o editor reescreve `tutorial.json` com `Finished = false` a cada mudança. Se o app cair com o editor aberto, a próxima abertura oferece recuperar. Sair do app com alterações não salvas mantém a pasta pelo mesmo motivo; sem alterações, apaga.
- Salvar grava direto no arquivo atual depois da primeira vez (não há "Salvar como").
- Salvar e exportar rodam fora da thread da UI, com a janela travada.

### Testes
- Novos: `TutorialEditorTests.cs` (`ThumbnailCacheTests` 5, `StepImageEditingTests` 2), `FileNameBuilderTests` +8 (`Sanitize`).
- `dotnet test`: **343/343** (eram 328).
- Smoke test visual (teste temporário, removido): tutorial sintético de 4 passos a 125% → lista com miniaturas e números, prévia com destaque e marcador, inserir nota, cópia de trabalho com `finished: false`, `CloseForExit` com gravação nova mantém a pasta. O overlay do "Editar imagem" **não** foi exercitado aqui (é modal e cobre o monitor).

### Pendente (manual, para o Bruno)
O Fragmo instalado está aberto (instância única): fechar pela bandeja e rodar `src\Fragmo\bin\Release\net10.0-windows10.0.19041.0\Fragmo.exe`.
- [ ] Gravar 10 passos → parar → editor abre com 10 passos; renomear título, editar 2 textos, subir/descer, excluir, inserir nota, desligar destaque de 1 passo, desfazer cada operação.
- [ ] "Editar imagem" → desenhar seta → Concluir → miniatura atualizada; Restaurar original volta. Testar com imagem pequena (barra cabe) e Esc (cancela).
- [ ] Salvar `.fragmotut`, fechar, "Abrir tutorial…" → tudo igual; a pasta de trabalho anterior sumiu de `%LOCALAPPDATA%\Fragmo\Tutorials`.
- [ ] Exportar os 4 formatos a partir do editor; texto e imagem editados aparecem.
- [ ] 200 passos (passo manual repetido) → < ~300 MB no Gerenciador de Tarefas com o editor aberto, rolando a lista (RNF4).
- [ ] Monitores 150% + 100%: destaque bate com o elemento nos dois; mover o editor entre eles mantém a lista legível.
- [ ] Matar o processo com o editor aberto e alterado → reabrir → pergunta de recuperação → "Sim" abre o editor.
- [ ] Os checklists manuais das Fases 1, 3 e 4 continuam pendentes.

## 2026-11-16 · Fase 6 (acabamento e release): código pronto, testes manuais pendentes

### Feito
- `--tutorial-spike` removido (`Program.cs`, `Tutorial/TutorialSpike.cs`) e, com ele, o parâmetro `showControls` da `RecordingBar` (só o spike usava).
- README (pt e en): seção "Modo tutorial" (como gravar, o que vira passo, editor, exportações, avisos: ocultação automática não substitui a revisão, título da janela, `.fragmotut` com imagens originais, PDF só com alfabeto latino, janelas de administrador, antivírus), atalhos `Ctrl+Shift+G`/`Ctrl+Shift+Y` na tabela, pasta `Tutorials` em Privacidade.
- Print novo `docs/images/tutorial-editor.png` (teste temporário com `DrawToBitmap`, já apagado; imagens dos passos = `configuracoes.png`).
- Versão 0.4.0 em `Fragmo.csproj` e `installer/Fragmo.iss`; linha da 0.4.0 no histórico de `docs/Spec.md`.
- `StringsTests`: novo teste que exige os mesmos marcadores `{n}` em pt e en.

### Medições
| Item | Valor |
|---|---|
| `dotnet test` | 344/344 |
| `Fragmo.exe` (single-file) | 81 947 691 bytes (78,2 MB) |
| `Fragmo-Setup.exe` | 76 207 054 bytes (72,7 MB) |
| Windows Defender (`MpCmdRun -ScanType 3`) | EXE e Setup: nenhuma ameaça |
| `grep -ri` pelo nome do projeto de inspiração (repo inteiro + EXE) | nada |

### Desvio
- A regra das janelas de administrador vinha do spike da Fase 1, que ainda não rodou. O README diz o caso conservador: o clique pode não virar passo ou sair sem o nome do elemento. Ajustar depois do spike, se ele mostrar outra coisa.

### Pendente (manual, para o Bruno)
- [ ] Teste do PRD: procedimento de 10 cliques → exportado em menos de 1 minuto depois de parar.
- [ ] Os checklists manuais das Fases 1, 3, 4 e 5 (o build não foi aberto aqui porque o Fragmo instalado está rodando).
- [ ] Publicar o release v0.4.0 no GitHub (push + `Fragmo-Setup.exe` e `Fragmo.exe`) depois dos testes manuais.
