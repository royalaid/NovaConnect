namespace NovaConnect.Core;

public sealed record SwitchConfiguration(
    string HeadsetPlaybackId,
    string FallbackPlaybackId,
    string? HeadsetCaptureId,
    string? FallbackCaptureId,
    bool SwitchCapture);

public readonly record struct EndpointSwitchPlan(
    string? PlaybackTargetId,
    string? CaptureTargetId)
{
    public static EndpointSwitchPlan None { get; } = new(null, null);
}

public enum DefaultEndpointRole
{
    Console,
    Multimedia,
    Communications
}

public readonly record struct DefaultEndpointSnapshot(
    string? Console,
    string? Multimedia,
    string? Communications)
{
    public string? Get(DefaultEndpointRole role) => role switch
    {
        DefaultEndpointRole.Console => Console,
        DefaultEndpointRole.Multimedia => Multimedia,
        DefaultEndpointRole.Communications => Communications,
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };
}

public sealed record RoleEndpointSwitchPlan(
    string? PlaybackTargetId,
    IReadOnlyList<DefaultEndpointRole> PlaybackRoles,
    string? CaptureTargetId,
    IReadOnlyList<DefaultEndpointRole> CaptureRoles)
{
    public static RoleEndpointSwitchPlan None { get; } = new(
        null,
        Array.Empty<DefaultEndpointRole>(),
        null,
        Array.Empty<DefaultEndpointRole>());
}

public static class EndpointSwitchPlanner
{
    private static readonly DefaultEndpointRole[] AllRoles =
    [
        DefaultEndpointRole.Console,
        DefaultEndpointRole.Multimedia,
        DefaultEndpointRole.Communications
    ];

    public static RoleEndpointSwitchPlan PlanRoles(
        ConnectionTransition transition,
        DefaultEndpointSnapshot currentPlayback,
        DefaultEndpointSnapshot currentCapture,
        SwitchConfiguration configuration)
    {
        if (transition == ConnectionTransition.Connected)
        {
            return new RoleEndpointSwitchPlan(
                configuration.HeadsetPlaybackId,
                AllRoles,
                configuration.SwitchCapture ? configuration.HeadsetCaptureId : null,
                configuration.SwitchCapture ? AllRoles : Array.Empty<DefaultEndpointRole>());
        }

        if (transition != ConnectionTransition.Disconnected)
        {
            return RoleEndpointSwitchPlan.None;
        }

        DefaultEndpointRole[] playbackRoles = RolesOwnedBy(
            currentPlayback,
            configuration.HeadsetPlaybackId);
        DefaultEndpointRole[] captureRoles = configuration.SwitchCapture
            ? RolesOwnedBy(currentCapture, configuration.HeadsetCaptureId)
            : Array.Empty<DefaultEndpointRole>();

        return new RoleEndpointSwitchPlan(
            playbackRoles.Length > 0 ? configuration.FallbackPlaybackId : null,
            playbackRoles,
            captureRoles.Length > 0 ? configuration.FallbackCaptureId : null,
            captureRoles);
    }

    public static EndpointSwitchPlan Plan(
        ConnectionTransition transition,
        string? currentPlaybackId,
        string? currentCaptureId,
        SwitchConfiguration configuration)
    {
        if (transition == ConnectionTransition.Connected)
        {
            return new EndpointSwitchPlan(
                configuration.HeadsetPlaybackId,
                configuration.SwitchCapture ? configuration.HeadsetCaptureId : null);
        }

        if (transition == ConnectionTransition.Disconnected)
        {
            string? playback = string.Equals(
                currentPlaybackId,
                configuration.HeadsetPlaybackId,
                StringComparison.OrdinalIgnoreCase)
                ? configuration.FallbackPlaybackId
                : null;
            string? capture = configuration.SwitchCapture
                && string.Equals(
                    currentCaptureId,
                    configuration.HeadsetCaptureId,
                    StringComparison.OrdinalIgnoreCase)
                ? configuration.FallbackCaptureId
                : null;
            return new EndpointSwitchPlan(playback, capture);
        }

        return EndpointSwitchPlan.None;
    }

    private static DefaultEndpointRole[] RolesOwnedBy(
        DefaultEndpointSnapshot snapshot,
        string? headsetEndpointId) => AllRoles
        .Where(role => string.Equals(
            snapshot.Get(role),
            headsetEndpointId,
            StringComparison.OrdinalIgnoreCase))
        .ToArray();
}
