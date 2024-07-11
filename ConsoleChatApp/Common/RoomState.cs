using System.Text.Json;

namespace Common
{
    [Serializable]
    public class RoomState
    {
        public RoomState(User[] users, string[] messages)
        {
            Users = users;
            Messages = messages;
        }

        public User[] Users { get; }
        public string[] Messages { get; }

        public static RoomState Empty { get; } = new(Array.Empty<User>(), Array.Empty<string>());

        public static string Serialize(RoomState roomState)
        {
            return JsonSerializer.Serialize(roomState);
        }

        public static RoomState? Deserialize(string json)
        {
            return JsonSerializer.Deserialize<RoomState>(json);
        }
    }
}