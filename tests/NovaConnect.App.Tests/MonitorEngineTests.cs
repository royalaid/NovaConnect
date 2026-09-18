using NovaConnect.App;
using NovaConnect.Core;
using Xunit;

namespace NovaConnect.App.Tests;

public sealed class MonitorEngineTests
{
    [Fact]
    public async Task RunIterationAsync_FirstConnectedSample_SwitchesToHeadset()
    {
        var statusSource = new FakeStatusSource(new Nova5Status(true, 80, Nova5ChargingState.OnBattery));
        var audio = new FakeAudioController("speakers", "desk-mic");
        var configuration = new AppConfiguration
        {
            HeadsetPlaybackId = "headset-out",
            FallbackPlaybackId = "speakers",
            HeadsetCaptureId = "headset-mic",
            FallbackCaptureId = "desk-mic",
            SwitchMicrophone = true,
            SwitchOnStartup = true
        };
        var engine = new MonitorEngine(statusSource, audio, configuration);

        MonitorIterationResult result = await engine.RunIterationAsync(CancellationToken.None);

        Assert.Equal(ConnectionTransition.Connected, result.Transition);
        Assert.Equal((AudioFlow.Playback, "headset-out"), audio.Applied[0]);
        Assert.Equal((AudioFlow.Capture, "headset-mic"), audio.Applied[1]);
    }

    [Fact]
    public async Task RunIterationAsync_FailedSwitch_RetriesSameHeadsetState()
    {
        var statusSource = new FakeStatusSource(new Nova5Status(true, 80, Nova5ChargingState.OnBattery));
        var audio = new FlakyAudioController();
        var configuration = new AppConfiguration
        {
            HeadsetPlaybackId = "headset-out",
            FallbackPlaybackId = "speakers",
            SwitchMicrophone = false,
            SwitchOnStartup = true
        };
        var engine = new MonitorEngine(statusSource, audio, configuration);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.RunIterationAsync(CancellationToken.None));
        MonitorIterationResult retry = await engine.RunIterationAsync(CancellationToken.None);

