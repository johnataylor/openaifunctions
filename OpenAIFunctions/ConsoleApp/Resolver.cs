using Azure.AI.OpenAI;
using System.Text.Json.Nodes;

namespace ConsoleApp
{
    internal class Resolver
    {
        private const int MAX_ITERATIONS = 10;

        public static async Task<string> RunAsync(string utterance, OpenAIClient client, string deploymentOrModelName, List<FunctionDefinition> functionDefinitions, IDictionary<string, Func<JsonNode, Task<JsonNode>>> functionImplementations)
        {
            var resolver = new FunctionResolver(client, deploymentOrModelName, functionDefinitions, functionImplementations);
            var messages = new List<ChatMessage> { new(ChatRole.User, utterance) };
            await resolver.RunAsync(messages);
            return messages.Last().Content;
        }
    }
}
