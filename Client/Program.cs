using Client;
using System.Net.Sockets;
using System.Text;

var client = new ChatClient();
await client.RunAsync();

internal class ChatClient
{
    public async Task RunAsync()
    {
        TcpClient? client = null;
        ServerConfig? config = ConfigManager.Load();

        if (config != null)
        {
            Console.WriteLine($"found saved config: {config.IP}:{config.Port}");
            client = await TryConnect(config.IP, config.Port);
        }

        if (client == null)
        {
            config = await PromptForConnection();
            if (config == null)
            {
                Console.WriteLine("exiting...");
                return;
            }

            client = await TryConnect(config.IP, config.Port);
            if (client == null)
            {
                Console.WriteLine("failed to connect");
                return;
            }

            ConfigManager.Save(config);
        }
        
        try
        {
            var networkStream = client.GetStream();
            var reader = new StreamReader(networkStream, Encoding.Unicode);
            var writer = new StreamWriter(networkStream, Encoding.Unicode);

            bool success = await PerformHandshake(reader, writer, client);
            if (!success)
            {
                Console.WriteLine("handshake failed (timeout or rejected)");
                client.Close();
                return;
            }
            
            var receiveTask = ReceiveMessagesAsync(reader);
            var sendTask = SendMessagesAsync(writer);
            await Task.WhenAny(receiveTask, sendTask);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }

        client.Close();
    }

    async Task SendMessagesAsync(StreamWriter writer)
    {
        Console.WriteLine("to send type and press enter");
        while (true)
        {
            string? message = Console.ReadLine();
            if (string.IsNullOrEmpty(message)) continue;

            await writer.WriteLineAsync(message);
            await writer.FlushAsync();
        }
    }

    async Task ReceiveMessagesAsync(StreamReader reader)
    {
        while (true)
        {
            try
            {
                string? message = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(message)) continue;

                string[] splitMessage = message.Split(":", 2);
                string timeStamp = DateTime.Now.ToString("HH:mm");

                Console.WriteLine($"{splitMessage[0]} ({timeStamp}): {splitMessage[1]}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                break;
            }
        }
    }
    
    async Task<TcpClient?> TryConnect(string ip, int port)
    {
        var client = new TcpClient();
        try
        {
            await client.ConnectAsync(ip, port);
            Console.WriteLine("connected successfully!");
            return client;
        }
        catch
        {
            Console.WriteLine("connection failed.");
            client.Dispose();
            return null;
        }
    }

    async Task<ServerConfig?> PromptForConnection()
    {
        while (true)
        {
            Console.Write("enter ip (default: localhost): ");
            string ip = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(ip)) ip = "localhost";

            Console.Write("enter port (default: 11000): ");
            string portStr = Console.ReadLine();
            if (!int.TryParse(portStr, out int port)) port = 11000;

            var config = new ServerConfig { IP = ip, Port = port };
            var test = await TryConnect(ip, port);

            if (test != null)
            {
                test.Dispose();
                return config;
            }

            Console.Write("try again? (y/n): ");
            string? retry = Console.ReadLine();
            if (retry == null || !retry.Equals("y", StringComparison.OrdinalIgnoreCase))
                return null;
        }
    }
    
    private async Task<bool> PerformHandshake(StreamReader reader, StreamWriter writer, TcpClient client)
    {
        var handshakeTask = Task.Run(async () =>
        {
            string userName;
            while (true)
            {
                Console.Write("enter username: ");
                userName = Console.ReadLine();

                await writer.WriteLineAsync(userName);
                await writer.FlushAsync();

                string response = await reader.ReadLineAsync();
                if (response == null) return false;

                if (response == "ok")
                    break;

                Console.WriteLine($"server: {response}");
            }

            string usersOnline = await reader.ReadLineAsync();
            Console.WriteLine(usersOnline);

            string welcome = await reader.ReadLineAsync();
            Console.WriteLine(welcome);

            return true;
        });

        var completed = await Task.WhenAny(handshakeTask, Task.Delay(10000));
        if (completed != handshakeTask)
        {
            client.Close();
            return false;
        }

        return await handshakeTask;
    }
}