# Fragmo

Captura de tela 100% local para Windows. / 100% local screen capture for Windows.

[Português](#português) · [English](#english)

---

## Português

### Download e instalação

O Fragmo é um único arquivo `Fragmo.exe`, sem instalador, e não precisa de permissão de administrador
nem do .NET instalado.

1. Baixe o `Fragmo.exe` da versão mais recente em **[Releases](https://github.com/brunokdalcastel/fragmo/releases/latest)**.
2. Crie a pasta `%LOCALAPPDATA%\Programs\Fragmo` (cole esse caminho na barra de endereço do Explorador de Arquivos)
   e mova o `Fragmo.exe` para ela.
3. Dê dois cliques no `Fragmo.exe`. Como o EXE não é assinado, o Windows SmartScreen pode mostrar
   "O Windows protegeu o computador": clique em **Mais informações › Executar assim mesmo** (só na primeira vez).
4. O ícone laranja aparece na bandeja, perto do relógio. Se ele ficar escondido, clique na setinha `^` e arraste-o
   para a barra.
5. Opcional: clique com o botão direito no `Fragmo.exe` › **Mostrar mais opções › Enviar para › Área de trabalho
   (criar atalho)**.

Ou, pelo PowerShell:

```powershell
$dir = "$env:LOCALAPPDATA\Programs\Fragmo"
New-Item -ItemType Directory -Force $dir | Out-Null
Invoke-WebRequest "https://github.com/brunokdalcastel/fragmo/releases/latest/download/Fragmo.exe" -OutFile "$dir\Fragmo.exe"
Start-Process "$dir\Fragmo.exe"
```

### Configuração recomendada

Clique com o botão direito no ícone da bandeja › **Configurações…**:

- **Iniciar com o Windows:** marque para o Fragmo abrir sozinho, direto na bandeja.
- **Atalhos:** clique numa caixa e pressione a combinação desejada; `Backspace` limpa. Vale na hora, sem reiniciar.
- **Salvar:** pasta, formato (PNG, JPEG ou BMP), qualidade do JPEG, prefixo do nome e se deve perguntar onde salvar.
- **Geral:** lupa, tema (escuro, claro ou seguir o Windows) e idioma (português ou inglês).

**Liberar a tecla Print Screen no Windows 11.** O Windows 11 usa a tecla Print Screen para abrir a própria
ferramenta de captura; nesse caso o Fragmo usa `Ctrl+Shift+S` e avisa na bandeja. Para liberar a tecla:
**Configurações do Windows › Acessibilidade › Teclado** e desative "Usar a tecla Print Screen para abrir a captura
de tela". Depois, feche o Fragmo (bandeja › Sair) e abra de novo.

### Atualizar e desinstalar

- **Atualizar:** bandeja › **Sair**, substitua o `Fragmo.exe` pelo novo e abra de novo. As configurações são mantidas.
- **Desinstalar:** em Configurações, desmarque "Iniciar com o Windows"; bandeja › **Sair**; apague a pasta
  `%LOCALAPPDATA%\Programs\Fragmo` e, se quiser apagar as configurações, `%APPDATA%\Fragmo`.

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
instalado). O EXE não é assinado digitalmente.

### Privacidade

O Fragmo não acessa a rede e não tem telemetria. As capturas e as configurações ficam só no seu computador
(`%USERPROFILE%\Pictures\Fragmo` e `%APPDATA%\Fragmo`). "Iniciar com o Windows" grava apenas o valor
`Fragmo` em `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.

---

## English

### Download and install

Fragmo is a single `Fragmo.exe` file with no installer. It needs neither admin rights nor .NET installed.

1. Download `Fragmo.exe` from the latest version under **[Releases](https://github.com/brunokdalcastel/fragmo/releases/latest)**.
2. Create the folder `%LOCALAPPDATA%\Programs\Fragmo` (paste that path into File Explorer's address bar) and move
   `Fragmo.exe` into it.
3. Double-click `Fragmo.exe`. Since the EXE is not signed, Windows SmartScreen may show "Windows protected your PC":
   click **More info › Run anyway** (first run only).
4. The orange icon shows up in the tray, next to the clock. If it is hidden, click the `^` arrow and drag it to the
   taskbar.
5. Optional: right-click `Fragmo.exe` › **Show more options › Send to › Desktop (create shortcut)**.

Or, with PowerShell:

```powershell
$dir = "$env:LOCALAPPDATA\Programs\Fragmo"
New-Item -ItemType Directory -Force $dir | Out-Null
Invoke-WebRequest "https://github.com/brunokdalcastel/fragmo/releases/latest/download/Fragmo.exe" -OutFile "$dir\Fragmo.exe"
Start-Process "$dir\Fragmo.exe"
```

### Recommended setup

Right-click the tray icon › **Settings…**:

- **Start with Windows:** check it so Fragmo starts on its own, straight to the tray.
- **Hotkeys:** click a box and press the combination you want; `Backspace` clears it. Applies immediately.
- **Saving:** folder, format (PNG, JPEG or BMP), JPEG quality, file name prefix and whether to ask where to save.
- **General:** magnifier, theme (dark, light or follow Windows) and language (Portuguese or English).

**Free the Print Screen key on Windows 11.** Windows 11 uses the Print Screen key to open its own snipping tool; when
that happens Fragmo falls back to `Ctrl+Shift+S` and shows a tray notice. To free the key: **Windows Settings ›
Accessibility › Keyboard** and turn off "Use the Print screen key to open screen capture". Then exit Fragmo
(tray › Exit) and start it again.

### Update and uninstall

- **Update:** tray › **Exit**, replace `Fragmo.exe` with the new one and start it again. Settings are kept.
- **Uninstall:** in Settings, uncheck "Start with Windows"; tray › **Exit**; delete the folder
  `%LOCALAPPDATA%\Programs\Fragmo` and, to remove the settings too, `%APPDATA%\Fragmo`.

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
installed). The EXE is not digitally signed.

### Privacy

Fragmo never touches the network and has no telemetry. Captures and settings stay on your computer
(`%USERPROFILE%\Pictures\Fragmo` and `%APPDATA%\Fragmo`). "Start with Windows" only writes the `Fragmo`
value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
