using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Common;

public class Client
{
    private ClientWebSocket? webSocket;
    private ClientGUI gui;
    private string? username;
    private string currentLine = string.Empty;

    public void Start(string uri = "ws://localhost:5000/")
    {
        // Connect to the server
        Console.WriteLine($"Connecting to {uri} ...");
        webSocket = new ClientWebSocket();
        webSocket.ConnectAsync(new Uri(uri), CancellationToken.None).Wait();
        Console.WriteLine("Connection successful.");

        // Send username
        string generatedUserName = $"user{new Random().Next(10, 99)}";
        Console.WriteLine($"Enter username or press enter to accept {generatedUserName}: ");
        username = Console.ReadLine()!;
        if (username == string.Empty)
        {
            username = generatedUserName;
        }
        webSocket.SendAsync(Encoding.UTF8.GetBytes(username), WebSocketMessageType.Text, true, CancellationToken.None).Wait();

        gui = new ClientGUI(username);
        gui.Invalidate(RoomState.Empty);

        Task.WaitAll(ListenRoutine(), SendRoutine());

        Console.WriteLine("Client finished");
    }

    private async Task ListenRoutine()
    {
        try
        {
            while (webSocket!.State == WebSocketState.Open)
            {
                byte[] buffer = new byte[1024];
                WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Console.WriteLine($"Server closed the connection. status=\"{result.CloseStatus}\", description=\"{result.CloseStatusDescription}\"");
                    continue;
                }
                
                string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                RoomState roomState = RoomState.Deserialize(message)!;
                gui.Invalidate(roomState);
                Console.Write(currentLine); // in case we invalidated mid-writing
            }
        }
        catch (WebSocketException)
        {
            Console.WriteLine("Lost connection to the server.");
        }
        catch (Exception e)
        {
            Console.WriteLine("An Exception occured. See details below:");
            Console.WriteLine(e);
        }
    }

    private async Task SendRoutine()
    {
        while (webSocket!.State == WebSocketState.Open)
        {
            // this abomination is necessary to inform the server when we start and stop typing...
            string messageBody = InputLoop((str) =>
            {
                byte[] bytes = Encoding.UTF8.GetBytes(str);
                if (webSocket.State != WebSocketState.Open)
                {
                    return;
                }
                webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None).Wait(); // note: blocking wait
            });
            if (messageBody.Trim() == string.Empty)
            {
                continue;
            }
            string message = $"{username}|message|{messageBody}";
            if (webSocket.State != WebSocketState.Open)
            {
                break;
            }
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            await webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }

    private string InputLoop(Action<string> sendToServer)
    {
        StringBuilder sb = new();
        int sbCountLastLoopRun = 0;
        while (webSocket!.State == WebSocketState.Open)
        {
            if (Console.KeyAvailable == false)
            {
                continue;
            }
            
            ConsoleKeyInfo keyInfo = Console.ReadKey(true);
            if (IsValidCharacter(keyInfo.KeyChar))
            {
                sb.Append(keyInfo.KeyChar);
                currentLine = sb.ToString();
                Console.Write(keyInfo.KeyChar);
            }
            else if (keyInfo.Key == ConsoleKey.Backspace)
            {
                if (sb.Length > 0)
                {
                    sb.Remove(sb.Length - 1, 1);
                    currentLine = sb.ToString();

                    // Console.Write("\x1B[1D"); // Move the cursor one unit to the left
                    // Console.Write("\x1B[1P"); // Delete the character
                    Console.Write("\b \b"); 
                }
            }
            else if (keyInfo.Key == ConsoleKey.Enter)
            {
                if (sb.Length > 0)
                {
                    sendToServer($"{username}|stoppedTyping");
                    currentLine = string.Empty;
                }

                break;
            }

            if (sbCountLastLoopRun == 0 && sb.Length > 0)
            {
                sendToServer($"{username}|startedTyping");
            }
            else if (sbCountLastLoopRun > 0 && sb.Length == 0)
            {
                sendToServer($"{username}|stoppedTyping");
            }

            sbCountLastLoopRun = sb.Length;
        }
        
        return sb.ToString(); 
    }

    private bool IsValidCharacter(char c)
    {
        // NOTE: "|" is not a valid character, it's a message separator
        const string validCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 !?.,:;-_()\"'";
        
        return validCharacters.Contains(c);
    }
}