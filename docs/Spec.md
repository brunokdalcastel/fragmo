# Spec: Fragmo — captura de tela para Windows

> Baseado em `docs/PRD.md`. Cada fase é pensada para caber num único `/implement N`, com `/clear` entre as fases.
> Raiz do projeto: `C:\Users\Usuario\fragmo`. Todos os caminhos abaixo são relativos a essa raiz.

## Decisões que fecham as perguntas do PRD

| # | Decisão |
|---|---|
| Q1 | Tema **escuro** por padrão, com cor de destaque própria **`#A8204F`** (`Palette.Accent`: botões, ícone e destaques sobre fundo claro) e o tom claro **`#D9467A`** (`Palette.AccentLight`: textos e bordas sobre fundo escuro). Claro e "seguir o Windows" continuam como opções em Configurações. |
| Q2 | PNG por padrão; nome `Fragmo_yyyy-MM-dd_HHmmss.png`; pasta `%USERPROFILE%\Pictures\Fragmo`. |
| Q3 | Interface em **português e inglês**. Todos os textos ficam em `src/Fragmo/Strings.cs`, cada um com as duas versões lado a lado. O idioma padrão segue o Windows (`pt*` → português, qualquer outro → inglês) e pode ser trocado em Configurações. |
| Q4 | `git init` na Fase 1 e um commit por fase, sem push e **sem linha `Co-Authored-By`**. |
| R1 | Atalho de área padrão `PrintScreen`; se `RegisterHotKey` falhar, registra `Ctrl+Shift+S` e avisa na bandeja como liberar a tecla Print Screen. |

### Atalhos padrão

| Ação | Atalho | Fase |
|---|---|---|
| Capturar área | `PrintScreen` (alternativo: `Ctrl+Shift+S`) | 2 |
| Tela cheia → salvar direto | `Ctrl+Shift+F` | 4 |
| Janela ativa → editor | `Ctrl+Shift+W` | 4 |
| Repetir última área | `Ctrl+Shift+R` | 4 |

### Regras gerais (valem para todas as fases)

- `Nullable` e `ImplicitUsings` ativos; `TreatWarningsAsErrors` ativo no app e nos testes.
- Todo P/Invoke fica em `src/Fragmo/Native/*.cs`, usando `[LibraryImport]` e classes `internal static partial`.
- Tudo é calculado em **pixels físicos**. Os formulários de tela cheia usam `AutoScaleMode.None`.
- Toda lógica pura (geometria, nomes, parsing, histórico) fica em classes sem dependência de UI e com testes em `tests/Fragmo.Tests`.
- Objetos `Bitmap`/`Graphics`/`Pen`/`Brush` sempre com `using` ou `Dispose` explícito.
- Nenhum acesso à rede e nenhum pacote NuGet no app (os testes usam só xUnit).

---

## Phase 1: Esqueleto (solução, bandeja, instância única, configurações)

### `.gitignore`
- Action: Create
- Instruction: Ignorar `bin/`, `obj/`, `.vs/`, `*.user`, `artifacts/`, `TestResults/`.

### `README.md`
- Action: Create
- Instruction: Nome, descrição em uma linha ("captura de tela 100% local para Windows"), pré-requisito (.NET 10 SDK), comandos `dotnet build`, `dotnet test`, `dotnet run --project src/Fragmo`, e uma seção "Privacidade" (sem rede, sem telemetria). A seção de publicação entra na Fase 5.

### `Fragmo.sln`
- Action: Create
- Instruction: Criar com `dotnet new sln -n Fragmo` e adicionar os dois projetos (`dotnet sln add src/Fragmo/Fragmo.csproj tests/Fragmo.Tests/Fragmo.Tests.csproj`).

### `src/Fragmo/Fragmo.csproj`
- Action: Create
- Instruction: Projeto WinForms.
- *Snippet*:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>
    <RootNamespace>Fragmo</RootNamespace>
    <AssemblyName>Fragmo</AssemblyName>
    <Version>0.1.0</Version>
    <Product>Fragmo</Product>
  </PropertyGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="Fragmo.Tests" />
  </ItemGroup>
</Project>
```

### `src/Fragmo/app.manifest`
- Action: Create
- Instruction: `requestedExecutionLevel level="asInvoker"`; ~~`<dpiAware>true/pm</dpiAware>` + `<dpiAwareness>PerMonitorV2</dpiAwareness>`~~ **(removido na implementação: o .NET 10 gera o aviso WFO0003 quando o DPI está no manifesto e no `ApplicationHighDpiMode`; com `TreatWarningsAsErrors` isso quebra o build. O PerMonitorV2 continua vindo do `.csproj` via `ApplicationConfiguration.Initialize()`)**; `supportedOS` com o GUID do Windows 10/11 (`{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}`); `Microsoft.Windows.Common-Controls` 6.0.

### `src/Fragmo/Strings.cs`
- Action: Create
- Instruction: `internal static class Strings` bilíngue (PT-BR + EN), sem `.resx`: cada texto é uma propriedade que escolhe a versão conforme o idioma atual.
  - `static UiLanguage Current { get; private set; }` e `static void Apply(UiLanguage setting)`: `System` → `CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt"` ? `Portuguese` : `English`.
  - Helper privado `T(string pt, string en)`.
  - Mínimo nesta fase: `AppName`, `TrayTooltip`, `MenuCaptureArea`, `MenuSettings`, `MenuOpenFolder`, `MenuAbout`, `MenuExit`, `AlreadyRunning`, `AboutText` (versão + "100% local, sem telemetria" / "100% local, no telemetry").
  - As fases seguintes **acrescentam** propriedades aqui, sempre com as duas versões. Nenhum texto visível fica solto no código.
  - Textos com parâmetros usam `string.Format(Strings.SavedTo, path)`.
- *Snippet*:
```csharp
namespace Fragmo;

internal static class Strings
{
    public static UiLanguage Current { get; private set; } = UiLanguage.Portuguese;

    public static void Apply(UiLanguage setting) => Current = setting switch
    {
        UiLanguage.System => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt"
            ? UiLanguage.Portuguese : UiLanguage.English,
        _ => setting,
    };

    private static string T(string pt, string en) => Current == UiLanguage.Portuguese ? pt : en;

    public const string AppName = "Fragmo";
    public static string MenuCaptureArea => T("Capturar área", "Capture area");
    public static string MenuExit => T("Sair", "Exit");
    public static string AlreadyRunning => T("O Fragmo já está aberto na bandeja.",
                                             "Fragmo is already running in the tray.");
    // ...
}
```

