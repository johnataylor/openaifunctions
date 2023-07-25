using Azure.AI.OpenAI;
using System.Text.Json.Nodes;

namespace ConsoleApp
{
    internal class Resolver
    {
        private const int MAX_ITERATIONS = 10;

        public static async Task<string> Run(string utterance, OpenAIClient client, string deploymentOrModelName, List<FunctionDefinition> functionDefinitions, IDictionary<string, Func<JsonNode, JsonNode>> functionImplementations)
        {
            var conversationMessages = new List<ChatMessage>()
            {
                new(ChatRole.System, "Don't make assumptions about what values to plug into functions. Ask for clarification if a user request is ambiguous."),
                new(ChatRole.User, utterance),
            };

            for (int i = 0; i < MAX_ITERATIONS; i++)
            {
                var chatCompletionsOptions = new ChatCompletionsOptions();
                foreach (var chatMessage in conversationMessages)
                {
                    chatCompletionsOptions.Messages.Add(chatMessage);
                }
                foreach (var functionDefinition in functionDefinitions)
                {
                    chatCompletionsOptions.Functions.Add(functionDefinition);
                }

                var chatComplations = await client.GetChatCompletionsAsync(deploymentOrModelName, chatCompletionsOptions);

                if (chatComplations.GetRawResponse().IsError)
                {
                    throw new Exception("error on call to GPT");
                }

                var choice = chatComplations.Value.Choices[0] ?? throw new Exception("what?! we have no choice!");
                var message = choice.Message;

                // add to the transcript
                conversationMessages.Add(choice.Message);

                if (choice.FinishReason == CompletionsFinishReason.FunctionCall)
                {
                    var functionName = message.FunctionCall.Name;
                    var arguments = message.FunctionCall.Arguments;

                    // arguments is a string of JSON embedded in a property of type string - NOTE: a production implementation should retry the call to GPT
                    var argumentsArguments = JsonNode.Parse(arguments) ?? throw new InvalidDataException("expected arguments to contain JSON");

                    if (functionImplementations.TryGetValue(functionName, out var func))
                    {
                        Trace($"function call:\n{functionName}('{argumentsArguments}')");

                        // call the function
                        var functionResponse = func(argumentsArguments);

                        Trace($"response:\n'{functionResponse}'");

                        conversationMessages.Add(new ChatMessage(ChatRole.Function, functionResponse.ToJsonString()) { Name = functionName });
                    }
                    else
                    {
                        // Either we messed up our metadata or the model is hallucinating - if the later, we should retry the call to GPT
                        throw new InvalidDataException($"unable to answer the question because function '{functionName}' doesn't exist");
                    }
                }
                else
                {
                    // finish reason is not FunctionCall so we must be done!

                    if (choice.FinishReason == CompletionsFinishReason.TokenLimitReached)
                    {
                        throw new Exception("hit the token limit");
                    }

                    return message.Content;
                }
            }

            Trace("reaching max iterations");

            return "unable to answer the question";
        }

        private static void Trace(string message)
        {
            var forgroundColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(message);
            Console.ForegroundColor = forgroundColor;
        }
    }
}
