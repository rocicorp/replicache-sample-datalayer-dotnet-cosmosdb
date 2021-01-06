using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Azure.Cosmos;
using Microsoft.AspNetCore.Cors;

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
        public async Task<IActionResult> Post(BatchRequest request)
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
                    // For permanent errors, return a note to client and
                    // continue.
                    infos.Add(new MutationInfo
                    {
                        Id = mutation.Id,
                        Message = e.Message,
                    });
                }
                // Other exceptions bubble up and result in 500.

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

    // Error handling for a sync protocol is slightly more involved than
    // a REST API because the client must know whether to retry a
    // mutation or not.
    //
    // Thus Replicache batch endpoints must distinguish between
    // temporary and permanent errors.
    //
    // Temporary errors are things like servers being down. The client
    // should retry the mutation later, until the needed resource comes
    // back.
    //
    // Permanent errors are things like malformed requests. The client
    // sent something the server can't ever process. The server marks
    // the request as handled and moves on.
    //
    // Under normal circumstances, permanent errors are *not expected*.
    // These are effectively programmer errors since the client should
    // only ever send messages the server can understand.
    //
    // Another way to think about it is this:
    // - say you have no concept of permanent errors
    // - eventually you write a bug on the client so that it sends a
    //   message server can't process
    // - client keeps retrying and you notice it in the logs
    // - solution is to "accept" the bad message from the client and
    //   turn it into a nop.
    // - Permanent errors are just a reification of this pattern.
    public class PermanentError : Exception
    {
        public PermanentError(String message, Exception cause)
            : base(message, cause)
        {
        }
    }
}
