using ExternalAccessory;
using Foundation;
using System.Linq;

namespace KTMConnectedMaui;

public class BluetoothManager
{
    private EASession? _session;
    private EAAccessory? _accessory;
    private const string MfiProtocol = "com.ktm.myride"; // replace with actual protocol if necessary

    public bool IsConnected => _session != null && _accessory != null;

    public Task<bool> ConnectAsync()
    {
        try
        {
            var manager = EAAccessoryManager.SharedAccessoryManager;
            foreach (var accessory in manager.ConnectedAccessories)
            {
                if (IsKtmDevice(accessory.Name) && accessory.ProtocolStrings.Contains(MfiProtocol))
                {
                    _accessory = accessory;
                    _session = new EASession(accessory, MfiProtocol);
                    _session.InputStream?.Open();
                    _session.OutputStream?.Open();
                    return Task.FromResult(IsConnected);
                }
            }
            return Task.FromResult(false);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private static bool IsKtmDevice(string? name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return name.Contains("KTM") || name.Contains("LC8");
    }

    public Task<bool> SendAsync(byte[]? data)
    {
        if (data == null || !IsConnected) return Task.FromResult(false);
        try
        {
            var written = _session!.OutputStream.Write(data, (nint)data.Length);
            return Task.FromResult(written == data.Length);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task CloseAsync()
    {
        try
        {
            _session?.InputStream?.Close();
            _session?.OutputStream?.Close();
        }
        catch
        {
            // ignore
        }
        _session = null;
        _accessory = null;
        return Task.CompletedTask;
    }
}
