namespace NovaConnect.App;

internal sealed class MonitorTaskCoordinator
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _currentCancellation;
    private Task? _currentTask;

    internal async Task ReplaceAsync(Func<CancellationToken, Task> runMonitor)
    {
        ArgumentNullException.ThrowIfNull(runMonitor);

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopCurrentAsync().ConfigureAwait(false);

            var cancellation = new CancellationTokenSource();
            Task task;
            try
            {
                task = runMonitor(cancellation.Token)
                    ?? throw new InvalidOperationException("The monitor runner returned no task.");
            }
            catch
            {
                cancellation.Dispose();
                throw;
            }

            _currentCancellation = cancellation;
            _currentTask = task;
        }
        finally
        {
            _gate.Release();
        }
    }

    internal async Task StopAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopCurrentAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task StopCurrentAsync()
    {
        CancellationTokenSource? cancellation = _currentCancellation;
        Task? task = _currentTask;
        if (cancellation is null)
        {
            return;
        }

        cancellation.Cancel();
        try
        {
            if (task is not null)
            {
                await task.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(_currentCancellation, cancellation))
            {
                _currentCancellation = null;
                _currentTask = null;
            }

            cancellation.Dispose();
        }
    }
}