### `src/Fragmo/Settings/AppSettings.cs`
- Action: Create
- Instruction: Modelo serializável com valores padrão. Tipos auxiliares no mesmo arquivo.
- *Snippet*:
```csharp
namespace Fragmo.Settings;

public enum ImageFormatKind { Png, Jpeg, Bmp }
public enum ThemeMode { System, Light, Dark }
public enum UiLanguage { System, Portuguese, English }

public sealed class HotkeySettings
{
    public string CaptureArea { get; set; } = "PrintScreen";
    public string CaptureAreaFallback { get; set; } = "Ctrl+Shift+S";
    public string CaptureFullScreen { get; set; } = "Ctrl+Shift+F";
    public string CaptureWindow { get; set; } = "Ctrl+Shift+W";
    public string RepeatLastArea { get; set; } = "Ctrl+Shift+R";
}

public sealed record RegionDto(int X, int Y, int Width, int Height);

public sealed class AppSettings
{
    public int Version { get; set; } = 1;
    public HotkeySettings Hotkeys { get; set; } = new();
    public string SaveFolder { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Fragmo");
    public ImageFormatKind DefaultFormat { get; set; } = ImageFormatKind.Png;
    public int JpegQuality { get; set; } = 90;          // 1..100
    public string FileNamePrefix { get; set; } = "Fragmo";
    public bool AskWhereToSave { get; set; } = true;
    public bool ShowMagnifier { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;
    public ThemeMode Theme { get; set; } = ThemeMode.Dark;
    public UiLanguage Language { get; set; } = UiLanguage.System;
    public string LastColor { get; set; } = "#FF3B30";
    public int LastThickness { get; set; } = 3;          // 1..20
    public RegionDto? LastRegion { get; set; }
}
```

### `src/Fragmo/Settings/SettingsStore.cs`
- Action: Create
- Instruction: `public sealed class SettingsStore`.
  - Construtor `SettingsStore(string directory)`; `static SettingsStore CreateDefault()` usa `%APPDATA%\Fragmo`.
  - `string FilePath => Path.Combine(directory, "settings.json")`.
  - `AppSettings Load()`: se o arquivo não existir, devolve `new AppSettings()`. Se o JSON for inválido, renomeia para `settings.bad.json` (sobrescrevendo) e devolve o padrão. Depois de carregar, chama `Normalize` (limita `JpegQuality` a 1..100 e `LastThickness` a 1..20; troca `null`/vazio por padrões).
  - `void Save(AppSettings s)`: cria a pasta, grava em `settings.json.tmp` e faz `File.Move(tmp, FilePath, overwrite: true)` (gravação atômica).
  - `JsonSerializerOptions`: `WriteIndented = true`, `JsonStringEnumConverter`, `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`.

### `src/Fragmo/Ui/IconFactory.cs`
- Action: Create
- Instruction: `internal static class IconFactory` com `Icon CreateAppIcon(int size)`: desenha em GDI+ um quadrado arredondado `#A8204F` com um "colchete de recorte" branco (dois cantos em L) e devolve `Icon.FromHandle(bmp.GetHicon())` clonado (chamar `DestroyIcon` via `Native/User32.cs` depois de clonar). Ícone próprio, sem nenhum recurso de terceiros. Na Fase 5 ele também é exportado para `.ico`.

### `src/Fragmo/Native/User32.cs`
- Action: Create
- Instruction: `internal static partial class User32` só com `DestroyIcon(IntPtr)` nesta fase (as próximas fases acrescentam funções).

### `src/Fragmo/TrayContext.cs`
- Action: Create
- Instruction: `internal sealed class TrayContext : ApplicationContext`.
  - Recebe `SettingsStore` e `AppSettings` no construtor.
  - Cria `NotifyIcon` (ícone `IconFactory.CreateAppIcon(32)`, texto `Strings.TrayTooltip`, `Visible = true`).
  - `ContextMenuStrip`: Capturar área (desativado nesta fase), Abrir pasta de capturas (cria a pasta se faltar e abre no Explorer), Configurações… (nesta fase abre a pasta `%APPDATA%\Fragmo` no Explorer), Sobre (`MessageBox` com `Strings.AboutText`), separador, Sair.
  - `public void ShowInfo(string text)` e `ShowError(string text)` → `ShowBalloonTip(4000, …)`.
  - `ExitThreadCore`: `Visible = false`, `Dispose` do ícone e do menu.

### `src/Fragmo/Program.cs`
- Action: Create
- Instruction: `[STAThread] static void Main()`:
  1. `using var mutex = new Mutex(true, @"Local\Fragmo.SingleInstance", out bool created);` — se `!created`, mostra `MessageBox` com `Strings.AlreadyRunning` e sai.
  2. `ApplicationConfiguration.Initialize();`
  3. `Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException)` + handlers que gravam o erro em `%APPDATA%\Fragmo\error.log` (só local) e mostram `MessageBox`.
  4. `var store = SettingsStore.CreateDefault(); var settings = store.Load(); Strings.Apply(settings.Language);` (como o mutex é verificado antes de carregar as configurações, a mensagem `AlreadyRunning` usa o idioma do Windows: chamar `Strings.Apply(UiLanguage.System)` antes do passo 1).
  5. `Application.Run(new TrayContext(store, settings));`

### `tests/Fragmo.Tests/Fragmo.Tests.csproj`
- Action: Create
- Instruction: `TargetFramework` `net10.0-windows`, `UseWindowsForms` true, `Nullable` enable, `IsPackable` false, `ProjectReference` para `..\..\src\Fragmo\Fragmo.csproj`, pacotes `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio` (versões estáveis atuais). Pode partir de `dotnet new xunit` e ajustar.

### `tests/Fragmo.Tests/SettingsStoreTests.cs`
- Action: Create
- Instruction: Usar uma pasta temporária única por teste (`Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())`, apagada em `Dispose`). Casos:
  - `Load_WhenMissing_ReturnsDefaults`
  - `SaveThenLoad_RoundTripsAllValues` (inclui `LastRegion` e enums)
  - `Load_WhenCorrupt_ReturnsDefaultsAndKeepsBackup` (verifica `settings.bad.json`)
  - `Load_ClampsOutOfRangeValues` (`JpegQuality = 500` → 100, `LastThickness = 0` → 1)
  - `Defaults_AreDarkThemeAndSystemLanguage`

### `tests/Fragmo.Tests/StringsTests.cs`
- Action: Create
- Instruction: `[Collection("Strings")]` (estado estático: sem paralelismo). Casos:
  - `Apply(Portuguese)` → `MenuExit == "Sair"`; `Apply(English)` → `"Exit"`.
  - Por reflexão, todas as propriedades `static string` de `Strings` retornam texto não vazio **nos dois idiomas** e diferente entre eles (exceto `AppName`). Esse teste cobre automaticamente os textos que as próximas fases acrescentarem.
  - Restaurar `Apply(Portuguese)` no `Dispose`.

### `nuget.config` *(acrescentado na implementação)*
- Action: Create
- Instruction: Fonte `nuget.org` local ao repositório, porque a máquina não tinha nenhuma fonte NuGet configurada e os pacotes do xUnit não restauravam. O app continua sem pacotes.

### Git
- Action: Create
- Instruction: `git init`, `git add .`, commit `chore: esqueleto do Fragmo (fase 1)` sem `Co-Authored-By`.

### Verification
- [x] `dotnet build Fragmo.sln` sem avisos.
- [x] `dotnet test` → testes de `SettingsStore` e `Strings` passando.
- [ ] Com `"language": "english"` no `settings.json`, o menu da bandeja aparece em inglês depois de reiniciar o app.
- [ ] `dotnet run --project src/Fragmo` mostra o ícone na bandeja; o menu abre; "Sair" encerra o processo.
- [x] Rodar uma segunda vez com o app aberto mostra "já está aberto" e não cria um segundo ícone.
- [x] Mudar qualquer valor e chamar `Save` cria `%APPDATA%\Fragmo\settings.json` legível.

