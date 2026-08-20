using System.Text.Json;
using NovaConnect.Core;

namespace NovaConnect.App;

internal sealed record AppConfiguration
{
    internal static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NovaConnect");

    internal static string DefaultPath => Path.Combine(DefaultDirectory, "config.json");

    public bool Enabled { get; init; } = true;
    public bool SwitchOnStartup { get; init; } = true;
    public bool SwitchMicrophone { get; init; } = true;
    public int PollIntervalMs { get; init; } = 1_000;
    public string HeadsetPlaybackId { get; init; } = string.Empty;
    public string HeadsetPlaybackName { get; init; } = string.Empty;
    public string FallbackPlaybackId { get; init; } = string.Empty;
    public string FallbackPlaybackName { get; init; } = string.Empty;
    public string? HeadsetCaptureId { get; init; }
    public string? HeadsetCaptureName { get; init; }
    public string? FallbackCaptureId { get; init; }
    public string? FallbackCaptureName { get; init; }

    internal IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(HeadsetPlaybackId))
        {
            errors.Add("Select a headset playback endpoint.");
        }

        if (string.IsNullOrWhiteSpace(FallbackPlaybackId))
        {
            errors.Add("Select a fallback playback endpoint.");
        }

        if (!string.IsNullOrWhiteSpace(HeadsetPlaybackId)
            && string.Equals(HeadsetPlaybackId, FallbackPlaybackId, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Headset and fallback playback endpoints must be different.");
        }

        if (PollIntervalMs is < 250 or > 60_000)
        {
            errors.Add("Poll interval must be between 250 and 60000 milliseconds.");
        }

        if (SwitchMicrophone
            && (string.IsNullOrWhiteSpace(HeadsetCaptureId)
                || string.IsNullOrWhiteSpace(FallbackCaptureId)))
        {
            errors.Add("Select both microphone endpoints or disable microphone switching.");
        }

        if (SwitchMicrophone
            && !string.IsNullOrWhiteSpace(HeadsetCaptureId)
            && string.Equals(HeadsetCaptureId, FallbackCaptureId, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Headset and fallback microphone endpoints must be different.");
        }

        return errors;
    }

    internal SwitchConfiguration ToSwitchConfiguration() => new(
        HeadsetPlaybackId,
        FallbackPlaybackId,
        HeadsetCaptureId,
        FallbackCaptureId,
        SwitchMicrophone);
}

internal static class ConfigurationStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    internal static bool TryLoad(string path, out AppConfiguration? configuration)
    {
        try
        {
            configuration = Load(path);
            return true;
        }
        catch (Exception exception) when (
            exception is JsonException
            or InvalidDataException
            or IOException
            or UnauthorizedAccessException)
        {
            configuration = null;
            return false;
        }
    }

    internal static AppConfiguration Load(string path)
    {
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AppConfiguration>(json, SerializerOptions)
            ?? throw new InvalidDataException("NovaConnect configuration is empty.");
    }

    internal static void Save(string path, AppConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporaryPath = path + ".tmp";
        string json = JsonSerializer.Serialize(configuration, SerializerOptions);
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, path, overwrite: true);
    }
}
