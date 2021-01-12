using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Azure.Cosmos;
using Microsoft.AspNetCore.Cors;

namespace todo.Controllers
{
    [ApiController]
    [Route("/replicache-client-view")]
    public class ClientViewController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public ClientViewController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [EnableCors]
        [HttpPost]
        public async Task<ActionResult<ClientViewResponse>> Get(ClientViewRequest request)
        {
            if (request.ClientID == null || request.ClientID == "")
            {
                return BadRequest("clientID field is required");
            }

            const String accountID = "TODO";

            var db = new Db(_configuration);
            var mutationID = await db.GetMutationID(accountID, request.ClientID);
            var def = new QueryDefinition("SELECT * FROM c WHERE c.accountID = @accountID AND STARTSWITH(c.id, @id)")
                .WithParameter("@accountID", accountID)
                .WithParameter("@id", "/todo/");
            var response = new ClientViewResponse
            {
                LastMutationID = mutationID,
                ClientView = new Dictionary<string, Todo>(),
            };
            await foreach (var todo in db.Items.GetItemQueryIterator<CosmosTodo>(def))
            {
                response.ClientView.Add(todo.ID, todo.ToTodo());
            }

            return Ok(response);
        }
    }

    public class ClientViewRequest
    {
        public String ClientID { get; set; }
    }

    public class ClientViewResponse
    {
        public UInt64 LastMutationID { get; set; }
        public Dictionary<String, Todo> ClientView { get; set; }
    }
}
