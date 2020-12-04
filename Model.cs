using System;
using System.Text.Json.Serialization;

namespace todo
{
    public class Todo
    {
        [JsonPropertyName("accountID")]
        public String AccountID { get; set; }
        [JsonPropertyName("id")]
        public String ID { get; set; }
        [JsonPropertyName("title")]
        public String Title { get; set; }
        [JsonPropertyName("order")]
        public String Order { get; set; }
        [JsonPropertyName("complete")]
        public bool Complete { get; set; }
    }

    public class ClientState
    {
        // Note: Store the ClientState entities colocated in the logical
        // partition with other account data, otherwise ordering guarantees
        // aren't upheld by CosmosDB.
        [JsonPropertyName("accountID")]
        public String AccountID { get; set; }
        [JsonPropertyName("id")]
        public String ID { get; set; }
        [JsonPropertyName("lastMutationID")]
        public UInt64 LastMutationID { get; set; }
    }
}
