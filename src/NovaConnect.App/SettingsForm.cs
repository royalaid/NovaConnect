using System.Drawing;

namespace NovaConnect.App;

internal sealed class SettingsForm : Form
{
    private readonly ComboBox _headsetPlayback = CreateEndpointComboBox();
    private readonly ComboBox _fallbackPlayback = CreateEndpointComboBox();
    private readonly ComboBox _headsetCapture = CreateEndpointComboBox();
    private readonly ComboBox _fallbackCapture = CreateEndpointComboBox();
    private readonly CheckBox _enabled = CreateCheckBox("Enable automatic switching");
    private readonly CheckBox _switchMicrophone = CreateCheckBox("Switch microphone too");
    private readonly CheckBox _switchOnStartup = CreateCheckBox("Apply detected state on launch");
    private readonly CheckBox _startWithWindows = CreateCheckBox("Start with Windows");
    private readonly NumericUpDown _pollInterval = new()
    {
        Minimum = 250,
        Maximum = 10_000,
        Increment = 250,
        Width = 94,
        Font = UiTheme.BodyFont,
        BackColor = UiTheme.SurfaceElevated,
        ForeColor = UiTheme.Text,
        BorderStyle = BorderStyle.FixedSingle
    };
    private readonly Label _validationLabel = new()
    {
        AutoSize = true,
        BackColor = UiTheme.Surface,
        ForeColor = UiTheme.Danger,
        Font = UiTheme.SmallSemiboldFont,
        MaximumSize = new Size(470, 42),
        AccessibleName = "Validation errors",
        AccessibleRole = AccessibleRole.Alert,
        Margin = new Padding(0)
    };
    private readonly ToolTip _endpointToolTip = new()
    {
        AutoPopDelay = 10_000,
        InitialDelay = 350,
        ReshowDelay = 100,
        ShowAlways = true
    };
    private readonly Icon _windowIcon;
    private readonly Bitmap _headerBitmap;
    private readonly TableLayoutPanel _content;
    private readonly ThemedTableLayoutPanel _statusBanner;
    private readonly Label _statusLabel;

    internal AppConfiguration Configuration { get; private set; }
    internal bool StartWithWindows => _startWithWindows.Checked;

    internal SettingsForm(
        IReadOnlyList<AudioEndpoint> endpoints,
        AppConfiguration configuration,
        string statusText,
        bool startWithWindows)
    {
        Configuration = configuration;
        _windowIcon = BrandIcons.CreateWindowIcon();
        _headerBitmap = BrandIcons.CreateHeaderBitmap();

        Text = "NovaConnect Settings";
        Icon = _windowIcon;
        ClientSize = new Size(820, 470);
        MinimumSize = new Size(760, 460);
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;
        MaximizeBox = false;
        BackColor = UiTheme.Window;
        ForeColor = UiTheme.Text;
        Font = UiTheme.BodyFont;
        AutoScaleMode = AutoScaleMode.Dpi;
        DoubleBuffered = true;

        _statusBanner = new ThemedTableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            Padding = new Padding(10, 7, 10, 7),
            Margin = new Padding(0, 0, 0, 10)
        };
        _statusLabel = new Label
        {
            AutoSize = true,
            Font = UiTheme.SmallSemiboldFont,
            Margin = new Padding(0),
            AccessibleName = "Headset status"
        };
        _statusBanner.Controls.Add(_statusLabel);
        ApplyStatus(statusText);

        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Window,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        _content = CreateContent();
        shell.Controls.Add(_content, 0, 0);
        shell.Controls.Add(CreateFooter(), 0, 1);
        Controls.Add(shell);

