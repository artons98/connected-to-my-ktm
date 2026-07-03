using ExternalAccessory;
using Foundation;

namespace KTMConnectedMaui;

// iOS can only reach the dash's Classic-BT serial channel via ExternalAccessory (MFi).
// A session opens only if the dash advertises a protocol string that is ALSO declared
// in Info.plist (UISupportedExternalAccessoryProtocols). KTM's real string is unknown,
// so this class doubles as a diagnostic: DescribeAccessories() dumps every accessory
// and its protocol strings so the real value can be captured and added to Info.plist.
public class BluetoothManager
{
    private EASession? _session;
    private EAAccessory? _accessory;

    public bool IsConnected => _session != null;
    public string? ConnectedAccessoryName => _accessory?.Name;
    public string? ConnectedProtocol { get; private set; }

    public static string[] DeclaredProtocols =>
        (NSBundle.MainBundle.ObjectForInfoDictionary("UISupportedExternalAccessoryProtocols")
            as NSArray) is { } arr
            ? Enumerable.Range(0, (int)arr.Count).Select(i => arr.GetItem<NSString>((nuint)i).ToString()).ToArray()
            : Array.Empty<string>();

    // Diagnostic dump: every MFi accessory iOS currently sees, with its protocol strings.
    // If the KTM dash is paired but absent here, iOS is not exposing an iAP data channel
    // for it and the ExternalAccessory route is dead. If it IS here, the listed protocol
    // strings are the missing puzzle piece — add them to Info.plist and rebuild.
    public static List<string> DescribeAccessories()
    {
        var accessories = EAAccessoryManager.SharedAccessoryManager.ConnectedAccessories;
        if (accessories.Length == 0)
            return new List<string> { "Geen MFi-accessoires zichtbaar. Is het dashboard gekoppeld via Instellingen > Bluetooth?" };
        return accessories.Select(a =>
            $"• {a.Name} ({a.Manufacturer}, model {a.ModelNumber})\n  protocollen: " +
            (a.ProtocolStrings.Length > 0 ? string.Join(", ", a.ProtocolStrings) : "(geen)"))
            .ToList();
    }

    // All MFi accessories iOS currently sees — no name filtering; the user picks.
    public static EAAccessory[] Accessories =>
        EAAccessoryManager.SharedAccessoryManager.ConnectedAccessories;

    // Connects to the user-chosen accessory, on the first protocol string it shares
    // with Info.plist. Returns a human-readable result line for the on-screen log.
    public Task<string> ConnectAsync(EAAccessory accessory)
    {
        Close();
        var protocol = accessory.ProtocolStrings.FirstOrDefault(DeclaredProtocols.Contains);
        if (protocol == null)
            return Task.FromResult(
                $"'{accessory.Name}' adverteert geen gedeclareerd protocol. Gevonden: " +
                (accessory.ProtocolStrings.Length > 0 ? string.Join(", ", accessory.ProtocolStrings) : "(geen)") +
                " — voeg toe aan Info.plist en herbouw.");
        try
        {
            var session = new EASession(accessory, protocol);
            if (session.OutputStream == null)
                return Task.FromResult($"Sessie geweigerd voor '{accessory.Name}' ({protocol}) — vermoedelijk MFi-blokkade.");
            session.OutputStream.Schedule(NSRunLoop.Main, NSRunLoopMode.Default);
            session.OutputStream.Open();
            _session = session;
            _accessory = accessory;
            ConnectedProtocol = protocol;
            return Task.FromResult($"Verbonden met '{accessory.Name}' via {protocol}");
        }
        catch (Exception e)
        {
            return Task.FromResult($"Sessie naar '{accessory.Name}' ({protocol}) mislukt: {e.Message}");
        }
    }

    public async Task<bool> SendAsync(byte[]? data)
    {
        if (data == null || _session?.OutputStream is not { } stream) return false;
        int offset = 0;
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (offset < data.Length)
        {
            if (DateTime.UtcNow > deadline) return false;
            if (!stream.HasSpaceAvailable())
            {
                await Task.Delay(20);
                continue;
            }
            var chunk = offset == 0 ? data : data[offset..];
            nint written = stream.Write(chunk, (nuint)chunk.Length);
            if (written < 0) return false;
            offset += (int)written;
        }
        return true;
    }

    public void Close()
    {
        try
        {
            _session?.OutputStream?.Close();
            _session?.OutputStream?.Unschedule(NSRunLoop.Main, NSRunLoopMode.Default);
            _session?.InputStream?.Close();
        }
        catch
        {
            // ignore
        }
        _session = null;
        _accessory = null;
        ConnectedProtocol = null;
    }
}
