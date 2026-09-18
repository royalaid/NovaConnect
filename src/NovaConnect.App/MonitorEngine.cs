using NovaConnect.Core;

namespace NovaConnect.App;

internal interface IHeadsetStatusSource
{
    Task<Nova5Status> QueryOnceAsync(CancellationToken cancellationToken);
}

internal interface IAudioEndpointController
{
    DefaultEndpointSnapshot GetDefaultEndpointIds(AudioFlow flow);
    IReadOnlyList<AudioEndpoint> EnumerateEndpoints();
    void SetDefaultEndpoints(
        AudioFlow flow,
        string endpointId,
        IReadOnlyCollection<DefaultEndpointRole> roles);
}

internal sealed class Nova5HidStatusSource : IHeadsetStatusSource
{
    public Task<Nova5Status> QueryOnceAsync(CancellationToken cancellationToken) =>
        Nova5HidProbe.QueryOnceAsync(cancellationToken);
}

internal sealed class CoreAudioEndpointController : IAudioEndpointController
{
    public DefaultEndpointSnapshot GetDefaultEndpointIds(AudioFlow flow) =>
        CoreAudioService.GetDefaultEndpointIds(flow);

    public IReadOnlyList<AudioEndpoint> EnumerateEndpoints() =>
        CoreAudioService.EnumerateEndpoints();

    public void SetDefaultEndpoints(
        AudioFlow flow,
        string endpointId,
        IReadOnlyCollection<DefaultEndpointRole> roles) =>
        CoreAudioService.SetDefaultEndpoints(flow, endpointId, roles);
}

internal readonly record struct MonitorIterationResult(
    Nova5Status Status,
    ConnectionTransition Transition,
    RoleEndpointSwitchPlan Plan,
    AppConfiguration Configuration);

internal sealed class MonitorEngine
{
    private readonly IHeadsetStatusSource _statusSource;
    private readonly IAudioEndpointController _audio;
    private AppConfiguration _configuration;
    private readonly ConnectionTransitionDetector _transitionDetector;
    private readonly DisconnectDebouncer _disconnectDebouncer = new(requiredDisconnectedSamples: 2);

    internal MonitorEngine(
        IHeadsetStatusSource statusSource,
        IAudioEndpointController audio,
        AppConfiguration configuration)
    {
        _statusSource = statusSource;
        _audio = audio;
        _configuration = configuration;
        _transitionDetector = new ConnectionTransitionDetector(configuration.SwitchOnStartup);
    }

    internal async Task<MonitorIterationResult> RunIterationAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<AudioEndpoint> endpoints = _audio.EnumerateEndpoints();
        _configuration = AudioEndpointResolver.Resolve(_configuration, endpoints);

        Nova5Status status;
        try
        {
            status = await ReadStatusAsync(endpoints, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            _disconnectDebouncer.Reset();
            throw;
        }

        bool? confirmedConnectionState = _disconnectDebouncer.Observe(status.IsConnected);
        if (confirmedConnectionState is null)
        {
            return new MonitorIterationResult(
                status,
                ConnectionTransition.None,
                RoleEndpointSwitchPlan.None,
                _configuration);
        }

        ConnectionTransition transition = _transitionDetector.Observe(confirmedConnectionState.Value);
        RoleEndpointSwitchPlan plan = RoleEndpointSwitchPlan.None;

        if (_configuration.Enabled && transition != ConnectionTransition.None)
        {
            try
            {
                plan = EndpointSwitchPlanner.PlanRoles(
                    transition,
                    _audio.GetDefaultEndpointIds(AudioFlow.Playback),
                    _audio.GetDefaultEndpointIds(AudioFlow.Capture),
                    _configuration.ToSwitchConfiguration());

                if (!string.IsNullOrWhiteSpace(plan.PlaybackTargetId))
                {
                    _audio.SetDefaultEndpoints(
                        AudioFlow.Playback,
                        plan.PlaybackTargetId,
                        plan.PlaybackRoles);
                }

                if (!string.IsNullOrWhiteSpace(plan.CaptureTargetId))
                {
                    _audio.SetDefaultEndpoints(
                        AudioFlow.Capture,
                        plan.CaptureTargetId,
                        plan.CaptureRoles);
                }
            }
            catch
            {
                _transitionDetector.RollbackLastObservation();
                throw;
            }
        }

        return new MonitorIterationResult(status, transition, plan, _configuration);
    }

    private async Task<Nova5Status> ReadStatusAsync(
        IReadOnlyList<AudioEndpoint> endpoints,
        CancellationToken cancellationToken)
    {
        AudioEndpoint? headsetPlayback = endpoints.FirstOrDefault(endpoint =>
            endpoint.Flow == AudioFlow.Playback
            && string.Equals(
                endpoint.Id,
                _configuration.HeadsetPlaybackId,
                StringComparison.OrdinalIgnoreCase));

        if (headsetPlayback is { IsActive: false })
        {
            return new Nova5Status(false, 0, Nova5ChargingState.Unknown);
        }

        try
        {
            return await _statusSource.QueryOnceAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException) when (headsetPlayback is { IsActive: true })
        {
            return new Nova5Status(true, 0, Nova5ChargingState.Unknown);
        }
    }
}
