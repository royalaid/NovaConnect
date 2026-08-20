using HidSharp;
using NovaConnect.Core;

namespace NovaConnect.App;

internal static class Nova5HidProbe
{
    internal static async Task<Nova5Status> QueryOnceAsync(CancellationToken cancellationToken)
    {
        HidDevice device = FindControlInterface()
            ?? throw new InvalidOperationException(
                "SteelSeries Arctis Nova 5 HID control interface was not found.");

        var openConfiguration = new OpenConfiguration();
        openConfiguration.SetOption(OpenOption.Exclusive, false);
        using HidStream stream = device.Open(openConfiguration);
        stream.ReadTimeout = 1_000;

        int outputLength = device.GetMaxOutputReportLength();
        byte[] request = Nova5Protocol.BuildStatusRequest(outputLength);
        await stream.WriteAsync(request, 0, request.Length, cancellationToken).ConfigureAwait(false);

        int inputLength = Math.Max(64, device.GetMaxInputReportLength());
        return await ReadMatchingStatusAsync(
            (buffer, token) => stream.ReadAsync(buffer, 0, buffer.Length, token),
            inputLength,
            TimeSpan.FromSeconds(1),
            cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<Nova5Status> ReadMatchingStatusAsync(
        Func<byte[], CancellationToken, Task<int>> readAsync,
        int inputLength,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(readAsync);
        ArgumentOutOfRangeException.ThrowIfLessThan(inputLength, 5);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeoutCancellation.CancelAfter(timeout);
        var response = new byte[inputLength];

        try
        {
            while (true)
            {
                int read = await readAsync(response, timeoutCancellation.Token).ConfigureAwait(false);
                if (read == 0)
                {
                    throw new TimeoutException("Nova 5 status query returned no data.");
                }

                if (IsStatusResponse(response, read))
                {
                    return ParseResponse(response, read);
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Timed out waiting for the Nova 5 status response.");
        }
    }

    private static bool IsStatusResponse(byte[] response, int read) =>
        read > 0 && (response[0] == 0xB0
            || (read > 1 && response[0] == 0x00 && response[1] == 0xB0));

    private static Nova5Status ParseResponse(byte[] response, int read)
    {
        ReadOnlySpan<byte> packet = response.AsSpan(0, read);
        if (packet.Length >= 6 && packet[0] == 0x00 && packet[1] == 0xB0)
        {
            packet = packet[1..];
        }

        return Nova5StatusParser.Parse(packet);
    }

    private static HidDevice? FindControlInterface()
    {
        return DeviceList.Local
            .GetHidDevices(Nova5Protocol.VendorId, Nova5Protocol.ProductId)
            .FirstOrDefault(HasNova5ControlUsage);
    }

    private static bool HasNova5ControlUsage(HidDevice device)
    {
        try
        {
            return device.GetReportDescriptor().DeviceItems.Any(item =>
                item.Usages.GetAllValues().Any(usage =>
                    (usage >> 16) == Nova5Protocol.UsagePage
                    && (usage & 0xFFFF) == Nova5Protocol.Usage));
        }
        catch
        {
            return false;
        }
    }
}
