namespace KTMConnectedMaui;

public partial class MainPage : ContentPage
{
    private readonly BluetoothManager _bluetooth = new();

    public MainPage()
    {
        InitializeComponent();
    }

    private async void OnConnectClicked(object sender, EventArgs e)
    {
        if (_bluetooth.IsConnected)
        {
            await _bluetooth.CloseAsync();
            StatusLabel.Text = "Disconnected";
            ConnectButton.Text = "Connect";
        }
        else
        {
            StatusLabel.Text = "Connecting...";
            ConnectButton.IsEnabled = false;
            
            var connected = await _bluetooth.ConnectAsync();
            if (connected)
            {
                StatusLabel.Text = "Connected";
                ConnectButton.Text = "Disconnect";

                var obj = new SendingObject
                {
                    UiContext = "guidance",
                    TurnRoad = "Ready"
                };
                var sent = await _bluetooth.SendAsync(obj.GetBytes(0));
                if (!sent)
                {
                    StatusLabel.Text = "Connected but failed to send data";
                }
            }
            else
            {
                StatusLabel.Text = "Failed to connect";
            }
            
            ConnectButton.IsEnabled = true;
        }
    }
}
