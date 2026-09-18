namespace NovaConnect.App;

internal static class AppConfigurationFactory
{
    internal static AppConfiguration Create(
        IReadOnlyList<AudioEndpoint> endpoints,
        string? currentPlaybackId,
        string? currentCaptureId)
    {
        AudioEndpoint? headsetPlayback = FindHeadset(endpoints, AudioFlow.Playback, currentPlaybackId);
        AudioEndpoint? headsetCapture = FindHeadset(
            endpoints,
            AudioFlow.Capture,
            currentCaptureId,
            headsetPlayback);
        AudioEndpoint? fallbackPlayback = FindFallback(
            endpoints,
            AudioFlow.Playback,
            currentPlaybackId,
            headsetPlayback?.Id);
        AudioEndpoint? fallbackCapture = FindFallback(
            endpoints,
            AudioFlow.Capture,
            currentCaptureId,
            headsetCapture?.Id);

        return new AppConfiguration
        {
            HeadsetPlaybackId = headsetPlayback?.Id ?? string.Empty,
            HeadsetPlaybackName = headsetPlayback?.Name ?? string.Empty,
            FallbackPlaybackId = fallbackPlayback?.Id ?? string.Empty,
            FallbackPlaybackName = fallbackPlayback?.Name ?? string.Empty,
            HeadsetCaptureId = headsetCapture?.Id,
            HeadsetCaptureName = headsetCapture?.Name,
            FallbackCaptureId = fallbackCapture?.Id,
            FallbackCaptureName = fallbackCapture?.Name,
            SwitchMicrophone = headsetCapture is not null && fallbackCapture is not null
        };
    }

    private static AudioEndpoint? FindHeadset(
        IEnumerable<AudioEndpoint> endpoints,
        AudioFlow flow,
        string? currentId,
        AudioEndpoint? pairedPlayback = null) => endpoints
        .Where(endpoint => endpoint.Flow == flow && endpoint.IsActive)
        .Select(endpoint => (endpoint, score: HeadsetEndpointScorer.Score(endpoint, pairedPlayback)))
        .Where(candidate => candidate.score > 0)
        .OrderByDescending(candidate =>
            string.Equals(candidate.endpoint.Id, currentId, StringComparison.OrdinalIgnoreCase))
        .ThenByDescending(candidate => candidate.score)
        .ThenBy(candidate => candidate.endpoint.Name, StringComparer.CurrentCultureIgnoreCase)
        .Select(candidate => candidate.endpoint)
        .FirstOrDefault();

    private static AudioEndpoint? FindFallback(
        IReadOnlyList<AudioEndpoint> endpoints,
        AudioFlow flow,
        string? currentId,
        string? headsetId)
    {
        AudioEndpoint[] others = endpoints
            .Where(endpoint =>
                endpoint.Flow == flow
                && endpoint.IsActive
                && !string.Equals(endpoint.Id, headsetId, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        AudioEndpoint? current = others.FirstOrDefault(endpoint =>
            string.Equals(endpoint.Id, currentId, StringComparison.OrdinalIgnoreCase));
        if (current is not null && !HeadsetEndpointScorer.LooksLikeHeadset(current))
        {
            return current;
        }

        return others
            .OrderBy(endpoint => HeadsetEndpointScorer.LooksLikeHeadset(endpoint) ? 1 : 0)
            .ThenBy(endpoint => endpoint.Name, StringComparer.CurrentCultureIgnoreCase)
            .FirstOrDefault() ?? current;
    }
}
