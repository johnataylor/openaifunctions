using Azure;
using Azure.AI.OpenAI;
using ConsoleApp;
using System.Text.Json.Nodes;

// This code use the official Azure.OpenAI client library, which has been updated to use api-version 2023-07-01-preview
// If you are deploying a model in Azure make sure to specific version 613

var useAzureOpenAI = true;

// in Azure use the deployment name, using openai.com use the model name
var deploymentOrModelName = "testfunctions";

OpenAIClient client = useAzureOpenAI
    ? new OpenAIClient(
        new Uri("https://your-azure-openai-resource.com/"),
        new AzureKeyCredential("your-azure-openai-resource-api-key"))
    : new OpenAIClient("your-api-key-from-platform.openai.com");


// You can create the function metadata however you like, here we are simply loading it from a file

var functionDescriptions = JsonNode.Parse(File.ReadAllText("descriptions.json")) ?? throw new Exception("unable to read descriptions");
var functionDefinitions = new List<FunctionDefinition>();

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
//  if we're going to expect our language model to perform "joins," it's only reasonable to give it functions that handle collections right!? :-)
//  but seriously, note the schema we give the model should be fully specified to include the type of the elements in the array, this is important
//  but please do experiment, if you uncomment this line (AND remember to update the schema appropriately!) this scenario it will still work! - but with more calls to the model 
//  { "get_work_order_details", get_work_order_details },
    { "get_multiple_work_order_details", arguments => mapcar(arguments["work_order_ids"]?.AsArray(), get_work_order_details) },
    { "get_work_orders_by_account", get_work_orders_by_account },
};

// the user question we want answering, note in this example a "join" is needed, this is handled with iteration, and then the intermediate result will need "filtering"

var result = await Resolver.RunAsync("what are the 'in progress' work orders for account 01234?", client, deploymentOrModelName, functionDefinitions, functionImplementations);
Console.WriteLine(result);

//var entityExtraction = new EntityExtraction(client, deploymentOrModelName);
//var email = File.ReadAllText("email.txt");
//await entityExtraction.RunAsync(email);


// **** **** **** **** function implementations **** **** **** ****

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
