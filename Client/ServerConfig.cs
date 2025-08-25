namespace Client;

using System.Text.Json;

public class ServerConfig
{
    public string IP { get; set; }
    public int Port { get; set; }
}

public static class ConfigManager
{
    private const string ConfigFile = "../../../data/serverconfig.json";

    public static void Save(ServerConfig config)
    {
        var json = JsonSerializer.Serialize(config);
        File.WriteAllText(ConfigFile, json);
    }

    public static ServerConfig? Load()
    {
        if (!File.Exists(ConfigFile)) return null;

        try
        {
            string json = File.ReadAllText(ConfigFile);
            return JsonSerializer.Deserialize<ServerConfig>(json);
        }
        catch
        {
            return null;
        }
    }
}