---

## Phase 2: Captura + overlay de seleção + copiar/salvar (MVP)

### `src/Fragmo/Native/User32.cs`
- Action: Modify [x]
- Instruction: Acrescentar `RegisterHotKey`, `UnregisterHotKey`, `GetForegroundWindow`, `SetWindowPos`, `GetCursorPos` e as constantes `WM_HOTKEY = 0x0312`, `WM_DPICHANGED = 0x02E0`, `MOD_ALT = 1`, `MOD_CONTROL = 2`, `MOD_SHIFT = 4`, `MOD_WIN = 8`, `MOD_NOREPEAT = 0x4000`, `SWP_NOZORDER = 0x4`, `SWP_NOACTIVATE = 0x10`.

### `src/Fragmo/Hotkeys/HotkeyBinding.cs`
- Action: Create [x]
- Instruction: `public readonly record struct HotkeyBinding(HotkeyModifiers Modifiers, Keys Key)` com `[Flags] enum HotkeyModifiers { None = 0, Alt = 1, Control = 2, Shift = 4, Win = 8 }` (valores iguais aos `MOD_*`).
  - `static bool TryParse(string text, out HotkeyBinding binding)`: separa por `+`, ignora espaços e maiúsculas/minúsculas; aceita `Ctrl`/`Control`, `Alt`, `Shift`, `Win`; a última parte é a tecla (`Enum.TryParse<Keys>`, aceitando `PrintScreen` como `Keys.PrintScreen`). Rejeita: texto vazio, tecla ausente, tecla que é só modificador, modificador repetido.
  - `override string ToString()` na ordem fixa `Ctrl+Alt+Shift+Win+Tecla` (o parse do resultado deve voltar ao mesmo valor).

### `src/Fragmo/Hotkeys/HotkeyManager.cs`
- Action: Create [x]
- Instruction: `internal sealed class HotkeyManager : NativeWindow, IDisposable`.
  - No construtor chama `CreateHandle(new CreateParams { Parent = new IntPtr(-3) })` (`HWND_MESSAGE`, janela só de mensagens).
  - `bool TryRegister(HotkeyAction action, HotkeyBinding binding)`: id = `(int)action + 1`; desregistra o anterior desse id; chama `RegisterHotKey(Handle, id, (uint)binding.Modifiers | MOD_NOREPEAT, (uint)binding.Key)`; devolve `false` se falhar (sem lançar exceção).
  - `event Action<HotkeyAction>? Pressed`; `WndProc` trata `WM_HOTKEY`.
  - `Dispose`: desregistra todos os ids e chama `DestroyHandle()`.
  - `enum HotkeyAction { CaptureArea, CaptureFullScreen, CaptureWindow, RepeatLastArea }` no mesmo arquivo.

### `src/Fragmo/Capture/ScreenCapturer.cs`
- Action: Create [x]
- Instruction: `internal static class ScreenCapturer`.
  - `CapturedScreen CaptureVirtualScreen()`: `var vs = SystemInformation.VirtualScreen;` bitmap `Format32bppArgb` do tamanho `vs.Size`, `CopyFromScreen(vs.Left, vs.Top, 0, 0, vs.Size, CopyPixelOperation.SourceCopy)`.
  - `sealed record CapturedScreen(Bitmap Image, Rectangle VirtualBounds) : IDisposable` (guarda a origem, que pode ser negativa).
  - `Bitmap Crop(Bitmap source, Rectangle bitmapRect)`: `source.Clone(rect, PixelFormat.Format32bppArgb)` depois de `Intersect` com os limites do bitmap; lança `ArgumentException` se a interseção ficar vazia.

### `src/Fragmo/Capture/CoordinateMapper.cs`
- Action: Create [x]
- Instruction: `public static class CoordinateMapper` (lógica pura, risco R3):
  - `Point ScreenToBitmap(Point screen, Rectangle virtualBounds)` → `new(screen.X - virtualBounds.X, screen.Y - virtualBounds.Y)`.
  - `Rectangle BitmapToScreen(Rectangle r, Rectangle virtualBounds)` → deslocamento inverso.

### `src/Fragmo/Overlay/SelectionGeometry.cs`
- Action: Create [x]
- Instruction: `public static class SelectionGeometry` (lógica pura):
  - `Rectangle FromPoints(Point a, Point b)` → retângulo normalizado (aceita arrasto em qualquer direção).
  - `Rectangle Clamp(Rectangle r, Size bounds)` → limita aos limites do bitmap.
  - `enum Handle { None, Move, N, S, E, W, NE, NW, SE, SW }`.
  - `Handle HitTest(Rectangle sel, Point p, int grip = 6)` → alça sob o cursor (cantos têm prioridade, interior = `Move`).
  - `Rectangle Resize(Rectangle original, Handle h, Point delta)` → aplica o delta e normaliza (cruzar a borda oposta não gera largura negativa).
  - `Rectangle Move(Rectangle original, Point delta, Size bounds)` → move sem sair dos limites.
  - `Rectangle MagnifierSource(Point p, int radius)` → `new(p.X - radius, p.Y - radius, 2*radius+1, 2*radius+1)`.

### `src/Fragmo/Overlay/SelectionOverlay.cs`
- Action: Create [x]
- Instruction: `internal sealed class SelectionOverlay : Form`.
  - Construtor `(CapturedScreen screen, AppSettings settings)`. Estilo: `FormBorderStyle.None`, `ShowInTaskbar = false`, `TopMost = true`, `StartPosition = Manual`, `AutoScaleMode = None`, `KeyPreview = true`, `Cursor = Cursors.Cross`, `SetStyle(AllPaintingInWmPaint | OptimizedDoubleBuffer | UserPaint, true)`.
  - Em `OnHandleCreated`: `SetWindowPos(Handle, …, vs.X, vs.Y, vs.Width, vs.Height, SWP_NOZORDER | SWP_NOACTIVATE)` para posicionar em pixels físicos.
  - `WndProc`: ignora `WM_DPICHANGED` (não repassa para `base`), para o WinForms não redimensionar o overlay ao cruzar monitores com escalas diferentes (R2).
  - Estados: `Idle` (sem seleção) → `Dragging` → `Selected`. Em `Selected` o mouse pode mover/redimensionar pelas alças (`SelectionGeometry.HitTest`), trocando o cursor conforme a alça.
  - Pintura (`OnPaint`): desenha o bitmap congelado inteiro; por cima, um véu preto com alfa 110 em toda a área **menos** a seleção (usar `Region` com `Exclude`); borda de 1px `#D9467A` (`Palette.AccentLight`, por estar sobre o véu escuro) + 8 alças quadradas de 6px; rótulo `"{W} × {H}"` acima da seleção (ou dentro, se não couber).
  - Lupa (se `settings.ShowMagnifier` e estado ≠ `Selected`): quadrado de 120px perto do cursor (troca de lado na borda da tela), `InterpolationMode.NearestNeighbor`, fonte `MagnifierSource(cursor, 7)` ampliada 8×, mira central, e abaixo a cor do pixel em `#RRGGBB` e a posição `X, Y`.
  - Invalidação: só a união do retângulo antigo e do novo (seleção + rótulo + lupa) para não piscar.
  - Teclado/mouse: `Esc` → `DialogResult.Cancel`; `Enter`, `Ctrl+C` ou duplo clique na seleção → `Result = OverlayResult.Copy`; `Ctrl+S` → `OverlayResult.Save`; botão direito sem seleção → cancela, com seleção → limpa a seleção.
  - Propriedades públicas: `OverlayResult Result` (`enum OverlayResult { Cancel, Copy, Save }` no mesmo arquivo) e `Rectangle Selection` (coordenadas do bitmap).
  - Depois de `Show`, chamar `Activate()` e `Focus()` para receber o teclado.

