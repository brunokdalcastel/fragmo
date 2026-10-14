using System.Diagnostics;
using Fragmo.Capture;
using Fragmo.Hotkeys;
using Fragmo.Output;
using Fragmo.Pin;
using Fragmo.Settings;
using Fragmo.Ui;

namespace Fragmo;

/// <summary>
/// Contexto principal do app: ícone na bandeja, menu de contexto e atalhos globais.
/// </summary>
internal sealed class TrayContext : ApplicationContext
{
    // Tempo para o menu da bandeja sumir da tela antes da captura.
    private const int MenuCloseDelayMs = 200;

    private readonly SettingsStore _store;
    private readonly AppSettings _settings;
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private readonly Icon _icon;
    private readonly HotkeyManager _hotkeys;
    private readonly CaptureWorkflow _workflow;
    private readonly List<PinWindow> _pins = [];

    public TrayContext(SettingsStore store, AppSettings settings)
    {
        _store = store;
        _settings = settings;
        _icon = IconFactory.CreateAppIcon(32);
        _menu = BuildMenu();
        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = Strings.TrayTooltip,
            ContextMenuStrip = _menu,
            Visible = true,
        };

        _workflow = new CaptureWorkflow(settings, store, new OutputService(settings), this);
        _notifyIcon.DoubleClick += (_, _) => _workflow.CaptureArea();
        _hotkeys = new HotkeyManager();
        _hotkeys.Pressed += OnHotkeyPressed;
        RegisterCaptureAreaHotkey();
        RegisterOtherHotkeys();
    }

    public void ShowInfo(string text) =>
        _notifyIcon.ShowBalloonTip(4000, Strings.AppName, text, ToolTipIcon.Info);

    public void ShowError(string text) =>
        _notifyIcon.ShowBalloonTip(4000, Strings.AppName, text, ToolTipIcon.Error);

    private void RegisterCaptureAreaHotkey()
    {
        if (TryRegister(HotkeyAction.CaptureArea, _settings.Hotkeys.CaptureArea))
        {
            return;
        }

        if (TryRegister(HotkeyAction.CaptureArea, _settings.Hotkeys.CaptureAreaFallback))
        {
            ShowInfo(Strings.PrintScreenTaken);
            return;
        }

        ShowError(Strings.HotkeyFailed);
    }

    /// <summary>Registra tela cheia, janela ativa e repetir; avisa por balão os que falharem.</summary>
    private void RegisterOtherHotkeys()
    {
        (HotkeyAction Action, string Text)[] hotkeys =
        [
            (HotkeyAction.CaptureFullScreen, _settings.Hotkeys.CaptureFullScreen),
            (HotkeyAction.CaptureWindow, _settings.Hotkeys.CaptureWindow),
            (HotkeyAction.RepeatLastArea, _settings.Hotkeys.RepeatLastArea),
        ];

        List<string> failed = [];
        foreach ((HotkeyAction action, string text) in hotkeys)
        {
            if (!string.IsNullOrWhiteSpace(text) && !TryRegister(action, text))
            {
                failed.Add(text);
            }
        }

        if (failed.Count > 0)
        {
            ShowError(string.Format(Strings.HotkeysInUse, string.Join(", ", failed)));
        }
    }

    /// <summary>Mostra uma imagem fixada e guarda a referência para fechá-la ao sair.</summary>
    public void AddPin(PinWindow pin)
    {
        _pins.Add(pin);
        pin.FormClosed += (_, _) => _pins.Remove(pin);
        pin.Info += ShowInfo;
        pin.Show();
    }

    private void CloseAllPins()
    {
        foreach (PinWindow pin in _pins.ToArray())
        {
            pin.Close();
        }
    }

    private bool TryRegister(HotkeyAction action, string text) =>
        HotkeyBinding.TryParse(text, out HotkeyBinding binding) && _hotkeys.TryRegister(action, binding);

    private void OnHotkeyPressed(HotkeyAction action)
    {
        switch (action)
        {
            case HotkeyAction.CaptureArea:
                _workflow.CaptureArea();
                break;
            case HotkeyAction.CaptureFullScreen:
                _workflow.CaptureFullScreen();
                break;
            case HotkeyAction.CaptureWindow:
                _workflow.CaptureWindow();
                break;
            case HotkeyAction.RepeatLastArea:
                _workflow.RepeatLastArea();
                break;
        }
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        AddCaptureItem(menu, Strings.MenuCaptureArea, () => _workflow.CaptureArea());
        AddCaptureItem(menu, Strings.MenuFullScreen, () => _workflow.CaptureFullScreen());
        AddCaptureItem(menu, Strings.MenuWindow, () => _workflow.CaptureWindow());
        AddCaptureItem(menu, Strings.MenuRepeatLast, () => _workflow.RepeatLastArea());
        var closePins = new ToolStripMenuItem(Strings.MenuClosePins, null, (_, _) => CloseAllPins());
        menu.Items.Add(closePins);
        menu.Opening += (_, _) => closePins.Enabled = _pins.Count > 0;
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Strings.MenuOpenFolder, null, (_, _) => OpenCapturesFolder());
        menu.Items.Add(Strings.MenuSettings, null, (_, _) => OpenSettings());
        menu.Items.Add(Strings.MenuAbout, null, (_, _) => ShowAbout());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Strings.MenuExit, null, (_, _) => ExitThread());
        return menu;
    }

    /// <summary>Item de captura: espera o menu sumir da tela antes de capturar.</summary>
    private static void AddCaptureItem(ContextMenuStrip menu, string text, Action capture) =>
        menu.Items.Add(text, null, async (_, _) =>
        {
            await Task.Delay(MenuCloseDelayMs);
            capture();
        });

    private void OpenCapturesFolder() => OpenInExplorer(_settings.SaveFolder);

    // Nesta fase "Configurações…" só abre a pasta onde fica o settings.json.
    private void OpenSettings() => OpenInExplorer(_store.Directory);

    private void OpenInExplorer(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private static void ShowAbout() =>
        MessageBox.Show(Strings.AboutText, Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);

    protected override void ExitThreadCore()
    {
        CloseAllPins();
        _hotkeys.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _icon.Dispose();
        base.ExitThreadCore();
    }
}
