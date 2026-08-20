namespace NovaConnect.App;

internal readonly record struct EnabledToggleResult(
    bool Succeeded,
    Exception? Error = null);

internal static class EnabledToggleCoordinator
{
    internal static async Task<EnabledToggleResult> ApplyAsync(
        bool current,
        bool desired,
        Func<bool, Task> apply,
        Action<bool> restore)
    {
        ArgumentNullException.ThrowIfNull(apply);
        ArgumentNullException.ThrowIfNull(restore);

        if (current == desired)
        {
            return new EnabledToggleResult(true);
        }

        try
        {
            await apply(desired).ConfigureAwait(true);
            return new EnabledToggleResult(true);
        }
        catch (Exception exception)
        {
            restore(current);
            return new EnabledToggleResult(false, exception);
        }
    }
}
