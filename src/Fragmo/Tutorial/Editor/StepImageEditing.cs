using Fragmo.Capture;
using Fragmo.Overlay;
using Fragmo.Settings;
using Fragmo.Ui;

namespace Fragmo.Tutorial.Editor;

/// <summary>
/// "Editar imagem" de um passo (D8): abre o editor de anotações da captura sobre a imagem composta
/// e devolve o resultado achatado.
/// </summary>
internal static class StepImageEditing
{
    /// <summary>
    /// Abre o <see cref="SelectionOverlay"/> em modo tutorial no monitor do <paramref name="owner"/>. A imagem fica
    /// centralizada sobre um fundo do tamanho do monitor (assim a barra sempre cabe, mesmo com imagem pequena);
    /// a seleção é a imagem inteira. Imagem maior que o monitor passa da tela (limitação conhecida).
    /// </summary>
    /// <returns>A imagem editada, ou <c>null</c> se o usuário cancelou.</returns>
    public static Bitmap? EditInOverlay(Bitmap composed, AppSettings settings, IWin32Window owner)
    {
        Rectangle monitor = Screen.FromHandle(owner.Handle).Bounds;
        Rectangle canvasBounds = CanvasBounds(monitor, composed.Size);
        Rectangle selection = ImagePlacement(canvasBounds.Size, composed.Size);

        var canvas = new Bitmap(canvasBounds.Width, canvasBounds.Height);
        using (Graphics g = Graphics.FromImage(canvas))
        {
            g.Clear(Theme.Dark.Background);
            g.DrawImageUnscaled(composed, selection.Location);
        }

        using var screen = new CapturedScreen(canvas, canvasBounds); // descarta o canvas
        using var overlay = new SelectionOverlay(screen, settings, selection, notify: null, tutorialMode: true);
        overlay.ShowDialog(owner);
        return overlay.Result == OverlayResult.Done ? overlay.ComposeResult() : null;
    }

    /// <summary>Fundo: o monitor inteiro, crescendo para a direita/baixo se a imagem for maior.</summary>
    internal static Rectangle CanvasBounds(Rectangle monitor, Size image) =>
        new(monitor.Location, new Size(Math.Max(monitor.Width, image.Width), Math.Max(monitor.Height, image.Height)));

    /// <summary>Posição da imagem no fundo: centralizada.</summary>
    internal static Rectangle ImagePlacement(Size canvas, Size image) =>
        new(new Point((canvas.Width - image.Width) / 2, (canvas.Height - image.Height) / 2), image);
}
