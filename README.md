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

The sample use stored procedures written in JavaScript since that is required
for doing [multikey
transactions](https://docs.microsoft.com/en-us/azure/cosmos-db/database-transactions-optimistic-concurrency#multi-item-transactions).
This ensures that mutations always occur atomically with the update of
`LastMutationID`.

# Demo

You can modify the [lit-redo
sample](https://github.com/rocicorp/replicache-sdk-js/tree/master/sample/lit-todo)
in the [replicache-sdk-js repo](https://github.com/rocicorp/replicache-sdk-js)
to work with this CosmosDB backend. Here is how:

```sh
git clone https://github.com/rocicorp/replicache-sdk-js.git
cd replicache-sdk-js.git
npm install
```

Until we have self serve for the client view you need to run the diff-server locally:

```sh
# in replicache-sdk-js/
bin/diff-server serve --client-view=https://localhost:5001/replicache-client-view --db=/tmp/diff-server
```

Now we need to change the sample to use our local diff server and to use the `replicache-client-view` and the `replicache-batch` endpoints.

Modify [sample/lit-todo/main.js](https://github.com/rocicorp/replicache-sdk-js/blob/master/sample/lit-todo/main.js)
to use `https://localhost:50001/replicache-batch` as the
[`batchURL`](https://github.com/rocicorp/replicache-sdk-js/blob/932976225b2f09b59fb31e8da1f8f6be9f9edcde/sample/lit-todo/main.js#L38).

and you need to update
[sample/lit-todo/main.js](https://github.com/rocicorp/replicache-sdk-js/blob/master/sample/lit-todo/main.js)
to use your local diff-server by changing
[`diffServerURL`](https://github.com/rocicorp/replicache-sdk-js/blob/932976225b2f09b59fb31e8da1f8f6be9f9edcde/sample/lit-todo/main.js#L30) to `http://localhost:7001/pull`.

Now you need to build lit-redo and start a web server hosting it.

```sh
# in replicache-sdk-js/sample/lit-redo
npm install # first time only
npm run build
npx http-server .
```

Don't forget to `dotnet run` as described in [Run](#run).
