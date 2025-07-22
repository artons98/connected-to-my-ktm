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
            await _bluetooth.ConnectAsync();
            if (_bluetooth.IsConnected)
            {
                StatusLabel.Text = "Connected";
                ConnectButton.Text = "Disconnect";

                var obj = new SendingObject
                {
                    UiContext = "guidance",
                    TurnRoad = "Ready"
                };
                await _bluetooth.SendAsync(obj.GetBytes(0));
            }
            else
            {
                StatusLabel.Text = "Failed to connect";
            }
        }
    }
}
