using NovaConnect.App;
using NovaConnect.Core;
using Xunit;

namespace NovaConnect.App.Tests;

public sealed class Nova5HidProbeTests
{
    [Fact]
    [Trait("Category", "Hardware")]
    public async Task QueryOnceAsync_AttachedNova5_ReturnsValidatedStatus()
    {
        Nova5Status result = await Nova5HidProbe.QueryOnceAsync(CancellationToken.None);

        Assert.InRange(result.BatteryPercent, 0, 100);
        Assert.True(result.ChargingState is >= Nova5ChargingState.Unknown and <= Nova5ChargingState.OnBattery);
    }

    [Fact]
    public async Task ReadMatchingStatusAsync_IgnoresUnrelatedReportBeforeStatus()
    {
        var reports = new Queue<byte[]>(
        [
            [0x99, 0x01, 0x02, 0x03, 0x04],
            [0xB0, 0x03, 0x02, 64, 0x03]
        ]);
        int reads = 0;

        Task<int> ReadAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[] report = reports.Dequeue();
            report.CopyTo(buffer, 0);
            reads++;
            return Task.FromResult(report.Length);
        }

        Nova5Status result = await Nova5HidProbe.ReadMatchingStatusAsync(
            ReadAsync,
            inputLength: 64,
            timeout: TimeSpan.FromSeconds(1),
            CancellationToken.None);

        Assert.True(result.IsConnected);
        Assert.Equal(64, result.BatteryPercent);
        Assert.Equal(2, reads);
    }
}
