using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;

namespace KTMConnectedMaui;

public class BluetoothManager
{
    private readonly IAdapter _adapter;
    private IDevice? _device;
    private readonly Guid _ktmUuid = Guid.Parse("cc4c1fb3-482e-4389-bdeb-57b7aac889ae");

    public BluetoothManager()
    {
        _adapter = CrossBluetoothLE.Current.Adapter;
    }

    public bool IsConnected => _device != null && _adapter.ConnectedDevices.Contains(_device);

    public async Task ConnectAsync()
    {
        var devices = await _adapter.GetSystemConnectedOrPairedDevicesAsync();
        _device = devices.FirstOrDefault(d => d.Name?.Contains("KTM") == true || d.Name?.Contains("LC8") == true);
        if (_device != null)
        {
            await _adapter.ConnectToDeviceAsync(_device);
        }
    }

    public async Task SendAsync(byte[]? data)
    {
        if (_device == null || data == null) return;
        var service = await _device.GetServiceAsync(_ktmUuid);
        if (service == null) return;
        var characteristic = (await service.GetCharacteristicsAsync()).FirstOrDefault();
        if (characteristic != null && characteristic.CanWrite)
        {
            await characteristic.WriteAsync(data);
        }
    }

    public async Task CloseAsync()
    {
        if (_device != null)
        {
            await _adapter.DisconnectDeviceAsync(_device);
            _device = null;
        }
    }
}
