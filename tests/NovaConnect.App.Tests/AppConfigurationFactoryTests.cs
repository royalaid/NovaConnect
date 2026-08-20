using NovaConnect.App;
using Xunit;

namespace NovaConnect.App.Tests;

public sealed class AppConfigurationFactoryTests
{
    [Fact]
    public void Create_DetectsNovaEndpointsAndKeepsCurrentDefaultsAsFallback()
    {
        AudioEndpoint[] endpoints =
        [
            new("headset-out", "Headphones (SteelSeries Arctis Nova 5)", AudioFlow.Playback, true),
            new("speakers", "Speakers", AudioFlow.Playback, true),
            new("headset-mic", "Microphone (SteelSeries Arctis Nova 5)", AudioFlow.Capture, true),
            new("desk-mic", "USB Microphone", AudioFlow.Capture, true)
        ];

        AppConfiguration configuration = AppConfigurationFactory.Create(
            endpoints,
            currentPlaybackId: "speakers",
            currentCaptureId: "desk-mic");

        Assert.Equal("headset-out", configuration.HeadsetPlaybackId);
        Assert.Equal("speakers", configuration.FallbackPlaybackId);
        Assert.Equal("headset-mic", configuration.HeadsetCaptureId);
        Assert.Equal("desk-mic", configuration.FallbackCaptureId);
    }
}
