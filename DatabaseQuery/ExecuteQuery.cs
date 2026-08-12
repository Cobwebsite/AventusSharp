using System.Text.Json;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Data.Storage.Mssql;
using AventusSharp.Data.Storage.Mysql;
using AventusSharp.Data.Storage.Postgresql;
using AventusSharp.Data.Storage.Sqlite;
using AventusSharp.Tools;

namespace DatabaseQuery;

public static class ExecuteQuery
{
    public static async Task<ResultWithError<List<Dictionary<string, string?>>>> Run(QueryPayload payload)
    {
        ResultWithError<List<Dictionary<string, string?>>> result = new();
        StorageCredentials credentials = new StorageCredentials()
        {
            Host = payload.Host,
            Username = payload.Username,
            Password = payload.Password,
            Database = payload.Database,
            Port = payload.Port,
            TrustServerCertificate = payload.TrustServerCertificate,
        };
        IDBStorage? storage = null;

        if (payload.Type == "mysql")
        {
            storage = new MySQLStorage(credentials, false);
        }
        else if (payload.Type == "mssql")
        {
            storage = new MsSqlStorage(credentials, false);
        }
        else if (payload.Type == "postgresql")
        {
            storage = new PostgreSqlStorage(credentials, false);
        }
        else if (payload.Type == "sqlite")
        {
            storage = new SqliteStorage(payload.Host, false);
        }

        if (storage == null)
        {
            result.Errors.Add(new GenericError(500, "Database type " + payload.Type + " can't be used"));
            return result;
        }

        await result.RunAsync(() => storage.ConnectWithError());

        List<Dictionary<string, string?>>? queryResult = await result.ExtractAsync(() => storage.Query(payload.Query));
        await storage.Close();
        if (queryResult == null) return result;

        result.Result = queryResult;
        return result;
    }
}