namespace NovaConnect.App;

internal static class ShutdownFinalizer
{
    internal static void Run(Action finalLog, Action exit)
    {
        ArgumentNullException.ThrowIfNull(finalLog);
        ArgumentNullException.ThrowIfNull(exit);

        try
        {
            finalLog();
        }
        catch
        {
            // Shutdown must complete even if the log destination is unavailable.
        }
        finally
        {
            exit();
        }
    }
}
