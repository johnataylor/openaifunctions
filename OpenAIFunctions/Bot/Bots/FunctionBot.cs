// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure;
using Azure.AI.OpenAI;
using Bot;
using ConsoleApp;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.TraceExtensions;
using Microsoft.Bot.Schema;
using System.Text.Json.Nodes;

namespace Microsoft.BotBuilderSamples.Bots
{
    public class FunctionBot : ActivityHandler
    {
        private readonly State _state;
        private readonly FunctionResolver _resolver;

        public FunctionBot(State state)
        {
            _state = state;

            var deploymentOrModelName = "testfunctions";

            OpenAIClient client = new OpenAIClient(
              new Uri(""),
              new AzureKeyCredential(""));

            var functionDefinitions = new List<FunctionDefinition>();
            var functionDescriptions = JsonNode.Parse(File.ReadAllText("descriptions.json")) ?? throw new Exception("unable to read descriptions");
            foreach (var item in functionDescriptions.AsArray())
            {
                functionDefinitions.Add(new FunctionDefinition
                {
                    Name = item?["name"]?.GetValue<string>(),
                    Description = item?["description"]?.GetValue<string>(),
                    Parameters = BinaryData.FromString(item?["parameters"]?.ToJsonString() ?? throw new Exception("unable to read descriptions"))
                });
            }

            var functionImplementations = new Dictionary<string, Func<JsonNode, Task<JsonNode>>>
            {
                { "get_multiple_work_order_details", arguments => mapcar(arguments["work_order_ids"]?.AsArray(), get_work_order_details) },
                { "get_work_orders_by_account", get_work_orders_by_account },
                { "get_current_datetime", get_current_datetime },
            };

            _resolver = new FunctionResolver(client, deploymentOrModelName, functionDefinitions, functionImplementations);
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            var utterance = turnContext.Activity.Text;

            _state.ConversationState.Add(new(ChatRole.User, utterance));

            await _resolver.RunAsync(_state.ConversationState, async (msg) => await turnContext.TraceActivityAsync(msg));

            var replyText = _state.ConversationState.Last().Content;

            await turnContext.SendActivityAsync(MessageFactory.Text(replyText, replyText), cancellationToken);
        }

        // **** **** **** **** function implementations **** **** **** ****

        Task<JsonNode> get_current_datetime(JsonNode arguments)
        {
            return Task.FromResult<JsonNode>(new JsonObject { { "current-datetime-utc", DateTime.UtcNow.ToString() } });
        }

        // best practice: when dealing with data identity is important

        JsonNode get_work_order_details(JsonNode? arguments)
        {
            // mock up some data

            var work_order_id = arguments?["work_order_id"]?.GetValue<string>() ?? throw new InvalidDataException("expected work_order_id");

            work_order_id = work_order_id.PadLeft(5, '0');

            switch (work_order_id)
            {
                case "00052":
                    return new JsonObject { { "createdOn", "06/22/2023" }, { "work_order_type", "installation" }, { "status", "in progress" }, { "summary", "install car tires" } };

                case "00042":
                    return new JsonObject { { "createdOn", "06/22/2023" }, { "work_order_type", "repair" }, { "status", "pending" }, { "summary", "fix car" } };

                case "52341":
                    return new JsonObject { { "createdOn", "06/22/2023" }, { "work_order_type", "installation" }, { "status", "in progress" }, { "summary", "tow hitch" } };

                default:
                    return new JsonObject();
            }
        }

        // best practice: when dealing with data operating on collections is important

        Task<JsonNode> mapcar(JsonArray? array, Func<JsonNode?, JsonNode> func)
        {
            array = array ?? throw new ArgumentNullException("array");
            return Task.FromResult<JsonNode>(new JsonArray(array.Select((element, Index) => func(element)).ToArray()));
        }

        Task<JsonNode> get_work_orders_by_account(JsonNode arguments)
        {
            // mock up some data

            return Task.FromResult<JsonNode>(new JsonArray { new JsonObject { { "work_order_id", "00052" } }, new JsonObject { { "work_order_id", "00042" } }, new JsonObject { { "work_order_id", "52341" } } });
        }
    }
}
