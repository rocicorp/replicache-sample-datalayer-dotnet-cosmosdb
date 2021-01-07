using System;
using System.Text.Json.Serialization;

namespace todo
{
    public class Todo<IDType>
    {
        [JsonPropertyName("accountID")]
        public String AccountID { get; set; }
        [JsonPropertyName("id")]
        public IDType ID { get; set; }
        [JsonPropertyName("listID")]
        public UInt64 ListID { get; set; }
        [JsonPropertyName("text")]
        public String Text { get; set; }
        [JsonPropertyName("order")]
        public String Order { get; set; }
        [JsonPropertyName("complete")]
        public bool Complete { get; set; }

        public static Todo<UInt64> ToNumberVersion(Todo<string> todo)
        {
            UInt64 id = UInt64.Parse(todo.ID.Substring("/todo/".Length));
            return new Todo<UInt64>
            {
                AccountID = todo.AccountID,
                ID = id,
                Text = todo.Text,
                Order = todo.Order,
                Complete = todo.Complete,
            };
        }
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
