using System.Drawing;

namespace NovaConnect.App;

internal sealed class SettingsForm : Form
{
    private readonly ComboBox _headsetPlayback = CreateEndpointComboBox();
    private readonly ComboBox _fallbackPlayback = CreateEndpointComboBox();
    private readonly ComboBox _headsetCapture = CreateEndpointComboBox();
    private readonly ComboBox _fallbackCapture = CreateEndpointComboBox();
    private readonly CheckBox _enabled = new() { Text = "Enable automatic switching", AutoSize = true };
    private readonly CheckBox _switchMicrophone = new() { Text = "Switch microphone too", AutoSize = true };
    private readonly CheckBox _switchOnStartup = new() { Text = "Apply the detected state when NovaConnect starts", AutoSize = true };
    private readonly CheckBox _startWithWindows = new() { Text = "Start NovaConnect with Windows", AutoSize = true };
    private readonly NumericUpDown _pollInterval = new()
    {
        Minimum = 250,
        Maximum = 10_000,
        Increment = 250,
        Width = 110
    };
    private readonly Label _validationLabel = new()
    {
        AutoSize = true,
        ForeColor = Color.Firebrick,
        MaximumSize = new Size(640, 0)
    };

    internal AppConfiguration Configuration { get; private set; }
    internal bool StartWithWindows => _startWithWindows.Checked;

    internal SettingsForm(
        IReadOnlyList<AudioEndpoint> endpoints,
        AppConfiguration configuration,
        string statusText,
        bool startWithWindows)
    {
        Configuration = configuration;
        Text = "NovaConnect Settings";
        Width = 720;
        Height = 590;
        MinimumSize = new Size(680, 540);
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;
        Font = new Font("Segoe UI", 9F);
        AutoScaleMode = AutoScaleMode.Dpi;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(18)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        root.Controls.Add(CreateHeader(statusText));
        root.Controls.Add(CreatePlaybackGroup());
        root.Controls.Add(CreateMicrophoneGroup());
        root.Controls.Add(CreateOptionsGroup());
        root.Controls.Add(_validationLabel);
        root.Controls.Add(CreateButtons());
        Controls.Add(root);

        PopulateEndpoints(endpoints, configuration);
        _enabled.Checked = configuration.Enabled;
        _switchMicrophone.Checked = configuration.SwitchMicrophone;
        _switchOnStartup.Checked = configuration.SwitchOnStartup;
        _startWithWindows.Checked = startWithWindows;
        _pollInterval.Value = Math.Clamp(configuration.PollIntervalMs, 250, 10_000);
        _switchMicrophone.CheckedChanged += (_, _) => UpdateMicrophoneControls();
        UpdateMicrophoneControls();
    }

