using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.Exceptions;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.EventArgs;

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
    public bool IsConnected => _device != null && _device.State == DeviceState.Connected;

    public async Task<bool> ConnectAsync()
    {
        try
        {
            IDevice? foundDevice = null;
            void OnDeviceDiscovered(object? sender, DeviceEventArgs args)
            {
                if (args.Device.Name?.Contains("KTM") == true || args.Device.Name?.Contains("LC8") == true)
                {
                    foundDevice = args.Device;
                }
            }

            _adapter.DeviceDiscovered += OnDeviceDiscovered;
            await _adapter.StartScanningForDevicesAsync();

            _adapter.DeviceDiscovered -= OnDeviceDiscovered;

            if (foundDevice != null)
            {
                _device = foundDevice;
                await _adapter.ConnectToDeviceAsync(_device);
                return IsConnected;
            }
            return false;
        }
        catch (DeviceConnectionException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> SendAsync(byte[]? data)
    {
        if (_device == null || data == null || !IsConnected) return false;
        
        try
        {
            var service = await _device.GetServiceAsync(_ktmUuid);
            if (service == null) return false;
            
            var characteristic = (await service.GetCharacteristicsAsync()).FirstOrDefault();
            if (characteristic != null && characteristic.CanWrite)
            {
                await characteristic.WriteAsync(data);
                return true;
            }
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task CloseAsync()
    {
        if (_device != null)
        {
            try
            {
                await _adapter.DisconnectDeviceAsync(_device);
            }
            catch (Exception)
            {
                // Ignore disconnect errors
            }
            finally
            {
                _device = null;
            }
        }
    }
}
