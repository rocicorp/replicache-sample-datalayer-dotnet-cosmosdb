using System;
using System.Text.Json.Serialization;

namespace todo
{
    public class TodoBase
    {
        [JsonPropertyName("listID")]
        public UInt64 ListID { get; set; }
        [JsonPropertyName("text")]
        public string Text { get; set; }
        [JsonPropertyName("order")]
        public string Order { get; set; }
        [JsonPropertyName("complete")]
        public bool Complete { get; set; }
    }

    // Inside Cosmos the ID of a todo has the shape `/todo/123456` and it also
    // has an `AccountID`.
    public class CosmosTodo : TodoBase
    {
        [JsonPropertyName("accountID")]
        public string AccountID { get; set; }
        [JsonPropertyName("id")]
        public string ID { get; set; }

        public Todo ToTodo()
        {
            return new Todo
            {
                ID = UInt64.Parse(ID.Substring("/todo/".Length)),
                ListID = ListID,
                Text = Text,
                Order = Order,
                Complete = Complete,
            };
        }
    }

    public class Todo : TodoBase
    {
        [JsonPropertyName("id")]
        public UInt64 ID { get; set; }
    }

    public class ClientState
    {
        // Note: Store the ClientState entities colocated in the logical
        // partition with other account data, otherwise ordering guarantees
        // aren't upheld by CosmosDB.
        [JsonPropertyName("accountID")]
        public string AccountID { get; set; }
        [JsonPropertyName("id")]
        public string ID { get; set; }
        [JsonPropertyName("lastMutationID")]
        public UInt64 LastMutationID { get; set; }
    }

}
