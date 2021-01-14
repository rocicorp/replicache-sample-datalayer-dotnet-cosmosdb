using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Cors;
using System.Text.Json.Serialization;

namespace todo
{
    [ApiController]
    [Route("[controller]")]
    public class BatchController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public BatchController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [EnableCors]
        [HttpPost]
        [Route("/replicache-batch")]
        public async Task<ActionResult<List<MutationInfo>>> Post(BatchRequest request)
        {
            // TODO - authenticate user and get their account ID.
            // Replicache sends an auth token through the normal 'Authorization'
            // HTTP header. Provide it on the client side via the
            // getDataLayerAuth API: https://js.replicache.dev/classes/default.html#getdatalayerauth
            const String accountID = "TODO";

            if (request.ClientId == null || request.ClientId == "")
            {
                return BadRequest("clientID field is required");
            }

            var db = new Db(_configuration);

            // Process each request in order, ensuring each mutation is
            // processed in order and exactly once.
            var infos = new List<MutationInfo>();
            foreach (var mutation in request.Mutations)
            {
                if (mutation.Id == 0)
                {
                    return BadRequest("id field of mutation must be non-zero");
                }

                var result = await ProcessMutation(db, accountID, request.ClientId, mutation);
                if (result is BadRequestObjectResult)
                {
                    return result as BadRequestObjectResult;
                }
                if (result is OkObjectResult)
                {
                    var value = (result as OkObjectResult).Value as MutationInfo;
                    if (value != null)
                    {
                        infos.Add(value);
                    }
                }
            }

            return Ok(infos);
        }

        private async Task<IActionResult> ProcessMutation(
            Db db,
            string accountID,
            string clientID,
            Mutation mutation)
        {
            JsonElement result = await db.ExecuteStoredProcedure<JsonElement>(
                accountID,
                "spProcessMutation",
                new dynamic[] { accountID, clientID, mutation });
            string kind = result.GetProperty("kind").GetString();
            if (kind == "BadRequest")
            {
                string message = result.GetProperty("message").GetString();
                return BadRequest(message);
            }

            var value = result.GetProperty("value");
            if (value.ValueKind == JsonValueKind.Null)
            {
                return Ok(null);
            }
            var info = MutationInfo.FromJsonElement(value);
            return Ok(info);
        }
    }

    public class BatchRequest
    {
        public String ClientId { get; set; }
        public Mutation[] Mutations { get; set; }
    }

    public class Mutation
    {
        [JsonPropertyName("id")]
        public UInt64 Id { get; set; }

        [JsonPropertyName("name")]
        public String Name { get; set; }

        [JsonPropertyName("args")]
        public JsonElement Args { get; set; }
    }

    public class BatchPushResponse
    {
        public MutationInfo[] MutationInfos { get; set; }
    }

    public class MutationInfo
    {
        [JsonPropertyName("id")]
        public UInt64 Id { get; set; }

        [JsonPropertyName("error")]
        public String Error { get; set; }

        static public MutationInfo FromJsonElement(JsonElement obj)
        {
            // TODO(arv): Isn't there a built in way to do this?
            return new MutationInfo
            {
                Id = obj.GetProperty("id").GetUInt64(),
                Error = obj.GetProperty("error").GetString(),
            };
        }
    }
}
