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

        public async Task<UInt64> GetMutationID(String accountID, String clientID)
        {
            var def = new QueryDefinition("SELECT * FROM c WHERE c.accountID = @accountID AND c.id = @id")
                .WithParameter("@accountID", accountID)
                .WithParameter("@id", GetReplicacheStateID(clientID));
            await foreach (ClientState state in Items.GetItemQueryIterator<ClientState>(def))
            {
                return state.LastMutationID;
            }
            return 0;
        }

        public async Task SetMutationID(String accountID, String clientID, UInt64 mutationID)
        {
            await Items.UpsertItemAsync(new ClientState
            {
                AccountID = accountID,
                ID = GetReplicacheStateID(clientID),
                LastMutationID = mutationID,
            });
        }

        private static String GetReplicacheStateID(String clientID)
        {
            return String.Format("/replicache-client-state/{0}", clientID);
        }
    }
}
