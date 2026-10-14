using Fragmo.Ocr;
using Fragmo.Output;
using Fragmo.Overlay;
using Fragmo.Pin;
using Fragmo.Settings;

namespace Fragmo.Capture;

/// <summary>Fluxos de captura: área, janela ativa, repetir a última área e tela cheia.</summary>
internal sealed class CaptureWorkflow(AppSettings settings, SettingsStore store, OutputService output, TrayContext tray)
{
    private bool _busy;

    public void CaptureArea() => Run(() =>
    {
        // Captura antes de mostrar o overlay, para a tela ficar "congelada".
        using var screen = ScreenCapturer.CaptureVirtualScreen();
        RunOverlay(screen, null);
    });

    /// <summary>Abre o editor com a janela ativa já selecionada; sem janela, cai na seleção normal.</summary>
    public void CaptureWindow() => Run(() =>
    {
        // A janela precisa ser lida antes: depois o overlay é que fica em primeiro plano.
        Rectangle? window = ScreenCapturer.GetForegroundWindowBounds();
        using var screen = ScreenCapturer.CaptureVirtualScreen();
        RunOverlay(screen, window is Rectangle bounds ? ToBitmap(bounds, screen) : null);
    });

    /// <summary>Reabre a seleção na última área usada; sem área anterior, abre a seleção normal.</summary>
    public void RepeatLastArea() => Run(() =>
    {
        using var screen = ScreenCapturer.CaptureVirtualScreen();
        RegionDto? last = settings.LastRegion;
        Rectangle? initial = last is null
            ? null
            : ToBitmap(new Rectangle(last.X, last.Y, last.Width, last.Height), screen);
        RunOverlay(screen, initial);
    });

    /// <summary>Salva o monitor principal direto na pasta, sem diálogo.</summary>
    public void CaptureFullScreen() => Run(() =>
    {
        using var screen = ScreenCapturer.CaptureVirtualScreen();
        Rectangle primary = ScreenCapturer.GetPrimaryScreenBounds();
        var rect = new Rectangle(CoordinateMapper.ScreenToBitmap(primary.Location, screen.VirtualBounds), primary.Size);
        using Bitmap image = ScreenCapturer.Crop(screen.Image, rect);
        string path = output.SaveDirect(image);
        tray.ShowInfo(string.Format(Strings.SavedTo, path));
    });

    private void Run(Action capture)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        try
        {
            capture();
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

    /// <summary>Retângulo de tela → bitmap, limitado à imagem; <c>null</c> se ficar fora dela.</summary>
    private static Rectangle? ToBitmap(Rectangle screenRect, CapturedScreen screen)
    {
        var r = new Rectangle(CoordinateMapper.ScreenToBitmap(screenRect.Location, screen.VirtualBounds), screenRect.Size);
        Rectangle clamped = SelectionGeometry.Clamp(r, screen.Image.Size);
        return clamped.IsEmpty ? null : clamped;
    }

    private void RunOverlay(CapturedScreen screen, Rectangle? initialSelection)
    {
        OverlayResult result;
        Rectangle selection;
        bool styleChanged;
        Bitmap? composed = null;
        Bitmap? ocrSource = null;
        using (var overlay = new SelectionOverlay(screen, settings, initialSelection, tray.ShowInfo))
        {
            overlay.ShowDialog();
            result = overlay.Result;
            selection = overlay.Selection;
            styleChanged = overlay.StrokeStyleChanged;
            if (result != OverlayResult.Cancel && selection.Width > 0 && selection.Height > 0)
            {
                // O OCR lê a captura sem anotações; as outras saídas incluem as anotações
                // e precisam do overlay vivo (é ele que guarda o histórico).
                if (result == OverlayResult.CopyText)
                {
                    ocrSource = overlay.CropSelection();
                }
                else
                {
                    composed = overlay.ComposeResult();
                }
            }
        }

        Rectangle region = CoordinateMapper.BitmapToScreen(selection, screen.VirtualBounds);
        if (ocrSource is not null)
        {
            settings.LastRegion = new RegionDto(region.X, region.Y, region.Width, region.Height);
            store.Save(settings);
            _ = CopyTextAsync(ocrSource);
            return;
        }

        if (composed is null)
        {
            if (styleChanged)
            {
                store.Save(settings); // cor/espessura novas valem mesmo se cancelar
            }
            return;
        }

        if (result == OverlayResult.Pin)
        {
            // A janela fixada passa a ser dona da imagem.
            var pin = new PinWindow(composed, region.Location, output);
            tray.AddPin(pin);
        }
        else
        {
            using Bitmap image = composed;
            switch (result)
            {
                case OverlayResult.Copy:
                    output.CopyToClipboard(image);
                    tray.ShowInfo(Strings.Copied);
                    break;
                case OverlayResult.Print:
                    output.Print(image, null);
                    break;
                default:
                    string? path = output.Save(image, null);
                    if (path is not null)
                    {
                        tray.ShowInfo(string.Format(Strings.SavedTo, path));
                    }
                    break;
            }
        }

        settings.LastRegion = new RegionDto(region.X, region.Y, region.Width, region.Height);
        store.Save(settings);
    }

    /// <summary>Reconhece o texto da imagem, copia e avisa na bandeja. Fica com a posse da imagem.</summary>
    private async Task CopyTextAsync(Bitmap image)
    {
        try
        {
            OcrText? text;
            using (image)
            {
                text = await OcrService.RecognizeAsync(image);
            }

            if (text is null)
            {
                MessageBox.Show(Strings.OcrNoLanguage, Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (text.Lines.Count == 0)
            {
                tray.ShowInfo(Strings.OcrNoText);
                return;
            }
            output.CopyText(text.FullText);
            tray.ShowInfo(Strings.LinesCopied(text.Lines.Count));
        }
        catch (Exception ex)
        {
            tray.ShowError(string.Format(Strings.OcrFailed, ex.Message));
        }
    }
}
