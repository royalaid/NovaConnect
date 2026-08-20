namespace NovaConnect.App;

internal enum StartupToggleStatus
{
    Succeeded,
    Failed,
    Suppressed
}

internal readonly record struct StartupToggleResult(
    StartupToggleStatus Status,
    Exception? Error = null);

internal sealed class StartupToggleCoordinator
{
    private bool _isApplying;

    internal StartupToggleResult Apply(
        bool desired,
        Action<bool> persist,
        Action<bool> restoreChecked)
    {
        if (_isApplying)
        {
            return new StartupToggleResult(StartupToggleStatus.Suppressed);
        }

        _isApplying = true;
        try
        {
            persist(desired);
            return new StartupToggleResult(StartupToggleStatus.Succeeded);
        }
        catch (Exception exception)
        {
            restoreChecked(!desired);
            return new StartupToggleResult(StartupToggleStatus.Failed, exception);
        }
        finally
        {
            _isApplying = false;
        }
    }
}