        PopulateEndpoints(endpoints, configuration);
        AttachEndpointToolTip(_headsetPlayback);
        AttachEndpointToolTip(_fallbackPlayback);
        AttachEndpointToolTip(_headsetCapture);
        AttachEndpointToolTip(_fallbackCapture);
        _enabled.Checked = configuration.Enabled;
        _switchMicrophone.Checked = configuration.SwitchMicrophone;
        _switchOnStartup.Checked = configuration.SwitchOnStartup;
        _startWithWindows.Checked = startWithWindows;
        _pollInterval.Value = Math.Clamp(configuration.PollIntervalMs, 250, 10_000);
        _switchMicrophone.CheckedChanged += (_, _) => UpdateMicrophoneControls();
        UpdateMicrophoneControls();
        Shown += (_, _) =>
        {
            FitToWorkingArea();
            WindowTheme.TryEnableDarkControls(this);
            ActiveControl = AcceptButton as Control;
        };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WindowTheme.TryEnableDarkTitleBar(Handle);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _endpointToolTip.Dispose();
            _headerBitmap.Dispose();
            _windowIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    private TableLayoutPanel CreateContent()
    {
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Window,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(20, 16, 20, 14),
            Margin = new Padding(0)
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.Controls.Add(CreateHeader(), 0, 0);
        content.Controls.Add(_statusBanner, 0, 1);
        content.Controls.Add(CreateMainGrid(), 0, 2);
        return content;
    }

    internal void UpdateStatus(string statusText)
    {
        if (IsDisposed)
        {
            return;
        }

        ApplyStatus(statusText);
    }

    private void ApplyStatus(string statusText)
    {
        StatusPresentation status = StatusPresentation.FromText(statusText);
        string label = "●  " + status.Text;
        Color foreground = UiTheme.StatusForeground(status.Tone);
        Color background = UiTheme.StatusBackground(status.Tone);
        if (_statusLabel.Text == label
            && _statusBanner.BackColor == background
            && _statusBanner.BorderColor == foreground)
        {
            return;
        }

        _statusBanner.BackColor = background;
        _statusBanner.BorderColor = foreground;
        _statusLabel.Text = label;
        _statusLabel.BackColor = background;
        _statusLabel.ForeColor = foreground;
        _statusBanner.Invalidate();
    }

    private void FitToWorkingArea()
    {
        Rectangle workingArea = Screen.FromControl(this).WorkingArea;
        int margin = Math.Max(16, (int)Math.Round(16 * DeviceDpi / 96F));
        int maximumWidth = Math.Max(480, workingArea.Width - (margin * 2));
        int maximumHeight = Math.Max(360, workingArea.Height - (margin * 2));
        bool constrained = Width > maximumWidth || Height > maximumHeight;
        if (!constrained)
        {
            return;
        }

        MinimumSize = new Size(
            Math.Min(MinimumSize.Width, maximumWidth),
            Math.Min(MinimumSize.Height, maximumHeight));
        Size = new Size(Math.Min(Width, maximumWidth), Math.Min(Height, maximumHeight));
        _content.AutoScroll = true;
        _content.AutoScrollMinSize = _content.GetPreferredSize(
            new Size(Math.Max(1, _content.ClientSize.Width), 0));
    }

