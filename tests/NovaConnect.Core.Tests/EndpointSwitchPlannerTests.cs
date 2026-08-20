using NovaConnect.Core;
using Xunit;

namespace NovaConnect.Core.Tests;

public sealed class EndpointSwitchPlannerTests
{
    private static readonly SwitchConfiguration Configuration = new(
        HeadsetPlaybackId: "headset-out",
        FallbackPlaybackId: "speakers",
        HeadsetCaptureId: "headset-mic",
        FallbackCaptureId: "desk-mic",
        SwitchCapture: true);

    [Fact]
    public void Plan_Connect_SelectsConfiguredHeadsetEndpoints()
    {
        EndpointSwitchPlan plan = EndpointSwitchPlanner.Plan(
            ConnectionTransition.Connected,
            currentPlaybackId: "speakers",
            currentCaptureId: "desk-mic",
            Configuration);

        Assert.Equal("headset-out", plan.PlaybackTargetId);
        Assert.Equal("headset-mic", plan.CaptureTargetId);
    }

    [Fact]
    public void Plan_DisconnectFromHeadset_SelectsFallbackEndpoints()
    {
        EndpointSwitchPlan plan = EndpointSwitchPlanner.Plan(
            ConnectionTransition.Disconnected,
            currentPlaybackId: "headset-out",
            currentCaptureId: "headset-mic",
            Configuration);

        Assert.Equal("speakers", plan.PlaybackTargetId);
        Assert.Equal("desk-mic", plan.CaptureTargetId);
    }

    [Fact]
    public void Plan_DisconnectAfterManualChoice_DoesNotOverrideCurrentEndpoints()
    {
        EndpointSwitchPlan plan = EndpointSwitchPlanner.Plan(
            ConnectionTransition.Disconnected,
            currentPlaybackId: "hdmi-monitor",
            currentCaptureId: "webcam-mic",
            Configuration);

        Assert.Null(plan.PlaybackTargetId);
        Assert.Null(plan.CaptureTargetId);
    }

    [Fact]
    public void Plan_ConnectWithCaptureDisabled_ChangesPlaybackOnly()
    {
        SwitchConfiguration playbackOnly = Configuration with { SwitchCapture = false };

        EndpointSwitchPlan plan = EndpointSwitchPlanner.Plan(
            ConnectionTransition.Connected,
            currentPlaybackId: "speakers",
            currentCaptureId: "desk-mic",
            playbackOnly);

        Assert.Equal("headset-out", plan.PlaybackTargetId);
        Assert.Null(plan.CaptureTargetId);
    }

    [Fact]
    public void PlanRoles_Disconnect_SwitchesOnlyRolesStillOwnedByHeadset()
    {
        var playback = new DefaultEndpointSnapshot(
            Console: "headset-out",
            Multimedia: "manual-speakers",
            Communications: "headset-out");
        var capture = new DefaultEndpointSnapshot(
            Console: "headset-mic",
            Multimedia: "headset-mic",
            Communications: "conference-mic");

        RoleEndpointSwitchPlan plan = EndpointSwitchPlanner.PlanRoles(
            ConnectionTransition.Disconnected,
            playback,
            capture,
            Configuration);

        Assert.Equal("speakers", plan.PlaybackTargetId);
        Assert.Equal(
            [DefaultEndpointRole.Console, DefaultEndpointRole.Communications],
            plan.PlaybackRoles);
        Assert.Equal("desk-mic", plan.CaptureTargetId);
        Assert.Equal(
            [DefaultEndpointRole.Console, DefaultEndpointRole.Multimedia],
            plan.CaptureRoles);
    }
}
