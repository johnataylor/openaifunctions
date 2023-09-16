using Azure.AI.OpenAI;
using System.Text.Json.Nodes;

namespace ConsoleApp
{
    public class FunctionResolver
    {
        private const int MAX_ITERATIONS = 10;

        private readonly OpenAIClient _client;
        private readonly string _deploymentOrModelName;
        private readonly List<FunctionDefinition> _functionDefinitions;
        private readonly IDictionary<string, Func<JsonNode, Task<JsonNode>>> _functionImplementations;

        public FunctionResolver(OpenAIClient client, string deploymentOrModelName, List<FunctionDefinition> functionDefinitions, IDictionary<string, Func<JsonNode, Task<JsonNode>>> functionImplementations)
        {
            _client = client;
            _deploymentOrModelName = deploymentOrModelName;
            _functionDefinitions = functionDefinitions;
            _functionImplementations = functionImplementations;
        }

        public async Task RunAsync(List<ChatMessage> conversationMessages, Func<string, Task> trace)
        {
            var iterations = 0;

            // this loops terminates when either we have an answer or we have been told to run a function we don't have the implementation for

            while (conversationMessages.Last().Role != ChatRole.Assistant && iterations++ < MAX_ITERATIONS)
            {
                var response = await CallChatCompletionAsync(conversationMessages);
                await ProcessResponseAsync(response, conversationMessages, trace);
            }

            // TODO: warning if we reached max iterations
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

        private async Task ProcessResponseAsync(ChatChoice chatChoice, List<ChatMessage> conversationMessages, Func<string, Task> trace)
        {
            if (chatChoice.FinishReason == CompletionsFinishReason.FunctionCall)
            {
                var functionName = chatChoice.Message.FunctionCall.Name;
                var arguments = chatChoice.Message.FunctionCall.Arguments;

                var argumentsArguments = JsonNode.Parse(arguments);

                if (argumentsArguments != null)
                {
                    conversationMessages.Add(chatChoice.Message);

                    if (_functionImplementations.TryGetValue(functionName, out var func))
                    {
                        await trace($"function call:\n{functionName}('{argumentsArguments}')");

                        // call the function
                        var functionResponse = await func(argumentsArguments);

                        await trace($"response:\n'{functionResponse}'");

                        conversationMessages.Add(new ChatMessage(ChatRole.Function, functionResponse.ToJsonString()) { Name = functionName });
                    }
                    // we have been told to run a function but we don't have the implementation
                }
                // just retry if we are told to run a function but we don't have good JSON
            }
            else
            {
                // we have the answer
                conversationMessages.Add(chatChoice.Message);
                // TODO: warning if the finish reason is not CompletionsFinishReason.Stopped
            }
        }
    }
}
