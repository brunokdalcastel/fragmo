using System.Diagnostics;
using Fragmo.Capture;
using Fragmo.Hotkeys;
using Fragmo.Output;
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

    private bool TryRegister(HotkeyAction action, string text) =>
        HotkeyBinding.TryParse(text, out HotkeyBinding binding) && _hotkeys.TryRegister(action, binding);

    private void OnHotkeyPressed(HotkeyAction action)
    {
        if (action == HotkeyAction.CaptureArea)
        {
            _workflow.CaptureArea();
        }
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(Strings.MenuCaptureArea, null, async (_, _) =>
        {
            await Task.Delay(MenuCloseDelayMs);
            _workflow.CaptureArea();
        });
        menu.Items.Add(Strings.MenuOpenFolder, null, (_, _) => OpenCapturesFolder());
        menu.Items.Add(Strings.MenuSettings, null, (_, _) => OpenSettings());
        menu.Items.Add(Strings.MenuAbout, null, (_, _) => ShowAbout());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Strings.MenuExit, null, (_, _) => ExitThread());
        return menu;
    }

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
        _hotkeys.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _icon.Dispose();
        base.ExitThreadCore();
    }
}
