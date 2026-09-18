namespace NovaConnect.App;

internal static class HeadsetEndpointScorer
{
    private static readonly string[] HeadsetTokens =
    [
        "headset",
        "headphone",
        "earphone",
        "earbuds",
        "airpods",
        "buds",
        "hands-free",
        "handsfree"
    ];

    private static readonly string[] SpeakerTokens =
    [
        "speaker",
        "display",
        "hdmi",
        "monitor",
        "realtek",
        "nvidia",
        "steam streaming"
    ];

    internal static int Score(AudioEndpoint endpoint, AudioEndpoint? pairedPlayback = null)
    {
        int score = 0;
        if (ContainsAny(endpoint.Name, HeadsetTokens))
        {
            score += 10;
        }

        if (ContainsAny(endpoint.Name, SpeakerTokens))
        {
            score -= 8;
        }

        string? marker = pairedPlayback is null ? null : DeviceMarker(pairedPlayback.Name);
        if (!string.IsNullOrEmpty(marker)
            && endpoint.Name.Contains(marker, StringComparison.OrdinalIgnoreCase))
        {
            score += 8;
        }

        return score;
    }

    internal static bool LooksLikeHeadset(AudioEndpoint endpoint) => Score(endpoint) > 0;

    private static bool ContainsAny(string name, string[] tokens) =>
        tokens.Any(token => name.Contains(token, StringComparison.OrdinalIgnoreCase));

    internal static string? DeviceMarker(string name)
    {
        string normalized = AudioEndpointResolver.NormalizeDeviceName(name);
        int open = normalized.IndexOf('(');
        int close = normalized.LastIndexOf(')');
        if (open < 0 || close <= open + 1)
        {
            return null;
        }

        string marker = normalized[(open + 1)..close].Trim();
        return marker.Length >= 4 ? marker : null;
    }
}
