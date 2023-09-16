using Azure.AI.OpenAI;

namespace Bot
{
    public class State
    {
        public List<ChatMessage> ConversationState { get; init; } = new List<ChatMessage>();
    }
}