### `src/Fragmo/Output/FileNameBuilder.cs`
- Action: Create [x]
- Instruction: `public static class FileNameBuilder` (lógica pura):
  - `string Build(string prefix, DateTime now, ImageFormatKind fmt)` → `"{prefix}_{now:yyyy-MM-dd_HHmmss}.{ext}"` (`png`/`jpg`/`bmp`); remove de `prefix` os caracteres de `Path.GetInvalidFileNameChars()`; prefixo vazio vira `Fragmo`.
  - `string MakeUnique(string folder, string fileName, Func<string, bool> exists)` → se existir, acrescenta `_2`, `_3`… antes da extensão.

### `src/Fragmo/Output/OutputService.cs`
- Action: Create [x]
- Instruction: `internal sealed class OutputService(AppSettings settings)`.
  - `void CopyToClipboard(Bitmap img)`: `Clipboard.SetImage` dentro de um laço de até 5 tentativas com 50 ms entre elas (a área de transferência pode estar presa por outro processo).
  - `string? Save(Bitmap img, IWin32Window? owner)`: se `AskWhereToSave`, abre `SaveFileDialog` (pasta inicial `SaveFolder`, nome do `FileNameBuilder`, filtro PNG/JPG/BMP com o formato padrão selecionado); caso contrário grava direto (`SaveDirect`). Devolve o caminho ou `null` se cancelar.
  - `string SaveDirect(Bitmap img)`: cria a pasta, usa `FileNameBuilder.Build` + `MakeUnique(File.Exists)`.
  - `void WriteImage(Bitmap img, string path, ImageFormatKind fmt)`: JPEG via `ImageCodecInfo` + `EncoderParameter(Encoder.Quality, settings.JpegQuality)`; `ImageFormatKind FormatFromExtension(string path)`.

### `src/Fragmo/Capture/CaptureWorkflow.cs`
- Action: Create [x]
- Instruction: `internal sealed class CaptureWorkflow(AppSettings settings, SettingsStore store, OutputService output, TrayContext tray)`.
  - Campo `bool _busy` para ignorar atalhos enquanto o overlay está aberto.
  - `void CaptureArea()`: `using var screen = ScreenCapturer.CaptureVirtualScreen();` (capturar **antes** de mostrar o overlay) → `using var overlay = new SelectionOverlay(screen, settings); overlay.ShowDialog();` → conforme `Result`: `Crop` + `CopyToClipboard` (balão "Copiado") ou `Save` (balão com o caminho). Grava `settings.LastRegion` (em coordenadas de tela, via `CoordinateMapper.BitmapToScreen`) e chama `store.Save`.
  - Erros: `try/catch` que mostra `tray.ShowError(ex.Message)`; `_busy` volta a `false` no `finally`.

### `src/Fragmo/TrayContext.cs`
- Action: Modify [x]
- Instruction: Criar `HotkeyManager`, `OutputService` e `CaptureWorkflow`. Registrar `CaptureArea`: tentar `settings.Hotkeys.CaptureArea`; se falhar, tentar `CaptureAreaFallback` e mostrar `Strings.PrintScreenTaken` (ex.: "Print Screen está em uso pelo Windows. Usando Ctrl+Shift+S. Para liberar: Configurações › Acessibilidade › Teclado › desative 'Usar a tecla Print Screen…'"); se os dois falharem, `Strings.HotkeyFailed`. Ativar "Capturar área" no menu e no duplo clique do ícone. Dispor o `HotkeyManager` em `ExitThreadCore`.

### `src/Fragmo/Strings.cs`
- Action: Modify [x]
- Instruction: Acrescentar `PrintScreenTaken`, `HotkeyFailed`, `Copied = "Imagem copiada"`, `SavedTo = "Salvo em {0}"`, `SaveFilter = "PNG|*.png|JPEG|*.jpg|Bitmap|*.bmp"`, `CaptureError`.
- Nota da implementação: `SaveFilter` virou `const` (é igual nos dois idiomas e o `StringsTests` exige texto diferente em PT/EN para as propriedades).

### `tests/Fragmo.Tests/HotkeyBindingTests.cs`
- Action: Create [x]
- Instruction: `[Theory]` para entradas válidas (`"Ctrl+Shift+S"`, `"ctrl + shift + s"`, `"PrintScreen"`, `"Alt+F1"`, `"Win+Shift+X"`) e inválidas (`""`, `"Ctrl+"`, `"Ctrl+Shift"`, `"Ctrl+Ctrl+A"`, `"Foo+A"`); teste de ida e volta `TryParse(b.ToString())`.

### `tests/Fragmo.Tests/SelectionGeometryTests.cs`
- Action: Create [x]
- Instruction: `FromPoints` nas 4 direções de arrasto; `Clamp` com retângulo parcialmente fora; `HitTest` em cada canto, borda, interior e fora; `Resize` cruzando a borda oposta (largura continua positiva); `Move` encostado na borda.

### `tests/Fragmo.Tests/CoordinateMapperTests.cs`
- Action: Create [x]
- Instruction: Tela virtual começando em `(-1920, -200)`: `ScreenToBitmap(new(-1920,-200))` → `(0,0)`; `ScreenToBitmap(new(0,0))` → `(1920,200)`; ida e volta com `BitmapToScreen`.

### `tests/Fragmo.Tests/FileNameBuilderTests.cs`
- Action: Create [x]
- Instruction: nome esperado para `2026-10-14 15:30:12` em cada formato; prefixo com `:`/`?` limpo; prefixo vazio; `MakeUnique` com `_2` e `_3` usando um `exists` falso.

### Git
- Action: Create [x]
- Instruction: commit `feat: captura de área com overlay, copiar e salvar (fase 2)`.

### Verification
- [x] `dotnet build` sem avisos e `dotnet test` verde (67 testes).
- [x] Apertar `PrintScreen` (ou `Ctrl+Shift+S`, com balão explicando) abre o overlay em todos os monitores com a tela congelada e escurecida.
- [x] Arrastar mostra o retângulo, o tamanho `W × H` e a lupa com a cor do pixel; as alças redimensionam e o interior move.
- [x] `Enter` → colar no Paint mostra exatamente a área selecionada, sem borda e sem escurecimento.
- [x] `Ctrl+S` → salva o PNG com o nome `Fragmo_…png` na pasta escolhida.
- [x] `Esc` cancela sem efeito colateral.
- [ ] (Se houver 2 monitores) monitor à esquerda do principal e escalas 100% + 150%: a área salva bate com o que foi selecionado.

---

