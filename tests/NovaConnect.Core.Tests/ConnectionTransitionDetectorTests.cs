using NovaConnect.Core;
using Xunit;

namespace NovaConnect.Core.Tests;

public sealed class ConnectionTransitionDetectorTests
{
    [Fact]
    public void Observe_FirstSampleAndStateChange_EmitTransitionsOnlyWhenNeeded()
    {
        var detector = new ConnectionTransitionDetector(emitInitialState: true);

        Assert.Equal(ConnectionTransition.Connected, detector.Observe(isConnected: true));
        Assert.Equal(ConnectionTransition.None, detector.Observe(isConnected: true));
        Assert.Equal(ConnectionTransition.Disconnected, detector.Observe(isConnected: false));
        Assert.Equal(ConnectionTransition.None, detector.Observe(isConnected: false));
    }
}
