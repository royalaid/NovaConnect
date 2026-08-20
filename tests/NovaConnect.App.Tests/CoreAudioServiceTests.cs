using NovaConnect.App;
using NovaConnect.Core;
using Xunit;

namespace NovaConnect.App.Tests;

public sealed class CoreAudioServiceTests
{
    [Fact]
    [Trait("Category", "Hardware")]
    public void EnumerateEndpoints_AttachedNova5_IncludesPlaybackAndCapture()
    {
        IReadOnlyList<AudioEndpoint> endpoints = CoreAudioService.EnumerateEndpoints();

        Assert.Contains(endpoints, endpoint =>
            endpoint.Flow == AudioFlow.Playback
            && endpoint.IsActive
            && endpoint.Name.Contains("Arctis Nova 5", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(endpoints, endpoint =>
            endpoint.Flow == AudioFlow.Capture
            && endpoint.IsActive
            && endpoint.Name.Contains("Arctis Nova 5", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Category", "Hardware")]
    public void GetDefaultEndpoint_ReturnsAnActiveEndpointForEachFlow()
    {
        IReadOnlyList<AudioEndpoint> endpoints = CoreAudioService.EnumerateEndpoints();

        string? playbackId = CoreAudioService.GetDefaultEndpointId(AudioFlow.Playback);
        string? captureId = CoreAudioService.GetDefaultEndpointId(AudioFlow.Capture);

        Assert.Contains(endpoints, endpoint => endpoint.IsActive && endpoint.Id == playbackId);
        Assert.Contains(endpoints, endpoint => endpoint.IsActive && endpoint.Id == captureId);
    }

    [Fact]
    [Trait("Category", "Hardware")]
    public void SetDefaultEndpoint_CurrentPlayback_IsANoOpSuccess()
    {
        string playbackId = Assert.IsType<string>(CoreAudioService.GetDefaultEndpointId(AudioFlow.Playback));

        CoreAudioService.SetDefaultEndpoint(AudioFlow.Playback, playbackId);

        Assert.Equal(playbackId, CoreAudioService.GetDefaultEndpointId(AudioFlow.Playback));
    }

    [Fact]
    [Trait("Category", "Hardware")]
    public void GetDefaultEndpointIds_ReturnsEveryPlaybackRole()
    {
        DefaultEndpointSnapshot snapshot = CoreAudioService.GetDefaultEndpointIds(AudioFlow.Playback);

        Assert.False(string.IsNullOrWhiteSpace(snapshot.Console));
        Assert.False(string.IsNullOrWhiteSpace(snapshot.Multimedia));
        Assert.False(string.IsNullOrWhiteSpace(snapshot.Communications));
    }

    [Fact]
    [Trait("Category", "Hardware")]
    public void SetDefaultEndpoints_CurrentConsoleRole_IsANoOpSuccess()
    {
        DefaultEndpointSnapshot before = CoreAudioService.GetDefaultEndpointIds(AudioFlow.Playback);
        string currentConsole = Assert.IsType<string>(before.Console);

        CoreAudioService.SetDefaultEndpoints(
            AudioFlow.Playback,
            currentConsole,
            [DefaultEndpointRole.Console]);

        DefaultEndpointSnapshot after = CoreAudioService.GetDefaultEndpointIds(AudioFlow.Playback);
        Assert.Equal(currentConsole, after.Console);
    }
}
