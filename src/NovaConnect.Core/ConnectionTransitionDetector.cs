namespace NovaConnect.Core;

public enum ConnectionTransition
{
    None,
    Connected,
    Disconnected
}

public sealed class ConnectionTransitionDetector(bool emitInitialState)
{
    private bool? _lastConnected;
    private bool? _stateBeforeLastObservation;

    public ConnectionTransition Observe(bool isConnected)
    {
        _stateBeforeLastObservation = _lastConnected;
        if (_lastConnected is null)
        {
            _lastConnected = isConnected;
            if (!emitInitialState)
            {
                return ConnectionTransition.None;
            }

            return isConnected ? ConnectionTransition.Connected : ConnectionTransition.Disconnected;
        }

        if (_lastConnected.Value == isConnected)
        {
            return ConnectionTransition.None;
        }

        _lastConnected = isConnected;
        return isConnected ? ConnectionTransition.Connected : ConnectionTransition.Disconnected;
    }

    public void RollbackLastObservation()
    {
        _lastConnected = _stateBeforeLastObservation;
    }
}
