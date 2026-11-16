using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Fragmo.Editor;
using Fragmo.Output;
using Fragmo.Settings;
using Fragmo.Tutorial.Export;
using Fragmo.Ui;

namespace Fragmo.Tutorial.Editor;

/// <summary>
/// Editor de tutorial: lista de passos e notas à esquerda, passo selecionado à direita. Trabalha numa pasta
/// (<see cref="WorkFolder"/>) com as imagens e uma cópia do <c>tutorial.json</c> reescrita a cada mudança com
/// <c>Finished = false</c>: se o app morrer com o editor aberto, a abertura seguinte oferece recuperar.
/// Ao fechar (salvo ou descartado) a pasta é apagada (RNF3).
/// </summary>
internal sealed class TutorialEditorForm : Form
{
    private const int ThumbnailCacheSize = 64;
    private const int ThumbnailBoxHeight = 90;
    private const int ItemPadding = 6;
    private const int NumberColumnWidth = 30;

    private enum ExportFormat { Html, Markdown, Pdf, Word }

    /// <summary>Última pasta usada nos diálogos de salvar/exportar, enquanto o app está aberto.</summary>
    private static string? s_lastFolder;

    private readonly AppSettings _settings;
    private readonly Theme _theme;
    private readonly Action<string> _info;
    private readonly Icon _icon;
    private readonly TutorialEditHistory _history = new();
    private readonly ThumbnailCache _thumbnails;
    private readonly Font _numberFont;
    private readonly Font _noteFont;

