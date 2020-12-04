# About

This is a basic sample of a Replicache backend for .Net/C#/CosmosDB.

# Run

1. Setup a CosmosDB account as [documented here](https://docs.microsoft.com/en-us/azure/cosmos-db/create-sql-api-dotnet-v4).
1. Create a new database called `replicache-sample-todo` and a container called `items`.
1. Run the sample: `dotnet run -- --cosmosEndpoint=<database-url> --cosmosAuthKey=<database-primary-key>`. Get the `database-url` and `database-primary-key` values from the "keys" pane of your CosmosDB dashboard.

# Other Notes

- In order to behave correctly, CosmosDB must be configured for a minimum of
  "Consistent Prefix" consistency.
- Additionally, the `ClientState` entities **MUST** be stored in the same logical
  partition as the relevant values that will be served in the Client View.

# Important!

This sample writes the `ClientState` keys non-atomically with changes to
entities. This is easier, because CosmosDB's transaction API uses JavaScript,
which is more involved to get setup. However, this implementation permits an anomaly:

It is possible for a client to sync an authoritative version of a change
without the `LastMutationID` field having yet been updated. This means that
the client will then replay the pending version of the change atop the
authoritative version. The result could be anything from a nop to user data
loss, depending on your application.

For a production application, you would want to use [multikey transactions](https://docs.microsoft.com/en-us/azure/cosmos-db/database-transactions-optimistic-concurrency#multi-item-transactions) to ensure
that mutations always occur atomically with the update of `LastMutationID`.
