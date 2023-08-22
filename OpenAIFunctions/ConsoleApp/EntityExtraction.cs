using Azure.AI.OpenAI;
using System.Text.Json.Nodes;

namespace ConsoleApp
{
    internal class EntityExtraction
    {
        private readonly OpenAIClient _client;
        private readonly string _deploymentOrModelName;
        private readonly List<FunctionDefinition> _functionDefinitions;

        public EntityExtraction(OpenAIClient client, string deploymentOrModelName)
        {
            _client = client;
            _deploymentOrModelName = deploymentOrModelName;

            var functionDescriptions = JsonNode.Parse(File.ReadAllText("extract.json")) ?? throw new Exception("unable to read descriptions");
            _functionDefinitions = new List<FunctionDefinition>();

            foreach (var item in functionDescriptions.AsArray())
            {
                _functionDefinitions.Add(new FunctionDefinition
                {
                    Name = item?["name"]?.GetValue<string>(),
                    Description = item?["description"]?.GetValue<string>(),
                    Parameters = BinaryData.FromString(item?["parameters"]?.ToJsonString() ?? throw new Exception("unable to read descriptions"))
                });
            }
        }

        public async Task RunAsync(string email)
        {
            var conversationMessages = new List<ChatMessage> { new(ChatRole.User, $"Create a work order from the following email: {email}") };
            var chatChoice = await CallChatCompletionAsync(conversationMessages);

            if (chatChoice.FinishReason == CompletionsFinishReason.FunctionCall)
            {
                if (chatChoice.Message.FunctionCall.Name == "create_work_order")
                {
                    var argumentsArguments = JsonNode.Parse(chatChoice.Message.FunctionCall.Arguments);

                    if (argumentsArguments != null)
                    {
                        Console.WriteLine(argumentsArguments);
                    }
                }
            }
        }

        private async Task<ChatChoice> CallChatCompletionAsync(List<ChatMessage> conversationMessages)
        {
            var chatCompletionsOptions = new ChatCompletionsOptions();

            chatCompletionsOptions.Messages.Add(new(ChatRole.System, "Don't make assumptions about what values to plug into functions. Ask for clarification if a user request is ambiguous."));

            foreach (var chatMessage in conversationMessages)
            {
                chatCompletionsOptions.Messages.Add(chatMessage);
            }
            foreach (var functionDefinition in _functionDefinitions)
            {
                chatCompletionsOptions.Functions.Add(functionDefinition);
            }

            var chatComplations = await _client.GetChatCompletionsAsync(_deploymentOrModelName, chatCompletionsOptions);

            if (chatComplations.GetRawResponse().IsError)
            {
                throw new Exception("error on call to GPT");
            }

            return chatComplations.Value.Choices[0] ?? throw new Exception("what?! we have no choice!");
        }
    }
}
