using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;

namespace KTMConnectedMaui;

public class BluetoothManager
{
    private BluetoothClient _client = new();
    private BluetoothDeviceInfo? _device;
    private readonly Guid _ktmUuid = Guid.Parse("cc4c1fb3-482e-4389-bdeb-57b7aac889ae");

    public bool IsConnected => _client?.Connected == true;

    public async Task<bool> ConnectAsync()
    {
        try
        {
            // Try paired devices first
            foreach (var dev in _client.PairedDevices)
            {
                if (IsKtmDevice(dev.DeviceName))
                {
                    _device = dev;
                    await _client.ConnectAsync(dev.DeviceAddress, _ktmUuid);
                    return IsConnected;
                }
            }

            // If none found, perform discovery
            var devices = await Task.Run(() => _client.DiscoverDevices());
            foreach (var dev in devices)
            {
                if (IsKtmDevice(dev.DeviceName))
                {
                    _device = dev;
                    await _client.ConnectAsync(dev.DeviceAddress, _ktmUuid);
                    return IsConnected;
                }
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsKtmDevice(string? name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return name.Contains("KTM") || name.Contains("LC8");
    }

    public async Task<bool> SendAsync(byte[]? data)
    {
        if (data == null || !IsConnected) return false;
        try
        {
            using var stream = _client.GetStream();
            await stream.WriteAsync(data, 0, data.Length);
            await stream.FlushAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public Task CloseAsync()
    {
        try
        {
            _client.Close();
        }
        catch
        {
            // ignore
        }
        _client = new BluetoothClient();
        _device = null;
        return Task.CompletedTask;
    }
}
