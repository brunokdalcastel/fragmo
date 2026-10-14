using Fragmo.Hotkeys;
using Fragmo.Output;
using Fragmo.Ui;

namespace Fragmo.Settings;

/// <summary>
/// Tela de configurações montada em código: atalhos, salvar e geral. Os textos são lidos na construção,
/// então uma troca de idioma pede para reabrir a tela (<see cref="ReopenRequested"/>).
/// </summary>
internal sealed class SettingsForm : Form
{
    private const int FieldWidth = 220;
    private static readonly Color ErrorColor = ColorTranslator.FromHtml("#FF5A5A");

    // Mesma ordem do enum ImageFormatKind.
    private static readonly string[] FormatNames = ["PNG", "JPEG", "BMP"];
    private static readonly ThemeMode[] ThemeOrder = [ThemeMode.Dark, ThemeMode.Light, ThemeMode.System];
    private static readonly UiLanguage[] LanguageOrder = [UiLanguage.System, UiLanguage.Portuguese, UiLanguage.English];

    private readonly AppSettings _settings;
    private readonly SettingsStore _store;
    private readonly Theme _theme;
    private readonly Icon _icon;

    private readonly Dictionary<HotkeyAction, (HotkeyBox Box, Panel Frame, Label Error)> _hotkeyRows = [];
    private readonly TextBox _folder = new() { Width = FieldWidth + 60 };
    private readonly ComboBox _format = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
    private readonly NumericUpDown _quality = new() { Minimum = 1, Maximum = 100, Width = 70 };
    private readonly TextBox _prefix = new() { Width = FieldWidth };
    private readonly Label _preview = new() { AutoSize = true };
    private readonly CheckBox _askWhere = new() { AutoSize = true };
    private readonly CheckBox _startWithWindows = new() { AutoSize = true };
    private readonly CheckBox _showMagnifier = new() { AutoSize = true };
    private readonly ComboBox _themeBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly ComboBox _language = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private Button? _okButton;

    private int _focusedHotkeyBoxes;
    private bool _reopen;
    private string? _lastFailureKey;

    /// <summary>Disparado depois que o OK grava as configurações.</summary>
    public event Action? Applied;

    /// <summary><c>true</c> quando uma caixa de atalho ganha o foco; <c>false</c> quando todas perdem.</summary>
    public event Action<bool>? CapturingHotkeyChanged;

    /// <summary>Idioma ou tema mudou: a tela deve ser reaberta para ler os textos e cores novos.</summary>
    public bool ReopenRequested => _reopen;

    public SettingsForm(AppSettings settings, SettingsStore store)
    {
        _settings = settings;
        _store = store;
        _theme = Theme.Resolve(settings.Theme);
        _icon = IconFactory.CreateAppIcon(32);

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = Strings.SettingsTitle;
        Icon = _icon;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        var root = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };
        root.Controls.Add(BuildHotkeysSection());
        root.Controls.Add(BuildSaveSection());
        root.Controls.Add(BuildGeneralSection());
        root.Controls.Add(BuildButtons());
        Controls.Add(root);

        BackColor = _theme.Background;
        ForeColor = _theme.Foreground;
        ApplyTheme(this);
        ResumeLayout(performLayout: true);

