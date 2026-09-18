namespace NovaConnect.App;

internal enum StatusTone
{
    Neutral,
    Connected,
    Disconnected,
    Warning
}

internal sealed record StatusPresentation(string Text, StatusTone Tone)
{
    internal static StatusPresentation FromText(string text)
    {
        string normalized = text.Trim();
        StatusTone tone = normalized.Contains("disconnected", StringComparison.OrdinalIgnoreCase)
            ? StatusTone.Disconnected
            : normalized.Contains("connected", StringComparison.OrdinalIgnoreCase)
                ? StatusTone.Connected
                : IsWarning(normalized)
                    ? StatusTone.Warning
                    : StatusTone.Neutral;

        return new StatusPresentation(normalized, tone);
    }

    private static bool IsWarning(string text) =>
        text.Contains("unavailable", StringComparison.OrdinalIgnoreCase)
        || text.Contains("required", StringComparison.OrdinalIgnoreCase)
        || text.Contains("review", StringComparison.OrdinalIgnoreCase)
        || text.Contains("failed", StringComparison.OrdinalIgnoreCase)
        || text.Contains("error", StringComparison.OrdinalIgnoreCase);
}
