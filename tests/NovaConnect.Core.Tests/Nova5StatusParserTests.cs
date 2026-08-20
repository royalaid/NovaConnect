using NovaConnect.Core;
using Xunit;

namespace NovaConnect.Core.Tests;

public sealed class Nova5StatusParserTests
{
    [Fact]
    public void Parse_RealOfflinePacket_ReportsDisconnectedState()
    {
        byte[] packet =
        [
            0xB0, 0x02, 0x02, 0x0E, 0x03, 0x00, 0x00, 0x30,
            0x36, 0x30, 0x34, 0x32, 0x36, 0x32, 0x35, 0x30
        ];

        Nova5Status status = Nova5StatusParser.Parse(packet);

        Assert.False(status.IsConnected);
        Assert.Equal(14, status.BatteryPercent);
        Assert.Equal(Nova5ChargingState.OnBattery, status.ChargingState);
    }

    [Fact]
    public void Parse_OnlinePacket_ReportsConnectedState()
    {
        byte[] packet = [0xB0, 0x03, 0x00, 0x57, 0x01];

        Nova5Status status = Nova5StatusParser.Parse(packet);

        Assert.True(status.IsConnected);
        Assert.Equal(87, status.BatteryPercent);
        Assert.Equal(Nova5ChargingState.Charging, status.ChargingState);
    }

    [Fact]
    public void Parse_ShortPacket_RejectsMalformedResponse()
    {
        FormatException error = Assert.Throws<FormatException>(() => Nova5StatusParser.Parse([0xB0, 0x03]));

        Assert.Contains("at least 5 bytes", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(new byte[] { 0xA0, 0x03, 0x00, 50, 0x03 })]
    [InlineData(new byte[] { 0xB0, 0x04, 0x00, 50, 0x03 })]
    [InlineData(new byte[] { 0xB0, 0x03, 0x00, 101, 0x03 })]
    [InlineData(new byte[] { 0xB0, 0x03, 0x00, 50, 0x04 })]
    public void Parse_InvalidFields_RejectsMalformedResponse(byte[] packet)
    {
        Assert.Throws<FormatException>(() => Nova5StatusParser.Parse(packet));
    }

    [Fact]
    public void BuildStatusRequest_CreatesPaddedHidOutputReport()
    {
        byte[] request = Nova5Protocol.BuildStatusRequest(64);

        Assert.Equal(64, request.Length);
        Assert.Equal(0x00, request[0]);
        Assert.Equal(0xB0, request[1]);
        Assert.All(request[2..], value => Assert.Equal(0x00, value));
    }

    [Fact]
    public void BuildStatusRequest_ReportedOutputLength_UsesEntireReport()
    {
        byte[] request = Nova5Protocol.BuildStatusRequest(65);

        Assert.Equal(65, request.Length);
        Assert.Equal(0x00, request[0]);
        Assert.Equal(0xB0, request[1]);
    }
}
