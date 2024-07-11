namespace Common
{
    [Serializable]
    public class User
    {
        public string Name { get; set; }
        public bool IsTyping { get; set; }

        public User(string name, bool isTyping)
        {
            Name = name;
            IsTyping = isTyping;
        }
    }
}