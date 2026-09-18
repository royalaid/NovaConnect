using NovaConnect.App;
using Xunit;

namespace NovaConnect.App.Tests;

public sealed class AudioEndpointResolverTests
{
    [Fact]
    public void Resolve_KeepsIdsWhenTheyAreStillActive()
    {
        AudioEndpoint[] endpoints =
        [
            Playback("headset-out", "Headphones (SteelSeries Arctis Nova 5)"),
            Playback("speakers", "Speakers (Realtek(R) Audio)")
        ];
        AppConfiguration configuration = HeadsetConfig(
            headsetId: "headset-out",
            headsetName: "Headphones (SteelSeries Arctis Nova 5)",
            fallbackId: "speakers",
            fallbackName: "Speakers (Realtek(R) Audio)");

        AppConfiguration resolved = AudioEndpointResolver.Resolve(configuration, endpoints);

        Assert.Equal("headset-out", resolved.HeadsetPlaybackId);
        Assert.Equal("speakers", resolved.FallbackPlaybackId);
        Assert.True(AudioEndpointResolver.SameEndpointIdentity(configuration, resolved));
    }

    [Fact]
    public void Resolve_RebindsStaleIdsUsingSavedFriendlyNames()
    {
        AudioEndpoint[] endpoints =
        [
            Playback("live-headset-out", "Headphones (3- SteelSeries Arctis Nova 5)"),
            Playback("speakers", "Speakers (Realtek(R) Audio)"),
            Capture("live-headset-mic", "Headset Microphone (3- SteelSeries Arctis Nova 5)"),
            Capture("desk-mic", "Microphone (Steam Streaming Microphone)")
        ];
        AppConfiguration configuration = new()
        {
            HeadsetPlaybackId = "stale-headset-out",
            HeadsetPlaybackName = "Headphones (3- SteelSeries Arctis Nova 5)",
            FallbackPlaybackId = "speakers",
            FallbackPlaybackName = "Speakers (Realtek(R) Audio)",
            HeadsetCaptureId = "stale-headset-mic",
            HeadsetCaptureName = "Headset Microphone (3- SteelSeries Arctis Nova 5)",
            FallbackCaptureId = "desk-mic",
            FallbackCaptureName = "Microphone (Steam Streaming Microphone)",
            SwitchMicrophone = true
        };

        AppConfiguration resolved = AudioEndpointResolver.Resolve(configuration, endpoints);

        Assert.Equal("live-headset-out", resolved.HeadsetPlaybackId);
        Assert.Equal("live-headset-mic", resolved.HeadsetCaptureId);
        Assert.Equal("speakers", resolved.FallbackPlaybackId);
        Assert.False(AudioEndpointResolver.SameEndpointIdentity(configuration, resolved));
    }

    [Fact]
    public void Resolve_RebindsAcrossUsbCompositeNamePrefix()
    {
        AudioEndpoint[] endpoints =
        [
            Playback("live-headset-out", "Headphones (3- SteelSeries Arctis Nova 5)"),
            Playback("speakers", "Speakers (Realtek(R) Audio)")
        ];
        AppConfiguration configuration = HeadsetConfig(
            headsetId: "stale-headset-out",
            headsetName: "Headphones (SteelSeries Arctis Nova 5)",
            fallbackId: "speakers",
            fallbackName: "Speakers (Realtek(R) Audio)");

        AppConfiguration resolved = AudioEndpointResolver.Resolve(configuration, endpoints);

        Assert.Equal("live-headset-out", resolved.HeadsetPlaybackId);
        Assert.Equal("Headphones (3- SteelSeries Arctis Nova 5)", resolved.HeadsetPlaybackName);
    }

    [Fact]
    public void Resolve_HeadsetSlotFallsBackToUniqueNovaName()
    {
        AudioEndpoint[] endpoints =
        [
            Playback("live-headset-out", "Headphones (3- SteelSeries Arctis Nova 5)"),
            Playback("speakers", "Speakers (Realtek(R) Audio)")
        ];
        AppConfiguration configuration = HeadsetConfig(
            headsetId: "stale-headset-out",
            headsetName: "Headphones (old name that no longer exists)",
            fallbackId: "speakers",
            fallbackName: "Speakers (Realtek(R) Audio)");

        AppConfiguration resolved = AudioEndpointResolver.Resolve(configuration, endpoints);

        Assert.Equal("live-headset-out", resolved.HeadsetPlaybackId);
    }

