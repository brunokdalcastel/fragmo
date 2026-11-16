# Spec: Modo tutorial (gravação de passos)

> Baseada em `docs/features/001-modo-tutorial/PRD.md` · Data: 2026-11-16 · Versão alvo: Fragmo 0.4.0
> Raiz: `C:\Users\Usuario\fragmo`. Caminhos relativos a ela. Uma fase por `/implement N`, com `/clear` entre fases.
> Registrar o andamento em `docs/features/001-modo-tutorial/progress.md`.

## Overview

Um gravador que transforma cliques, digitação e atalhos em passos (print da janela + elemento destacado + texto automático), um editor de tutorial e exportação para HTML, Markdown, PDF e DOCX, tudo local e sem NuGet novo. As regras gerais de `docs/Spec.md` continuam valendo (Nullable, `TreatWarningsAsErrors`, P/Invoke só em `Native/` com `[LibraryImport]`, pixels físicos, `using` em todo GDI+, textos em `Strings.cs`, sem `Co-Authored-By` nos commits).

## Decisões que fecham as dúvidas do PRD

| # | Decisão |
|---|---|
| D1 | Atalhos padrão: **`Ctrl+Shift+G`** gravar/parar (evita o `Ctrl+Shift+T` do navegador) e **`Ctrl+Shift+Y`** passo manual, registrado só durante a gravação. Ambos editáveis. |
| D2 | Arquivo de tutorial: extensão **`.fragmotut`** (ZIP com `tutorial.json` + `images/`). Sem associação de arquivo no instalador nesta versão; abre pelo menu "Abrir tutorial…". |
| D3 | Título da janela entra no texto do passo, **passando pelo mascarador** (RF9) e cortado em 60 caracteres. O README avisa que títulos podem conter dados pessoais. Trocar por nome do aplicativo fica para depois. |
| D4 | PDF: Helvetica + `WinAnsiEncoding`; caractere fora do WinAnsi vira `?`. Documentado no README. |
| D5 | UI Automation: **gerenciada via `<UseWPF>true</UseWPF>`**, isolada em um único arquivo (`ElementInspector.cs`). A Fase 1 mede o EXE; se crescer mais de 25 MB ou houver conflito de tipos sem solução local, troca para COM `IUIAutomation` com `[GeneratedComInterface]` **no mesmo arquivo**, sem mudar a interface pública. |
| D6 | Momento do print: **na descida do botão** (`WM_LBUTTONDOWN`/`WM_RBUTTONDOWN`), pela thread de trabalho, logo que o evento chega. Mostra a tela antes da reação ao clique. Digitação: print quando o grupo fecha (outro evento ou 1,2 s sem tecla). |
| D7 | Destaque e marcador **não são gravados na imagem**: ficam como dados do passo (retângulo do elemento + ponto do clique) e são desenhados na composição (`RectangleAnnotation` na cor de destaque + `StepAnnotation` com o número do passo no ponto do clique). |
| D8 | "Editar imagem" achata: o editor de anotações recebe a imagem **composta** (com tarjas e destaque) e o resultado vira a nova imagem do passo; tarjas automáticas e destaque desse passo deixam de ser aplicados por cima (`Edited = true`). "Restaurar original" volta à imagem crua com tarjas e destaque. |
| D9 | O `.fragmotut` guarda as imagens **originais** (é o projeto editável, como um PSD) e as tarjas como dados. As exportações sempre usam a imagem composta. O diálogo de salvar não avisa; o README sim. |
| D10 | Janelas do próprio Fragmo (barra, editor, configurações) nunca viram passo: o clique é descartado se a janela raiz sob o ponto pertence ao processo do Fragmo; teclas são descartadas se a janela em primeiro plano pertence ao Fragmo. |
| D11 | Pausar não remove os ganchos; o gravador só ignora os eventos. Os ganchos saem ao parar ou descartar. |

## Modelo de dados (referência para todas as fases)

```csharp
namespace Fragmo.Tutorial;

public enum StepAction { Click, RightClick, DoubleClick, Type, TypePassword, Shortcut, Manual }
public enum ElementKind { Other, Button, MenuItem, Tab, Link, CheckBox, RadioButton, Edit, ListItem, TreeItem, ComboBox }
public enum TutorialItemKind { Step, Note }

public sealed record RectDto(int X, int Y, int Width, int Height);   // coordenadas da imagem do passo
public sealed record PointDto(int X, int Y);

public sealed record TutorialItem
{
    public required string Id { get; init; }            // Guid "N"
    public TutorialItemKind Kind { get; init; }
    public string Text { get; init; } = "";             // texto do passo ou da nota
    public StepAction? Action { get; init; }
    public string? ImageFile { get; init; }             // "images/0001.png" (atual)
    public string? OriginalImageFile { get; init; }     // preenchido só quando Edited
    public bool Edited { get; init; }
    public RectDto? Highlight { get; init; }
    public PointDto? ClickPoint { get; init; }
    public bool ShowHighlight { get; init; } = true;
    public IReadOnlyList<RectDto> Redactions { get; init; } = [];
}

public sealed record TutorialDocument
{
    public int FormatVersion { get; init; } = 1;
    public string Title { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; }
    public bool Finished { get; init; }                 // false = sessão de gravação em andamento
    public IReadOnlyList<TutorialItem> Items { get; init; } = [];
}
```

Imutável de propósito: o desfazer guarda listas antigas (as imagens ficam no disco).

---

## Fase 1: Fundação nativa e spike

Objetivo: ter ganchos, localização de janela, UI Automation e exclusão de captura funcionando e medidos antes de construir em cima.

