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

    [Fact]
    public void Create_DetectsUsbCompositePrefixedNovaNames()
    {
        AudioEndpoint[] endpoints =
        [
            new("headset-out", "Headphones (3- SteelSeries Arctis Nova 5)", AudioFlow.Playback, true),
            new("speakers", "Speakers (Realtek(R) Audio)", AudioFlow.Playback, true),
            new("headset-mic", "Headset Microphone (3- SteelSeries Arctis Nova 5)", AudioFlow.Capture, true),
            new("desk-mic", "USB Microphone", AudioFlow.Capture, true)
        ];

        AppConfiguration configuration = AppConfigurationFactory.Create(
            endpoints,
            currentPlaybackId: "speakers",
            currentCaptureId: "desk-mic");

        Assert.Equal("headset-out", configuration.HeadsetPlaybackId);
        Assert.Equal("headset-mic", configuration.HeadsetCaptureId);
    }

    [Fact]
    public void Create_PrefersHeadphoneLikeDeviceOverSpeakers()
    {
        AudioEndpoint[] endpoints =
        [
            new("speakers", "Speakers (Realtek(R) Audio)", AudioFlow.Playback, true),
            new("bt", "Headphones (WH-1000XM5)", AudioFlow.Playback, true),
            new("desk-mic", "Microphone (USB Microphone)", AudioFlow.Capture, true),
            new("bt-mic", "Headset Microphone (WH-1000XM5 Hands-Free)", AudioFlow.Capture, true)
        ];

        AppConfiguration configuration = AppConfigurationFactory.Create(
            endpoints,
            currentPlaybackId: "speakers",
            currentCaptureId: "desk-mic");

        Assert.Equal("bt", configuration.HeadsetPlaybackId);
        Assert.Equal("speakers", configuration.FallbackPlaybackId);
        Assert.Equal("bt-mic", configuration.HeadsetCaptureId);
        Assert.Equal("desk-mic", configuration.FallbackCaptureId);
        Assert.True(configuration.SwitchMicrophone);
    }

    [Fact]
    public void Create_WhenCurrentDefaultIsAHeadset_KeepsItAsTheHeadsetDevice()
    {
        AudioEndpoint[] endpoints =
        [
            new("sony", "Headphones (WH-1000XM5)", AudioFlow.Playback, true),
            new("airpods", "Headphones (AirPods)", AudioFlow.Playback, true),
            new("speakers", "Speakers", AudioFlow.Playback, true)
        ];

        AppConfiguration configuration = AppConfigurationFactory.Create(
            endpoints,
            currentPlaybackId: "airpods",
            currentCaptureId: null);

        Assert.Equal("airpods", configuration.HeadsetPlaybackId);
        Assert.Equal("speakers", configuration.FallbackPlaybackId);
    }
}
