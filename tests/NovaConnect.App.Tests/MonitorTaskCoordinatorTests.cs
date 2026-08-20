using NovaConnect.App;
using Xunit;

namespace NovaConnect.App.Tests;

public sealed class MonitorTaskCoordinatorTests
{
    [Fact]
    public async Task ReplaceAsync_WaitsForCancelledMonitorBeforeStartingReplacement()
    {
        var coordinator = new MonitorTaskCoordinator();
        var firstStarted = NewSignal();
        var firstCancelled = NewSignal();
        var releaseFirst = NewSignal();
        var secondStarted = NewSignal();

        await coordinator.ReplaceAsync(async cancellationToken =>
        {
            firstStarted.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                firstCancelled.TrySetResult();
                await releaseFirst.Task;
            }
        });
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Task replacement = coordinator.ReplaceAsync(async cancellationToken =>
        {
            secondStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        });

        await firstCancelled.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.False(secondStarted.Task.IsCompleted);
        Assert.False(replacement.IsCompleted);

        releaseFirst.TrySetResult();
        await replacement.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.True(secondStarted.Task.IsCompleted);

        await coordinator.StopAsync();
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