    [Fact]
    public void Resolve_DoesNotGuessWhenMultipleNovaPlaybackEndpointsExist()
    {
        AudioEndpoint[] endpoints =
        [
            Playback("game", "Game (SteelSeries Arctis Nova 5)"),
            Playback("chat", "Chat (SteelSeries Arctis Nova 5)"),
            Playback("speakers", "Speakers")
        ];
        AppConfiguration configuration = HeadsetConfig(
            headsetId: "stale-headset-out",
            headsetName: "Headphones (SteelSeries Arctis Nova 5)",
            fallbackId: "speakers",
            fallbackName: "Speakers");

        AppConfiguration resolved = AudioEndpointResolver.Resolve(configuration, endpoints);

        Assert.Equal("stale-headset-out", resolved.HeadsetPlaybackId);
        Assert.Equal("speakers", resolved.FallbackPlaybackId);
    }

    [Fact]
    public void Resolve_FallbackDoesNotStealTheHeadsetEndpoint()
    {
        AudioEndpoint[] endpoints =
        [
            Playback("live-headset-out", "Headphones (SteelSeries Arctis Nova 5)"),
            Playback("speakers", "Speakers")
        ];
        AppConfiguration configuration = HeadsetConfig(
            headsetId: "stale-headset-out",
            headsetName: "Headphones (SteelSeries Arctis Nova 5)",
            fallbackId: "stale-speakers",
            fallbackName: "Missing speakers");

        AppConfiguration resolved = AudioEndpointResolver.Resolve(configuration, endpoints);

        Assert.Equal("live-headset-out", resolved.HeadsetPlaybackId);
        Assert.Equal("stale-speakers", resolved.FallbackPlaybackId);
    }

    [Fact]
    [Trait("Category", "Hardware")]
    public void Resolve_LiveNova5_RebindsStaleIdsToActiveEndpoints()
    {
        IReadOnlyList<AudioEndpoint> endpoints = CoreAudioService.EnumerateEndpoints();
        AudioEndpoint playback = Assert.Single(
            endpoints,
            endpoint => endpoint.Flow == AudioFlow.Playback
                && endpoint.IsActive
                && endpoint.Name.Contains("Arctis Nova 5", StringComparison.OrdinalIgnoreCase));
        AudioEndpoint capture = Assert.Single(
            endpoints,
            endpoint => endpoint.Flow == AudioFlow.Capture
                && endpoint.IsActive
                && endpoint.Name.Contains("Arctis Nova 5", StringComparison.OrdinalIgnoreCase));

        AppConfiguration stale = new()
        {
            HeadsetPlaybackId = "{0.0.0.00000000}.{00000000-0000-0000-0000-000000000000}",
            HeadsetPlaybackName = playback.Name,
            FallbackPlaybackId = "speakers",
            FallbackPlaybackName = "Speakers",
            HeadsetCaptureId = "{0.0.1.00000000}.{00000000-0000-0000-0000-000000000000}",
            HeadsetCaptureName = capture.Name,
            FallbackCaptureId = "desk-mic",
            FallbackCaptureName = "Microphone"
        };

        AppConfiguration resolved = AudioEndpointResolver.Resolve(stale, endpoints);

        Assert.Equal(playback.Id, resolved.HeadsetPlaybackId);
        Assert.Equal(capture.Id, resolved.HeadsetCaptureId);
    }

    private static AppConfiguration HeadsetConfig(
        string headsetId,
        string headsetName,
        string fallbackId,
        string fallbackName) => new()
    {
        HeadsetPlaybackId = headsetId,
        HeadsetPlaybackName = headsetName,
        FallbackPlaybackId = fallbackId,
        FallbackPlaybackName = fallbackName
    };

    private static AudioEndpoint Playback(string id, string name) =>
        new(id, name, AudioFlow.Playback, true);

    private static AudioEndpoint Capture(string id, string name) =>
        new(id, name, AudioFlow.Capture, true);
}
