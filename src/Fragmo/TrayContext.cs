using System.Diagnostics;
using Fragmo.Settings;
using Fragmo.Ui;

namespace Fragmo;

/// <summary>
/// Contexto principal do app: ícone na bandeja e menu de contexto.
/// </summary>
internal sealed class TrayContext : ApplicationContext
{
    private readonly SettingsStore _store;
    private readonly AppSettings _settings;
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private readonly Icon _icon;

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
    }

    public void ShowInfo(string text) =>
        _notifyIcon.ShowBalloonTip(4000, Strings.AppName, text, ToolTipIcon.Info);

    public void ShowError(string text) =>
        _notifyIcon.ShowBalloonTip(4000, Strings.AppName, text, ToolTipIcon.Error);

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem(Strings.MenuCaptureArea) { Enabled = false });
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
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _icon.Dispose();
        base.ExitThreadCore();
    }
}
