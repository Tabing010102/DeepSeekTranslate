namespace DeepSeekTranslate.Models
{
    public class PromptMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;

        public PromptMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }
        public PromptMessage() { }

        public override string ToString()
        {
            return $"{{\"role\":\"{Role}\",\"content\":\"{Content}\"}}";
        }
    }
}
