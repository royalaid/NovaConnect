using NovaConnect.Core;
using Xunit;

namespace NovaConnect.Core.Tests;

public sealed class DisconnectDebouncerTests
{
    [Fact]
    public void Observe_SingleDisconnectedSample_WaitsForConfirmation()
    {
        var debouncer = new DisconnectDebouncer(requiredDisconnectedSamples: 2);

        bool? connected = debouncer.Observe(isConnected: true);
        bool? firstDisconnect = debouncer.Observe(isConnected: false);
        bool? confirmedDisconnect = debouncer.Observe(isConnected: false);

        Assert.True(connected);
        Assert.Null(firstDisconnect);
        Assert.False(confirmedDisconnect);
    }
}
