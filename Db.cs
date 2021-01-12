using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Cosmos;
using Azure.Cosmos.Scripts;
using Microsoft.Extensions.Configuration;

namespace todo
{
    public class Db
    {
        public CosmosClient Client { get; }

        public CosmosContainer Items { get; }

        public Db(IConfiguration configuration)
        {
            String endpoint = configuration.GetValue<String>("cosmosEndpoint");
            if (endpoint == null)
            {
                throw new Exception("cosmosEndpoint config key required");
            }
            String authKey = configuration.GetValue<String>("cosmosAuthKey");
            if (authKey == null)
            {
                throw new Exception("cosmosAuthKey config key required");
            }
            Client = new CosmosClient(endpoint, authKey);
            Items = Client.GetContainer("replicache-sample-todo", "items");
        }

        public async Task RegisterStoredProcedures()
        {
            string[] sources = { "util", "replicache", "mutators" };
            string utilSource = "";
            foreach (var source in sources)
            {
                var path = $"js/{source}.js";
                utilSource += $"\n// {path}\n{File.ReadAllText(path)}";
            }
            string[] ids = { "spProcessMutation", "spGetMutationID" };
            foreach (string id in ids)
            {
                var props = new StoredProcedureProperties
                {
                    Id = id,
                    Body = File.ReadAllText($"js/{id}.js") + "\n" + utilSource,
                };
                try
                {
                    await Items.Scripts.ReplaceStoredProcedureAsync(props);
                }
                catch (CosmosException ex)
                {
                    if (ex.Status == 404)
                    {
                        await Items.Scripts.CreateStoredProcedureAsync(props);
                    }
                    else
                    {
                        throw;
                    }
                }

            }
        }

        public async Task<UInt64> GetMutationID(String accountID, String clientID)
        {
            return await ExecuteStoredProcedure<UInt64>(accountID, "spGetMutationID", new dynamic[] { accountID, clientID });
        }

        public async Task<T> ExecuteStoredProcedure<T>(string accountID, string spName, dynamic[] args)
        {
            var result = await Items.Scripts.ExecuteStoredProcedureAsync<T>(
                spName,
                new PartitionKey(accountID),
                args,
                new StoredProcedureRequestOptions { EnableScriptLogging = true });
            if (result.ScriptLog != null && result.ScriptLog != "")
            {
                Console.WriteLine($"{spName}: ScriptLog: {result.ScriptLog}");
            }
            return result.Value;
        }
    }
}