## Phase 3: Editor básico (barra de ferramentas, anotações, desfazer/refazer)

### `src/Fragmo/Ui/Theme.cs`
- Action: Create [x]
- Instruction: `internal sealed record Theme(Color Background, Color Surface, Color Foreground, Color Border, Color Accent, Color AccentText, Color AccentForeground)`, com `static Theme Light`, `static Theme Dark` (Accent `#A8204F` nos dois; AccentText `#D9467A` no escuro e `#A8204F` no claro, vindos da classe `Palette`; o **escuro é o padrão** do app: fundo `#1E1E22`, superfície `#2A2A30`, texto `#F2F2F2`, borda `#3A3A42`) e `static Theme Resolve(ThemeMode mode)`: em `System`, lê `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme` (0 = escuro; ausente = claro).

### `src/Fragmo/Editor/IAnnotation.cs`
- Action: Create [x]
- Instruction:
```csharp
namespace Fragmo.Editor;
public interface IAnnotation
{
    Rectangle Bounds { get; }            // usado para invalidar só a área necessária
    void Draw(Graphics g, Bitmap source); // source: bitmap base (usado pelo desfoque na Fase 4)
}
public sealed record StrokeStyle(Color Color, int Thickness);
```

### `src/Fragmo/Editor/Annotations.cs`
- Action: Create [x]
- Instruction: Classes `sealed` que implementam `IAnnotation`, todas com `SmoothingMode.AntiAlias` e `Pen` com `LineCap.Round`:
  - `PenAnnotation(StrokeStyle, List<Point> points)` → `DrawLines` (1 ponto vira um círculo).
  - `LineAnnotation(StrokeStyle, Point a, Point b)`.
  - `ArrowAnnotation(StrokeStyle, Point a, Point b)` → linha + ponta triangular preenchida no ponto `b`, de tamanho `max(10, thickness * 4)`.
  - `RectangleAnnotation(StrokeStyle, Rectangle r)` → só o contorno.
  - `TextAnnotation(Color, float fontSize, Point origin, string text)` → `Segoe UI` semibold, com contorno de 1px na cor contrastante (preto ou branco, conforme a luminância) para ler sobre qualquer fundo.
  - `Bounds` de cada um inclui a espessura + 2px de margem.

### `src/Fragmo/Editor/AnnotationHistory.cs`
- Action: Create [x]
- Instruction: `public sealed class AnnotationHistory` (lógica pura): `IReadOnlyList<IAnnotation> Items`, `void Add(IAnnotation a)` (limpa a pilha de refazer), `bool Undo()`, `bool Redo()`, `bool CanUndo`, `bool CanRedo`, `void Clear()`, `event Action? Changed`.

### `src/Fragmo/Editor/ToolKind.cs`
- Action: Create [x]
- Instruction: `public enum ToolKind { Select, Pen, Line, Arrow, Rectangle, Text }` (a Fase 4 acrescenta `Marker, Blur, Step`).

### `src/Fragmo/Editor/ImageComposer.cs`
- Action: Create [x]
- Instruction: `internal static class ImageComposer` com `Bitmap Compose(Bitmap baseImage, IReadOnlyList<IAnnotation> items, Rectangle selection)`: copia o bitmap base, desenha todas as anotações nele (coordenadas do bitmap) e devolve `ScreenCapturer.Crop(result, selection)`. É o único caminho de exportação (copiar, salvar e, depois, imprimir/fixar).

### `src/Fragmo/Editor/EditorToolbar.cs`
- Action: Create [x]
- Instruction: `internal sealed class EditorToolbar : Control` desenhado à mão com `Theme` (sem `ToolStrip`, para ter layout próprio):
  - Botões de 32×32 px com ícones vetoriais desenhados em GDI+ (sem imagens): ferramentas `Pen, Line, Arrow, Rectangle, Text`; separador; amostra da cor atual (clique abre `ColorDialog` + 8 cores rápidas num menu); espessura (−/+ ou menu 1/2/3/5/8); separador; Desfazer, Refazer; separador; Copiar, Salvar, Fechar.
  - Botão ativo com fundo `Accent`; foco/hover com `Surface`; dica (`ToolTip`) com o atalho em cada botão.
  - Eventos: `ToolChanged(ToolKind)`, `StyleChanged(StrokeStyle)`, `UndoClicked`, `RedoClicked`, `CopyClicked`, `SaveClicked`, `CloseClicked`. `void SetUndoRedo(bool canUndo, bool canRedo)` desativa os botões.
  - Duas barras: horizontal (ações) e vertical (ferramentas), como instâncias com `Orientation`.
- Nota da implementação: o evento virou `StrokeStyleChanged` (`StyleChanged` já existe em `Control` e dava CS0108). A vertical tem ferramentas + cor + espessura; a horizontal tem desfazer/refazer + copiar/salvar/fechar. Clicar na ferramenta ativa volta para `Select`. Acrescentado `SetTool(ToolKind)` para refletir os atalhos de teclado.

### `src/Fragmo/Overlay/SelectionOverlay.cs`
- Action: Modify [x]
- Instruction: Integrar o editor ao overlay.
  - Ao entrar em `Selected`, criar as duas `EditorToolbar` como controles filhos: horizontal abaixo da seleção à direita, vertical à direita da seleção; se não couber, inverter o lado (usar `Screen.FromPoint` para ficar no mesmo monitor e não sair da tela). Reposicionar quando a seleção mudar.
  - Campo `AnnotationHistory _history`, `ToolKind _tool = Select`, `StrokeStyle _style` (vem de `settings.LastColor`/`LastThickness`).
  - Com ferramenta ≠ `Select`, o mouse dentro da seleção cria uma anotação "em andamento" (desenhada em `OnPaint` mas fora do histórico); no `MouseUp` ela é adicionada ao histórico. `Shift` em linha/seta trava em 0/45/90°; `Shift` em retângulo vira quadrado.
  - Texto: clique abre um `TextBox` sem borda, com fundo transparente simulado, posicionado no ponto; `Enter` confirma (vira `TextAnnotation`), `Esc` descarta; `Shift+Enter` quebra linha.
  - Pintura: bitmap → anotações do histórico → anotação em andamento → véu → borda/alças. As anotações são **recortadas** à seleção (`g.SetClip(selection)`).
  - Atalhos: `P` caneta, `L` linha, `A` seta, `R` retângulo, `T` texto, `V` seleção, `Ctrl+Z`/`Ctrl+Y` (e `Ctrl+Shift+Z`), `Ctrl+C`, `Ctrl+S`, `Esc` (se estiver digitando texto, só fecha a caixa).
  - Copiar/salvar agora expõem `Bitmap ComposeResult()` → `ImageComposer.Compose(...)`.
  - Ao fechar, gravar a cor e a espessura usadas em `settings.LastColor`/`LastThickness`.
- Nota da implementação: a propriedade pública é `bool StrokeStyleChanged` (mesmo motivo do CS0108). Com uma ferramenta de desenho ativa, o duplo clique não copia (seria parte do desenho) e clicar fora da seleção não inicia outra; as alças continuam redimensionando. As barras somem durante o arrasto/redimensionamento e reaparecem reposicionadas ao soltar. O tamanho do texto acompanha a espessura (`12 + 3 × espessura` px).

