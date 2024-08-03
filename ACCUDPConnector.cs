using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace Sim.AssettoCorsaCompetizione;

public class ACCUDPConnector
{
    private string udpUrl = "127.0.0.1";
    private int udpPort;
    private string udpCommandPassword;
    private string udpConnectionPassword;
    private AccClient client;
    public AccClient Client => client;
    public EventHandler<UDPState> OnConnectionStateChange;
    public EventHandler<TrackData> OnTrackDataUpdate;
    public EventHandler<EntryListCar> OnEntryListCarUpdate;
    public EventHandler<RealtimeUpdate> OnRealtimeUpdate;
    public EventHandler<RealtimeCarUpdate> OnRealtimeCarUpdate;
    public EventHandler<BroadcastingEvent> OnBroadcastingEvent;
    public UDPState ConnectionState => client?.ConnectionState ?? UDPState.Disconnected;
    
    public ACCUDPConnector(Action<string> logger)
    {
        if (!GetUdpCredentials())
            return;

        client = new AccClient(logger);

        client.OnConnectionStateChange += (sender, args) => OnConnectionStateChange?.Invoke(sender, args);
        client.OnTrackDataUpdate += (sender, args) => OnTrackDataUpdate?.Invoke(sender, args);
        client.OnEntryListCarUpdate += (sender, args) => OnEntryListCarUpdate?.Invoke(sender, args);
        client.OnRealtimeUpdate += (sender, args) => OnRealtimeUpdate?.Invoke(sender, args);
        client.OnRealtimeCarUpdate += (sender, args) => OnRealtimeCarUpdate?.Invoke(sender, args);
        client.OnBroadcastingEvent += (sender, args) => OnBroadcastingEvent?.Invoke(sender, args);
    }

    public void Start()
    {
        client.Start(udpUrl, udpPort, udpConnectionPassword);
    }

    public void Control()
    {
        if (ConnectionState != UDPState.Connected)
            return;
        
        client.RequestFocusChange(carIndex: 0, cameraSet: "drivable", camera: "Bonnet"); // Car focus and camera
        client.RequestHudPage("Help");
        client.RequestInstantReplay(startTime: 42, durationMs: 10000);
    }

    public void Stop()
    {
        if (ConnectionState == UDPState.Disconnected)
            return;
        
        client.Stop();
    }

    private bool GetUdpCredentials()
    {
        var broadcastingFilePath = "N/A";
        var broadcastingJson = "N/A";
        
        try
        {
            broadcastingFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Assetto Corsa Competizione", "Config", "broadcasting.json");
            if (!File.Exists(broadcastingFilePath))
            {
                broadcastingJson = "{\n    \"updListenerPort\": 9000,\n    \"connectionPassword\": \"asd\",\n    \"commandPassword\": \"\"\n}";
                File.WriteAllText(broadcastingFilePath, broadcastingJson);
                MessageBox.Show("Please restart ACC to take full effect of the data.\n\nbroadcasting.json file created with default values.", "Missing broadcast.json file in ACC documents");
                return false;
            }

            broadcastingJson = File.ReadAllText(broadcastingFilePath, Encoding.Unicode);
            var broadcasting = JObject.Parse(broadcastingJson);
            udpPort = (broadcasting["updListenerPort"] ?? 0).Value<int>();
            udpConnectionPassword = broadcasting["connectionPassword"]?.Value<string>();
            udpCommandPassword = broadcasting["commandPassword"]?.Value<string>();
            return true;
        }
        catch (Exception ex)
        {
            ex.Data.Add("broadcastingFilePath", broadcastingFilePath);
            ex.Data.Add("broadcastingJson", broadcastingJson);
            throw;
        }
    }
    
    private Encoding DetectEncoding(byte[] bytes)
    {
        // Check for UTF-8 BOM
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8;

        // Check for UTF-16 LE BOM
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode;

        // Check for UTF-16 BE BOM
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode;

        // If no BOM is detected, default to UTF-8
        return Encoding.UTF8;
    }
}