        ReadFields(settings, StartupRegistration.IsEnabled());
    }

    /// <summary>Marca em vermelho os atalhos que o Windows recusou; os demais voltam ao normal.</summary>
    public void ShowHotkeyFailures(IReadOnlyCollection<HotkeyAction> failed)
    {
        foreach ((HotkeyAction action, (HotkeyBox _, Panel frame, Label error)) in _hotkeyRows)
        {
            bool isFailed = failed.Contains(action);
            frame.BackColor = isFailed ? ErrorColor : _theme.Background;
            error.Text = isFailed ? Strings.HotkeyTaken : string.Empty;
        }
    }

    private Control BuildHotkeysSection()
    {
        TableLayoutPanel grid = NewGrid();
        foreach ((HotkeyAction action, string _) in SettingsValidator.HotkeyEntries(_settings.Hotkeys))
        {
            var box = new HotkeyBox { Width = FieldWidth, Location = new Point(2, 2) };
            box.Enter += (_, _) => OnHotkeyBoxFocus(+1);
            box.Leave += (_, _) => OnHotkeyBoxFocus(-1);
            var frame = new Panel { AutoSize = true, Padding = new Padding(2), Margin = new Padding(1) };
            frame.Controls.Add(box);
            var error = new Label { AutoSize = true, Tag = ErrorColor, Anchor = AnchorStyles.Left };
            AddRow(grid, Strings.HotkeyLabel(action), frame, error);
            _hotkeyRows[action] = (box, frame, error);
        }
        AddWide(grid, new Label { Text = Strings.HotkeyHint, AutoSize = true });
        return Section(Strings.SectionHotkeys, grid);
    }

    private Control BuildSaveSection()
    {
        TableLayoutPanel grid = NewGrid();

        var browse = new Button { Text = Strings.BrowseFolder, AutoSize = true };
        browse.Click += (_, _) => BrowseFolder();
        AddRow(grid, Strings.LabelFolder, _folder, browse);

        _format.Items.AddRange(FormatNames);
        _format.SelectedIndexChanged += (_, _) => UpdateSaveFields();
        AddRow(grid, Strings.LabelFormat, _format);
        AddRow(grid, Strings.LabelJpegQuality, _quality);

        _prefix.TextChanged += (_, _) => UpdateSaveFields();
        AddRow(grid, Strings.LabelPrefix, _prefix);
        AddWide(grid, _preview);

        _askWhere.Text = Strings.AskWhereToSave;
        AddWide(grid, _askWhere);
        return Section(Strings.SectionSave, grid);
    }

    private Control BuildGeneralSection()
    {
        TableLayoutPanel grid = NewGrid();

        _startWithWindows.Text = Strings.StartWithWindows;
        AddWide(grid, _startWithWindows);
        _showMagnifier.Text = Strings.ShowMagnifier;
        AddWide(grid, _showMagnifier);

        _themeBox.Items.AddRange([Strings.ThemeDark, Strings.ThemeLight, Strings.FollowWindows]);
        AddRow(grid, Strings.LabelTheme, _themeBox);

        // Os nomes dos idiomas ficam na própria língua, iguais nos dois idiomas da interface.
        _language.Items.AddRange([Strings.FollowWindows, "Português", "English"]);
        AddRow(grid, Strings.LabelLanguage, _language);
        return Section(Strings.SectionGeneral, grid);
    }

    private Control BuildButtons()
    {
        var defaults = new Button { Text = Strings.ButtonDefaults, AutoSize = true };
        defaults.Click += (_, _) =>
        {
            ReadFields(new AppSettings(), startWithWindows: false);
            ShowHotkeyFailures([]);
        };

        var ok = _okButton = new Button { Text = Strings.ButtonOk, AutoSize = true, MinimumSize = new Size(80, 0), Tag = _theme.Accent };
        ok.Click += (_, _) => OnOk();
        var cancel = new Button { Text = Strings.ButtonCancel, AutoSize = true, MinimumSize = new Size(80, 0), DialogResult = DialogResult.Cancel };
        AcceptButton = ok;
        CancelButton = cancel;

        var right = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Anchor = AnchorStyles.Right, WrapContents = false };
        right.Controls.Add(ok);
        right.Controls.Add(cancel);

        var bar = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 0) };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.Controls.Add(defaults, 0, 0);
        bar.Controls.Add(right, 1, 0);
        return bar;
    }

    private void ReadFields(AppSettings s, bool startWithWindows)
    {
        foreach ((HotkeyAction action, string text) in SettingsValidator.HotkeyEntries(s.Hotkeys))
        {
            _hotkeyRows[action].Box.Binding = HotkeyBinding.TryParse(text, out HotkeyBinding b) ? b : null;
        }

        _folder.Text = s.SaveFolder;
        _format.SelectedIndex = (int)s.DefaultFormat;
        _quality.Value = Math.Clamp(s.JpegQuality, 1, 100);
        _prefix.Text = s.FileNamePrefix;
        _askWhere.Checked = s.AskWhereToSave;
        _startWithWindows.Checked = startWithWindows;
        _showMagnifier.Checked = s.ShowMagnifier;
        _themeBox.SelectedIndex = Array.IndexOf(ThemeOrder, s.Theme);
        _language.SelectedIndex = Array.IndexOf(LanguageOrder, s.Language);
        UpdateSaveFields();
    }

    /// <summary>Copia os campos para <paramref name="s"/>, sem mexer no que a tela não mostra (última área, cor…).</summary>
    private void WriteFields(AppSettings s)
    {
        string HotkeyText(HotkeyAction action) => _hotkeyRows[action].Box.Binding?.ToString() ?? string.Empty;

        s.Hotkeys.CaptureArea = HotkeyText(HotkeyAction.CaptureArea);
        s.Hotkeys.CaptureFullScreen = HotkeyText(HotkeyAction.CaptureFullScreen);
        s.Hotkeys.CaptureWindow = HotkeyText(HotkeyAction.CaptureWindow);
        s.Hotkeys.RepeatLastArea = HotkeyText(HotkeyAction.RepeatLastArea);
        s.SaveFolder = _folder.Text.Trim();
        s.DefaultFormat = SelectedFormat;
        s.JpegQuality = (int)_quality.Value;
        s.FileNamePrefix = _prefix.Text.Trim();
        s.AskWhereToSave = _askWhere.Checked;
        s.StartWithWindows = _startWithWindows.Checked;
        s.ShowMagnifier = _showMagnifier.Checked;
        s.Theme = ThemeOrder[Math.Max(0, _themeBox.SelectedIndex)];
        s.Language = LanguageOrder[Math.Max(0, _language.SelectedIndex)];
    }

    private ImageFormatKind SelectedFormat => (ImageFormatKind)Math.Max(0, _format.SelectedIndex);

    private void UpdateSaveFields()
    {
        _quality.Enabled = SelectedFormat == ImageFormatKind.Jpeg;
        _preview.Text = string.Format(Strings.FileNamePreview,
            FileNameBuilder.Build(_prefix.Text, DateTime.Now, SelectedFormat));
    }

    private void OnOk()
    {
        var draft = new AppSettings();
        WriteFields(draft);
        IReadOnlyList<string> errors = SettingsValidator.Validate(draft);
        if (errors.Count > 0)
        {
            MessageBox.Show(this, string.Format(Strings.SettingsErrors, string.Join("\n", errors)), Strings.AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _reopen |= draft.Language != _settings.Language || draft.Theme != _settings.Theme;
        WriteFields(_settings);
        _store.Save(_settings);
        ApplyStartup(_settings.StartWithWindows);

        // O TrayContext registra os atalhos de novo e chama ShowHotkeyFailures.
        ShowHotkeyFailures([]);
        Applied?.Invoke();

        // Com falha, a tela fica aberta para mostrar o problema; um segundo OK sem mudanças fecha mesmo assim.
        string failureKey = string.Join("|", _hotkeyRows
            .Where(r => r.Value.Error.Text.Length > 0)
            .Select(r => $"{r.Key}={r.Value.Box.Text}"));
        if (failureKey.Length > 0 && failureKey != _lastFailureKey)
        {
            _lastFailureKey = failureKey;
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void ApplyStartup(bool enabled)
    {
        try
        {
            if (StartupRegistration.IsEnabled() != enabled)
            {
                StartupRegistration.Set(enabled);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            MessageBox.Show(this, string.Format(Strings.StartupError, ex.Message), Strings.AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BrowseFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = Strings.FolderDialogDescription,
            UseDescriptionForTitle = true,
            SelectedPath = _folder.Text,
            ShowNewFolderButton = true,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _folder.Text = dialog.SelectedPath;
        }
    }

    private void OnHotkeyBoxFocus(int delta)
    {
        bool wasCapturing = _focusedHotkeyBoxes > 0;
        _focusedHotkeyBoxes = Math.Max(0, _focusedHotkeyBoxes + delta);
        bool isCapturing = _focusedHotkeyBoxes > 0;
        if (wasCapturing != isCapturing)
        {
            CapturingHotkeyChanged?.Invoke(isCapturing);
        }
    }

    // Foco inicial fora das caixas de atalho, para não suspender os atalhos ao abrir.
    // Só pode ser feito com a janela visível: no construtor o WinForms recusa (ArgumentException).
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _okButton?.Focus();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (_focusedHotkeyBoxes > 0)
        {
            _focusedHotkeyBoxes = 0;
            CapturingHotkeyChanged?.Invoke(false);
        }
        base.OnFormClosed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _icon.Dispose();
        }
        base.Dispose(disposing);
    }

    // ----- montagem e tema -----

    private static TableLayoutPanel NewGrid()
    {
        var grid = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Dock = DockStyle.Fill };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        return grid;
    }

    private static void AddRow(TableLayoutPanel grid, string label, Control field, Control? extra = null)
    {
        int row = grid.RowCount++;
        grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 3, 12, 3) }, 0, row);
        grid.Controls.Add(field, 1, row);
        if (extra is not null)
        {
            grid.Controls.Add(extra, 2, row);
        }
    }

    private static void AddWide(TableLayoutPanel grid, Control control)
    {
        int row = grid.RowCount++;
        grid.Controls.Add(control, 0, row);
        grid.SetColumnSpan(control, 3);
    }

    private Control Section(string title, Control content)
    {
        var panel = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 10) };
        var header = new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font(Font.FontFamily, Font.Size + 2, FontStyle.Bold),
            Tag = _theme.Accent,
            Margin = new Padding(0, 4, 0, 4),
        };
        panel.Controls.Add(header);
        panel.Controls.Add(content);
        return panel;
    }

    /// <summary>Pinta os controles com o tema. <c>Tag</c> com uma cor força a cor do texto (títulos, erros, OK).</summary>
    private void ApplyTheme(Control parent)
    {
        foreach (Control c in parent.Controls)
        {
            switch (c)
            {
                case Button b:
                    b.FlatStyle = FlatStyle.Flat;
                    b.FlatAppearance.BorderColor = _theme.Border;
                    if (b.Tag is Color accent)
                    {
                        b.BackColor = accent;
                        b.ForeColor = _theme.AccentForeground;
                    }
                    else
                    {
                        b.BackColor = _theme.Surface;
                        b.ForeColor = _theme.Foreground;
                    }
                    break;
                case TextBox or ComboBox or NumericUpDown:
                    c.BackColor = _theme.Surface;
                    c.ForeColor = _theme.Foreground;
                    if (c is ComboBox combo)
                    {
                        combo.FlatStyle = FlatStyle.Flat;
                    }
                    if (c is TextBox text)
                    {
                        text.BorderStyle = BorderStyle.FixedSingle;
                    }
                    break;
                default:
                    c.ForeColor = c.Tag is Color forced ? forced : _theme.Foreground;
                    break;
            }
            ApplyTheme(c);
        }
    }
}
