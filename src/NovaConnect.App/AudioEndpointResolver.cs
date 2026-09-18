using System.Text.RegularExpressions;

namespace NovaConnect.App;

internal static class AudioEndpointResolver
{
    private static readonly Regex UsbCompositePrefix = new(
        @"\(\s*\d+\s*-\s*",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static AppConfiguration Resolve(
        AppConfiguration configuration,
        IReadOnlyList<AudioEndpoint> endpoints)
    {
        AudioEndpoint? headsetPlayback = Match(
            endpoints,
            AudioFlow.Playback,
            configuration.HeadsetPlaybackId,
            configuration.HeadsetPlaybackName,
            headsetSlot: true);
        AudioEndpoint? headsetCapture = Match(
            endpoints,
            AudioFlow.Capture,
            configuration.HeadsetCaptureId,
            configuration.HeadsetCaptureName,
            headsetSlot: true);
        AudioEndpoint? fallbackPlayback = Match(
            endpoints,
            AudioFlow.Playback,
            configuration.FallbackPlaybackId,
            configuration.FallbackPlaybackName,
            headsetSlot: false,
            excludedId: headsetPlayback?.Id);
        AudioEndpoint? fallbackCapture = Match(
            endpoints,
            AudioFlow.Capture,
            configuration.FallbackCaptureId,
            configuration.FallbackCaptureName,
            headsetSlot: false,
            excludedId: headsetCapture?.Id);

        return configuration with
        {
            HeadsetPlaybackId = headsetPlayback?.Id ?? configuration.HeadsetPlaybackId,
            HeadsetPlaybackName = headsetPlayback?.Name ?? configuration.HeadsetPlaybackName,
            HeadsetCaptureId = headsetCapture?.Id ?? configuration.HeadsetCaptureId,
            HeadsetCaptureName = headsetCapture?.Name ?? configuration.HeadsetCaptureName,
            FallbackPlaybackId = fallbackPlayback?.Id ?? configuration.FallbackPlaybackId,
            FallbackPlaybackName = fallbackPlayback?.Name ?? configuration.FallbackPlaybackName,
            FallbackCaptureId = fallbackCapture?.Id ?? configuration.FallbackCaptureId,
            FallbackCaptureName = fallbackCapture?.Name ?? configuration.FallbackCaptureName
        };
    }

    internal static bool SameEndpointIdentity(AppConfiguration left, AppConfiguration right) =>
        string.Equals(left.HeadsetPlaybackId, right.HeadsetPlaybackId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.FallbackPlaybackId, right.FallbackPlaybackId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.HeadsetCaptureId, right.HeadsetCaptureId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.FallbackCaptureId, right.FallbackCaptureId, StringComparison.OrdinalIgnoreCase);

    internal static AudioEndpoint? Match(
        IEnumerable<AudioEndpoint> endpoints,
        AudioFlow flow,
        string? savedId,
        string? savedName,
        bool headsetSlot,
        string? excludedId = null)
    {
        AudioEndpoint[] active = endpoints
            .Where(endpoint =>
                endpoint.Flow == flow
                && endpoint.IsActive
                && !string.Equals(endpoint.Id, excludedId, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        AudioEndpoint? byId = FindById(active, savedId);
        if (byId is not null)
        {
            return byId;
        }

        AudioEndpoint? byName = FindByName(active, savedName);
        if (byName is not null)
        {
            return byName;
        }

        if (!headsetSlot)
        {
            return null;
        }

        AudioEndpoint[] headsetMatches = active
            .Where(HeadsetEndpointScorer.LooksLikeHeadset)
            .ToArray();
        return headsetMatches.Length == 1 ? headsetMatches[0] : null;
    }

    internal static string NormalizeDeviceName(string name) =>
        UsbCompositePrefix.Replace(name, "(");

    private static AudioEndpoint? FindById(IEnumerable<AudioEndpoint> endpoints, string? savedId)
    {
        if (string.IsNullOrWhiteSpace(savedId))
        {
            return null;
        }

        return endpoints.FirstOrDefault(endpoint =>
            string.Equals(endpoint.Id, savedId, StringComparison.OrdinalIgnoreCase));
    }

    private static AudioEndpoint? FindByName(IEnumerable<AudioEndpoint> endpoints, string? savedName)
    {
        if (string.IsNullOrWhiteSpace(savedName))
        {
            return null;
        }

        AudioEndpoint? exact = endpoints.FirstOrDefault(endpoint =>
            string.Equals(endpoint.Name, savedName, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        string normalizedSaved = NormalizeDeviceName(savedName);
        return endpoints.FirstOrDefault(endpoint =>
            string.Equals(
                NormalizeDeviceName(endpoint.Name),
                normalizedSaved,
                StringComparison.OrdinalIgnoreCase));
    }
}