### `src/Fragmo/Native/User32.cs` (MODIFICAR)
- Adicionar: `WH_MOUSE_LL = 14`, `WM_LBUTTONDOWN/WM_RBUTTONDOWN/WM_LBUTTONUP`, `MSLLHOOKSTRUCT` (`POINT pt; uint mouseData, flags, time; nuint dwExtraInfo`), `LLMHF_INJECTED = 1`, `LLKHF_INJECTED = 0x10`, delegate `LowLevelMouseProc`.
- Funções: `WindowFromPoint(POINT)`, `GetAncestor(IntPtr, uint)` (`GA_ROOT = 2`), `GetWindowThreadProcessId`, `GetDoubleClickTime`, `GetSystemMetrics` (`SM_CXDOUBLECLK = 36`, `SM_CYDOUBLECLK = 37`), `SetWindowDisplayAffinity(IntPtr, uint)` (`WDA_EXCLUDEFROMCAPTURE = 0x11`), `GetMessage`, `TranslateMessage`, `DispatchMessage`, `PostThreadMessage`, `SetTimer`/`KillTimer` (timer de thread, `hWnd = 0`), `MSG` struct, `WM_QUIT`, `WM_TIMER`.
- Seguir o padrão existente do arquivo (`internal static partial`, `[LibraryImport]`, `SetLastError` onde a doc pede).

### `src/Fragmo/Tutorial/Input/RawInputEvent.cs` (CRIAR)
- `internal enum RawInputKind { LeftDown, RightDown, KeyDown }`
- `internal readonly record struct RawInputEvent(RawInputKind Kind, Point ScreenPoint, int VirtualKey, HotkeyModifiers Modifiers, uint TimeMs)` — `ScreenPoint` em pixels físicos (o `MSLLHOOKSTRUCT.pt` já é per-monitor-aware). Sem caractere: o conteúdo digitado nunca existe em memória além do código de tecla.

### `src/Fragmo/Tutorial/Input/InputHookThread.cs` (CRIAR)
- Classe `internal sealed class InputHookThread : IDisposable`. Cria uma thread **dedicada** (background, `IsBackground = true`) com loop `GetMessage`, instala `WH_MOUSE_LL` e `WH_KEYBOARD_LL` nela.
- Callbacks: ignoram `nCode < 0`, eventos injetados (`LLMHF_INJECTED`/`LLKHF_INJECTED`), teclas soltas e teclas só-modificador; montam `RawInputEvent` (modificadores via `GetAsyncKeyState`) e chamam `Action<RawInputEvent> sink` — que **só enfileira** (`Channel<RawInputEvent>` sem limite, `TryWrite`). Nenhum trabalho pesado no callback (RNF1: < 5 ms).
- Timer de thread de 60 s reinstala os dois ganchos (mesmo motivo do `HotkeyManager._hookRefresh`).
- `Start()`, `Dispose()` → `PostThreadMessage(WM_QUIT)` + `Join(2 s)`; os ganchos saem na própria thread antes de encerrar.
- Delegates guardados em campos (não deixar o GC coletar).

### `src/Fragmo/Tutorial/Capture/WindowLocator.cs` (CRIAR)
- `internal static Rectangle GetCaptureBounds(Point screenPoint, out string windowTitle, out bool isOwnProcess)`:
  - `WindowFromPoint` → `GetAncestor(GA_ROOT)`; `isOwnProcess` via `GetWindowThreadProcessId == Environment.ProcessId`;
  - classes `Progman`, `WorkerW`, `Shell_TrayWnd`, `Shell_SecondaryTrayWnd`, `#32768` (menu solto) ou janela menor que 200×100 → limites do monitor do ponto (`Screen.FromPoint(...).Bounds`), título vazio;
  - senão `DWMWA_EXTENDED_FRAME_BOUNDS` (extrair do `ScreenCapturer.GetForegroundWindowBounds` um helper `ScreenCapturer.GetWindowFrameBounds(IntPtr hwnd)` e reutilizar), recortado por `SystemInformation.VirtualScreen`.
- `GetForegroundCaptureBounds(out string title, out bool isOwnProcess)` para digitação, atalhos e passo manual.

### `src/Fragmo/Capture/ScreenCapturer.cs` (MODIFICAR)
- Extrair `GetWindowFrameBounds(IntPtr hwnd)` (usado por `GetForegroundWindowBounds` e pelo `WindowLocator`).
- Adicionar `Bitmap CaptureRect(Rectangle screenRect)` — `CopyFromScreen` só da área (mais rápido que a tela virtual inteira).

### `src/Fragmo/Tutorial/Capture/ElementInfo.cs` (CRIAR)
- `internal sealed record ElementInfo(string Name, ElementKind Kind, Rectangle ScreenBounds, bool IsPassword, bool? IsToggledOn)`.

### `src/Fragmo/Tutorial/Capture/ElementInspector.cs` (CRIAR) — **único arquivo que toca UI Automation**
- Alias no topo: `using UiaPoint = System.Windows.Point;` e nada mais do namespace WPF fora deste arquivo.
- `internal sealed class ElementInspector : IDisposable` com uma thread **MTA própria** que processa pedidos em fila (doc: chamadas UIA numa thread separada da UI).
- `Task<ElementInfo?> FromPointAsync(Point screenPoint)` e `Task<ElementInfo?> FocusedAsync()`; cada uma com limite de **500 ms** (`Task.WhenAny` com `Task.Delay`) → `null` no estouro. Captura `ElementNotAvailableException`/`COMException` → `null`.
- Mapeamento `ControlType` → `ElementKind` (Button/SplitButton→Button; MenuItem→MenuItem; TabItem→Tab; Hyperlink→Link; CheckBox→CheckBox; RadioButton→RadioButton; Edit/Document→Edit; ListItem/DataItem→ListItem; TreeItem→TreeItem; ComboBox→ComboBox; resto→Other). `IsToggledOn` via `TogglePattern` quando houver.
- Se o elemento do ponto for de processo do Fragmo, devolve `null`.

### `src/Fragmo/Tutorial/RecordingBar.cs` (CRIAR — só a casca nesta fase)
- `Form` pequeno, sem borda, `TopMost`, `ShowInTaskbar = false`, no canto superior central do monitor principal. No `OnHandleCreated`: `SetWindowDisplayAffinity(Handle, WDA_EXCLUDEFROMCAPTURE)`; se falhar, segue visível e loga `Debug.WriteLine`.
- Nesta fase só mostra "Gravando · 0 passos" e um botão Parar (o resto vem na Fase 3).

