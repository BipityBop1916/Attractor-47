namespace serverprogram;
using System.Text.Json;

public class User
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public static class UserManager
{
    private const string FilePath = "../../../data/users.json";
    private static List<User> _users = new List<User>();

    public static void Load()
    {
        if (!File.Exists(FilePath)) return;
        var json = File.ReadAllText(FilePath);
        _users = JsonSerializer.Deserialize<List<User>>(json) ?? new List<User>();
    }

    public static void Save()
    {
        var json = JsonSerializer.Serialize(_users, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(FilePath, json);
    }

    public static User? GetUser(string username)
    {
        Console.WriteLine($"Looking for user: '{username}'");
        var user = _users.FirstOrDefault(u => u.Username.Equals(username.Trim(), StringComparison.OrdinalIgnoreCase));
        Console.WriteLine(user == null ? "User not found" : "User found");
        return user;
    }

    public static void AddUser(string username, string password)
    {
        _users.Add(new User { Username = username, Password = password });
        Save();
    }
}
