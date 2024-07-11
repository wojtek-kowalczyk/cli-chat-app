using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Common;

public class Server
{
    private readonly List<string> messages = new();
    private readonly List<User> users = new();
    private readonly List<WebSocket> activeConnections = new();
    
    public void Start()
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:5000/");
        listener.Start();
        Console.WriteLine("Listening...");

        while (true)
        {
            Console.WriteLine("Waiting for request to process...");
            HttpListenerContext context = listener.GetContext();
            Console.WriteLine("Got request");
            if (context.Request.IsWebSocketRequest)
            {
                Task.Run(() =>
                {
                    try
                    {
                        Console.WriteLine($"--- task {Environment.CurrentManagedThreadId} started");
                        ProcessRequest(context).Wait();
                        Console.WriteLine($"--- task {Environment.CurrentManagedThreadId} ended");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("An Exception occured when handling request. See details below:");
                        Console.WriteLine(e);
                    }
                });
            }
        }
    }

    private async Task ProcessRequest(HttpListenerContext context)
    {
        WebSocketContext wsContext = await context.AcceptWebSocketAsync(null);
        WebSocket webSocket = wsContext.WebSocket;
        activeConnections.Add(webSocket);
        
        try // TODO : could I use disposable somehow? or get it to close on error automatically?
        {
            await CommunicationRoutine(wsContext.WebSocket);
        }
        finally
        {
            try
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.InternalServerError, "An error occured when processing the request", CancellationToken.None);
            } 
            catch (WebSocketException)
            {
                // handle failure when client terminates the process - cannot handshake close
            }
            activeConnections.Remove(webSocket);
            Console.WriteLine("====================== socket closed");
        }
    }
    
    private async Task CommunicationRoutine(WebSocket webSocket)
    {
        Console.WriteLine("communication routine started");
        
        byte[] usernameBuffer = new byte[32];
        WebSocketReceiveResult usernameResult = await webSocket.ReceiveAsync(usernameBuffer, CancellationToken.None);
        string username = Encoding.UTF8.GetString(usernameBuffer, 0, usernameResult.Count);
        users.Add(new User(username, false));
        messages.Add($"[SERVER] {username} joined the chat.");
        await SendToAllAsync(RoomState.Serialize(GetRoomState()));

        while (webSocket.State == WebSocketState.Open)
        {
            byte[] buffer = new byte[1024];
            try
            {
                // note : only one receive can be called at a time on one websocket, else undefined behaviour
                WebSocketReceiveResult result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);  // receive from the particular one ...
                string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                try
                {
                    ParseMessage(message);
                    string serializedRoomState = RoomState.Serialize(GetRoomState());
                    await SendToAllAsync(serializedRoomState); // ... but send to all
                }
                catch (Exception e)
                {
                    Console.WriteLine($"An Exception occured when parsing client message: \"{message}\"");
                    // Note : No send to client -> no refresh will happen on client side
                    continue;
                }
                
                Console.WriteLine($"server received: \"{message}\"");
            }
            catch (WebSocketException)
            {
                Console.WriteLine("Lost connection to the client.");
                // Note : this likely wasn't a graceful disconnect
                users.Remove(users.Find(x => x.Name == username)!);
                activeConnections.Remove(webSocket);
                messages.Add($"[SERVER] {username} disconnected.");
                await SendToAllAsync(RoomState.Serialize(GetRoomState()));
                continue;
            }
        }

        Console.WriteLine("communication listen routine ended");
    }

    private void ParseMessage(string message)
    {
        string[] parts = message.Split("|");
        string user = parts[0];
        string command = parts[1];
        switch (command)
        {
            case "message":
                string messageBody = parts[2] = parts[2].Trim();
                messages.Add($"{user}: {messageBody}");
                break;
            
            case "startedTyping":
                users.Find(x => x.Name == user)!.IsTyping = true;
                break;
            
            case "stoppedTyping":
                users.Find(x => x.Name == user)!.IsTyping = false;
                break;
        }
    }
    
    private Task SendToAllAsync(string message)
    {
        List<Task> tasks = new();
        
        foreach (WebSocket webSocket in activeConnections)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            Task task = webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
            tasks.Add(task);
        }

        return Task.WhenAll(tasks);
    }
    
    private RoomState GetRoomState()
    {
        return new RoomState(users.ToArray(), messages.ToArray());
    }
}