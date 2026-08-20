using NovaConnect.App;
using Xunit;

namespace NovaConnect.App.Tests;

public sealed class StartupToggleCoordinatorTests
{
    [Fact]
    public void Apply_PersistFailure_SuppressesReentrantCheckboxEvent()
    {
        var coordinator = new StartupToggleCoordinator();
        int persistCalls = 0;
        int restoreCalls = 0;
        StartupToggleResult nestedResult = default;

        StartupToggleResult result = coordinator.Apply(
            desired: true,
            persist: _ =>
            {
                persistCalls++;
                throw new UnauthorizedAccessException("denied");
            },
            restoreChecked: restored =>
            {
                restoreCalls++;
                nestedResult = coordinator.Apply(
                    restored,
                    _ => persistCalls++,
                    _ => restoreCalls++);
            });

        Assert.Equal(StartupToggleStatus.Failed, result.Status);
        Assert.Equal(StartupToggleStatus.Suppressed, nestedResult.Status);
        Assert.Equal(1, persistCalls);
        Assert.Equal(1, restoreCalls);
    }
}
