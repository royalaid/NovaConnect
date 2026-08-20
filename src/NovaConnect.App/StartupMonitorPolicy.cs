namespace NovaConnect.App;

internal static class StartupMonitorPolicy
{
    internal static bool ShouldStart(bool requiresReview, int validationErrorCount) =>
        !requiresReview && validationErrorCount == 0;
}
