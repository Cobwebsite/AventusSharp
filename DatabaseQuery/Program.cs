using System.Text.Json;
using AventusSharp.Tools;
using DatabaseQuery;

string inputJson = await Console.In.ReadToEndAsync();
QueryPayload? payload = JsonSerializer.Deserialize<QueryPayload>(inputJson);

if (payload == null)
{
    ResultWithError<string> result = new() { Errors = [new GenericError(500, "Can't parse the json")] };
    Console.WriteLine(JsonSerializer.Serialize(result));
    return;
}

ResultWithError<List<Dictionary<string, string?>>> queryResult = await ExecuteQuery.Run(payload);
Console.WriteLine(JsonSerializer.Serialize(queryResult));