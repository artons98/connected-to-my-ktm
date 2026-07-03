using ExternalAccessory;

namespace KTMConnectedMaui;

public partial class MainPage : ContentPage
{
    private readonly BluetoothManager _bluetooth = new();
    private int _msgId;

    public MainPage()
    {
        InitializeComponent();
        Log($"Gedeclareerde protocollen: {string.Join(", ", BluetoothManager.DeclaredProtocols)}");
    }

    private void Log(string line)
    {
        LogLabel.Text = $"{DateTime.Now:HH:mm:ss} {line}\n{LogLabel.Text}";
    }

    private void OnShowAccessoriesClicked(object sender, EventArgs e)
    {
        foreach (var line in BluetoothManager.DescribeAccessories())
            Log(line);
    }

    // Opens the iOS system picker so a new Bluetooth accessory can be paired
    // without leaving the app.
    private void OnPairNewClicked(object sender, EventArgs e)
    {
        EAAccessoryManager.SharedAccessoryManager.ShowBluetoothAccessoryPicker(null!, error =>
            MainThread.BeginInvokeOnMainThread(() =>
                Log(error == null ? "Picker gesloten." : $"Picker: {error.LocalizedDescription}")));
    }

    private async void OnConnectClicked(object sender, EventArgs e)
    {
        if (_bluetooth.IsConnected)
        {
            _bluetooth.Close();
            SetConnectedUi(false);
            Log("Verbinding gesloten.");
            return;
        }

        // No name filtering — list everything iOS sees and let the user pick.
        var accessories = BluetoothManager.Accessories;
        if (accessories.Length == 0)
        {
            Log("Geen MFi-accessoires gevonden. Koppel het dashboard eerst (via Instellingen > Bluetooth of de picker-knop).");
            return;
        }

        var chosen = accessories[0];
        if (accessories.Length > 1)
        {
            var names = accessories.Select((a, i) => $"{i + 1}. {a.Name}").ToArray();
            var pick = await DisplayActionSheet("Kies apparaat", "Annuleer", null, names);
            var index = Array.IndexOf(names, pick);
            if (index < 0) return;
            chosen = accessories[index];
        }

        ConnectButton.IsEnabled = false;
        var result = await _bluetooth.ConnectAsync(chosen);
        Log(result);
        if (_bluetooth.IsConnected)
        {
            SetConnectedUi(true);
            // Like the proven Android client: push a default/Restore frame on connect
            // so the dash draws the nav UI.
            _msgId = 0;
            await Send(new SendingObject(), "restore na verbinden");
        }
        ConnectButton.IsEnabled = true;
    }

    private void SetConnectedUi(bool connected)
    {
        StatusLabel.Text = connected
            ? $"Verbonden: {_bluetooth.ConnectedAccessoryName} ({_bluetooth.ConnectedProtocol})"
            : "Niet verbonden";
        ConnectButton.Text = connected ? "Verbinding verbreken" : "Verbinden";
        SimNavButton.IsEnabled = connected;
        SimCameraButton.IsEnabled = connected;
    }

    private async Task<bool> Send(SendingObject obj, string description)
    {
        var ok = await _bluetooth.SendAsync(obj.GetBytes(_msgId++));
        Log(ok ? $"Verstuurd: {description}" : $"Versturen mislukt: {description}");
        if (!ok && !_bluetooth.IsConnected) SetConnectedUi(false);
        return ok;
    }

    // Replays a realistic guidance sequence the way the official app signals it:
    // gon (guidance on) -> lup (location updates counting down) -> mup (new maneuver)
    // -> goff (back to default).
    private async void OnSimulateNavigationClicked(object sender, EventArgs e)
    {
        SimNavButton.IsEnabled = false;
        Log("Navigatiesimulatie gestart…");

        var turn = new SendingObject
        {
            UiContext = "guidance",
            MsgIdPrefix = "gon",
            TurnIcon = "QUITE_RIGHT",
            TurnDist = "500",
            TurnDistUnit = "m",
            TurnRoad = "Teststraat",
            TurnInfo = "",
            Eta = DateTime.Now.AddMinutes(18).ToString("HH:mm"),
            Dist2Target = "12 km, 18 min",
        };
        if (!await Send(turn, "gon: rechtsaf over 500 m")) { SimNavButton.IsEnabled = true; return; }

        foreach (var dist in new[] { "400", "300", "200", "100" })
        {
            await Task.Delay(1500);
            turn.MsgIdPrefix = "lup";
            turn.TurnDist = dist;
            if (!await Send(turn, $"lup: nog {dist} m")) { SimNavButton.IsEnabled = true; return; }
        }

        await Task.Delay(1500);
        turn.MsgIdPrefix = "mup";
        turn.TurnIcon = "RAB_SECT_8_RH";
        turn.TurnDist = "250";
        turn.TurnRoad = "Rotondeweg";
        turn.TurnInfo = "3e afslag";
        if (!await Send(turn, "mup: rotonde, 3e afslag")) { SimNavButton.IsEnabled = true; return; }

        await Task.Delay(3000);
        await Send(new SendingObject { MsgIdPrefix = "goff" }, "goff: einde navigatie");
        Log("Navigatiesimulatie klaar.");
        SimNavButton.IsEnabled = true;
    }

    // The protocol has no dedicated speed-camera widget; the NotificationText line is
    // the only free-text channel, so a camera alert is simulated there.
    private async void OnSimulateSpeedCameraClicked(object sender, EventArgs e)
    {
        SimCameraButton.IsEnabled = false;
        var alert = new SendingObject
        {
            UiContext = "guidance",
            MsgIdPrefix = "lup",
            NotificationText = "Flitspaal over 300 m",
            TurnIcon = "GO_STRAIGHT",
            TurnDist = "300",
            TurnDistUnit = "m",
            TurnRoad = "Teststraat",
        };
        if (await Send(alert, "flitspaalmelding"))
        {
            await Task.Delay(5000);
            alert.NotificationText = null;
            await Send(alert, "flitspaalmelding wissen");
        }
        SimCameraButton.IsEnabled = true;
    }
}
