using System.Text;
using System.Text.Json;

namespace KTMConnectedMaui;

public class SendingObject
{
    public string? TurnRoad { get; set; }
    public string? TurnDist { get; set; }
    public string? TurnDistUnit { get; set; }
    public string? TurnIcon { get; set; }
    public string? Eta { get; set; }
    public string? Dist2Target { get; set; }
    public string? TurnInfo { get; set; }
    public string? NotificationText { get; set; }
    public string GpsIcon { get; set; } = "GPS";
    public string UiContext { get; set; } = "default";

    private static JsonElement BuildIconJson(string? value)
    {
        var json = new { Image = value ?? "UNDEFINED", Visibility = value != null ? "full" : "off" };
        return JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(json));
    }

    private static JsonElement BuildTextJson(string? value)
    {
        var json = new { Text = value ?? string.Empty, Visibility = value != null ? "full" : "off" };
        return JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(json));
    }

    public string GetJson()
    {
        var obj = new Dictionary<string, object?>
        {
            ["UiContext"] = UiContext,
            ["UpdateUI"] = new Dictionary<string, object?>
            {
                ["TurnRoad"] = BuildTextJson(TurnRoad),
                ["TurnDist"] = BuildTextJson(TurnDist),
                ["TurnDistUnit"] = BuildTextJson(TurnDistUnit),
                ["TurnIcon"] = BuildIconJson(TurnIcon),
                ["GpsIcon"] = BuildIconJson(GpsIcon),
                ["Dist2Target"] = BuildTextJson(Dist2Target),
                ["ETA"] = BuildTextJson(Eta),
                ["TurnInfo"] = BuildTextJson(TurnInfo),
                ["NotificationText"] = BuildTextJson(NotificationText),
                ["NotificationIcon"] = new { Visibility = "off" }
            },
            ["MsgId"] = UiContext == "guidance" ? "gon" : "Restore"
        };
        return JsonSerializer.Serialize(obj).Replace("\\/", "/");
    }

    public byte[] GetBytes(int id)
    {
        var jsonDict = JsonSerializer.Deserialize<Dictionary<string, object>>(GetJson())!;
        jsonDict["MsgId"] = jsonDict["MsgId"] + "#" + id;
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(jsonDict).Replace("\\/", "/"));
        int length = bytes.Length + 1;
        var header = new byte[]
        {
            (byte)((length >> 24) & 0xFF),
            (byte)((length >> 16) & 0xFF),
            (byte)((length >> 8) & 0xFF),
            (byte)(length & 0xFF),
            1
        };
        return header.Concat(bytes).ToArray();
    }
}
