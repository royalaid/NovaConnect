using System.Diagnostics;
using System.Drawing;
using NovaConnect.Core;

namespace NovaConnect.App;

internal sealed class NovaConnectApplicationContext : ApplicationContext
{
    private readonly string _configurationPath = AppConfiguration.DefaultPath;
    private readonly string _logPath = Path.Combine(AppConfiguration.DefaultDirectory, "NovaConnect.log");
    private readonly FileLogger _logger;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _enabledItem;
    private readonly ToolStripMenuItem _startupItem;
    private readonly System.Windows.Forms.Timer _uiTimer;
    private readonly StartupToggleCoordinator _startupToggleCoordinator = new();
    private readonly MonitorTaskCoordinator _monitorTaskCoordinator = new();
    private AppConfiguration _configuration;
    private string _pendingStatus = "Starting…";
    private string? _lastError;
    private bool _settingsOpen;
    private bool _exitRequested;

    internal NovaConnectApplicationContext()
    {
        _logger = new FileLogger(_logPath);
        _configuration = LoadOrCreateConfiguration(out bool needsReview);

        _statusItem = new ToolStripMenuItem(_pendingStatus) { Enabled = false };
        _enabledItem = new ToolStripMenuItem("Automatic switching")
        {
            Checked = _configuration.Enabled,
            CheckOnClick = true
        };
        _enabledItem.CheckedChanged += async (_, _) => await ToggleEnabledAsync();
        _startupItem = new ToolStripMenuItem("Start with Windows")
        {
            Checked = StartupRegistration.IsEnabled(),
            CheckOnClick = true
        };
        _startupItem.CheckedChanged += (_, _) => ToggleStartup();

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_enabledItem);
        menu.Items.Add("Switch to headset now", null, (_, _) => SwitchNow(toHeadset: true));
        menu.Items.Add("Switch to fallback now", null, (_, _) => SwitchNow(toHeadset: false));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startupItem);
        menu.Items.Add("Settings…", null, (_, _) => ShowSettings());
        menu.Items.Add("Open log folder", null, (_, _) => OpenDataFolder());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());

        _notifyIcon = new NotifyIcon
        {
            Text = "NovaConnect — starting",
            Icon = SystemIcons.Information,
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => ShowSettings();

        _uiTimer = new System.Windows.Forms.Timer { Interval = 250 };
        _uiTimer.Tick += (_, _) => ApplyPendingStatus();
        _uiTimer.Start();

        int startupValidationErrors = _configuration.Validate().Count;
        needsReview = needsReview || startupValidationErrors > 0;
        if (StartupMonitorPolicy.ShouldStart(needsReview, startupValidationErrors))
        {
            _ = StartMonitorAsync();
        }
        else
        {
            SetPendingStatus(needsReview ? "Review settings before switching" : "Configuration required");
        }

        _logger.Info("NovaConnect started.");

        if (needsReview)
        {
            var reviewTimer = new System.Windows.Forms.Timer { Interval = 250 };
            reviewTimer.Tick += (_, _) =>
            {
                reviewTimer.Stop();
                reviewTimer.Dispose();
                ShowSettings();
            };
            reviewTimer.Start();
        }
    }

    private AppConfiguration LoadOrCreateConfiguration(out bool needsReview)
    {
        needsReview = true;
        if (File.Exists(_configurationPath))
        {
            if (ConfigurationStore.TryLoad(_configurationPath, out AppConfiguration? savedConfiguration)
                && savedConfiguration is not null)
            {
                needsReview = false;
                return savedConfiguration;
            }

            _logger.Error("Configuration could not be loaded; review is required before switching.");
        }

        try
        {
            IReadOnlyList<AudioEndpoint> endpoints = CoreAudioService.EnumerateEndpoints();
            return AppConfigurationFactory.Create(
                endpoints,
                CoreAudioService.GetDefaultEndpointId(AudioFlow.Playback),
                CoreAudioService.GetDefaultEndpointId(AudioFlow.Capture));
        }
        catch (Exception exception)
        {
            _logger.Error("Automatic configuration failed: " + exception.Message);
            return new AppConfiguration();
        }
    }

    private async Task StartMonitorAsync()
    {
        IReadOnlyList<string> errors = _configuration.Validate();
        if (errors.Count > 0)
        {
            await _monitorTaskCoordinator.StopAsync();
            SetPendingStatus("Configuration required");
            _logger.Error("Monitor not started: " + string.Join("; ", errors));
            return;
        }

        AppConfiguration monitorConfiguration = _configuration;
        try
        {
            await _monitorTaskCoordinator.ReplaceAsync(token =>
                Task.Run(() => MonitorLoopAsync(monitorConfiguration, token), token));
        }
        catch (Exception exception)
        {
            SetPendingStatus("Monitor could not be restarted");
            _logger.Error("Monitor restart failed: " + exception.Message);
        }
    }

    private async Task MonitorLoopAsync(
        AppConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var engine = new MonitorEngine(
            new Nova5HidStatusSource(),
            new CoreAudioEndpointController(),
            configuration);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                MonitorIterationResult result = await engine
                    .RunIterationAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (!AudioEndpointResolver.SameEndpointIdentity(configuration, result.Configuration))
                {
                    configuration = result.Configuration;
                    _configuration = result.Configuration;
                    ConfigurationStore.Save(_configurationPath, result.Configuration);
                    _logger.Info(
                        "Rebound audio endpoints after Windows reassigned device IDs: "
                        + $"playback={result.Configuration.HeadsetPlaybackId}; "
                        + $"capture={result.Configuration.HeadsetCaptureId}.");
                }

                _lastError = null;
                string switching = configuration.Enabled ? string.Empty : " — switching disabled";
                SetPendingStatus(
                    result.Status.IsConnected
                        ? $"Headset connected — {result.Status.BatteryPercent}%{switching}"
                        : $"Headset disconnected{switching}");

                if (result.Transition != ConnectionTransition.None)
                {
                    _logger.Info(
                        $"Headset transition: {result.Transition}; battery={result.Status.BatteryPercent}%; "
                        + $"playbackTarget={result.Plan.PlaybackTargetId ?? "unchanged"}; "
                        + $"captureTarget={result.Plan.CaptureTargetId ?? "unchanged"}.");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                SetPendingStatus("Headset status unavailable");
                string error = exception.GetType().Name + ": " + exception.Message;
                if (!string.Equals(error, _lastError, StringComparison.Ordinal))
                {
                    _lastError = error;
                    _logger.Error("Status query failed: " + error);
                }
            }

            try
            {
                await Task.Delay(configuration.PollIntervalMs, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async void ShowSettings()
    {
        if (_settingsOpen)
        {
            return;
        }

        _settingsOpen = true;
        try
        {
            IReadOnlyList<AudioEndpoint> endpoints = CoreAudioService.EnumerateEndpoints();
            AppConfiguration shownConfiguration = _configuration;
            if (!File.Exists(_configurationPath))
            {
                shownConfiguration = AppConfigurationFactory.Create(
                    endpoints,
                    CoreAudioService.GetDefaultEndpointId(AudioFlow.Playback),
                    CoreAudioService.GetDefaultEndpointId(AudioFlow.Capture));
            }

            using var form = new SettingsForm(
                endpoints,
                shownConfiguration,
                _pendingStatus,
                StartupRegistration.IsEnabled());
            if (form.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            _configuration = form.Configuration;
            ConfigurationStore.Save(_configurationPath, _configuration);
            StartupRegistration.SetEnabled(form.StartWithWindows);
            _enabledItem.Checked = _configuration.Enabled;
            _startupItem.Checked = form.StartWithWindows;
            _logger.Info("Configuration saved.");
            await StartMonitorAsync();
        }
        catch (Exception exception)
        {
            _logger.Error("Settings failed: " + exception.Message);
            ShowBalloon("NovaConnect settings error", exception.Message, ToolTipIcon.Error);
        }
        finally
        {
            _settingsOpen = false;
        }
    }

    private async Task ToggleEnabledAsync()
    {
        AppConfiguration previous = _configuration;
        bool desired = _enabledItem.Checked;
        EnabledToggleResult result = await EnabledToggleCoordinator.ApplyAsync(
            previous.Enabled,
            desired,
            async enabled =>
            {
                AppConfiguration updated = previous with { Enabled = enabled };
                ConfigurationStore.Save(_configurationPath, updated);
                _configuration = updated;
                _logger.Info("Automatic switching " + (enabled ? "enabled." : "disabled."));
                await StartMonitorAsync();
            },
            restored =>
            {
                _configuration = previous;
                _enabledItem.Checked = restored;
                try
                {
                    ConfigurationStore.Save(_configurationPath, previous);
                }
                catch
                {
                    // The original error is reported below; keep runtime and UI state consistent.
                }
            });

        if (result.Succeeded)
        {
            return;
        }

        Exception exception = result.Error
            ?? new InvalidOperationException("Automatic switching update failed.");
        try
        {
            _logger.Error("Automatic switching update failed: " + exception.Message);
        }
        catch
        {
            // A configuration failure may also make the adjacent log unavailable.
        }

        try
        {
            ShowBalloon("NovaConnect settings error", exception.Message, ToolTipIcon.Error);
        }
        catch
        {
            // Keep async UI event failures contained during shutdown or tray disposal.
        }
    }

    private void ToggleStartup()
    {
        StartupToggleResult result = _startupToggleCoordinator.Apply(
            _startupItem.Checked,
            StartupRegistration.SetEnabled,
            restored => _startupItem.Checked = restored);

        if (result.Status == StartupToggleStatus.Suppressed)
        {
            return;
        }

        if (result.Status == StartupToggleStatus.Succeeded)
        {
            _logger.Info("Start with Windows " + (_startupItem.Checked ? "enabled." : "disabled."));
            return;
        }

        Exception exception = result.Error
            ?? new InvalidOperationException("Startup registration failed.");
        _logger.Error("Startup registration failed: " + exception.Message);
        ShowBalloon("NovaConnect startup error", exception.Message, ToolTipIcon.Error);
    }

    private void SwitchNow(bool toHeadset)
    {
        try
        {
            string playbackId = toHeadset
                ? _configuration.HeadsetPlaybackId
                : _configuration.FallbackPlaybackId;
            CoreAudioService.SetDefaultEndpoint(AudioFlow.Playback, playbackId);

            string? captureId = toHeadset
                ? _configuration.HeadsetCaptureId
                : _configuration.FallbackCaptureId;
            if (_configuration.SwitchMicrophone && !string.IsNullOrWhiteSpace(captureId))
            {
                CoreAudioService.SetDefaultEndpoint(AudioFlow.Capture, captureId);
            }

            string target = toHeadset ? "headset" : "fallback";
            _logger.Info("Manual switch to " + target + ".");
            ShowBalloon("NovaConnect", "Switched to " + target + ".", ToolTipIcon.Info);
        }
        catch (Exception exception)
        {
            _logger.Error("Manual switch failed: " + exception.Message);
            ShowBalloon("NovaConnect switch failed", exception.Message, ToolTipIcon.Error);
        }
    }

    private void OpenDataFolder()
    {
        Directory.CreateDirectory(AppConfiguration.DefaultDirectory);
        Process.Start(new ProcessStartInfo(AppConfiguration.DefaultDirectory)
        {
            UseShellExecute = true
        });
    }

    private void SetPendingStatus(string status) =>
        Interlocked.Exchange(ref _pendingStatus, status);

    private void ApplyPendingStatus()
    {
        string status = Volatile.Read(ref _pendingStatus);
        _statusItem.Text = status;
        _notifyIcon.Text = status.Length <= 63 ? status : status[..63];
    }

    private void ShowBalloon(string title, string text, ToolTipIcon icon)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = text;
        _notifyIcon.BalloonTipIcon = icon;
        _notifyIcon.ShowBalloonTip(3_000);
    }

    protected override void ExitThreadCore()
    {
        if (_exitRequested)
        {
            return;
        }

        _exitRequested = true;
        _ = CompleteExitAsync();
    }

    private async Task CompleteExitAsync()
    {
        try
        {
            await _monitorTaskCoordinator.StopAsync();
        }
        catch (Exception exception)
        {
            _logger.Error("Monitor shutdown failed: " + exception.Message);
        }
        finally
        {
            ShutdownFinalizer.Run(
                () =>
                {
                    _uiTimer.Stop();
                    _uiTimer.Dispose();
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                    _logger.Info("NovaConnect stopped.");
                },
                base.ExitThreadCore);
        }
    }
}
