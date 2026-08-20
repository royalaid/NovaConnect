namespace NovaConnect.Core;

public static class Nova5Protocol
{
    public const int VendorId = 0x1038;
    public const int ProductId = 0x2232;
    public const int UsagePage = 0xFFC0;
    public const int Usage = 0x0001;
    public const int InterfaceNumber = 3;

    public static byte[] BuildStatusRequest(int outputReportLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(outputReportLength, 2);
        var request = new byte[outputReportLength];
        request[1] = 0xB0;
        return request;
    }
}
