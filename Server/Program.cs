using System.Net;
using System.Net.Sockets;
using System.Text;
using serverprogram;

var server = new Server();
await server.Start();

class Client
{
    protected internal string Guid { get; } = System.Guid.NewGuid().ToString();
    public string Username { get; set; }
    protected internal StreamWriter Writer { get; }
    protected internal StreamReader Reader { get; }

    private TcpClient _client;
    private Server _server;

    public Client(TcpClient tcpClient, Server serverObject)
    {
        _client = tcpClient;
        _server = serverObject;

        var stream = _client.GetStream();
        Reader = new StreamReader(stream, Encoding.Unicode);
        Writer = new StreamWriter(stream, Encoding.Unicode);
    }

    public async Task ProcessAsync()
    {
        try
        {
            string? userName;
            User? user = null;
            int attempts = 0;

            await Writer.WriteLineAsync("enter username:");
            await Writer.FlushAsync();
            
            while (true)
            {
                string? username = await Reader.ReadLineAsync();
                Console.WriteLine($"received username: '{username}'");

                if (string.IsNullOrWhiteSpace(username))
                {
                    await Writer.WriteLineAsync("username cannot be empty");
                    await Writer.FlushAsync();
                    continue;
                }

                user = UserManager.GetUser(username);

                if (user != null)
                {
                    while (attempts < 3)
                    {
                        await Writer.WriteLineAsync("enter password:");
                        await Writer.FlushAsync();

                        string? password = await Reader.ReadLineAsync();
                        if (password == user.Password) break;

                        attempts++;
                        await Writer.WriteLineAsync($"wrong password ({attempts}/3)");
                        await Writer.FlushAsync();
                    }

                    if (attempts >= 3)
                    {
                        await Writer.WriteLineAsync("too many wrong attempts. disconnecting...");
                        await Writer.FlushAsync();
                        Close();
                        return;
                    }

                    break;
                }
                else
                {
                    await Writer.WriteLineAsync("user not found. register? (y/n)");
                    await Writer.FlushAsync();

                    string? answer = await Reader.ReadLineAsync();
                    if (answer?.ToLower() == "y")
                    {
                        await Writer.WriteLineAsync("enter password:");
                        await Writer.FlushAsync();
                        string? newPassword = await Reader.ReadLineAsync();

                        UserManager.AddUser(username, newPassword);
                        user = UserManager.GetUser(username);
                        break;
                    }
                    else
                    {
                        await Writer.WriteLineAsync("enter a different username");
                        await Writer.FlushAsync();
                        continue;
                    }
                }
            }

            await Writer.WriteLineAsync("ok");
            await Writer.FlushAsync();
            userName = user!.Username;

            var otherUsers = _server.GetUsernamesExcept(Username);
            string userList = otherUsers.Any() ? string.Join(", ", otherUsers) : "no other users online.";
            await Writer.WriteLineAsync("users online: " + userList);
            await Writer.FlushAsync();
            await Writer.WriteLineAsync("welcome to the chat.");
            await Writer.FlushAsync();

            string? message = $"{userName} entered chat";
            Username = userName;
            _server.AddClient(this);

            try
            {
                while (true)
                {
                    message = await Reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(message)) continue;
                    Console.WriteLine(message);

                    if (message.StartsWith("->"))
                    {
                        string pmContent = message.Substring(2).Trim();

                        int colonIndex = pmContent.IndexOf(':');
                        if (colonIndex > 0)
                        {
                            string targetUsersStr = pmContent.Substring(0, colonIndex).Trim();
                            string pmMessage = pmContent.Substring(colonIndex + 1).Trim();

                            var targets = targetUsersStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(t => t.Trim())
                                .ToArray();

                            var connectedTargets = targets.Where(t => _server.IsUsernameTaken(t)).ToArray();

                            if (connectedTargets.Length == 0)
                            {
                                await Writer.WriteLineAsync("no valid target users connected");
                                await Writer.FlushAsync();
                                continue;
                            }

                            string fullMessage =
                                $"(private) {Username} -> {string.Join(", ", connectedTargets)}: {pmMessage}";

                            await _server.PrivateMessageAsync(fullMessage, connectedTargets);

                            continue;
                        }
                    }

                    message = $"{Username}:{message}";
                    await _server.BroadcastMessageAsync(message, Username);
                }
            }
            catch
            {
                message = $"{Username} left chat";
                Console.WriteLine(message);
                await _server.BroadcastMessageAsync(message, Username);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
        finally
        {
            _server.RemoveClient(Username);
        }
    }

    protected internal void Close()
    {
        Writer.Close();
        Reader.Close();
        _client.Close();
    }
}

class Server
{
    private TcpListener _tcpListener = new(IPAddress.Any, 11000);
    private Dictionary<string, Client> _clients = new();

    protected internal async Task BroadcastMessageAsync(string message, string id)
    {
        foreach (var (_, client) in _clients)
        {
            if (client.Username != id)
            {
                await client.Writer.WriteLineAsync(message);
                await client.Writer.FlushAsync();
            }
        }
    }

    protected internal async Task PrivateMessageAsync(string message, string[] usernames)
    {
        foreach (var (name, client) in _clients)
        {
            if (usernames.Contains(name))
            {
                await client.Writer.WriteLineAsync(message);
                await client.Writer.FlushAsync();
            }
        }
    }

    protected internal bool IsUsernameTaken(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName)) return true;
        return _clients.ContainsKey(userName);
    }

    public List<string> GetUsernamesExcept(string username)
    {
        return _clients.Keys.Where(u => u != username).ToList();
    }

    protected internal void AddClient(Client client)
    {
        _clients.Add(client.Username, client);
    }

    protected internal void RemoveClient(string username)
    {
        if (_clients.TryGetValue(username, out var client))
        {
            _clients.Remove(username);
            client.Close();
        }
    }


    protected internal async Task Start()
    {
        try
        {
            _tcpListener.Start();
            Console.WriteLine("server open");

            while (true)
            {
                TcpClient tcpClient = await _tcpListener.AcceptTcpClientAsync();

                Client clientObject = new Client(tcpClient, this);
                Task.Run(clientObject.ProcessAsync);
            }
        }

        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            DisconnectAll();
        }
    }

    protected internal void DisconnectAll()
    {
        foreach (var (_, client) in _clients)
        {
            client.Close();
        }

        _tcpListener.Stop();
    }
}