### `src/Fragmo/Capture/CaptureWorkflow.cs`
- Action: Modify [x]
- Instruction: Trocar o `Crop` direto por `overlay.ComposeResult()` (para incluir as anotações) e salvar as configurações também quando a cor/espessura mudarem.

### `src/Fragmo/Strings.cs`
- Action: Modify [x]
- Instruction: Acrescentar os nomes/dicas das ferramentas e ações (`ToolPen = "Caneta (P)"`, etc.).

### `tests/Fragmo.Tests/AnnotationHistoryTests.cs`
- Action: Create [x]
- Instruction: Usar uma anotação falsa (`FakeAnnotation : IAnnotation`). Casos: `Add` aumenta `Items`; `Undo`/`Redo` em sequência; `Add` depois de `Undo` limpa o refazer; `Undo` com lista vazia devolve `false`; `Changed` disparado uma vez por operação.

### `tests/Fragmo.Tests/ImageComposerTests.cs`
- Action: Create [x]
- Instruction: Bitmap base 100×100 branco + `RectangleAnnotation` vermelho; `Compose` com seleção `(10,10,50,50)` → resultado 50×50 e pixel vermelho na borda esperada; seleção fora do bitmap → `ArgumentException`.

### Git
- Action: Create [x]
- Instruction: commit `feat: editor com anotações e desfazer/refazer (fase 3)`.

### Verification
- [x] `dotnet build` sem avisos e `dotnet test` verde (75 testes).
- [x] Depois de selecionar, as duas barras aparecem junto da seleção e não saem da tela (testar seleção no canto inferior direito).
- [x] Caneta, linha, seta, retângulo e texto desenham dentro da seleção, na cor e espessura escolhidas; `Shift` trava ângulo/quadrado.
- [x] `Ctrl+Z`/`Ctrl+Y` desfazem e refazem; os botões ficam desativados quando não há o que desfazer.
- [x] Copiar/salvar inclui as anotações e não inclui barras, alças nem véu.
- [x] A barra aparece no tema escuro (padrão); com `"theme": "light"` no `settings.json` ela fica clara.
- [x] As dicas das ferramentas aparecem em português e em inglês conforme `language`.

---

## Phase 4: Melhorias (marcador, desfoque, passos, fixar, janela, tela cheia, repetir, imprimir)

### `src/Fragmo/Editor/ToolKind.cs`
- Action: Modify
- Instruction: Acrescentar `Marker, Blur, Step`.

### `src/Fragmo/Editor/Annotations.cs`
- Action: Modify
- Instruction: Acrescentar:
  - `MarkerAnnotation(Color, int thickness, List<Point>)` → caneta com alfa 90 e espessura `thickness * 4`, `LineCap.Square`.
  - `BlurAnnotation(Rectangle r, int blockSize = 12)` → pixelização: recorta `source` em `r`, reduz para `ceil(w/blockSize) × ceil(h/blockSize)` e amplia de volta com `InterpolationMode.NearestNeighbor` + `PixelOffsetMode.Half`. Usa o bitmap **base**, de modo que nunca revela o conteúdo original.
  - `StepAnnotation(Color, int number, Point center, int radius)` → círculo preenchido com número branco centralizado.

### `src/Fragmo/Editor/StepCounter.cs`
- Action: Create
- Instruction: `public static class StepCounter` com `int Next(IReadOnlyList<IAnnotation> items)` → `1 + max(number)` dos `StepAnnotation` existentes (0 se nenhum), para que desfazer um passo reutilize o número.

### `src/Fragmo/Editor/EditorToolbar.cs`
- Action: Modify
- Instruction: Botões `Marker (M)`, `Blur (B)`, `Step (N)` na barra vertical; `Pin` (fixar) e `Print` (imprimir) na barra horizontal; eventos `PinClicked`, `PrintClicked`.

### `src/Fragmo/Overlay/SelectionOverlay.cs`
- Action: Modify
- Instruction: Suporte às 3 novas ferramentas (o desfoque é desenhado como retângulo e vira `BlurAnnotation`; o passo é criado com um clique usando `StepCounter.Next`). Construtor aceita `Rectangle? initialSelection` opcional: se vier, já começa em `Selected` (usado por "repetir última área" e "janela ativa"). `OverlayResult` ganha `Pin` e `Print`; atalhos `Ctrl+P` imprimir, `Ctrl+T` fixar.

### `src/Fragmo/Pin/PinWindow.cs`
- Action: Create
- Instruction: `internal sealed class PinWindow : Form` mostrando um `Bitmap` (que ela passa a possuir e descarta no `Dispose`):
  - Sem borda, `TopMost`, `ShowInTaskbar = false`, borda de 1px `Accent`, abre na mesma posição de tela da seleção.
  - Arrastar com o botão esquerdo move a janela; roda do mouse = zoom 25%–400% (passos de 10%); `Ctrl+roda` = opacidade 20%–100%; duplo clique ou `Esc` fecha.
  - Menu de contexto: Copiar, Salvar…, Zoom 100%, Fechar.
  - Várias janelas fixadas podem coexistir; o `TrayContext` guarda uma lista para fechá-las ao sair.

### `src/Fragmo/Native/Dwmapi.cs`
- Action: Create
- Instruction: `DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT rect, int size)` com `DWMWA_EXTENDED_FRAME_BOUNDS = 9` e `struct RECT`.

### `src/Fragmo/Capture/ScreenCapturer.cs`
- Action: Modify
- Instruction: Acrescentar `Rectangle? GetForegroundWindowBounds()`: `GetForegroundWindow()` → ignora a área de trabalho/barra de tarefas (comparar classe com `Progman`, `WorkerW`, `Shell_TrayWnd` via `GetClassName`, acrescentado em `User32.cs`) → `DwmGetWindowAttribute(…EXTENDED_FRAME_BOUNDS…)` (sem a sombra invisível); devolve `null` se falhar. E `Rectangle GetPrimaryScreenBounds()` → `Screen.PrimaryScreen!.Bounds`.

### `src/Fragmo/Output/OutputService.cs`
- Action: Modify
- Instruction: Acrescentar `void Print(Bitmap img, IWin32Window? owner)`: `PrintDocument` + `PrintDialog`; no `PrintPage` desenha a imagem escalada para caber em `e.MarginBounds` mantendo a proporção (nunca amplia acima de 100%).

### `src/Fragmo/Output/PrintLayout.cs`
- Action: Create
- Instruction: `public static class PrintLayout` com `Rectangle Fit(Size image, Rectangle bounds)` (lógica pura, centralizado, sem ampliar), usado pelo `Print`.

### `src/Fragmo/Capture/CaptureWorkflow.cs`
- Action: Modify
- Instruction:
  - `CaptureFullScreen()`: captura a tela virtual, recorta o monitor principal (`CoordinateMapper.ScreenToBitmap`) e chama `output.SaveDirect` (sem diálogo) + balão com o caminho.
  - `CaptureWindow()`: obtém a janela **antes** de capturar; abre o overlay com `initialSelection` = bounds convertidos para o bitmap; se `null`, cai em `CaptureArea()`.
  - `RepeatLastArea()`: se `settings.LastRegion` for `null`, chama `CaptureArea()`; senão abre o overlay com essa seleção (limitada com `SelectionGeometry.Clamp`).
  - Tratar `OverlayResult.Pin` (abre `PinWindow` e registra no `TrayContext`) e `Print`.