    private static Control CreateHeader(string statusText)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            Padding = new Padding(0, 0, 0, 10)
        };
        panel.Controls.Add(new Label
        {
            Text = "NovaConnect",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold)
        });
        panel.Controls.Add(new Label
        {
            Text = "Arctis Nova 5 power detection and audio fallback without SteelSeries GG",
            AutoSize = true,
            ForeColor = SystemColors.GrayText
        });
        panel.Controls.Add(new Label
        {
            Text = statusText,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0),
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold)
        });
        return panel;
    }

    private Control CreatePlaybackGroup()
    {
        var group = new GroupBox
        {
            Text = "Playback",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(12)
        };
        var table = CreateTwoColumnTable();
        AddEndpointRow(table, 0, "When headset is on", _headsetPlayback);
        AddEndpointRow(table, 1, "When headset is off", _fallbackPlayback);
        group.Controls.Add(table);
        return group;
    }

    private Control CreateMicrophoneGroup()
    {
        var group = new GroupBox
        {
            Text = "Microphone",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(12)
        };
        var table = CreateTwoColumnTable();
        table.Controls.Add(_switchMicrophone, 0, 0);
        table.SetColumnSpan(_switchMicrophone, 2);
        AddEndpointRow(table, 1, "When headset is on", _headsetCapture);
        AddEndpointRow(table, 2, "When headset is off", _fallbackCapture);
        group.Controls.Add(table);
        return group;
    }

    private Control CreateOptionsGroup()
    {
        var group = new GroupBox
        {
            Text = "Options",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(12)
        };
        var table = CreateTwoColumnTable();
        table.Controls.Add(_enabled, 0, 0);
        table.SetColumnSpan(_enabled, 2);
        table.Controls.Add(_switchOnStartup, 0, 1);
        table.SetColumnSpan(_switchOnStartup, 2);
        table.Controls.Add(_startWithWindows, 0, 2);
        table.SetColumnSpan(_startWithWindows, 2);
        table.Controls.Add(new Label
        {
            Text = "Detection interval (ms)",
            AutoSize = true,
            Anchor = AnchorStyles.Left
        }, 0, 3);
        table.Controls.Add(_pollInterval, 1, 3);
        group.Controls.Add(table);
        return group;
    }

    private Control CreateButtons()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 10, 0, 0)
        };
        var saveButton = new Button
        {
            Text = "Save",
            AutoSize = true,
            Padding = new Padding(14, 3, 14, 3)
        };
        saveButton.Click += (_, _) => SaveAndClose();
        var cancelButton = new Button
        {
            Text = "Cancel",
            AutoSize = true,
            Padding = new Padding(14, 3, 14, 3),
            DialogResult = DialogResult.Cancel
        };
        panel.Controls.Add(saveButton);
        panel.Controls.Add(cancelButton);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
        return panel;
    }

    private void PopulateEndpoints(
        IReadOnlyList<AudioEndpoint> endpoints,
        AppConfiguration configuration)
    {
        PopulateCombo(_headsetPlayback, endpoints, AudioFlow.Playback, configuration.HeadsetPlaybackId);
        PopulateCombo(_fallbackPlayback, endpoints, AudioFlow.Playback, configuration.FallbackPlaybackId);
        PopulateCombo(_headsetCapture, endpoints, AudioFlow.Capture, configuration.HeadsetCaptureId);
        PopulateCombo(_fallbackCapture, endpoints, AudioFlow.Capture, configuration.FallbackCaptureId);
    }

    private static void PopulateCombo(
        ComboBox combo,
        IEnumerable<AudioEndpoint> endpoints,
        AudioFlow flow,
        string? selectedId)
    {
        combo.BeginUpdate();
        combo.Items.Clear();
        foreach (AudioEndpoint endpoint in endpoints.Where(endpoint => endpoint.Flow == flow && endpoint.IsActive))
        {
            combo.Items.Add(endpoint);
        }

        combo.DisplayMember = nameof(AudioEndpoint.Name);
        AudioEndpoint? selected = combo.Items
            .Cast<AudioEndpoint>()
            .FirstOrDefault(endpoint => string.Equals(endpoint.Id, selectedId, StringComparison.OrdinalIgnoreCase));
        combo.SelectedItem = selected ?? (combo.Items.Count > 0 ? combo.Items[0] : null);
        combo.EndUpdate();
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
        }

        if (_switchMicrophone.Checked
            && headsetCapture?.Id == fallbackCapture?.Id
            && headsetCapture is not null)
        {
            errors.Add("Headset and fallback microphone endpoints must be different.");
        }

        if (errors.Count > 0)
        {
            _validationLabel.Text = string.Join(Environment.NewLine, errors.Select(error => "• " + error));
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private static ComboBox CreateEndpointComboBox() => new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Dock = DockStyle.Fill,
        IntegralHeight = false,
        DropDownHeight = 240
    };

    private static TableLayoutPanel CreateTwoColumnTable()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(0)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return table;
    }

    private static void AddEndpointRow(
        TableLayoutPanel table,
        int row,
        string label,
        ComboBox combo)
    {
        table.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 8, 8, 8)
        }, 0, row);
        combo.Margin = new Padding(3, 4, 3, 4);
        table.Controls.Add(combo, 1, row);
    }
}
