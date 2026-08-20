using NovaConnect.App;
using Xunit;

namespace NovaConnect.App.Tests;

public sealed class ConfigurationStoreTests
{
    [Fact]
    public void SaveThenLoad_RoundTripsConfiguration()
    {
        string directory = Path.Combine(Path.GetTempPath(), "NovaConnect.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            string path = Path.Combine(directory, "config.json");
            var expected = new AppConfiguration
            {
                HeadsetPlaybackId = "headset-out",
                FallbackPlaybackId = "speakers",
                HeadsetCaptureId = "headset-mic",
                FallbackCaptureId = "desk-mic",
                SwitchMicrophone = true,
                PollIntervalMs = 750
            };

            ConfigurationStore.Save(path, expected);
            AppConfiguration actual = ConfigurationStore.Load(path);

            Assert.Equal(expected, actual);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void TryLoad_InvalidJson_ReturnsFalse()
    {
        string directory = Path.Combine(Path.GetTempPath(), "NovaConnect.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            string path = Path.Combine(directory, "config.json");
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, "{not-json");

            bool loaded = ConfigurationStore.TryLoad(path, out AppConfiguration? configuration);

            Assert.False(loaded);
            Assert.Null(configuration);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void TryLoad_NullJson_ReturnsFalse()
    {
        string directory = Path.Combine(Path.GetTempPath(), "NovaConnect.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            string path = Path.Combine(directory, "config.json");
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, "null");

            bool loaded = ConfigurationStore.TryLoad(path, out AppConfiguration? configuration);

            Assert.False(loaded);
            Assert.Null(configuration);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void Validate_MissingEndpointsAndFastPolling_ReturnsActionableErrors()
    {
        var configuration = new AppConfiguration { PollIntervalMs = 50 };

        IReadOnlyList<string> errors = configuration.Validate();

        Assert.Contains(errors, error => error.Contains("headset playback", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("fallback playback", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("250", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("microphone", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_IdenticalPlaybackEndpoints_RejectsUnsafeConfiguration()
    {
        var configuration = new AppConfiguration
        {
            HeadsetPlaybackId = "same",
            FallbackPlaybackId = "same",
            SwitchMicrophone = false
        };

        IReadOnlyList<string> errors = configuration.Validate();

        Assert.Contains(errors, error => error.Contains("different", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_IdenticalMicrophoneEndpoints_RejectsUnsafeConfiguration()
    {
        var configuration = new AppConfiguration
        {
            HeadsetPlaybackId = "headset",
            FallbackPlaybackId = "speakers",
            HeadsetCaptureId = "same-mic",
            FallbackCaptureId = "same-mic",
            SwitchMicrophone = true
        };

        IReadOnlyList<string> errors = configuration.Validate();

        Assert.Contains(errors, error => error.Contains("microphone", StringComparison.OrdinalIgnoreCase)
            && error.Contains("different", StringComparison.OrdinalIgnoreCase));
    }
}
