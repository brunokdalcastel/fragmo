using Fragmo.Output;
using Fragmo.Overlay;
using Fragmo.Settings;

namespace Fragmo.Capture;

/// <summary>Fluxo completo: congelar a tela, selecionar a área e copiar ou salvar.</summary>
internal sealed class CaptureWorkflow(AppSettings settings, SettingsStore store, OutputService output, TrayContext tray)
{
    private bool _busy;

    public void CaptureArea()
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        try
        {
            // Captura antes de mostrar o overlay, para a tela ficar "congelada".
            using var screen = ScreenCapturer.CaptureVirtualScreen();

            OverlayResult result;
            Rectangle selection;
            using (var overlay = new SelectionOverlay(screen, settings))
            {
                overlay.ShowDialog();
                result = overlay.Result;
                selection = overlay.Selection;
            }

            if (result == OverlayResult.Cancel || selection.Width <= 0 || selection.Height <= 0)
            {
                return;
            }

            using var image = ScreenCapturer.Crop(screen.Image, selection);
            if (result == OverlayResult.Copy)
            {
                output.CopyToClipboard(image);
                tray.ShowInfo(Strings.Copied);
            }
            else
            {
                string? path = output.Save(image, null);
                if (path is not null)
                {
                    tray.ShowInfo(string.Format(Strings.SavedTo, path));
                }
            }

            Rectangle region = CoordinateMapper.BitmapToScreen(selection, screen.VirtualBounds);
            settings.LastRegion = new RegionDto(region.X, region.Y, region.Width, region.Height);
            store.Save(settings);
        }
        catch (Exception ex)
        {
            tray.ShowError(string.Format(Strings.CaptureError, ex.Message));
        }
        finally
        {
            _busy = false;
        }
    }
}
