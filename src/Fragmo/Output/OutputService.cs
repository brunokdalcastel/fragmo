using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using Fragmo.Settings;

namespace Fragmo.Output;

/// <summary>Copia a imagem para a área de transferência ou grava em arquivo.</summary>
internal sealed class OutputService(AppSettings settings)
{
    private const int ClipboardAttempts = 5;
    private const int ClipboardRetryDelayMs = 50;

    public void CopyToClipboard(Bitmap img)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                Clipboard.SetImage(img);
                return;
            }
            catch (ExternalException) when (attempt < ClipboardAttempts)
            {
                // Outro processo está segurando a área de transferência.
                Thread.Sleep(ClipboardRetryDelayMs);
            }
        }
    }

    public void CopyText(string text)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                Clipboard.SetText(text);
                return;
            }
            catch (ExternalException) when (attempt < ClipboardAttempts)
            {
                Thread.Sleep(ClipboardRetryDelayMs);
            }
        }
    }

    public string? Save(Bitmap img, IWin32Window? owner)
    {
        if (!settings.AskWhereToSave)
        {
            return SaveDirect(img);
        }

        Directory.CreateDirectory(settings.SaveFolder);
        using var dialog = new SaveFileDialog
        {
            InitialDirectory = settings.SaveFolder,
            FileName = FileNameBuilder.Build(settings.FileNamePrefix, DateTime.Now, settings.DefaultFormat),
            Filter = Strings.SaveFilter,
            FilterIndex = (int)settings.DefaultFormat + 1,
            DefaultExt = FileNameBuilder.Extension(settings.DefaultFormat),
            AddExtension = true,
            OverwritePrompt = true,
        };

        if (dialog.ShowDialog(owner) != DialogResult.OK)
        {
            return null;
        }

        WriteImage(img, dialog.FileName, FormatFromExtension(dialog.FileName));
        return dialog.FileName;
    }

    public string SaveDirect(Bitmap img)
    {
        Directory.CreateDirectory(settings.SaveFolder);
        string name = FileNameBuilder.Build(settings.FileNamePrefix, DateTime.Now, settings.DefaultFormat);
        string path = Path.Combine(settings.SaveFolder, FileNameBuilder.MakeUnique(settings.SaveFolder, name, File.Exists));
        WriteImage(img, path, settings.DefaultFormat);
        return path;
    }

    /// <summary>Imprime numa página, reduzindo para caber nas margens (nunca amplia).</summary>
    public void Print(Bitmap img, IWin32Window? owner)
    {
        using var document = new PrintDocument { DocumentName = Strings.AppName };
        document.PrintPage += (_, e) =>
        {
            Graphics g = e.Graphics!;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(img, PrintLayout.Fit(img.Size, e.MarginBounds));
            e.HasMorePages = false;
        };

        using var dialog = new PrintDialog { Document = document, UseEXDialog = true, AllowSomePages = false };
        if (dialog.ShowDialog(owner) == DialogResult.OK)
        {
            document.Print();
        }
    }

    public void WriteImage(Bitmap img, string path, ImageFormatKind fmt)
    {
        switch (fmt)
        {
            case ImageFormatKind.Jpeg:
                ImageCodecInfo codec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
                using (var parameters = new EncoderParameters(1))
                {
                    parameters.Param[0] = new EncoderParameter(Encoder.Quality, (long)settings.JpegQuality);
                    img.Save(path, codec, parameters);
                }
                break;
            case ImageFormatKind.Bmp:
                img.Save(path, ImageFormat.Bmp);
                break;
            default:
                img.Save(path, ImageFormat.Png);
                break;
        }
    }

    public static ImageFormatKind FormatFromExtension(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => ImageFormatKind.Jpeg,
            ".bmp" => ImageFormatKind.Bmp,
            _ => ImageFormatKind.Png,
        };
}