    private Control CreateHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = UiTheme.Window,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 10)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.Controls.Add(new PictureBox
        {
            Image = _headerBitmap,
            Size = new Size(40, 40),
            SizeMode = PictureBoxSizeMode.Zoom,
            Margin = new Padding(0, 0, 12, 0),
            AccessibleName = "NovaConnect icon"
        }, 0, 0);

        var text = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            BackColor = UiTheme.Window,
            ColumnCount = 1,
            Margin = new Padding(0)
        };
        text.Controls.Add(new Label
        {
            Text = "NovaConnect",
            AutoSize = true,
            BackColor = UiTheme.Window,
            ForeColor = UiTheme.Text,
            Font = UiTheme.TitleFont,
            Margin = new Padding(0, 0, 0, 1)
        });
        text.Controls.Add(new Label
        {
            Text = "Arctis Nova 5 audio routing",
            AutoSize = true,
            BackColor = UiTheme.Window,
            ForeColor = UiTheme.MutedText,
            Font = UiTheme.SmallFont,
            Margin = new Padding(0)
        });
        header.Controls.Add(text, 1, 0);
        return header;
    }

    private Control CreateMainGrid()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Window,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        Control audio = CreateAudioCard();
        audio.Margin = new Padding(0, 0, 5, 0);
        Control behavior = CreateBehaviorCard();
        behavior.Margin = new Padding(5, 0, 0, 0);
        grid.Controls.Add(audio, 0, 0);
        grid.Controls.Add(behavior, 1, 0);
        return grid;
    }

    private Control CreateAudioCard()
    {
        var card = CreateCard("Audio routing");
        card.Controls.Add(CreateSubheading("Playback"));
        var playback = CreateEndpointTable();
        AddEndpointRow(playback, 0, "Headset on", "Playback when headset is on", _headsetPlayback);
        AddEndpointRow(playback, 1, "Headset off", "Playback when headset is off", _fallbackPlayback);
        playback.Margin = new Padding(0, 0, 0, 9);
        card.Controls.Add(playback);

        _switchMicrophone.Margin = new Padding(0, 0, 0, 4);
        card.Controls.Add(_switchMicrophone);
        var microphone = CreateEndpointTable();
        AddEndpointRow(microphone, 0, "Headset on", "Microphone when headset is on", _headsetCapture);
        AddEndpointRow(microphone, 1, "Headset off", "Microphone when headset is off", _fallbackCapture);
        card.Controls.Add(microphone);
        return card;
    }

    private Control CreateBehaviorCard()
    {
        var card = CreateCard("Behavior");
        AddCardCheckBox(card, _enabled);
        AddCardCheckBox(card, _switchOnStartup);
        AddCardCheckBox(card, _startWithWindows);

        var interval = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = UiTheme.Surface,
            ColumnCount = 1,
            Margin = new Padding(0, 8, 0, 0)
        };
        interval.Controls.Add(CreateSubheading("Detection interval"));
        var value = new FlowLayoutPanel
        {
            AutoSize = true,
            BackColor = UiTheme.Surface,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0),
            WrapContents = false
        };
        _pollInterval.Margin = new Padding(0);
        _pollInterval.AccessibleName = "Detection interval in milliseconds";
        _pollInterval.AccessibleDescription = "How often NovaConnect checks the headset state, from 250 to 10000 milliseconds.";
        value.Controls.Add(_pollInterval);
        value.Controls.Add(new Label
        {
            Text = "ms",
            AutoSize = true,
            BackColor = UiTheme.Surface,
            ForeColor = UiTheme.MutedText,
            Margin = new Padding(6, 5, 0, 0)
        });
        interval.Controls.Add(value);
        card.Controls.Add(interval);
        return card;
    }

    private Control CreateFooter()
    {
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Surface,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(20, 0, 20, 10),
            Margin = new Padding(0)
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 11));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var line = new Panel
        {
            Dock = DockStyle.Top,
            Height = 1,
            BackColor = UiTheme.Border,
            Margin = new Padding(0)
        };
        footer.Controls.Add(line, 0, 0);
        footer.SetColumnSpan(line, 2);
        _validationLabel.Anchor = AnchorStyles.Left;
        footer.Controls.Add(_validationLabel, 0, 1);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            BackColor = UiTheme.Surface,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0),
            WrapContents = false
        };
        var saveButton = CreatePrimaryButton("Save");
        saveButton.Click += (_, _) => SaveAndClose();
        var cancelButton = CreateSecondaryButton("Cancel");
        cancelButton.DialogResult = DialogResult.Cancel;
        buttons.Controls.Add(saveButton);
        buttons.Controls.Add(cancelButton);
        footer.Controls.Add(buttons, 1, 1);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
        return footer;
    }

    private void PopulateEndpoints(
        IReadOnlyList<AudioEndpoint> endpoints,
        AppConfiguration configuration)
    {
        PopulateCombo(
            _headsetPlayback,
            endpoints,
            AudioFlow.Playback,
            configuration.HeadsetPlaybackId,
            configuration.HeadsetPlaybackName,
            headsetSlot: true);
        PopulateCombo(
            _fallbackPlayback,
            endpoints,
            AudioFlow.Playback,
            configuration.FallbackPlaybackId,
            configuration.FallbackPlaybackName,
            headsetSlot: false);
        PopulateCombo(
            _headsetCapture,
            endpoints,
            AudioFlow.Capture,
            configuration.HeadsetCaptureId,
            configuration.HeadsetCaptureName,
            headsetSlot: true);
        PopulateCombo(
            _fallbackCapture,
            endpoints,
            AudioFlow.Capture,
            configuration.FallbackCaptureId,
            configuration.FallbackCaptureName,
            headsetSlot: false);
    }

    private static void PopulateCombo(
        ComboBox combo,
        IEnumerable<AudioEndpoint> endpoints,
        AudioFlow flow,
        string? selectedId,
        string? selectedName,
        bool headsetSlot)
    {
        AudioEndpoint[] active = endpoints
            .Where(endpoint => endpoint.Flow == flow && endpoint.IsActive)
            .ToArray();

        combo.BeginUpdate();
        combo.Items.Clear();
        foreach (AudioEndpoint endpoint in active)
        {
            combo.Items.Add(endpoint);
        }

        combo.DisplayMember = nameof(AudioEndpoint.Name);
        AudioEndpoint? selected = AudioEndpointResolver.Match(
            active,
            flow,
            selectedId,
            selectedName,
            headsetSlot);
        combo.SelectedItem = selected
            ?? (headsetSlot || combo.Items.Count == 0 ? null : combo.Items[0]);
        combo.EndUpdate();
    }

    private void AttachEndpointToolTip(ComboBox combo)
    {
        void Update() => _endpointToolTip.SetToolTip(
            combo,
            (combo.SelectedItem as AudioEndpoint)?.Name ?? "No active endpoint");

        combo.SelectedIndexChanged += (_, _) => Update();
        Update();
    }

    private void UpdateMicrophoneControls()
    {
        _headsetCapture.Enabled = _switchMicrophone.Checked;
        _fallbackCapture.Enabled = _switchMicrophone.Checked;
    }

    private void SaveAndClose()
    {
        AudioEndpoint? headsetPlayback = _headsetPlayback.SelectedItem as AudioEndpoint;
        AudioEndpoint? fallbackPlayback = _fallbackPlayback.SelectedItem as AudioEndpoint;
        AudioEndpoint? headsetCapture = _headsetCapture.SelectedItem as AudioEndpoint;
        AudioEndpoint? fallbackCapture = _fallbackCapture.SelectedItem as AudioEndpoint;
        Control? firstInvalidControl = headsetPlayback is null
            ? _headsetPlayback
            : fallbackPlayback is null
                ? _fallbackPlayback
                : _switchMicrophone.Checked && headsetCapture is null
                    ? _headsetCapture
                    : _switchMicrophone.Checked && fallbackCapture is null
                        ? _fallbackCapture
                        : null;

        Configuration = new AppConfiguration
        {
            Enabled = _enabled.Checked,
            SwitchOnStartup = _switchOnStartup.Checked,
            SwitchMicrophone = _switchMicrophone.Checked,
            PollIntervalMs = (int)_pollInterval.Value,
            HeadsetPlaybackId = headsetPlayback?.Id ?? string.Empty,
            HeadsetPlaybackName = headsetPlayback?.Name ?? string.Empty,
            FallbackPlaybackId = fallbackPlayback?.Id ?? string.Empty,
            FallbackPlaybackName = fallbackPlayback?.Name ?? string.Empty,
            HeadsetCaptureId = headsetCapture?.Id,
            HeadsetCaptureName = headsetCapture?.Name,
            FallbackCaptureId = fallbackCapture?.Id,
            FallbackCaptureName = fallbackCapture?.Name
        };

        var errors = Configuration.Validate().ToList();
        if (headsetPlayback?.Id == fallbackPlayback?.Id && headsetPlayback is not null)
        {
            errors.Add("Headset and fallback playback endpoints must be different.");
            firstInvalidControl ??= _fallbackPlayback;
        }

        if (_switchMicrophone.Checked
            && headsetCapture?.Id == fallbackCapture?.Id
            && headsetCapture is not null)
        {
            errors.Add("Headset and fallback microphone endpoints must be different.");
            firstInvalidControl ??= _fallbackCapture;
        }

        if (errors.Count > 0)
        {
            _validationLabel.Text = errors.Count == 1
                ? errors[0]
                : $"{errors.Count} settings need attention.";
            _ = MessageBox.Show(
                this,
                "Review these settings:" + Environment.NewLine + Environment.NewLine
                + string.Join(Environment.NewLine, errors.Select(error => "• " + error)),
                "Check NovaConnect settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            firstInvalidControl?.Focus();
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private static ComboBox CreateEndpointComboBox()
    {
        var combo = new DarkComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 22,
            FlatStyle = FlatStyle.Flat,
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            DropDownHeight = 240,
            DropDownWidth = 540,
            BackColor = UiTheme.SurfaceElevated,
            ForeColor = UiTheme.Text,
            Font = UiTheme.BodyFont
        };
        combo.DrawItem += DrawComboItem;
        return combo;
    }

    private static void DrawComboItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox combo || e.Index < 0)
        {
            return;
        }

        bool selected = (e.State & DrawItemState.Selected) != 0;
        Color background = selected ? UiTheme.Accent : UiTheme.SurfaceElevated;
        Color foreground = combo.Enabled ? UiTheme.Text : UiTheme.MutedText;
        using var brush = new SolidBrush(background);
        e.Graphics.FillRectangle(brush, e.Bounds);
        var textBounds = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height);
        TextRenderer.DrawText(
            e.Graphics,
            combo.GetItemText(combo.Items[e.Index]),
            combo.Font,
            textBounds,
            foreground,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        e.DrawFocusRectangle();
    }

    private static CheckBox CreateCheckBox(string text) => new()
    {
        Text = text,
        AutoSize = true,
        BackColor = UiTheme.Surface,
        ForeColor = UiTheme.Text,
        Font = UiTheme.BodyFont
    };

    private static ThemedTableLayoutPanel CreateCard(string title)
    {
        var card = new ThemedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Surface,
            BorderColor = UiTheme.Border,
            ColumnCount = 1,
            Padding = new Padding(14, 12, 14, 12)
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        card.Controls.Add(new Label
        {
            Text = title,
            AutoSize = true,
            BackColor = UiTheme.Surface,
            ForeColor = UiTheme.Text,
            Font = UiTheme.SectionFont,
            Margin = new Padding(0, 0, 0, 9)
        });
        return card;
    }

    private static Label CreateSubheading(string text) => new()
    {
        Text = text,
        AutoSize = true,
        BackColor = UiTheme.Surface,
        ForeColor = UiTheme.MutedText,
        Font = UiTheme.CaptionFont,
        Margin = new Padding(0, 0, 0, 3)
    };

    private static TableLayoutPanel CreateEndpointTable()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = UiTheme.Surface,
            ColumnCount = 2,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return table;
    }

    private static void AddCardCheckBox(TableLayoutPanel card, CheckBox checkBox)
    {
        checkBox.Margin = new Padding(0, 1, 0, 8);
        card.Controls.Add(checkBox);
    }

    private static void AddEndpointRow(
        TableLayoutPanel table,
        int row,
        string label,
        string accessibleName,
        ComboBox combo)
    {
        table.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            BackColor = UiTheme.Surface,
            ForeColor = UiTheme.Text,
            Font = UiTheme.SmallFont,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 8, 6)
        }, 0, row);
        combo.AccessibleName = accessibleName;
        combo.AccessibleDescription = "Select an active Windows audio endpoint.";
        combo.Margin = new Padding(0, 2, 0, 4);
        table.Controls.Add(combo, 1, row);
    }

    private static Button CreatePrimaryButton(string text)
    {
        var button = CreateButton(text);
        button.BackColor = UiTheme.Accent;
        button.ForeColor = Color.White;
        button.FlatAppearance.BorderColor = UiTheme.Accent;
        button.FlatAppearance.BorderSize = 1;
        button.Margin = new Padding(8, 0, 0, 0);
        button.MouseEnter += (_, _) => button.BackColor = UiTheme.AccentHover;
        button.MouseLeave += (_, _) => button.BackColor = UiTheme.Accent;
        button.Enter += (_, _) => button.FlatAppearance.BorderColor = UiTheme.Focus;
        button.Leave += (_, _) => button.FlatAppearance.BorderColor = UiTheme.Accent;
        return button;
    }

    private static Button CreateSecondaryButton(string text)
    {
        var button = CreateButton(text);
        button.BackColor = UiTheme.SurfaceElevated;
        button.ForeColor = UiTheme.Text;
        button.FlatAppearance.BorderColor = UiTheme.ControlBorder;
        button.FlatAppearance.BorderSize = 1;
        button.Margin = new Padding(8, 0, 0, 0);
        button.Enter += (_, _) => button.FlatAppearance.BorderColor = UiTheme.Focus;
        button.Leave += (_, _) => button.FlatAppearance.BorderColor = UiTheme.ControlBorder;
        return button;
    }

    private static Button CreateButton(string text) => new()
    {
        Text = text,
        Width = 92,
        Height = 34,
        AutoSize = false,
        FlatStyle = FlatStyle.Flat,
        Font = UiTheme.ButtonFont,
        UseVisualStyleBackColor = false
    };
}
