namespace NovaConnect.Core;

public enum Nova5ChargingState : byte
{
    Unknown = 0,
    Charging = 1,
    PluggedNotCharging = 2,
    OnBattery = 3
}

public readonly record struct Nova5Status(
    bool IsConnected,
    int BatteryPercent,
    Nova5ChargingState ChargingState);

public static class Nova5StatusParser
{
    public static Nova5Status Parse(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < 5)
        {
            throw new FormatException("Nova 5 status response must contain at least 5 bytes.");
        }

        if (packet[0] != 0xB0)
        {
            throw new FormatException($"Unexpected Nova 5 status opcode 0x{packet[0]:X2}.");
        }

        if (packet[1] is not (0x02 or 0x03))
        {
            throw new FormatException($"Unknown Nova 5 power state 0x{packet[1]:X2}.");
        }

        if (packet[3] > 100)
        {
            throw new FormatException($"Invalid Nova 5 battery percentage {packet[3]}.");
        }

        if (packet[4] > (byte)Nova5ChargingState.OnBattery)
        {
            throw new FormatException($"Unknown Nova 5 charging state 0x{packet[4]:X2}.");
        }

        bool isConnected = packet[1] == 0x03;
        return new Nova5Status(isConnected, packet[3], (Nova5ChargingState)packet[4]);
    }
}
