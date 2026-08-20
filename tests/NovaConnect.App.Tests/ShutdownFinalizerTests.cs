using NovaConnect.App;
using Xunit;

namespace NovaConnect.App.Tests;

public sealed class ShutdownFinalizerTests
{
    [Fact]
    public void Run_LogFailure_StillExits()
    {
        bool exited = false;

        ShutdownFinalizer.Run(
            () => throw new IOException("simulated log failure"),
            () => exited = true);

        Assert.True(exited);
    }
}
