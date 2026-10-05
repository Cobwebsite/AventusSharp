using System.Text.Json;
using AventusSharp.Tools;
using AventusSharp.Localization;
using DatabaseQuery;

string inputJson = await Console.In.ReadToEndAsync();
QueryPayload? payload = JsonSerializer.Deserialize<QueryPayload>(inputJson);

if (payload == null)
{
    ResultWithError<string> result = new() { Errors = [new GenericError(500, AventusTranslations.Get(AventusMessageKeys.Data.JsonParsingFailed))] };
    Console.WriteLine(JsonSerializer.Serialize(result));
    return;
}

ResultWithError<List<Dictionary<string, string?>>> queryResult = await ExecuteQuery.Run(payload);
Console.WriteLine(JsonSerializer.Serialize(queryResult));