### `src/Fragmo/TrayContext.cs`
- Action: Modify
- Instruction: Registrar os outros 3 atalhos (`CaptureFullScreen`, `CaptureWindow`, `RepeatLastArea`), avisando por balão os que falharem (R5). Itens novos no menu: "Tela cheia (salvar)", "Janela ativa", "Repetir última área", "Fechar imagens fixadas". Manter `List<PinWindow>` e fechá-las em `ExitThreadCore`.

### `src/Fragmo/Strings.cs`
- Action: Modify
- Instruction: Textos das novas ferramentas, itens de menu e mensagens.

### `tests/Fragmo.Tests/StepCounterTests.cs`
- Action: Create
- Instruction: lista vazia → 1; passos 1, 2, 5 → 6; lista sem passos → 1.

### `tests/Fragmo.Tests/PrintLayoutTests.cs`
- Action: Create
- Instruction: imagem maior que a página (reduz e centraliza, mantém proporção); imagem menor (tamanho original, centralizada); imagem muito larga.

### `tests/Fragmo.Tests/BlurAnnotationTests.cs`
- Action: Create
- Instruction: bitmap base com metade preta/metade branca; depois de `Draw` de uma `BlurAnnotation` sobre a divisa, os pixels dentro de um mesmo bloco têm a mesma cor.

### Git
- Action: Create
- Instruction: commit `feat: desfoque, passos, marcador, fixar, janela, tela cheia e imprimir (fase 4)`.

### Verification
- [x] `dotnet build` sem avisos e `dotnet test` verde (85 testes).
- [x] Desfoque esconde o texto de verdade (salvar e abrir a imagem); passos numeram 1, 2, 3 e reaproveitam o número depois de desfazer.
- [x] `Ctrl+T` fixa o recorte na tela; roda = zoom, `Ctrl+roda` = opacidade, arrastar move, `Esc` fecha.
- [x] `Ctrl+Shift+F` salva o monitor principal direto na pasta, com balão.
- [x] `Ctrl+Shift+W` abre o editor com a janela ativa já selecionada, sem a sombra.
- [x] `Ctrl+Shift+R` reabre com a última área; sem área anterior, abre a seleção normal.
- [x] `Ctrl+P` abre a impressão e a prévia mostra a imagem inteira.

---

## Phase 5: Configurações, tema, ícone e publicação

