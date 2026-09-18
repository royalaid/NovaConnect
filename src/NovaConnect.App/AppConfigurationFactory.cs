namespace NovaConnect.App;

internal static class AppConfigurationFactory
{
    private const string HeadsetNameFragment = AudioEndpointResolver.HeadsetNameFragment;

    internal static AppConfiguration Create(
        IReadOnlyList<AudioEndpoint> endpoints,
        string? currentPlaybackId,
        string? currentCaptureId)
    {
        AudioEndpoint? headsetPlayback = FindHeadset(endpoints, AudioFlow.Playback);
        AudioEndpoint? headsetCapture = FindHeadset(endpoints, AudioFlow.Capture);
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
        AudioFlow flow) => endpoints.FirstOrDefault(endpoint =>
            endpoint.Flow == flow
            && endpoint.IsActive
            && endpoint.Name.Contains(HeadsetNameFragment, StringComparison.OrdinalIgnoreCase));

    private static AudioEndpoint? FindFallback(
        IReadOnlyList<AudioEndpoint> endpoints,
        AudioFlow flow,
        string? currentId,
        string? headsetId)
    {
        AudioEndpoint? current = endpoints.FirstOrDefault(endpoint =>
            endpoint.Flow == flow
            && endpoint.IsActive
            && string.Equals(endpoint.Id, currentId, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(endpoint.Id, headsetId, StringComparison.OrdinalIgnoreCase));

        return current ?? endpoints.FirstOrDefault(endpoint =>
            endpoint.Flow == flow
            && endpoint.IsActive
            && !string.Equals(endpoint.Id, headsetId, StringComparison.OrdinalIgnoreCase));
    }
}
