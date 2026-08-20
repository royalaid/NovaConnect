using System.Runtime.InteropServices;
using NovaConnect.Core;

namespace NovaConnect.App;

internal enum AudioFlow
{
    Playback,
    Capture
}

internal sealed record AudioEndpoint(
    string Id,
    string Name,
    AudioFlow Flow,
    bool IsActive);

internal static class CoreAudioService
{
    internal static IReadOnlyList<AudioEndpoint> EnumerateEndpoints()
    {
        var endpoints = new List<AudioEndpoint>();
        EnumerateFlow(EDataFlow.Render, AudioFlow.Playback, endpoints);
        EnumerateFlow(EDataFlow.Capture, AudioFlow.Capture, endpoints);
        return endpoints
            .OrderBy(endpoint => endpoint.Flow)
            .ThenByDescending(endpoint => endpoint.IsActive)
            .ThenBy(endpoint => endpoint.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    internal static string? GetDefaultEndpointId(AudioFlow flow) =>
        GetDefaultEndpointIds(flow).Multimedia;

    internal static DefaultEndpointSnapshot GetDefaultEndpointIds(AudioFlow flow)
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        try
        {
            EDataFlow nativeFlow = flow == AudioFlow.Playback ? EDataFlow.Render : EDataFlow.Capture;
            return new DefaultEndpointSnapshot(
                GetDefaultEndpointId(enumerator, nativeFlow, ERole.Console),
                GetDefaultEndpointId(enumerator, nativeFlow, ERole.Multimedia),
                GetDefaultEndpointId(enumerator, nativeFlow, ERole.Communications));
        }
        finally
        {
            Marshal.ReleaseComObject(enumerator);
        }
    }

    private static string? GetDefaultEndpointId(
        IMMDeviceEnumerator enumerator,
        EDataFlow flow,
        ERole role)
    {
        int hr = enumerator.GetDefaultAudioEndpoint(flow, role, out IMMDevice device);
        if (hr != 0 || device is null)
        {
            return null;
        }

        try
        {
            Marshal.ThrowExceptionForHR(device.GetId(out string id));
            return id;
        }
        finally
        {
            Marshal.ReleaseComObject(device);
        }
    }

    internal static void SetDefaultEndpoint(AudioFlow flow, string endpointId) =>
        SetDefaultEndpoints(flow, endpointId, Enum.GetValues<DefaultEndpointRole>());

    internal static void SetDefaultEndpoints(
        AudioFlow flow,
        string endpointId,
        IReadOnlyCollection<DefaultEndpointRole> roles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointId);
        if (roles.Count == 0)
        {
            return;
        }

        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        object? rawPolicy = null;
        try
        {
            if (!IsEndpointActive(enumerator, endpointId))
            {
                throw new InvalidOperationException($"Audio endpoint '{endpointId}' is not active.");
            }

            rawPolicy = new PolicyConfigClient();
            if (rawPolicy is not IPolicyConfig policy)
            {
                throw new NotSupportedException("This Windows version does not expose IPolicyConfig.");
            }

            EDataFlow nativeFlow = flow == AudioFlow.Playback ? EDataFlow.Render : EDataFlow.Capture;
            foreach (DefaultEndpointRole role in roles.Distinct())
            {
                ERole nativeRole = ToNativeRole(role);
                if (!IsDefaultEndpoint(enumerator, nativeFlow, nativeRole, endpointId))
                {
                    Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(endpointId, nativeRole));
                }

                if (!IsDefaultEndpoint(enumerator, nativeFlow, nativeRole, endpointId))
                {
                    throw new InvalidOperationException(
                        $"Windows did not apply endpoint '{endpointId}' for role '{role}'.");
                }
            }
        }
        finally
        {
            if (rawPolicy is not null)
            {
                Marshal.ReleaseComObject(rawPolicy);
            }

            Marshal.ReleaseComObject(enumerator);
        }
    }

    private static ERole ToNativeRole(DefaultEndpointRole role) => role switch
    {
        DefaultEndpointRole.Console => ERole.Console,
        DefaultEndpointRole.Multimedia => ERole.Multimedia,
        DefaultEndpointRole.Communications => ERole.Communications,
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    private static bool IsEndpointActive(IMMDeviceEnumerator enumerator, string endpointId)
    {
        int hr = enumerator.GetDevice(endpointId, out IMMDevice device);
        if (hr != 0)
        {
            return false;
        }

        try
        {
            return device.GetState(out AudioDeviceState state) == 0
                && state == AudioDeviceState.Active;
        }
        finally
        {
            Marshal.ReleaseComObject(device);
        }
    }

    private static bool IsDefaultEndpoint(
        IMMDeviceEnumerator enumerator,
        EDataFlow flow,
        ERole role,
        string endpointId)
    {
        int hr = enumerator.GetDefaultAudioEndpoint(flow, role, out IMMDevice device);
        if (hr != 0)
        {
            return false;
        }

        try
        {
            return device.GetId(out string id) == 0
                && string.Equals(id, endpointId, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Marshal.ReleaseComObject(device);
        }
    }

    private static void EnumerateFlow(
        EDataFlow nativeFlow,
        AudioFlow flow,
        ICollection<AudioEndpoint> endpoints)
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        IMMDeviceCollection? collection = null;
        try
        {
            int hr = enumerator.EnumAudioEndpoints(nativeFlow, AudioDeviceState.All, out collection);
            Marshal.ThrowExceptionForHR(hr);
            Marshal.ThrowExceptionForHR(collection.GetCount(out uint count));

            for (uint index = 0; index < count; index++)
            {
                Marshal.ThrowExceptionForHR(collection.Item(index, out IMMDevice device));
                try
                {
                    endpoints.Add(ReadEndpoint(device, flow));
                }
                finally
                {
                    Marshal.ReleaseComObject(device);
                }
            }
        }
        finally
        {
            if (collection is not null)
            {
                Marshal.ReleaseComObject(collection);
            }

            Marshal.ReleaseComObject(enumerator);
        }
    }

    private static AudioEndpoint ReadEndpoint(IMMDevice device, AudioFlow flow)
    {
        Marshal.ThrowExceptionForHR(device.GetId(out string id));
        Marshal.ThrowExceptionForHR(device.GetState(out AudioDeviceState state));
        Marshal.ThrowExceptionForHR(device.OpenPropertyStore(0, out IPropertyStore store));
        try
        {
            PropertyKey key = PropertyKey.DeviceFriendlyName;
            Marshal.ThrowExceptionForHR(store.GetValue(ref key, out PropVariant value));
            try
            {
                string name = value.GetString() ?? id;
                return new AudioEndpoint(id, name, flow, state == AudioDeviceState.Active);
            }
            finally
            {
                PropVariant.PropVariantClear(ref value);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(store);
        }
    }
}
