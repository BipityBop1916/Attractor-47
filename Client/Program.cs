using System.Net.Sockets;
using System.Text;

var client = new ChatClient();
await client.RunAsync();

internal class ChatClient
{
    public async Task RunAsync()
    {
        TcpClient? client = await ConnectAttempt();
        if (client == null) return;
        try
        {
            var networkStream = client.GetStream();
            var reader = new StreamReader(networkStream, Encoding.Unicode);
            var writer = new StreamWriter(networkStream, Encoding.Unicode);

            string userName;
            while (true)
            {
                Console.Write("enter username: ");
                userName = Console.ReadLine();

                await writer.WriteLineAsync(userName);
                await writer.FlushAsync();

                string response = await reader.ReadLineAsync();

                if (response == "ok") break;
                Console.WriteLine($"server: {response}");
            }

            string usersOnline = await reader.ReadLineAsync();
            Console.WriteLine(usersOnline);
            string welcome = await reader.ReadLineAsync();
            Console.WriteLine(welcome);
            var receiveTask = ReceiveMessagesAsync(reader);
            var sendTask = SendMessagesAsync(writer, userName);
            await Task.WhenAny(receiveTask, sendTask);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }

        client.Close();
    }

    async Task SendMessagesAsync(StreamWriter writer, string username)
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

    async Task<TcpClient?> ConnectAttempt()
    {
        while (true)
        {
            Console.Write("enter ip (default: localhost): ");
            string ip = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(ip)) ip = "localhost";

            Console.Write("enter port (default: 11000): ");
            string portStr = Console.ReadLine();
            int port;
            if (!int.TryParse(portStr, out port)) port = 11000;
            
            TcpClient client = new TcpClient();

            try
            {
                Console.WriteLine($"connecting to {ip}:{port}...");
                await client.ConnectAsync(ip, port);
                Console.WriteLine("connected");

                return client;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"connection failed: {ex.Message}");
                Console.Write("try again? (y/n): ");
                string? answer = Console.ReadLine();
                if (answer == null || !answer.Equals("y", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("exiting...");
                    return null;
                }
            }
        }
    }
}