### `src/Fragmo/Fragmo.csproj` (MODIFICAR)
- `<UseWPF>true</UseWPF>`. Se `ImplicitUsings` gerar ambiguidade (`Point`, `Application`, `MessageBox`…), remover os usings implícitos de WPF com `<Using Remove="..." />` no `.csproj` ou usar aliases; **nenhum** arquivo fora de `ElementInspector.cs` pode passar a importar `System.Windows`.

### `src/Fragmo/Program.cs` (MODIFICAR — temporário, sai na Fase 6)
- Flag oculta `--tutorial-spike [segundos]`: mostra a `RecordingBar`, liga `InputHookThread` + `ElementInspector` e grava em `%TEMP%\fragmo-spike\` um PNG (`CaptureRect` do `WindowLocator`) por clique e um `log.txt` com: hora do evento, atraso hook→fila, atraso fila→print, tempo da UIA, nome/tipo do elemento, retângulo. Sem gravar teclas imprimíveis no log (só "KeyDown <grupo>").

### Verificação Fase 1
- [x] `dotnet build -c Release` sem warnings; `dotnet test` verde.
- [x] EXE single-file medido antes/depois de `UseWPF` (anotar os dois tamanhos no `progress.md`). Se > +25 MB, trocar para COM conforme D5 e medir de novo.
- [ ] Spike: em Explorer, Configurações, Edge, Office (se houver) e um app Electron, ≥ 80% dos cliques em botões/menus trazem nome e retângulo corretos (anotar a tabela no `progress.md`).
- [ ] Spike: a `RecordingBar` **não** aparece nos PNGs gerados.
- [ ] Spike: retângulo do elemento bate com o print num monitor a 100% e noutro a 150% (ou mudando a escala do único monitor).
- [ ] Spike: anotar atraso médio clique→print e se algum print já mostra a reação ao clique (menu aberto). Se mais de 1 em 10 mostrar, reabrir D6 antes da Fase 3.
- [ ] Spike: anotar o que acontece clicando numa janela elevada (Gerenciador de Tarefas): gancho recebe? UIA lê? Registrar a regra resultante no `progress.md` e na seção Notas desta Spec.

---

## Fase 2: Modelo e lógica pura (com testes)

### `src/Fragmo/Tutorial/Model/TutorialModel.cs` (CRIAR)
- Os tipos da seção "Modelo de dados" acima.

### `src/Fragmo/Tutorial/Input/InputEventGrouper.cs` (CRIAR)
- Lógica pura, padrão do `PrintScreenKeyFilter`. Construtor: `(uint doubleClickMs, Size doubleClickArea, IReadOnlySet<HotkeyBinding> ignored, int typingIdleMs = 1200)`.
- `IReadOnlyList<GroupedAction> Push(RawInputEvent e)` e `IReadOnlyList<GroupedAction> Tick(uint nowMs)` (fecha o grupo de digitação por inatividade) e `Flush()` (ao parar).
- `GroupedAction`: `record GroupedAction(GroupedKind Kind, RawInputEvent Source, HotkeyBinding? Shortcut)` com `GroupedKind { NewClick, NewRightClick, PromoteLastToDoubleClick, TypingStarted, TypingFinished, Shortcut }`.
- Regras:
  - 2º `LeftDown` dentro de `doubleClickMs` e de `doubleClickArea` (centrada no 1º) → `PromoteLastToDoubleClick` (reaproveita o print do 1º); 3º clique começa um novo ciclo.
  - Tecla imprimível sem Ctrl/Alt/Win (Shift permitido) → `TypingStarted` na primeira; as seguintes não geram nada; fecha com `TypingFinished` por clique, atalho, `Tick` após `typingIdleMs` ou `Flush`. Backspace/Delete/setas dentro de um grupo de digitação continuam no grupo.
  - Ctrl/Alt/Win + tecla, ou Enter, Tab, Esc, Delete (fora de digitação), F1–F12 → fecha digitação aberta e emite `Shortcut`. Combinações que estão em `ignored` (atalhos do Fragmo) são descartadas.
  - "Imprimível" = `Keys.A–Z`, `D0–D9`, `NumPad0–9`, `Space`, `Oem*`, `Decimal`, `Add`, `Subtract`, `Multiply`, `Divide`.

### `src/Fragmo/Tutorial/Text/StepTextGenerator.cs` (CRIAR)
- `public static string Generate(StepTextInput input)` com `record StepTextInput(StepAction Action, ElementKind Kind, string? ElementName, string? WindowTitle, bool? IsToggledOn, string? Shortcut, bool MentionWindow, UiLanguage Language)`.
- `Normalize(string?)`: colapsa espaços/quebras, corta em 60 caracteres com "…", vazio → null.
- Modelos (pt; en equivalente). Aspas tipográficas “ ”:

| Ação / tipo | pt | en |
|---|---|---|
| Click Button | Clique no botão “X” | Click the “X” button |
| Click MenuItem | Clique no menu “X” | Click the “X” menu |
| Click Tab | Clique na aba “X” | Click the “X” tab |
| Click Link | Clique no link “X” | Click the “X” link |
| Click CheckBox | Marque a caixa “X” / Desmarque a caixa “X” (por `IsToggledOn` antes do clique; null → Clique na caixa “X”) | Check / Uncheck / Click the “X” box |
| Click RadioButton | Selecione a opção “X” | Select the “X” option |
| Click Edit | Clique no campo “X” | Click the “X” field |
| Click ListItem/TreeItem | Selecione “X” | Select “X” |
| Click ComboBox | Abra a lista “X” | Open the “X” list |
| Click Other | Clique em “X” | Click “X” |
| Sem nome | Clique na janela “T” (sem título: Clique na tela) | Click in the “T” window / Click the screen |
| RightClick | Clique com o botão direito em “X” | Right-click “X” |
| DoubleClick | Clique duas vezes em “X” | Double-click “X” |
| Type | Digite no campo “X” (sem nome: Digite o texto) | Type in the “X” field / Type the text |
| TypePassword | Digite a senha no campo “X” (sem nome: Digite a senha) | Type the password in the “X” field / Type the password |
| Shortcut | Pressione Ctrl+C | Press Ctrl+C |
| Manual | Observe a tela | Look at the screen |

  - `MentionWindow = true` (janela diferente do passo anterior) e título presente → acrescenta " na janela “T”" / " in the “T” window", exceto quando o modelo já é o "Sem nome".
- Os modelos ficam em `Strings.cs` como `string.Format` (ver arquivo abaixo); o gerador recebe o idioma para os testes não dependerem do estado global — implementar com um `Strings.For(UiLanguage)`-style helper ou passando o idioma ao `T`.

### `src/Fragmo/Ocr/SensitiveDataDetector.cs` (MODIFICAR)
- `public static string MaskText(string text)`: aplica as mesmas regras do `Find` direto sobre o texto (método interno `FindInText`, compartilhado com o `Find`) e troca cada trecho achado por "•••". (A linha sintética com `Rectangle.Empty` não serve: o `Find` descarta trecho sem retângulo.) Usado no texto do passo e no título (RF9).

### `src/Fragmo/Ocr/SensitiveRedactor.cs` (CRIAR)
- Extrair do `SelectionOverlay.AutoRedactAsync` a parte pura: `public static IReadOnlyList<Rectangle> Targets(OcrText text, Rectangle area, int padding)` (desloca para `area`, infla `padding`, recorta em `area`, remove vazios). O overlay passa a chamar esse método (comportamento idêntico; testes do overlay continuam verdes).

### `src/Fragmo/Tutorial/Model/TutorialSerializer.cs` (CRIAR)
- `System.Text.Json`, camelCase, enums como string, `WriteIndented`.
- `string ToJson(TutorialDocument)`, `TutorialDocument FromJson(string)` → lança `TutorialFormatException` se `FormatVersion` > 1, JSON inválido ou `ImageFile` fora de `images/` (bloquear `..`, caminho absoluto e `\`).
- `void SavePackage(TutorialDocument doc, string sourceFolder, string targetFile)`: grava em `targetFile + ".tmp"` um ZIP com `tutorial.json` + todos os arquivos referenciados (`ImageFile` e `OriginalImageFile`), depois `File.Move(overwrite: true)`.
- `TutorialDocument OpenPackage(string file, string targetFolder)`: extrai só `tutorial.json` e entradas `images/*.png` cujo caminho final fica dentro de `targetFolder` (anti zip-slip), valida que todo `ImageFile` existe.

### `src/Fragmo/Tutorial/Model/TutorialEditHistory.cs` (CRIAR)
- Pilha de desfazer de `TutorialDocument` (limite 50): `Push(doc)`, `TryUndo(out doc)`, `CanUndo`. Só operações estruturais e de texto confirmadas (subir, descer, excluir, inserir nota, editar texto ao sair do campo, ligar/desligar destaque, editar/restaurar imagem).
- Funções puras de edição: `static TutorialDocument MoveUp(doc, id)`, `MoveDown`, `Remove`, `InsertNoteAfter(doc, id?, text)`, `SetText`, `SetShowHighlight`, `SetTitle`.

### `src/Fragmo/Strings.cs` (MODIFICAR)
- Todos os modelos da tabela acima, mais os textos de UI que as fases 3–5 vão usar (podem entrar aqui ou nas fases; manter pt/en lado a lado).

### Testes — `tests/Fragmo.Tests/` (CRIAR)
- `InputEventGrouperTests.cs`: clique simples; duplo clique dentro/fora do tempo e da área; triplo clique; digitação agrupada fechada por clique, por atalho, por inatividade e por `Flush`; Shift+letra conta como digitação; Ctrl+C vira atalho; Enter fora de digitação vira atalho; atalho do Fragmo ignorado; Backspace dentro do grupo.
- `StepTextGeneratorTests.cs`: cada linha da tabela em pt e en; corte em 60 com "…"; `MentionWindow`; sem nome e sem título.
- `SensitiveDataDetectorTests.cs` (MODIFICAR): `MaskText` com CPF, e-mail e texto limpo.
- `SensitiveRedactorTests.cs`: deslocamento, padding e recorte.
- `TutorialSerializerTests.cs`: ida e volta JSON; versão futura rejeitada; caminho com `..` rejeitado; pacote ida e volta numa pasta temporária com 2 PNGs; zip-slip rejeitado.
- `TutorialEditHistoryTests.cs`: cada operação, limites (subir o primeiro, descer o último), desfazer.

### Verificação Fase 2
- [x] `dotnet test` verde, incluindo os 6 arquivos novos/alterados acima.
- [x] `dotnet build -c Release` sem warnings.
- [x] Nenhum tipo em `Tutorial/Model`, `Tutorial/Text` ou `InputEventGrouper` referencia `System.Windows.Forms` (exceto `Keys`) nem UI Automation.

---

## Fase 3: Gravação (sessão em disco, gravador, barra, bandeja, configurações)

### `src/Fragmo/Tutorial/Recording/RecordingSession.cs` (CRIAR)
- Pasta `%LOCALAPPDATA%\Fragmo\Tutorials\<yyyyMMdd-HHmmss>-<guid8>\` com `tutorial.json` (`Finished = false`) e `images/NNNN.png`.
- `AddStep(Bitmap image, TutorialItem item)` grava o PNG e reescreve o JSON de forma atômica (`.tmp` + `File.Move`); `UpdateItem(id, Func<TutorialItem,TutorialItem>)`; `MarkFinished()`; `Delete()`.
- `static IReadOnlyList<string> FindUnfinished()` e `static RecordingSession Resume(string folder)`.
- `Document` (somente leitura) e `Folder`.

### `src/Fragmo/Tutorial/Recording/TutorialRecorder.cs` (CRIAR)
- Dono de `InputHookThread`, `ElementInspector`, `InputEventGrouper`, `RecordingSession`.
- Thread de trabalho (`Task.Run` lendo o `Channel`) — nunca a thread da UI nem a do gancho. Para cada `GroupedAction`:
  - `NewClick`/`NewRightClick`: `WindowLocator.GetCaptureBounds` → se `isOwnProcess`, descarta; senão `ScreenCapturer.CaptureRect` **imediatamente**, depois `ElementInspector.FromPointAsync`, converte `ScreenBounds` e ponto para coordenadas da imagem (subtrai a origem da captura, `Rectangle.Intersect` com a imagem), gera o texto e chama `session.AddStep`.
  - `PromoteLastToDoubleClick`: `UpdateItem` do último passo → `DoubleClick` + texto novo.
  - `TypingStarted`: guarda `FocusedAsync()` (nome/`IsPassword`) e a janela; `TypingFinished`: captura a janela em primeiro plano e cria passo `Type`/`TypePassword`.
  - `Shortcut`: captura a janela em primeiro plano e cria passo `Shortcut` com `HotkeyBinding.ToString()`.
  - `ManualStep()` (chamado pelo atalho): passo `Manual`.
- `Tick` do agrupador a cada 250 ms (timer na thread de trabalho).
- Ocultação (RF9), se `settings.RedactTutorials`: o texto e o título passam por `SensitiveDataDetector.MaskText` **antes** de gerar o texto; o OCR do print roda depois de gravar o passo (`OcrService.RecognizeAsync` + `SensitiveRedactor.Targets(text, imagem inteira, 3)`) e entra com `UpdateItem(... Redactions = ...)`. Contador `PendingOcr` exposto.
- Eventos para a UI (sempre via `SynchronizationContext` capturado no construtor): `StepCountChanged(int)`, `Failed(string)`.
- `Pause()`/`Resume()` (D11), `Task<RecordingSession> StopAsync()` (flush do agrupador, espera a fila e o OCR pendente, `MarkFinished`; depois de uma falha não marca, e a sessão volta como "não finalizada"), `Task DiscardAsync()` (para tudo, espera as filas e `session.Delete()` — assíncrono para não apagar a pasta com o OCR ainda escrevendo).
- Antes de cada passo, se a pasta de sessão sumiu ou o disco encheu: `Failed` e para a gravação.

### `src/Fragmo/Tutorial/RecordingBar.cs` (MODIFICAR)
- "● Gravando · N passos" + botões Pausar/Continuar, Parar, Descartar (Descartar pede confirmação se N > 0). Arrastável pelo rótulo. Tema via `Theme.Resolve`. Mostra "Processando…" enquanto `StopAsync` espera o OCR.

### `src/Fragmo/Tutorial/TutorialWorkflow.cs` (CRIAR)
- `internal sealed class TutorialWorkflow(AppSettings settings, TrayContext tray)`:
  - `bool IsRecording`; `void ToggleRecording()`; `void ManualStep()`; `OpenTutorialFile()` só entra na Fase 5 (o item "Abrir tutorial…" não aparece no menu antes disso).
  - Ao iniciar: mostra a `RecordingBar` e dispara `RecordingChanged(true)`; o `TrayContext` registra/desregistra o atalho de passo manual nesse evento (e o inclui no `RegisterAllHotkeys` enquanto grava, para sobreviver à tela de configurações). Ao parar: desregistra, fecha a barra e (Fase 5) abre o editor; nesta fase mostra balão "Tutorial gravado: N passos em <pasta>".
  - `CheckUnfinishedOnStartup()`: para cada sessão de `FindUnfinished()`, `MessageBox` Sim (reabrir — na Fase 3 apenas marca como terminada e mostra a pasta), Não (descartar) ou Cancelar (perguntar na próxima abertura).

### `src/Fragmo/Hotkeys/HotkeyManager.cs` (MODIFICAR)
- `HotkeyAction` ganha `ToggleTutorial`, `TutorialManualStep`. Expor `TryUnregister(HotkeyAction)` para o passo manual sair ao parar.

### `src/Fragmo/Settings/AppSettings.cs` (MODIFICAR)
- `HotkeySettings.ToggleTutorial = "Ctrl+Shift+G"`, `HotkeySettings.TutorialManualStep = "Ctrl+Shift+Y"`, `AppSettings.RedactTutorials = true`. Settings antigos sem os campos carregam com os padrões (conferir em `SettingsStoreTests`).

### `src/Fragmo/Settings/SettingsValidator.cs` (MODIFICAR)
- `HotkeyEntries` inclui os dois atalhos (duplicidade já é verificada). `RegisterAllHotkeys` no `TrayContext` **pula** `TutorialManualStep` (registrado só durante a gravação).

### `src/Fragmo/Settings/SettingsForm.cs` (MODIFICAR)
- Dois `HotkeyBox` novos e a caixa "Ocultar dados sensíveis nos tutoriais", seguindo o layout das linhas atuais.

### `src/Fragmo/TrayContext.cs` (MODIFICAR)
- Menu: "Gravar tutorial" (vira "Parar gravação" durante a gravação). `OnHotkeyPressed` trata as duas ações novas. Clique esquerdo no ícone durante a gravação **não** abre captura (RF1). Captura de área, janela etc. ficam bloqueadas durante a gravação (balão "Pare a gravação primeiro"). Chamar `CheckUnfinishedOnStartup()` depois de registrar os atalhos (fora do modo `--tray`, só avisa por balão).
- A lista de atalhos ignorados pelo agrupador = todos os atalhos registrados do Fragmo.

### `src/Fragmo/Overlay/SelectionOverlay.cs` (MODIFICAR)
- Usar `SensitiveRedactor.Targets` (refatoração da Fase 2, se ainda não feita).

### Verificação Fase 3
- [ ] `dotnet build -c Release` sem warnings; `dotnet test` verde (incluindo `SettingsStoreTests`/`SettingsValidationTests` com os campos novos).
- [ ] Manual: gravar 10 cliques no Explorer → 10 PNGs + `tutorial.json` com textos corretos; a barra não aparece nos prints.
- [ ] Manual: duplo clique numa pasta → 1 passo "Clique duas vezes em …".
- [ ] Manual: digitar no Bloco de Notas, depois Ctrl+S → passo "Digite no campo …" + passo "Pressione Ctrl+S"; `grep` do texto digitado na pasta da sessão não encontra nada.
- [ ] Manual: campo de senha (ex.: diálogo de credencial) → "Digite a senha…".
- [ ] Manual: clique na barra e Ctrl+Shift+G não viram passo.
- [ ] Manual: um CPF válido visível na janela sai com tarja em `Redactions`.
- [ ] Manual: matar o processo no meio da gravação e reabrir → pergunta de sessão não finalizada.
- [ ] Manual: Gerenciador de Tarefas mostra uso de CPU ~0 com o gravador parado e os ganchos removidos (nenhuma thread `InputHookThread` viva).

---

## Fase 4: Composição e exportadores (com testes)

### `src/Fragmo/Tutorial/Export/StepImageRenderer.cs` (CRIAR)
- `Bitmap Render(TutorialItem step, int stepNumber, string folder)`: carrega `ImageFile` e chama `ImageComposer.Compose` com a imagem inteira como seleção e, só se `!Edited` (D8: passo editado já traz tudo achatado, nada é desenhado por cima): `RedactAnnotation` por `Redactions`; se `ShowHighlight` e `Highlight` != null: `RectangleAnnotation(new StrokeStyle(Palette.Accent, 3), Highlight)`; se `ShowHighlight` e `ClickPoint` != null: `StepAnnotation(Palette.Accent, stepNumber, ClickPoint, 14)`. Ordem: tarjas → retângulo → marcador. O caminho da imagem passa por `TutorialSerializer.IsValidImagePath` e o PNG é lido por bytes (não trava o arquivo). `Compose(Bitmap, TutorialItem, int)` faz a composição sem disco.
- `Bitmap RenderThumbnail(...)` (largura 160, para o editor).

### `src/Fragmo/Tutorial/Export/ExportDocument.cs` (CRIAR)
- `record ExportDocument(string Title, IReadOnlyList<ExportEntry> Entries)`; `ExportEntry` = `ExportNote(string Text)` | `ExportStep(int Number, string Text, byte[] Png, Size PixelSize)` (`Png` vazio = passo sem imagem, `HasImage`). Regras comuns de texto e gravação atômica (`.tmp` + `File.Move`) em `ExportText`, no mesmo arquivo.
- `static ExportDocument Build(TutorialDocument doc, string folder)` (numeração só conta passos; usa o `StepImageRenderer`; um passo por vez para não segurar todos os bitmaps).

### `src/Fragmo/Tutorial/Export/HtmlExporter.cs` (CRIAR)
- `string Build(ExportDocument)`: HTML5 único, `<meta charset="utf-8">`, CSS embutido (tema claro, `max-width: 960px`, `img { max-width: 100% }`, `@media print { .step { break-inside: avoid } }`), imagens `data:image/png;base64,…`, todo texto via `WebUtility.HtmlEncode`. Notas como `<h2>`. `Write(doc, path)` grava UTF-8 sem BOM. `alt` da imagem = texto do passo.

### `src/Fragmo/Tutorial/Export/MarkdownExporter.cs` (CRIAR)
- `void Write(ExportDocument doc, string mdPath)`: cria `<nome>_imagens/passo-01.png` (2 dígitos; 3 se > 99) ao lado do `.md`; links relativos com `/` e espaços como `%20`; escapa `*_[]#<>` no texto; notas como `## `. Acentos ficam crus no link; espaço, parênteses, `%`, `#`, `?` são codificados. Antes de gravar, apaga só os `passo-NN.png` de uma exportação anterior na mesma pasta.

### `src/Fragmo/Tutorial/Export/PdfWriter.cs` (CRIAR)
- Escritor PDF 1.4 mínimo, A4 595×842 pt, margem 40 pt.
- Objetos: catálogo, páginas, uma fonte `Helvetica` e `Helvetica-Bold` (`/Encoding /WinAnsiEncoding`), uma imagem por passo (`/XObject /Image`, `/DCTDecode`, JPEG qualidade 85, `/ColorSpace /DeviceRGB`), um stream de conteúdo por página. `xref` com offsets em bytes e `trailer`.
- Codificação de texto: `Encoding.GetEncoding(1252)` com `CodePagesEncodingProvider` registrado; fora do WinAnsi → `?` (D4); escapar `\ ( )`.
- Quebra de linha do texto por largura aproximada: `HelveticaMetrics.cs` (CRIAR) com as larguras AFM de ASCII para Helvetica e Helvetica-Bold; na metade alta do cp1252 a letra acentuada usa a largura da letra base e os símbolos comuns têm valor próprio. Cada caractere fora do BMP (emoji) vira **um** `?`, não um por metade do par UTF-16.
- `Write(doc, Stream)` além de `Write(doc, path)`; `/Info` com o título em UTF-16BE.
- Layout puro testável em `PdfLayout.cs` (CRIAR): `IReadOnlyList<PdfPagePlan> Plan(ExportDocument doc, PdfTextMeasure measure)` (delegate `(texto, negrito, tamanho) → pontos`, porque o título e as notas são em negrito; coordenadas a partir do topo da página) — título na 1ª página; cada passo = "N. texto" + imagem encaixada com `PrintLayout.Fit` na largura útil e altura máxima de 60% da página; se não couber no espaço restante, vai para a próxima página; imagem mais alta que a página inteira é reduzida. Imagem a 0,75 pt/px (96 DPI), sem ampliar.

### `src/Fragmo/Tutorial/Export/DocxWriter.cs` (CRIAR)
- `void Write(ExportDocument doc, string path)` com `ZipArchive`: `[Content_Types].xml` (Default `png`, `rels`, `xml`; Override `/word/document.xml`), `_rels/.rels`, `word/document.xml`, `word/_rels/document.xml.rels` (`rId1..n` → `media/imageN.png`), `word/media/imageN.png`.
- Título com `w:sz=40` negrito; cada passo: parágrafo "N. texto" + parágrafo com `<w:drawing><wp:inline>` (`wp:extent` em EMU = px × 9525, limitado à largura útil 6 in = 5 486 400 EMU mantendo proporção, e também a 8 in de altura, `wp:docPr id` único, `a:graphic`/`pic:pic` com `r:embed`). Seção A4 (`w:pgSz w:w="11906" w:h="16838"`), margens 1 in. Notas como parágrafo negrito.
- Texto via `SecurityElement.Escape`, `xml:space="preserve"`; antes disso tira os caracteres que o XML 1.0 não aceita; quebra de linha vira `<w:br/>`. Parágrafo do texto do passo com `w:keepNext` (fica junto da imagem). `Write(doc, Stream)` além de `Write(doc, path)`.

### Testes — `tests/Fragmo.Tests/` (CRIAR)
- `StepImageRendererTests.cs`: com `Edited` não desenha tarjas; `ShowHighlight=false` não desenha retângulo nem marcador (comparar pixels como em `RedactAnnotationTests`).
- `HtmlExporterTests.cs`: escapa `<script>`; uma `<img src="data:image/png;base64,` por passo; notas não numeradas.
- `MarkdownExporterTests.cs`: arquivos criados, links relativos válidos, escape.
- `PdfWriterTests.cs`: começa com `%PDF-1.4`, termina com `%%EOF`; cada offset do `xref` aponta para `N 0 obj`; número de `/Type /Page` = páginas planejadas; "ação" vira bytes 1252 corretos; emoji vira `?`.
- `PdfLayoutTests.cs`: quebra de página quando a imagem não cabe; imagem gigante reduzida; título só na 1ª página.
- `DocxWriterTests.cs`: abrir o ZIP, todo `r:embed` existe nos rels, todo alvo existe no ZIP, `document.xml` é XML válido, `docPr id` únicos, extent ≤ largura útil.

### Verificação Fase 4
- [ ] `dotnet test` verde; build sem warnings.
- [ ] Manual: exportar um tutorial gerado na Fase 3 (via teste manual ou chamada temporária) nos 4 formatos e abrir: HTML no Edge, MD no VS Code/Obsidian, PDF no Edge e no Acrobat/SumatraPDF, DOCX no Word **e** no LibreOffice, sem aviso de reparo.

---

## Fase 5: Editor de tutorial

### `src/Fragmo/Tutorial/Editor/TutorialEditorForm.cs` (CRIAR)
- Janela normal redimensionável (mínimo 900×600), tema via `Theme.Resolve`, `AutoScaleMode.Dpi`.
- Topo: caixa do título; botões Salvar (`Ctrl+S`), Exportar ▾ (HTML, Markdown, PDF, Word), Desfazer (`Ctrl+Z`).
- Esquerda: `ListBox` owner-draw (número, miniatura 160 px, texto em 2 linhas; nota em itálico sem miniatura). Miniaturas sob demanda num cache LRU de 64 itens (RNF4).
- Direita: imagem do passo composta (`StepImageRenderer`) num `PictureBox` com `Zoom`; `TextBox` multilinha do texto (confirma no `Leave`/Enter → `TutorialEditHistory`); caixa "Destacar elemento"; botões Subir, Descer, Excluir (`Del`), Inserir nota, Editar imagem, Restaurar original (visível só se `Edited`).
- Estado: `TutorialDocument` atual + `TutorialEditHistory` + pasta de trabalho + `string? currentFile` + `bool dirty` (gravação nova começa `dirty`; pacote aberto, não).
- Cópia de trabalho: a cada mudança o editor reescreve `tutorial.json` na pasta de trabalho com `Finished = false` (atômico). Se o app cair com o editor aberto, a abertura seguinte oferece recuperar (e "Reabrir" abre o editor).
- Salvar: `.fragmotut` via `TutorialSerializer.SavePackage` (nome sugerido = título saneado com `FileNameBuilder.Sanitize`). Depois do primeiro salvamento, salva direto no mesmo arquivo (sem "Salvar como"). Exportar: `ExportDocument.Build` + exportador escolhido, fora da thread da UI com a janela travada e cursor de espera; o diálogo lembra a última pasta (`settings.SaveFolder` por padrão).
- Fechar: se `dirty`, pergunta Salvar / Descartar / Cancelar. Depois de fechar (salvo ou descartado), apaga a pasta de trabalho (RNF3). Se só exportou e não salvou, também pergunta. Desligamento do Windows ou saída do app (`CloseForExit`): fecha sem perguntar; com alterações não salvas a pasta fica para recuperar.

### `src/Fragmo/Tutorial/Editor/StepImageEditing.cs` (CRIAR)
- `Bitmap? EditInOverlay(Bitmap composed, AppSettings settings, IWin32Window owner)`: monta `CapturedScreen(fundo, bounds)` com `bounds` = monitor onde está o editor (crescendo para a direita/baixo se a imagem for maior; aí a janela do overlay passa da tela, limitação documentada) e a imagem centralizada nesse fundo — assim a barra sempre cabe, mesmo com imagem pequena. Abre `SelectionOverlay` em modo tutorial com seleção = a imagem e devolve `ComposeResult()` se `Result == OverlayResult.Done`. Geometria testável em `CanvasBounds`/`ImagePlacement`.
- No editor: grava como `images/NNNN-e<k>.png`, `OriginalImageFile` = o original (só na primeira edição), `Edited = true`. "Restaurar original" faz o inverso.

### `src/Fragmo/Overlay/SelectionOverlay.cs` (MODIFICAR)
- `OverlayResult.Done`; parâmetro opcional `bool tutorialMode = false`. Em modo tutorial: a seleção é a imagem do passo e não pode ser movida, redimensionada nem refeita (sem borda nem alças; o véu só escurece o fundo em volta), toolbar só com ferramentas + Concluir + Cancelar, `Enter`/`Ctrl+C`/`Ctrl+S` → `Done`, `Ctrl+T`/`Ctrl+P`/`O` ignorados, botão direito não faz nada, `Esc` → Cancel, auto-ocultação não roda sozinha (o botão e a tecla `X` continuam).

### `src/Fragmo/Editor/EditorToolbar.cs` (MODIFICAR)
- Modo tutorial: esconde Copiar, Salvar, Fixar, Imprimir, Copiar texto; mostra botão "Concluir" (`DoneClicked`).

### `src/Fragmo/Tutorial/TutorialWorkflow.cs` (MODIFICAR)
- Ao parar: abre `TutorialEditorForm` (não modal; uma por vez; `IsBusy` do `CaptureWorkflow` não é afetado). Gravar de novo com o editor aberto é permitido (abre um segundo editor ao parar).
- "Abrir tutorial…" no menu da bandeja: `OpenPackage` numa pasta de trabalho nova em `Tutorials\` (`RecordingSession.NewFolderPath`) e abre o editor; erro de formato → apaga a pasta e `MessageBox` com a mensagem. O mesmo arquivo já aberto num editor → traz esse editor para a frente.
- `HasOpenEditors`: o pedido de fechamento do instalador é recusado com editor aberto; ao sair do app, `Dispose` chama `CloseForExit` em cada editor.
- Sessão não finalizada → "Reabrir" agora abre o editor.

### Verificação Fase 5
- [ ] Build sem warnings; `dotnet test` verde.
- [ ] Manual: gravar 10 passos → parar → editor abre com 10 passos; renomear título, editar 2 textos, subir/descer, excluir, inserir nota, desligar destaque de 1 passo, desfazer cada operação.
- [ ] Manual: "Editar imagem" → desenhar seta → Concluir → miniatura atualizada; Restaurar original volta.
- [ ] Manual: salvar `.fragmotut`, fechar, "Abrir tutorial…" → tudo igual; a pasta de trabalho anterior foi apagada.
- [ ] Manual: exportar os 4 formatos a partir do editor; texto editado e imagem editada aparecem.
- [ ] Manual: tutorial de 200 passos (passo manual repetido) → Gerenciador de Tarefas < ~300 MB com o editor aberto e rolando a lista (RNF4).
- [ ] Manual: monitor 150% + 100%: destaque bate com o elemento nos dois.

---

## Fase 6: Acabamento e release

### `src/Fragmo/Program.cs` (MODIFICAR)
- Remover `--tutorial-spike`.

### `README.md` (MODIFICAR)
- Seção "Modo tutorial": como gravar, atalhos, o que vira passo, exportações; avisos: a ocultação automática não substitui a revisão; títulos de janela podem ter dados pessoais; o `.fragmotut` guarda as imagens originais; PDF não mostra caracteres fora do alfabeto latino; janelas de administrador (regra da Fase 1); antivírus pode estranhar ganchos de teclado/mouse (só ativos durante a gravação, nada digitado é gravado). **Não citar** o projeto que inspirou a funcionalidade.
- Print novo do editor de tutorial em `docs/images/`.

### `src/Fragmo/Fragmo.csproj` + `installer/Fragmo.iss` (MODIFICAR)
- Versão `0.4.0` nos dois.

### `tests/Fragmo.Tests/StringsTests.cs` (MODIFICAR, se necessário)
- Garantir que os textos novos têm pt e en.

### Verificação Fase 6
- [x] `publish.ps1` gera EXE e Setup; anotar tamanho final no `progress.md`.
- [x] Windows Defender (varredura personalizada do EXE e do Setup) sem detecção; anotar.
- [ ] Teste do PRD: procedimento de 10 cliques → exportado em menos de 1 minuto depois de parar.
- [x] `grep -ri` no README e em `docs/` pelo nome do projeto de inspiração → nada.
- [x] Commit `chore: versão 0.4.0` (sem `Co-Authored-By`).

---

## Testes (resumo)
- [x] Unitários: `InputEventGrouper`, `StepTextGenerator`, `SensitiveDataDetector.MaskText`, `SensitiveRedactor`, `TutorialSerializer` (JSON + pacote + zip-slip), `TutorialEditHistory`, `StepImageRenderer`, `HtmlExporter`, `MarkdownExporter`, `PdfLayout`, `PdfWriter`, `DocxWriter`, configurações novas.
- [ ] Manuais: os checklists das Fases 1, 3, 5 e 6 (ganchos, UIA, DPI, recuperação, memória, Word/LibreOffice, Defender).

## Ordem de implementação
1. Fundação nativa e spike → 2. Modelo e lógica pura → 3. Gravação → 4. Composição e exportadores → 5. Editor → 6. Acabamento e release.

## Notas / Não fazer
- **Nenhum pacote NuGet no app** (nem OpenXML SDK, PdfSharp, QuestPDF, Markdig). `UseWPF` é referência de framework e só vale se passar no critério de tamanho (D5).
- **Nunca** converter tecla em caractere nem guardar texto digitado (nem em log de depuração). O agrupador só vê códigos de tecla e conta.
- Ganchos só existem durante a gravação; o `HotkeyManager` atual continua com o seu gancho da Print Screen separado — não juntar os dois.
- Callback de gancho: só enfileirar. Captura, UIA, OCR e disco ficam na thread de trabalho; UI só via `SynchronizationContext`.
- `System.Windows` (WPF) só em `ElementInspector.cs`.
- Copiar padrões: `PrintScreenKeyFilter` (lógica de teclas testável), `HotkeyManager._hookRefresh` (reinstalação do gancho), `SettingsStore` (gravação atômica), `RedactAnnotationTests` (teste por pixels), `PrintLayout.Fit` (encaixe).
- Fora do escopo (PRD §4): IA, vídeo/GIF, replay guiado, rolagem e arrastar, arrastar para reordenar, histórico de versões, nuvem, DOM de navegador, associação de arquivo `.fragmotut`.
- Se a implementação mostrar que algo desta Spec está errado, parar e propor a correção aqui antes de mudar o rumo.
