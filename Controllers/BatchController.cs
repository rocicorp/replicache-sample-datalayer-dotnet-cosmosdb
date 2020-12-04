using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Azure.Cosmos;
using System.Text.Json.Serialization;
using ObjectDumping;

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

        [HttpPost]
        [Route("/replicache-batch")]
        public async Task<IActionResult> Post(BatchRequest request)
        {
            // TODO
            const String accountID = "TODO";

            if (request.ClientId == null || request.ClientId == "")
            {
                return BadRequest("clientID field is required");
            }

            var db = new Db(_configuration);
            var infos = new List<MutationInfo>();
            foreach (var mutation in request.Mutations)
            {
                if (mutation.Id == 0)
                {
                    return BadRequest(String.Format("id field of mutation must be non-zero"));
                }

                var currentMutationID = await db.GetMutationID(accountID, request.ClientId);
                var expectedMutationID = currentMutationID + 1;

                if (mutation.Id > expectedMutationID)
                {
                    return BadRequest(String.Format("Mutation ID {0} is too high - next expected mutation is {1}", mutation.Id, expectedMutationID));
                }

                if (mutation.Id < expectedMutationID)
                {
                    infos.Add(new MutationInfo
                    {
                        Id = mutation.Id,
                        Message = String.Format("Mutation ID {0} has already been processed. Skipping.", mutation.Id),
                    });
                    continue;
                }

                try
                {
                    await ProcessMutation(db.Items, accountID, mutation);
                }
                catch (PermanentError e)
                {
                    infos.Add(new MutationInfo
                    {
                        Id = mutation.Id,
                        Message = e.Message,
                    });
                }
                catch (Exception e)
                {
                    return BadRequest(e.Message);
                }

                await db.SetMutationID(accountID, request.ClientId, expectedMutationID);
            }

            return Ok(JsonSerializer.Serialize(infos));
        }

        private async Task ProcessMutation(CosmosContainer items, String accountID, Mutation mutation)
        {
            switch (mutation.Name)
            {
                case "createTodo":
                    await createTodo(items, accountID, mutation.Args);
                    break;
                // ... etc ...
                default:
                    throw new PermanentError(String.Format("Unknown mutation: {0}", mutation.Name), null);
            }
        }

        private async Task createTodo(CosmosContainer items, String accountID, JsonElement args)
        {
            Todo todo;
            try
            {
                todo = JsonSerializer.Deserialize<Todo>(args.GetRawText());
            }
            catch (Exception e)
            {
                throw new PermanentError("Could not deserialize arguments: " + e.Message, e);
            }

            if (!todo.ID.StartsWith("/todo/"))
            {
                throw new PermanentError("Invalid id: must start with '/todo/'", null);
            }

            todo.AccountID = accountID;
            await items.CreateItemAsync(todo);
        }
    }

    public class PermanentError : Exception
    {
        public PermanentError(String message, Exception cause) : base(message, cause)
        {
        }
    }

    public class BatchRequest
    {
        public String ClientId { get; set; }
        public Mutation[] Mutations { get; set; }
    }

    public class Mutation
    {
        public UInt64 Id { get; set; }
        public String Name { get; set; }
        public JsonElement Args { get; set; }
    }

    public class BatchPushResponse
    {
        public MutationInfo[] MutationInfos { get; set; }
    }

    public class MutationInfo
    {
        public UInt64 Id { get; set; }
        public String Message { get; set; }
    }
}