### `src/Fragmo/Settings/StartupRegistration.cs`
- Action: Create
- Instruction: `internal static class StartupRegistration` usando `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, valor `Fragmo` = `"\"{Environment.ProcessPath}\" --tray"`. Métodos `bool IsEnabled()`, `void Set(bool enabled)` (apaga o valor quando `false`). É o único acesso ao registro com escrita.

### `src/Fragmo/Hotkeys/HotkeyBox.cs`
- Action: Create
- Instruction: `internal sealed class HotkeyBox : TextBox` (somente leitura): em `ProcessCmdKey`/`OnKeyDown` monta um `HotkeyBinding` com os modificadores atuais + a tecla (ignora teclas que são só modificador), mostra `binding.ToString()`; `Backspace`/`Delete` limpa. Propriedade `HotkeyBinding? Binding`. `PrintScreen` precisa ser tratado em `OnKeyUp` (o Windows não envia `KeyDown` para ela).

### `src/Fragmo/Settings/SettingsForm.cs`
- Action: Create
- Instruction: `internal sealed class SettingsForm : Form` montado em código (sem designer), com `Theme` aplicado e `AutoScaleMode.Dpi`. Abas (ou seções empilhadas):
  - **Atalhos:** um `HotkeyBox` por ação. Ao salvar, valida duplicatas entre si e tenta registrar cada um; o que falhar fica com borda vermelha e mensagem "em uso por outro programa" (os que funcionaram são mantidos).
  - **Salvar:** pasta (`TextBox` + `FolderBrowserDialog`), formato padrão (`ComboBox`), qualidade JPEG (`NumericUpDown` 1–100, ativo só para JPEG), prefixo do nome com prévia ao vivo (`FileNameBuilder.Build(prefix, DateTime.Now, fmt)`), "Perguntar onde salvar".
  - **Geral:** iniciar com o Windows (`StartupRegistration`), mostrar lupa, tema (Escuro/Claro/Sistema, padrão Escuro), idioma (Sistema/Português/English). Ao trocar o idioma: `Strings.Apply`, recriar o menu da bandeja e reabrir a própria `SettingsForm` (os textos são lidos na construção dos controles).
  - Botões OK/Cancelar/Restaurar padrões. OK → copia os campos para o `AppSettings`, `store.Save`, e dispara `event Action? Applied`.

### `src/Fragmo/TrayContext.cs`
- Action: Modify
- Instruction: "Configurações…" abre `SettingsForm` (uma só instância; se já estiver aberta, `Activate()`). Em `Applied`, re-registrar todos os atalhos (extrair o registro para um método `RegisterAllHotkeys()` que devolve a lista de falhas).

### `src/Fragmo/Program.cs`
- Action: Modify
- Instruction: Aceitar `--tray` (inicia sem nenhuma mensagem; é o modo usado pelo "iniciar com o Windows") e `--export-icon <caminho.ico>` (grava o ícone em 16/24/32/48/256 px e sai), usado para gerar o ícone do EXE.

### `src/Fragmo/Ui/IconFactory.cs`
- Action: Modify
- Instruction: Acrescentar `void SaveIco(string path, params int[] sizes)` gravando um `.ico` multi-tamanho com entradas PNG (cabeçalho `ICONDIR` + `ICONDIRENTRY` + bytes PNG de cada tamanho).

### `src/Fragmo/Resources/fragmo.ico`
- Action: Create
- Instruction: Gerado por `dotnet run --project src/Fragmo -- --export-icon src/Fragmo/Resources/fragmo.ico` e commitado.

### `src/Fragmo/Fragmo.csproj`
- Action: Modify
- Instruction: `<ApplicationIcon>Resources\fragmo.ico</ApplicationIcon>`; propriedades de publicação: `RuntimeIdentifier` win-x64 (só quando publicar), `SelfContained` true, `PublishSingleFile` true, `IncludeNativeLibrariesForSelfExtract` true, `EnableCompressionInSingleFile` true, `DebugType` embedded. **Não** usar `PublishTrimmed` (WinForms não suporta trimming).

### `publish.ps1`
- Action: Create
- Instruction: `dotnet test` → se passar, `dotnet publish src/Fragmo -c Release -r win-x64 -o artifacts/publish` → mostra o caminho e o tamanho de `Fragmo.exe`.

### `README.md`
- Action: Modify
- Instruction: Bilíngue (seção em PT-BR seguida de "English"). Tabela de atalhos, lista de ferramentas do editor, seção "Publicar" (`./publish.ps1`), a dica do Print Screen no Windows 11 (R1) e o aviso de que o EXE não é assinado (R6).

### `tests/Fragmo.Tests/SettingsValidationTests.cs`
- Action: Create
- Instruction: Extrair para `Settings/SettingsValidator.cs` (lógica pura, `[NEW]`) a validação de atalhos duplicados/inválidos e de pasta vazia; testar: atalhos repetidos → erro; atalho que não faz parse → erro; configuração padrão → sem erros.

### `src/Fragmo/Settings/SettingsValidator.cs`
- Action: Create
- Instruction: `public static class SettingsValidator` com `IReadOnlyList<string> Validate(AppSettings s)` (mensagens de `Strings`), usado pelo `SettingsForm` antes de salvar.

### `src/Fragmo/Hotkeys/HotkeyManager.cs` *(acrescentado na implementação)*
- Action: Modify
- Instruction: `UnregisterAll()`, para `RegisterAllHotkeys()` liberar tudo antes de registrar (trocar atalhos entre ações não colide) e para suspender os atalhos enquanto uma `HotkeyBox` tem o foco (senão o atalho registrado, ex. `PrintScreen`, dispara a captura em vez de chegar na caixa). A `SettingsForm` avisa por `event Action<bool> CapturingHotkeyChanged`.

### Notas da implementação
- Atalho vazio conta como inválido no `SettingsValidator` (o `SettingsStore.Normalize` trocaria o vazio pelo padrão ao carregar).
- O atalho alternativo da área (`Ctrl+Shift+S`) não aparece na tela; só entra quando o principal falha.
- Com atalho recusado pelo Windows, o OK salva, marca em vermelho e deixa a tela aberta; um segundo OK sem mudança fecha.
- Depois do OK, a tela é reaberta se o idioma **ou o tema** mudou.
- `--tray` também cala o aviso "já está aberto".

### Git
- Action: Create
- Instruction: commit `feat: tela de configurações, ícone e publicação (fase 5)`.

### Verification
- [x] `dotnet build` sem avisos e `dotnet test` verde (91 testes).
- [x] Trocar o atalho de área para `Ctrl+Alt+A` nas configurações funciona na hora, sem reiniciar; um atalho repetido é recusado.
- [x] "Iniciar com o Windows" cria/remove o valor em `HKCU\...\Run` (conferir no `regedit`) e o app abre na bandeja depois de reiniciar.
- [x] Trocar para JPEG + qualidade 70 gera `.jpg` com o nome da prévia.
- [x] Tema Escuro/Claro/Sistema muda as barras e a tela de configurações.
- [x] Trocar o idioma para English muda o menu da bandeja, a tela de configurações e as dicas do editor sem reiniciar o app.
- [x] `./publish.ps1` gera um único `artifacts/publish/Fragmo.exe` (49 MB) com o ícone do app. *(rodar numa máquina sem .NET: não testado)*
- [ ] Monitor de rede (ex.: `Resource Monitor`) não mostra nenhuma conexão do `Fragmo.exe`.

---

## Phase 6 (Opcional): OCR, captura com atraso e histórico

> Só implementar se o usuário pedir. Cada item é independente.

### `src/Fragmo/Fragmo.csproj`
- Action: Modify
- Instruction: Para o OCR, trocar o TFM para `net10.0-windows10.0.19041.0` (habilita as APIs WinRT `Windows.Media.Ocr` sem pacote externo).

### `src/Fragmo/Ocr/OcrService.cs`
- Action: Create
- Instruction: `Task<string> RecognizeAsync(Bitmap img)`: converte para `SoftwareBitmap` (PNG em `InMemoryRandomAccessStream` → `BitmapDecoder`), usa `OcrEngine.TryCreateFromUserProfileLanguages()`; se `null`, avisa que falta o pacote de idioma. Botão "Copiar texto (Ctrl+Shift+C)" no editor copia o resultado.

### `src/Fragmo/Capture/DelayedCapture.cs`
- Action: Create
- Instruction: Menu da bandeja "Capturar com atraso" → 3/5/10 s, com contagem regressiva no tooltip do ícone, e então `CaptureWorkflow.CaptureArea()`.

### `src/Fragmo/History/HistoryStore.cs`
- Action: Create
- Instruction: Guarda as últimas N (padrão 20) capturas salvas como caminhos em `%APPDATA%\Fragmo\history.json` (só caminhos, não imagens); submenu "Recentes" na bandeja abre o arquivo; entradas de arquivos apagados são removidas ao carregar. Teste unitário para o limite N e a remoção.

### Verification
- [ ] OCR de uma captura de texto da tela copia o texto correto (PT-BR).
- [ ] Atraso de 5 s permite abrir um menu antes da captura.
- [ ] "Recentes" lista só arquivos que existem, no máximo 20.

> A Fase 6 foi substituída pelas fases 7 a 9 (o OCR entrou na Fase 8).

---

## Phase 7: Barra de ferramentas única
- `Overlay/ToolbarPlacement.cs` (pura): centralizada abaixo da seleção; sem espaço, acima; seleção da altura toda, dentro, junto à borda inferior; sempre no monitor com a maior parte da seleção. Testes: `ToolbarPlacementTests`.
- `Editor/EditorToolbar.cs`: uma barra horizontal, cantos arredondados (raio 10), fundo do tema, destaque Accent. Grupos: [tamanho] | caneta, linha, seta, retângulo, marcador, texto, passos, desfoque, desfoque automático | cor, espessura | desfazer, refazer | fixar, imprimir, copiar, copiar texto, salvar, fechar.
- `SelectionOverlay`: o rótulo de tamanho no canto só aparece enquanto se arrasta ou redimensiona (a barra fica escondida nesse momento).

### Verification
- [x] `dotnet build` sem avisos e `dotnet test` verde.
- [ ] Barra abaixo, acima e dentro da seleção conforme o espaço, em 1 e 2 monitores.

## Phase 8: Copiar texto (OCR local)
- TFM `net10.0-windows10.0.19041.0` (app e testes); `Ocr/OcrService.cs` com `Windows.Media.Ocr`, idiomas do perfil, amplia imagens pequenas (`ChooseScale`, testado).
- Botão "Copiar texto" (O) → `OverlayResult.CopyText` → OCR da área sem anotações → área de transferência + aviso "N linhas copiadas". Sem idioma de OCR: mensagem de como instalar.

### Verification
- [x] OCR real reconhece texto pt-BR nesta máquina.
- [ ] Botão e tecla O copiam o texto de uma captura real.

## Phase 9: Desfoque automático
- `Ocr/SensitiveDataDetector.cs` (pura): CPF e CNPJ com dígitos verificadores, e-mail, IPv4, telefone BR. Testes: `SensitiveDataDetectorTests`.
- `Editor/AnnotationGroup`: lote numa única etapa de desfazer. Botão "Desfoque automático" (X); não repete áreas já desfocadas.
- Configuração `AutoBlurOnCapture` (desligada por padrão).
- `Ctrl+Shift+S` passa a ficar sempre registrado junto com a Print Screen (`HotkeyAction.CaptureAreaAlt`): o Windows pode aceitar o registro da Print Screen e mesmo assim não entregar a tecla.

### Verification
- [x] Ponta a ponta com OCR real: 4 itens desfocados, CPF inválido mantido, um Ctrl+Z desfaz o lote.
- [ ] Opção "ao capturar" desfoca sozinha ao selecionar.