        Assert.Equal(ConnectionTransition.Connected, retry.Transition);
        Assert.Equal([(AudioFlow.Playback, "headset-out")], audio.Applied);
    }

    [Fact]
    public async Task RunIterationAsync_SingleDisconnectedSample_DoesNotSwitchFallback()
    {
        var statusSource = new SequenceStatusSource(
            new Nova5Status(true, 80, Nova5ChargingState.OnBattery),
            new Nova5Status(false, 80, Nova5ChargingState.OnBattery),
            new Nova5Status(false, 80, Nova5ChargingState.OnBattery));
        var audio = new FakeAudioController("headset-out", null);
        var configuration = new AppConfiguration
        {
            HeadsetPlaybackId = "headset-out",
            FallbackPlaybackId = "speakers",
            SwitchMicrophone = false,
            SwitchOnStartup = false
        };
        var engine = new MonitorEngine(statusSource, audio, configuration);

        await engine.RunIterationAsync(CancellationToken.None);
        MonitorIterationResult firstDisconnect = await engine.RunIterationAsync(CancellationToken.None);
        MonitorIterationResult confirmedDisconnect = await engine.RunIterationAsync(CancellationToken.None);

        Assert.Equal(ConnectionTransition.None, firstDisconnect.Transition);
        Assert.Equal(ConnectionTransition.Disconnected, confirmedDisconnect.Transition);
        Assert.Equal([(AudioFlow.Playback, "speakers")], audio.Applied);
    }

    [Fact]
    public async Task RunIterationAsync_QueryFailure_BreaksDisconnectedSampleSequence()
    {
        var statusSource = new ScriptedStatusSource(
            new Nova5Status(true, 80, Nova5ChargingState.OnBattery),
            new Nova5Status(false, 80, Nova5ChargingState.OnBattery),
            new TimeoutException("simulated HID timeout"),
            new Nova5Status(false, 80, Nova5ChargingState.OnBattery),
            new Nova5Status(false, 80, Nova5ChargingState.OnBattery));
        var audio = new FakeAudioController("headset-out", null);
        var configuration = new AppConfiguration
        {
            HeadsetPlaybackId = "headset-out",
            FallbackPlaybackId = "speakers",
            SwitchMicrophone = false,
            SwitchOnStartup = false
        };
        var engine = new MonitorEngine(statusSource, audio, configuration);

        await engine.RunIterationAsync(CancellationToken.None);
        await engine.RunIterationAsync(CancellationToken.None);
        await Assert.ThrowsAsync<TimeoutException>(
            () => engine.RunIterationAsync(CancellationToken.None));
        MonitorIterationResult firstDisconnectAfterFailure =
            await engine.RunIterationAsync(CancellationToken.None);
        MonitorIterationResult confirmedDisconnect =
            await engine.RunIterationAsync(CancellationToken.None);

        Assert.Equal(ConnectionTransition.None, firstDisconnectAfterFailure.Transition);
        Assert.Equal(ConnectionTransition.Disconnected, confirmedDisconnect.Transition);
        Assert.Equal([(AudioFlow.Playback, "speakers")], audio.Applied);
    }

    [Fact]
    public async Task RunIterationAsync_Disconnect_PreservesManualRoleDefaults()
    {
        var statusSource = new SequenceStatusSource(
            new Nova5Status(true, 80, Nova5ChargingState.OnBattery),
            new Nova5Status(false, 80, Nova5ChargingState.OnBattery),
            new Nova5Status(false, 80, Nova5ChargingState.OnBattery));
        var audio = new RoleAwareAudioController(
            new DefaultEndpointSnapshot("headset-out", "manual-speakers", "headset-out"),
            new DefaultEndpointSnapshot("headset-mic", "headset-mic", "conference-mic"));
        var configuration = new AppConfiguration
        {
            HeadsetPlaybackId = "headset-out",
            FallbackPlaybackId = "speakers",
            HeadsetCaptureId = "headset-mic",
            FallbackCaptureId = "desk-mic",
            SwitchMicrophone = true,
            SwitchOnStartup = false
        };
        var engine = new MonitorEngine(statusSource, audio, configuration);

        await engine.RunIterationAsync(CancellationToken.None);
        await engine.RunIterationAsync(CancellationToken.None);
        await engine.RunIterationAsync(CancellationToken.None);

        Assert.Equal(
            [(AudioFlow.Playback, "speakers", DefaultEndpointRole.Console),
             (AudioFlow.Playback, "speakers", DefaultEndpointRole.Communications),
             (AudioFlow.Capture, "desk-mic", DefaultEndpointRole.Console),
             (AudioFlow.Capture, "desk-mic", DefaultEndpointRole.Multimedia)],
            audio.AppliedRoles);
    }

    [Fact]
    public async Task RunIterationAsync_StaleHeadsetEndpointIds_RebindByNameThenSwitch()
    {
        var statusSource = new FakeStatusSource(new Nova5Status(true, 32, Nova5ChargingState.OnBattery));
        var audio = new RemappingAudioController();
        var configuration = new AppConfiguration
        {
            HeadsetPlaybackId = "stale-headset-out",
            HeadsetPlaybackName = "Headphones (3- SteelSeries Arctis Nova 5)",
            FallbackPlaybackId = "speakers",
            FallbackPlaybackName = "Speakers (Realtek(R) Audio)",
            HeadsetCaptureId = "stale-headset-mic",
            HeadsetCaptureName = "Headset Microphone (3- SteelSeries Arctis Nova 5)",
            FallbackCaptureId = "desk-mic",
            FallbackCaptureName = "Microphone (Steam Streaming Microphone)",
            SwitchMicrophone = true,
            SwitchOnStartup = true
        };
        var engine = new MonitorEngine(statusSource, audio, configuration);

        MonitorIterationResult result = await engine.RunIterationAsync(CancellationToken.None);

        Assert.Equal(ConnectionTransition.Connected, result.Transition);
        Assert.Equal("live-headset-out", result.Configuration.HeadsetPlaybackId);
        Assert.Equal("live-headset-mic", result.Configuration.HeadsetCaptureId);
        Assert.Equal((AudioFlow.Playback, "live-headset-out"), audio.Applied[0]);
        Assert.Equal((AudioFlow.Capture, "live-headset-mic"), audio.Applied[1]);
    }

    private sealed class FakeStatusSource(Nova5Status status) : IHeadsetStatusSource
    {
        public Task<Nova5Status> QueryOnceAsync(CancellationToken cancellationToken) =>
            Task.FromResult(status);
    }

    private sealed class SequenceStatusSource(params Nova5Status[] statuses) : IHeadsetStatusSource
    {
        private readonly Queue<Nova5Status> _statuses = new(statuses);

        public Task<Nova5Status> QueryOnceAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_statuses.Dequeue());
    }

    private sealed class ScriptedStatusSource(params object[] outcomes) : IHeadsetStatusSource
    {
        private readonly Queue<object> _outcomes = new(outcomes);

        public Task<Nova5Status> QueryOnceAsync(CancellationToken cancellationToken)
        {
            object outcome = _outcomes.Dequeue();
            return outcome is Exception exception
                ? Task.FromException<Nova5Status>(exception)
                : Task.FromResult((Nova5Status)outcome);
        }
    }

    private sealed class FakeAudioController(
        string? playbackId,
        string? captureId) : IAudioEndpointController
    {
        public List<(AudioFlow Flow, string Id)> Applied { get; } = [];

        public DefaultEndpointSnapshot GetDefaultEndpointIds(AudioFlow flow)
        {
            string? id = flow == AudioFlow.Playback ? playbackId : captureId;
            return new DefaultEndpointSnapshot(id, id, id);
        }

        public IReadOnlyList<AudioEndpoint> EnumerateEndpoints() => [];

        public void SetDefaultEndpoints(
            AudioFlow flow,
            string endpointId,
            IReadOnlyCollection<DefaultEndpointRole> roles) =>
            Applied.Add((flow, endpointId));
    }

    private sealed class RoleAwareAudioController(
        DefaultEndpointSnapshot playback,
        DefaultEndpointSnapshot capture) : IAudioEndpointController
    {
        public List<(AudioFlow Flow, string Id, DefaultEndpointRole Role)> AppliedRoles { get; } = [];

        public DefaultEndpointSnapshot GetDefaultEndpointIds(AudioFlow flow) =>
            flow == AudioFlow.Playback ? playback : capture;

        public IReadOnlyList<AudioEndpoint> EnumerateEndpoints() => [];

        public void SetDefaultEndpoints(
            AudioFlow flow,
            string endpointId,
            IReadOnlyCollection<DefaultEndpointRole> roles)
        {
            foreach (DefaultEndpointRole role in roles)
            {
                AppliedRoles.Add((flow, endpointId, role));
            }
        }
    }

    private sealed class FlakyAudioController : IAudioEndpointController
    {
        private bool _failNextSet = true;

        public List<(AudioFlow Flow, string Id)> Applied { get; } = [];

        public DefaultEndpointSnapshot GetDefaultEndpointIds(AudioFlow flow)
        {
            string id = flow == AudioFlow.Playback ? "speakers" : "desk-mic";
            return new DefaultEndpointSnapshot(id, id, id);
        }

        public IReadOnlyList<AudioEndpoint> EnumerateEndpoints() => [];

        public void SetDefaultEndpoints(
            AudioFlow flow,
            string endpointId,
            IReadOnlyCollection<DefaultEndpointRole> roles)
        {
            if (_failNextSet)
            {
                _failNextSet = false;
                throw new InvalidOperationException("simulated endpoint failure");
            }

            Applied.Add((flow, endpointId));
        }
    }

    private sealed class RemappingAudioController : IAudioEndpointController
    {
        public List<(AudioFlow Flow, string Id)> Applied { get; } = [];

        public DefaultEndpointSnapshot GetDefaultEndpointIds(AudioFlow flow)
        {
            string id = flow == AudioFlow.Playback ? "speakers" : "desk-mic";
            return new DefaultEndpointSnapshot(id, id, id);
        }

        public IReadOnlyList<AudioEndpoint> EnumerateEndpoints() =>
        [
            new("live-headset-out", "Headphones (3- SteelSeries Arctis Nova 5)", AudioFlow.Playback, true),
            new("speakers", "Speakers (Realtek(R) Audio)", AudioFlow.Playback, true),
            new("live-headset-mic", "Headset Microphone (3- SteelSeries Arctis Nova 5)", AudioFlow.Capture, true),
            new("desk-mic", "Microphone (Steam Streaming Microphone)", AudioFlow.Capture, true)
        ];

        public void SetDefaultEndpoints(
            AudioFlow flow,
            string endpointId,
            IReadOnlyCollection<DefaultEndpointRole> roles)
        {
            if (endpointId.StartsWith("stale", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Audio endpoint '{endpointId}' is not active.");
            }

            Applied.Add((flow, endpointId));
        }
    }
}
