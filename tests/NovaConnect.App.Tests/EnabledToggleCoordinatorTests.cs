using NovaConnect.App;
using Xunit;

namespace NovaConnect.App.Tests;

public sealed class EnabledToggleCoordinatorTests
{
    [Fact]
    public async Task ApplyAsync_SaveFailure_RestoresPreviousState()
    {
        bool? restored = null;

        EnabledToggleResult result = await EnabledToggleCoordinator.ApplyAsync(
            current: true,
            desired: false,
            _ => Task.FromException(new IOException("simulated save failure")),
            value => restored = value);

        Assert.False(result.Succeeded);
        Assert.IsType<IOException>(result.Error);
        Assert.True(restored);
    }
}
