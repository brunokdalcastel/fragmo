# Fragmo

Captura de tela 100% local para Windows. / 100% local screen capture for Windows.

[Português](#português) · [English](#english)

---

## Português

### Atalhos

| Ação | Atalho padrão |
|---|---|
| Capturar área | `PrintScreen` (alternativo: `Ctrl+Shift+S`) |
| Tela cheia → salvar direto | `Ctrl+Shift+F` |
| Janela ativa → editor | `Ctrl+Shift+W` |
| Repetir última área | `Ctrl+Shift+R` |

Todos podem ser trocados em **Configurações…** (menu da bandeja), sem reiniciar o app.

No editor: `Enter`/`Ctrl+C` copia, `Ctrl+S` salva, `Ctrl+T` fixa na tela, `Ctrl+P` imprime,
`Ctrl+Z`/`Ctrl+Y` desfaz/refaz, `Esc` fecha.

### Ferramentas do editor

Caneta (`P`), linha (`L`), seta (`A`), retângulo (`R`), texto (`T`), marcador (`M`), desfoque (`B`) e
passos numerados (`N`), com cor e espessura ajustáveis.

### Print Screen no Windows 11

O Windows 11 usa a tecla Print Screen para abrir a própria ferramenta de captura. Nesse caso o Fragmo usa
`Ctrl+Shift+S` e avisa na bandeja. Para liberar a tecla: **Configurações › Acessibilidade › Teclado** e desative
"Usar a tecla Print Screen para abrir a captura de tela".

### Desenvolvimento

Pré-requisito: [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
dotnet build
dotnet test
dotnet run --project src/Fragmo
```

### Publicar

```powershell
./publish.ps1
```

Roda os testes e gera um único `artifacts/publish/Fragmo.exe` (com o runtime embutido; não precisa do .NET
instalado). Basta copiar o EXE para qualquer pasta.

> **Aviso:** o EXE não é assinado digitalmente. Na primeira execução o Windows SmartScreen pode mostrar
> "O Windows protegeu o computador" — clique em **Mais informações › Executar assim mesmo**.

### Privacidade

O Fragmo não acessa a rede e não tem telemetria. As capturas e as configurações ficam só no seu computador
(`%USERPROFILE%\Pictures\Fragmo` e `%APPDATA%\Fragmo`). "Iniciar com o Windows" grava apenas o valor
`Fragmo` em `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.

---

## English

### Hotkeys

| Action | Default hotkey |
|---|---|
| Capture area | `PrintScreen` (fallback: `Ctrl+Shift+S`) |
| Full screen → save directly | `Ctrl+Shift+F` |
| Active window → editor | `Ctrl+Shift+W` |
| Repeat last area | `Ctrl+Shift+R` |

All of them can be changed in **Settings…** (tray menu), with no restart.

In the editor: `Enter`/`Ctrl+C` copies, `Ctrl+S` saves, `Ctrl+T` pins to screen, `Ctrl+P` prints,
`Ctrl+Z`/`Ctrl+Y` undo/redo, `Esc` closes.

### Editor tools

Pen (`P`), line (`L`), arrow (`A`), rectangle (`R`), text (`T`), highlighter (`M`), blur (`B`) and
numbered steps (`N`), with adjustable color and thickness.

### Print Screen on Windows 11

Windows 11 uses the Print Screen key to open its own snipping tool. When that happens Fragmo falls back to
`Ctrl+Shift+S` and shows a tray notice. To free the key: **Settings › Accessibility › Keyboard** and turn off
"Use the Print screen key to open screen capture".

### Development

Requirement: [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
dotnet build
dotnet test
dotnet run --project src/Fragmo
```

### Publishing

```powershell
./publish.ps1
```

Runs the tests and produces a single `artifacts/publish/Fragmo.exe` (runtime included; .NET does not need to be
installed). Copy the EXE to any folder.

> **Note:** the EXE is not digitally signed. On first run Windows SmartScreen may show "Windows protected your PC" —
> click **More info › Run anyway**.

### Privacy

Fragmo never touches the network and has no telemetry. Captures and settings stay on your computer
(`%USERPROFILE%\Pictures\Fragmo` and `%APPDATA%\Fragmo`). "Start with Windows" only writes the `Fragmo`
value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
