using System;
using System.Text;
using Common;

public class Renderer
{
    private const int SIDEBAR_WIDTH = 20;
    
    private readonly StringBuilder sb = new();
    private int charsWritten;
    
    private string ClientUserName { get; set; }
    
    public Renderer(string clientUserName)
    {
        ClientUserName = clientUserName;
    }

    public void Render(string[] messages, User[] users)
    {
        int viewportHeight = Math.Max(Console.WindowHeight - 2, messages.Length);
        for (int i = 0; i < viewportHeight; i++)
        {
            DrawLine(messages, users, i, viewportHeight);
        }

        for (int i = 0; i < Console.WindowWidth - 1; i++) // -1 for newline
        {
            Write("\u2500"); // ─
        }
        
        Write($"\n{ClientUserName}> ");
        
        Console.Write(sb.ToString());
    }

    public void Clear()
    {
        charsWritten = 0;
        sb.Clear();
        Console.Clear();
        if (Environment.OSVersion.Platform == PlatformID.Unix)
        {
            // clear scrollback buffer on xterm compatible terminals
            Console.Write("\x1b[3J"); 
        }
    }

    private void DrawLine(string[] messages, User[] users, int currentLine, int maxLines)
    {
        charsWritten = 0;
        
        // Write message if any
        bool shouldWriteMessage = currentLine < messages.Length;
        if (shouldWriteMessage)
        {
            string line = messages[currentLine];
            Write(line);
        }

        // Write empty till sidebar
        while (charsWritten < Console.WindowWidth - SIDEBAR_WIDTH - 1 - 2) // 1 newline, 2 sidebar frame
        {
            Write(" ");
        }

        // Write sidebar
        Write("\u2502"); // │
        
        while (charsWritten < Console.WindowWidth - 2) // 1 for newline 1 for |
        {
            if (currentLine >= maxLines - users.Length)
            {
                int userIndex = currentLine - (maxLines - users.Length);
                User user = users[userIndex];
                Write(user.Name);
                if (user.IsTyping)
                {
                    Write("*");
                }
                // fill with empty till end
                while(charsWritten < Console.WindowWidth - 2)
                {
                    Write(" ");
                }
            }
            else
            {
                Write(" ");
            }
        }
        
        Write("\u2502"); // │
        Write("\n");
    }

    private void Write(string s)
    {
        sb.Append(s);
        charsWritten += s.Length;
    }
}