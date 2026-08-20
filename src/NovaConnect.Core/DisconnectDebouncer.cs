namespace NovaConnect.Core;

public sealed class DisconnectDebouncer
{
    private readonly int _requiredDisconnectedSamples;
    private int _disconnectedSamples;

    public DisconnectDebouncer(int requiredDisconnectedSamples)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(requiredDisconnectedSamples, 1);
        _requiredDisconnectedSamples = requiredDisconnectedSamples;
    }

    public void Reset() => _disconnectedSamples = 0;

    public bool? Observe(bool isConnected)
    {
        if (isConnected)
        {
            _disconnectedSamples = 0;
            return true;
        }

        _disconnectedSamples++;
        return _disconnectedSamples >= _requiredDisconnectedSamples ? false : null;
    }
}
