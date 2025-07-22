#if ANDROID
using Android.Bluetooth;
using Java.Util;
using System.IO;
using System.Threading.Tasks;
#endif

namespace KTMConnectedMaui;

public class BluetoothManager
{
#if ANDROID
    private readonly UUID _ktmUuid = UUID.FromString("cc4c1fb3-482e-4389-bdeb-57b7aac889ae");
    private BluetoothAdapter? _adapter;
    private BluetoothDevice? _device;
    private BluetoothSocket? _socket;
    private Stream? _outputStream;

    public bool IsConnected => _socket?.IsConnected == true;

    public Task<bool> ConnectAsync()
    {
        bool result = Connect();
        return Task.FromResult(result);
    }

    private bool Connect()
    {
        Close();
        _adapter = BluetoothAdapter.DefaultAdapter;
        if (_adapter?.IsEnabled != true)
            return false;

        foreach (var dev in _adapter.BondedDevices)
        {
            if (IsKtmDevice(dev.Name) && InitConnection(dev))
            {
                return true;
            }
        }
        return false;
    }

    private bool InitConnection(BluetoothDevice device)
    {
        try
        {
            _socket = device.CreateInsecureRfcommSocketToServiceRecord(_ktmUuid);
            _socket.Connect();
            _outputStream = _socket.OutputStream;
            _device = device;
            return true;
        }
        catch (Java.IO.IOException)
        {
            Close();
            return false;
        }
    }

    private static bool IsKtmDevice(string? name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return name.Contains("KTM") || name.Contains("LC8");
    }

    public Task<bool> SendAsync(byte[]? data)
    {
        if (data == null || _outputStream == null) return Task.FromResult(false);
        try
        {
            _outputStream.Write(data, 0, data.Length);
            _outputStream.Flush();
            return Task.FromResult(true);
        }
        catch (IOException)
        {
            Close();
            return Task.FromResult(false);
        }
    }

    public Task CloseAsync()
    {
        Close();
        return Task.CompletedTask;
    }

    private void Close()
    {
        try { _socket?.Close(); } catch { }
        try { _outputStream?.Close(); } catch { }
        _socket = null;
        _outputStream = null;
        _device = null;
        _adapter = null;
    }
#else
    public bool IsConnected => false;
    public Task<bool> ConnectAsync() => Task.FromResult(false);
    public Task<bool> SendAsync(byte[]? data) => Task.FromResult(false);
    public Task CloseAsync() => Task.CompletedTask;
#endif
}