    private readonly TextBox _titleBox = new() { Width = 420 };
    private readonly Button _saveButton = new() { AutoSize = true };
    private readonly Button _exportButton = new() { AutoSize = true };
    private readonly Button _undoButton = new() { AutoSize = true };
    private readonly ContextMenuStrip _exportMenu;
    private readonly SplitContainer _split = new() { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1 };
    private readonly ListBox _list = new()
    {
        Dock = DockStyle.Fill,
        DrawMode = DrawMode.OwnerDrawFixed,
        IntegralHeight = false,
        BorderStyle = BorderStyle.None,
    };
    private readonly PictureBox _preview = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
    private readonly TextBox _textBox = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        AcceptsReturn = true,
        ScrollBars = ScrollBars.Vertical,
    };
    private readonly CheckBox _highlight = new() { AutoSize = true, Margin = new Padding(3, 8, 12, 3) };
    private readonly Button _upButton = new() { AutoSize = true };
    private readonly Button _downButton = new() { AutoSize = true };
    private readonly Button _deleteButton = new() { AutoSize = true };
    private readonly Button _noteButton = new() { AutoSize = true };
    private readonly Button _editImageButton = new() { AutoSize = true };
    private readonly Button _restoreButton = new() { AutoSize = true };

    private TutorialDocument _doc;
    private Dictionary<string, int> _numbers = [];
    private string? _currentFile;
    private bool _dirty;
    private bool _busy;
    private bool _updatingUi;
    private bool _forceClose;
    private bool _keepWorkFolder;
    private (TutorialItem Item, int Number)? _previewKey;
    private string? _textItemId;

    /// <param name="workFolder">Pasta com <c>tutorial.json</c> e <c>images/</c>; passa a ser deste editor.</param>
    /// <param name="currentFile">O <c>.fragmotut</c> de onde veio, ou <c>null</c> para uma gravação nova.</param>
    public TutorialEditorForm(AppSettings settings, string workFolder, TutorialDocument doc, string? currentFile,
        Action<string> info)
    {
        _settings = settings;
        _theme = Theme.Resolve(settings.Theme);
        _info = info;
        WorkFolder = workFolder;
        _doc = doc;
        _currentFile = currentFile;
        _dirty = currentFile is null; // gravação nova: fechar sem salvar perderia tudo
        _icon = IconFactory.CreateAppIcon(32);
        _thumbnails = new ThumbnailCache(ThumbnailCacheSize, RenderThumbnail);
        _numberFont = new Font("Segoe UI Semibold", 10f);
        _noteFont = new Font("Segoe UI", 9.5f, FontStyle.Italic | FontStyle.Bold);
        _exportMenu = BuildExportMenu();

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Icon = _icon;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1180, 760);
        MinimumSize = new Size(900, 600);
        KeyPreview = true;

        Controls.Add(_split);
        Controls.Add(BuildTopBar());
        _split.Panel1.Controls.Add(_list);
        _split.Panel2.Controls.Add(BuildDetail());

        BackColor = _theme.Background;
        ForeColor = _theme.Foreground;
        ApplyTheme(this);
        _list.BackColor = _theme.Background;
        _list.ForeColor = _theme.Foreground;
        _preview.BackColor = _theme.Surface;
        _split.BackColor = _theme.Border;
        _split.Panel1.BackColor = _theme.Background;
        _split.Panel2.BackColor = _theme.Background;
        ResumeLayout(performLayout: true);

        WireEvents();
        _titleBox.Text = doc.Title;
        PersistWorkingCopy();
        RefreshAll(selectId: doc.Items.FirstOrDefault()?.Id, forceText: true);
    }

    /// <summary>Pasta de trabalho (imagens + cópia do JSON).</summary>
    public string WorkFolder { get; }

    /// <summary>O <c>.fragmotut</c> salvo/aberto por último, ou <c>null</c>.</summary>
    public string? CurrentFile => _currentFile;

    /// <summary>
    /// Fecha sem perguntar (o app está saindo). Com alterações não salvas, a pasta de trabalho fica e
    /// é oferecida para recuperar na próxima abertura; sem alterações, é apagada.
    /// </summary>
    public void CloseForExit()
    {
        CommitPendingEdits();
        _forceClose = true;
        _keepWorkFolder = _dirty;
        Close();
    }

    // ----- Montagem -----

    private Control BuildTopBar()
    {
        _saveButton.Text = Strings.TutorialSave;
        _exportButton.Text = Strings.TutorialExport;
        _undoButton.Text = Strings.TutorialUndo;
        var label = new Label { Text = Strings.TutorialTitleLabel, AutoSize = true, Margin = new Padding(3, 8, 3, 3) };
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = false,
            Padding = new Padding(8, 8, 8, 6),
        };
        _titleBox.Margin = new Padding(3, 5, 12, 3);
        bar.Controls.AddRange([label, _titleBox, _saveButton, _exportButton, _undoButton]);
        return bar;
    }

    private Control BuildDetail()
    {
        _highlight.Text = Strings.TutorialHighlight;
        _upButton.Text = Strings.TutorialMoveUp;
        _downButton.Text = Strings.TutorialMoveDown;
        _deleteButton.Text = Strings.TutorialDelete;
        _noteButton.Text = Strings.TutorialInsertNote;
        _editImageButton.Text = Strings.TutorialEditImage;
        _restoreButton.Text = Strings.TutorialRestoreImage;

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        buttons.Controls.AddRange([_highlight, _upButton, _downButton, _deleteButton, _noteButton, _editImageButton, _restoreButton]);

        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8, 0, 8, 8) };
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _textBox.Margin = new Padding(0, 8, 0, 4);
        _preview.Margin = new Padding(0);
        grid.Controls.Add(_preview, 0, 0);
        grid.Controls.Add(_textBox, 0, 1);
        grid.Controls.Add(buttons, 0, 2);
        return grid;
    }

    private ContextMenuStrip BuildExportMenu()
    {
        var menu = new ContextMenuStrip
        {
            BackColor = _theme.Background,
            ForeColor = _theme.Foreground,
            ShowImageMargin = false,
            Renderer = new ToolStripProfessionalRenderer(new EditorToolbar.ThemedColorTable(_theme)) { RoundedEdges = false },
        };
        menu.Items.Add(Strings.TutorialExportHtml, null, (_, _) => _ = ExportAsync(ExportFormat.Html));
        menu.Items.Add(Strings.TutorialExportMarkdown, null, (_, _) => _ = ExportAsync(ExportFormat.Markdown));
        menu.Items.Add(Strings.TutorialExportPdf, null, (_, _) => _ = ExportAsync(ExportFormat.Pdf));
        menu.Items.Add(Strings.TutorialExportWord, null, (_, _) => _ = ExportAsync(ExportFormat.Word));
        foreach (ToolStripItem item in menu.Items)
        {
            item.ForeColor = _theme.Foreground;
        }
        return menu;
    }

    private void WireEvents()
    {
        _saveButton.Click += (_, _) => _ = SaveAsync();
        _exportButton.Click += (_, _) => _exportMenu.Show(_exportButton, new Point(0, _exportButton.Height));
        _undoButton.Click += (_, _) => Undo();
        _titleBox.Leave += (_, _) => CommitTitle();
        _titleBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                CommitTitle();
                e.SuppressKeyPress = true;
            }
        };
        _textBox.Leave += (_, _) => CommitText();
        _textBox.KeyDown += (_, e) =>
        {
            // Enter confirma; Shift+Enter quebra a linha.
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                CommitText();
                e.SuppressKeyPress = true;
            }
        };
        _list.SelectedIndexChanged += (_, _) =>
        {
            if (!_updatingUi)
            {
                UpdateDetail(forceText: false);
            }
        };
        _list.DrawItem += DrawListItem;
        _highlight.CheckedChanged += (_, _) =>
        {
            if (!_updatingUi && Selected is { } item)
            {
                Apply(TutorialEditHistory.SetShowHighlight(_doc, item.Id, _highlight.Checked), item.Id);
            }
        };
        _upButton.Click += (_, _) => MoveSelected(-1);
        _downButton.Click += (_, _) => MoveSelected(+1);
        _deleteButton.Click += (_, _) => DeleteSelected();
        _noteButton.Click += (_, _) => InsertNote();
        _editImageButton.Click += (_, _) => EditImage();
        _restoreButton.Click += (_, _) => RestoreImage();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _split.Panel1MinSize = Px(280);
        _split.SplitterDistance = Px(420);
        UpdateItemHeight();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        UpdateItemHeight();
    }

    private void UpdateItemHeight() => _list.ItemHeight = Math.Min(255, Px(ThumbnailBoxHeight + 2 * ItemPadding));

    private int Px(int logical) => (int)Math.Round(logical * DeviceDpi / 96.0);

    private void ApplyTheme(Control parent)
    {
        foreach (Control c in parent.Controls)
        {
            switch (c)
            {
                case Button b:
                    b.FlatStyle = FlatStyle.Flat;
                    b.FlatAppearance.BorderColor = _theme.Border;
                    b.BackColor = _theme.Surface;
                    b.ForeColor = _theme.Foreground;
                    break;
                case TextBox text:
                    text.BackColor = _theme.Surface;
                    text.ForeColor = _theme.Foreground;
                    text.BorderStyle = BorderStyle.FixedSingle;
                    break;
                default:
                    c.ForeColor = _theme.Foreground;
                    break;
            }
            ApplyTheme(c);
        }
    }

    // ----- Estado -----

    private TutorialItem? Selected =>
        _list.SelectedItem is string id ? _doc.Items.FirstOrDefault(i => i.Id == id) : null;

    /// <summary>Aplica uma edição pura; se mudou algo, empilha o estado anterior para o desfazer.</summary>
    private void Apply(TutorialDocument next, string? selectId)
    {
        if (ReferenceEquals(next, _doc))
        {
            return;
        }
        _history.Push(_doc);
        _doc = next;
        _dirty = true;
        PersistWorkingCopy();
        RefreshAll(selectId, forceText: false);
    }

    private void Undo()
    {
        CommitPendingEdits();
        if (_busy || !_history.TryUndo(out TutorialDocument previous))
        {
            return;
        }
        string? selectId = Selected?.Id;
        _doc = previous;
        _dirty = true;
        PersistWorkingCopy();
        RefreshAll(selectId, forceText: true);
    }

    private void RefreshAll(string? selectId, bool forceText)
    {
        _updatingUi = true;
        try
        {
            int number = 0;
            _numbers = _doc.Items
                .Where(i => i.Kind == TutorialItemKind.Step)
                .ToDictionary(i => i.Id, _ => ++number);

            List<string> ids = _doc.Items.Select(i => i.Id).ToList();
            if (_list.Items.Cast<string>().SequenceEqual(ids))
            {
                _list.Invalidate(); // só o conteúdo mudou (texto, destaque, imagem)
            }
            else
            {
                int previousIndex = _list.SelectedIndex;
                _list.BeginUpdate();
                _list.Items.Clear();
                _list.Items.AddRange(ids.Cast<object>().ToArray());
                _list.EndUpdate();
                int index = selectId is null ? -1 : ids.IndexOf(selectId);
                if (index < 0 && ids.Count > 0)
                {
                    index = Math.Clamp(previousIndex, 0, ids.Count - 1);
                }
                _list.SelectedIndex = index;
            }

            if (forceText || !_titleBox.Focused)
            {
                _titleBox.Text = _doc.Title;
            }
        }
        finally
        {
            _updatingUi = false;
        }

        UpdateDetail(forceText);
        Text = string.Format(Strings.TutorialEditorTitle, (_dirty ? "• " : "") + DisplayTitle);
        _undoButton.Enabled = _history.CanUndo && !_busy;
    }

    private string DisplayTitle => string.IsNullOrWhiteSpace(_doc.Title) ? Strings.AppName : _doc.Title;

    /// <summary>Lado direito: imagem, texto e botões do item selecionado.</summary>
    private void UpdateDetail(bool forceText)
    {
        TutorialItem? item = Selected;
        bool isStep = item?.Kind == TutorialItemKind.Step;
        int index = _list.SelectedIndex;

        _updatingUi = true;
        try
        {
            if (forceText || item?.Id != _textItemId || !_textBox.Focused)
            {
                _textBox.Text = ToTextBox(item?.Text ?? "");
                _textItemId = item?.Id;
            }
            _textBox.Enabled = item is not null && !_busy;
            _highlight.Checked = isStep && item!.ShowHighlight;
            _highlight.Enabled = isStep && !item!.Edited && (item.Highlight is not null || item.ClickPoint is not null) && !_busy;
        }
        finally
        {
            _updatingUi = false;
        }

        _upButton.Enabled = index > 0 && !_busy;
        _downButton.Enabled = index >= 0 && index < _list.Items.Count - 1 && !_busy;
        _deleteButton.Enabled = item is not null && !_busy;
        _noteButton.Enabled = !_busy;
        _editImageButton.Enabled = isStep && !_busy;
        _restoreButton.Visible = isStep && item!.Edited;
        _restoreButton.Enabled = !_busy;
        _saveButton.Enabled = !_busy;
        _exportButton.Enabled = !_busy && _doc.Items.Count > 0;
        UpdatePreview(item);
    }

    private void UpdatePreview(TutorialItem? item)
    {
        (TutorialItem, int)? key = item is { Kind: TutorialItemKind.Step } ? (item, _numbers[item.Id]) : null;
        if (key == _previewKey)
        {
            return;
        }
        _previewKey = key;

        Image? old = _preview.Image;
        _preview.Image = null;
        old?.Dispose();
        if (key is (TutorialItem step, int number))
        {
            _preview.Image = TryRender(step, number);
        }
    }

    private Bitmap? TryRender(TutorialItem step, int number)
    {
        try
        {
            return StepImageRenderer.Render(step, number, WorkFolder);
        }
        catch (Exception ex) when (IsImageError(ex))
        {
            Debug.WriteLine($"TutorialEditorForm: {ex.Message}");
            return null;
        }
    }

    private Bitmap? RenderThumbnail(TutorialItem step, int number)
    {
        try
        {
            return StepImageRenderer.RenderThumbnail(step, number, WorkFolder);
        }
        catch (Exception ex) when (IsImageError(ex))
        {
            Debug.WriteLine($"TutorialEditorForm: {ex.Message}");
            return null;
        }
    }

    private static bool IsImageError(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or TutorialFormatException or ArgumentException or ExternalException;

    // O TextBox usa CRLF; o modelo guarda LF.
    private static string ToTextBox(string text) => text.Replace("\r\n", "\n").Replace("\n", "\r\n");

    private static string FromTextBox(string text) => text.Replace("\r\n", "\n");

    /// <summary>
    /// Reescreve a cópia de trabalho do JSON (sempre <c>Finished = false</c>), de forma atômica. Falha só
    /// perde a recuperação depois de uma queda; o editor continua.
    /// </summary>
    private void PersistWorkingCopy()
    {
        try
        {
            string path = Path.Combine(WorkFolder, TutorialSerializer.JsonEntryName);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, TutorialSerializer.ToJson(_doc with { Finished = false }));
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"TutorialEditorForm: {ex.Message}");
        }
    }

    // ----- Edições -----

    private void CommitPendingEdits()
    {
        CommitTitle();
        CommitText();
    }

    private void CommitTitle()
    {
        if (!_updatingUi)
        {
            Apply(TutorialEditHistory.SetTitle(_doc, _titleBox.Text.Trim()), Selected?.Id);
        }
    }

    private void CommitText()
    {
        if (_updatingUi || _textItemId is not { } id)
        {
            return;
        }
        Apply(TutorialEditHistory.SetText(_doc, id, FromTextBox(_textBox.Text).Trim()), Selected?.Id);
    }

    private void MoveSelected(int delta)
    {
        CommitPendingEdits();
        if (Selected is not { } item)
        {
            return;
        }
        TutorialDocument next = delta < 0
            ? TutorialEditHistory.MoveUp(_doc, item.Id)
            : TutorialEditHistory.MoveDown(_doc, item.Id);
        Apply(next, item.Id);
    }

    private void DeleteSelected()
    {
        CommitPendingEdits();
        if (Selected is not { } item)
        {
            return;
        }
        Apply(TutorialEditHistory.Remove(_doc, item.Id), selectId: null); // fica no mesmo índice
        _list.Focus();
    }

    private void InsertNote()
    {
        CommitPendingEdits();
        string? afterId = Selected?.Id;
        TutorialDocument next = TutorialEditHistory.InsertNoteAfter(_doc, afterId, Strings.TutorialNewNote);
        int index = afterId is null ? 0 : next.Items.ToList().FindIndex(i => i.Id == afterId) + 1;
        Apply(next, next.Items[index].Id);
        _textBox.Focus();
        _textBox.SelectAll();
    }

    /// <summary>D8: o editor de anotações recebe a imagem composta; o resultado vira a imagem do passo.</summary>
    private void EditImage()
    {
        CommitPendingEdits();
        if (_busy || Selected is not { Kind: TutorialItemKind.Step } step)
        {
            return;
        }

        Bitmap? edited;
        using (Bitmap? composed = TryRender(step, _numbers[step.Id]))
        {
            if (composed is null)
            {
                MessageBox.Show(this, Strings.TutorialImageMissing, Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            edited = StepImageEditing.EditInOverlay(composed, _settings, this);
        }
        if (edited is null || IsDisposed)
        {
            edited?.Dispose();
            return;
        }

        string relative;
        try
        {
            using (edited)
            {
                relative = NextEditedImagePath(step);
                SaveImageAtomically(edited, Path.Combine(WorkFolder, relative.Replace('/', Path.DirectorySeparatorChar)));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ExternalException)
        {
            MessageBox.Show(this, string.Format(Strings.TutorialEditFailed, ex.Message), Strings.AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Apply(TutorialEditHistory.Update(_doc, step.Id, i => i with
        {
            ImageFile = relative,
            OriginalImageFile = i.Edited ? i.OriginalImageFile : i.ImageFile, // o original é o da 1ª edição
            Edited = true,
        }), step.Id);
    }

    private void RestoreImage()
    {
        CommitPendingEdits();
        if (Selected is not { Edited: true, OriginalImageFile: not null } step)
        {
            return;
        }
        Apply(TutorialEditHistory.Update(_doc, step.Id, i => i with
        {
            ImageFile = i.OriginalImageFile,
            OriginalImageFile = null,
            Edited = false,
        }), step.Id);
    }

    /// <summary>
    /// <c>images/NNNN-e&lt;k&gt;.png</c> com o primeiro <c>k</c> livre. As edições anteriores ficam no disco
    /// (o desfazer pode voltar a elas); o pacote só leva as imagens citadas.
    /// </summary>
    private string NextEditedImagePath(TutorialItem step)
    {
        string stem = Path.GetFileNameWithoutExtension(step.OriginalImageFile ?? step.ImageFile) ?? "";
        int cut = stem.IndexOf("-e", StringComparison.Ordinal);
        if (cut > 0)
        {
            stem = stem[..cut];
        }
        if (stem.Length == 0)
        {
            stem = "step";
        }

        for (int k = 1; ; k++)
        {
            string relative = $"{TutorialSerializer.ImagesFolder}/{stem}-e{k}.png";
            string full = Path.Combine(WorkFolder, TutorialSerializer.ImagesFolder, $"{stem}-e{k}.png");
            if (!TutorialSerializer.IsValidImagePath(relative))
            {
                throw new IOException($"Nome de imagem inválido: {relative}");
            }
            if (!File.Exists(full))
            {
                return relative;
            }
        }
    }

    private static void SaveImageAtomically(Bitmap image, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string tmp = path + ".tmp";
        image.Save(tmp, ImageFormat.Png);
        File.Move(tmp, path, overwrite: true);
    }

    // ----- Salvar e exportar -----

    /// <summary>Salva no arquivo atual, ou pergunta onde na primeira vez. Devolve <c>false</c> se não salvou.</summary>
    private async Task<bool> SaveAsync()
    {
        CommitPendingEdits();
        if (_busy)
        {
            return false;
        }

        string? path = _currentFile ?? AskPath(Strings.TutorialFilter, ".fragmotut");
        if (path is null)
        {
            return false;
        }

        TutorialDocument saved = _doc;
        bool ok = await RunBusyAsync(() => TutorialSerializer.SavePackage(saved with { Finished = true }, WorkFolder, path),
            Strings.TutorialSaveFailed);
        if (!ok || IsDisposed)
        {
            return false;
        }

        _currentFile = path;
        _dirty = !ReferenceEquals(_doc, saved); // nada muda durante o salvamento (janela travada), mas confere
        RefreshAll(Selected?.Id, forceText: false);
        _info(string.Format(Strings.TutorialSaved, path));
        return true;
    }

    private async Task ExportAsync(ExportFormat format)
    {
        CommitPendingEdits();
        if (_busy)
        {
            return;
        }

        (string filter, string extension) = format switch
        {
            ExportFormat.Html => (Strings.TutorialFilterHtml, ".html"),
            ExportFormat.Markdown => (Strings.TutorialFilterMarkdown, ".md"),
            ExportFormat.Pdf => (Strings.TutorialFilterPdf, ".pdf"),
            _ => (Strings.TutorialFilterWord, ".docx"),
        };
        Action<ExportDocument, string> write = format switch
        {
            ExportFormat.Html => HtmlExporter.Write,
            ExportFormat.Markdown => MarkdownExporter.Write,
            ExportFormat.Pdf => PdfWriter.Write,
            _ => DocxWriter.Write,
        };
        string? path = AskPath(filter, extension);
        if (path is null)
        {
            return;
        }

        TutorialDocument snapshot = _doc;
        string folder = WorkFolder;
        bool ok = await RunBusyAsync(() => write(ExportDocument.Build(snapshot, folder), path), Strings.TutorialExportFailed);
        if (ok)
        {
            _info(string.Format(Strings.TutorialExported, path));
        }
    }

    private string? AskPath(string filter, string extension)
    {
        string initial = s_lastFolder
            ?? (_currentFile is not null ? Path.GetDirectoryName(_currentFile) : null)
            ?? _settings.SaveFolder;
        using var dialog = new SaveFileDialog
        {
            Filter = filter,
            DefaultExt = extension.TrimStart('.'),
            AddExtension = true,
            OverwritePrompt = true,
            FileName = FileNameBuilder.Sanitize(_doc.Title, "Tutorial") + extension,
            InitialDirectory = Directory.Exists(initial) ? initial : "",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return null;
        }
        s_lastFolder = Path.GetDirectoryName(dialog.FileName);
        return dialog.FileName;
    }

    /// <summary>Roda <paramref name="work"/> fora da UI com a janela travada e o cursor de espera.</summary>
    private async Task<bool> RunBusyAsync(Action work, string errorFormat)
    {
        SetBusy(true);
        try
        {
            await Task.Run(work);
            return true;
        }
        catch (Exception ex) when (IsImageError(ex))
        {
            if (!IsDisposed)
            {
                MessageBox.Show(this, string.Format(errorFormat, ex.Message), Strings.AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return false;
        }
        finally
        {
            if (!IsDisposed)
            {
                SetBusy(false);
            }
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        UseWaitCursor = busy;
        _titleBox.Enabled = !busy;
        _list.Enabled = !busy;
        UpdateDetail(forceText: false);
        _undoButton.Enabled = _history.CanUndo && !busy;
    }

    // ----- Teclado -----

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Control | Keys.S:
                _ = SaveAsync();
                return true;
            case Keys.Control | Keys.Z:
                // Texto ainda não confirmado: o desfazer da própria caixa.
                if (ActiveControl == _textBox && FromTextBox(_textBox.Text).Trim() != (Selected?.Text ?? "") ||
                    ActiveControl == _titleBox && _titleBox.Text.Trim() != _doc.Title)
                {
                    break;
                }
                Undo();
                return true;
            case Keys.Delete when ActiveControl == _list:
                DeleteSelected();
                return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // ----- Lista -----

    private void DrawListItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _list.Items.Count || _list.Items[e.Index] is not string id)
        {
            return;
        }
        TutorialItem? item = _doc.Items.FirstOrDefault(i => i.Id == id);
        if (item is null)
        {
            return;
        }

        Graphics g = e.Graphics;
        bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        Color back = selected ? _theme.Accent : _theme.Background;
        Color fore = selected ? _theme.AccentForeground : _theme.Foreground;
        using (var brush = new SolidBrush(back))
        {
            g.FillRectangle(brush, e.Bounds);
        }
        using (var line = new Pen(_theme.Border))
        {
            g.DrawLine(line, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        }

        int pad = Px(ItemPadding);
        int textLeft;
        string text = ExportTextPreview(item.Text);
        if (item.Kind == TutorialItemKind.Step)
        {
            int number = _numbers[item.Id];
            var numberBox = new Rectangle(e.Bounds.Left + pad, e.Bounds.Top + pad, Px(NumberColumnWidth), Px(20));
            TextRenderer.DrawText(g, number.ToString(System.Globalization.CultureInfo.CurrentCulture), _numberFont, numberBox, fore,
                TextFormatFlags.Left | TextFormatFlags.NoPadding);

            var thumbBox = new Rectangle(numberBox.Right, e.Bounds.Top + pad,
                Px(StepImageRenderer.ThumbnailWidth), e.Bounds.Height - 2 * pad);
            Bitmap? thumb = _thumbnails.Get(item, number);
            if (thumb is not null)
            {
                g.DrawImage(thumb, Fit(thumb.Size, thumbBox));
            }
            else
            {
                TextRenderer.DrawText(g, Strings.TutorialImageMissing, Font, thumbBox, fore,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            }
            textLeft = thumbBox.Right + pad;
            DrawItemText(g, text, Font, fore, Rectangle.FromLTRB(textLeft, e.Bounds.Top + pad,
                e.Bounds.Right - pad, e.Bounds.Bottom - pad));
        }
        else
        {
            textLeft = e.Bounds.Left + pad;
            TextRenderer.DrawText(g, Strings.TutorialNoteLabel, _numberFont,
                new Rectangle(textLeft, e.Bounds.Top + pad, e.Bounds.Width - 2 * pad, Px(20)), fore,
                TextFormatFlags.Left | TextFormatFlags.NoPadding);
            DrawItemText(g, text, _noteFont, fore, Rectangle.FromLTRB(textLeft, e.Bounds.Top + pad + Px(22),
                e.Bounds.Right - pad, e.Bounds.Bottom - pad));
        }
    }

    private static void DrawItemText(Graphics g, string text, Font font, Color color, Rectangle box) =>
        TextRenderer.DrawText(g, text, font, box, color,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl);

    private static string ExportTextPreview(string text) => text.Replace("\r\n", " ").Replace('\n', ' ');

    /// <summary>Encaixa mantendo a proporção, sem ampliar, centralizado verticalmente.</summary>
    private static Rectangle Fit(Size image, Rectangle box)
    {
        double scale = Math.Min(1.0, Math.Min((double)box.Width / image.Width, (double)box.Height / image.Height));
        int w = Math.Max(1, (int)(image.Width * scale));
        int h = Math.Max(1, (int)(image.Height * scale));
        return new Rectangle(box.Left, box.Top + (box.Height - h) / 2, w, h);
    }

    // ----- Fechar -----

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (_forceClose)
        {
            return;
        }
        if (e.CloseReason is CloseReason.WindowsShutDown or CloseReason.TaskManagerClosing)
        {
            // Sem perguntas: a pasta fica para recuperar se havia algo não salvo.
            CommitPendingEdits();
            _keepWorkFolder = _dirty;
            return;
        }
        if (_busy)
        {
            e.Cancel = true;
            return;
        }

        CommitPendingEdits();
        if (!_dirty)
        {
            return;
        }

        DialogResult answer = MessageBox.Show(this, string.Format(Strings.TutorialUnsaved, DisplayTitle), Strings.AppName,
            MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
        switch (answer)
        {
            case DialogResult.No:
                return;
            case DialogResult.Yes:
                e.Cancel = true;
                _ = SaveThenCloseAsync();
                return;
            default:
                e.Cancel = true;
                return;
        }
    }

    private async Task SaveThenCloseAsync()
    {
        if (await SaveAsync() && !_dirty && !IsDisposed)
        {
            Close();
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        if (_keepWorkFolder)
        {
            return;
        }
        try
        {
            if (Directory.Exists(WorkFolder))
            {
                Directory.Delete(WorkFolder, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"TutorialEditorForm: {ex.Message}");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _preview.Image?.Dispose();
            _preview.Image = null;
            _thumbnails.Dispose();
            _exportMenu.Dispose();
            _numberFont.Dispose();
            _noteFont.Dispose();
            _icon.Dispose();
        }
        base.Dispose(disposing);
    }
}
