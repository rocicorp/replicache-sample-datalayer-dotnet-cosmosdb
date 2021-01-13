// @ts-check

// All the mutators for the todo sample.

/** @type {{[name: string]: (accountID: string, args: any) => Promise<void>}} */
const mutators = {
  __proto__: null,
  createTodo,
  updateTodo,
  deleteTodo,
};

/**
 * @param {string} accountID
 * @param {Todo} todo
 */
async function createTodo(accountID, todo) {
  if (typeof todo.id !== "number") {
    throw new PermanentError("Invalid type for id: must be number");
  }

  // CosmosDB shares the id space bewtween the `todo` and the
  // `replicache-client-state` docs.
  /** @type {CosmosTodo} */
  const cosmosTodo = Object.assign({}, todo, {
    id: toCosmosID(todo.id),
    accountID,
  });

  await createDocument(cosmosTodo);
}

/**
 * @typedef {{
 *   id: number;
 *   text: string;
 *   order: string;
 *   complete: boolean;
 * }} UpdateInput
 */

/**
 * @param {string} accountID
 * @param {UpdateInput} input
 */
async function updateTodo(accountID, input) {
  const todo = await getTodo(accountID, input.id);

  if (!todo) {
    throw new PermanentError("specified todo not found");
  }

  // if (todo.ownerUserID !== userID) {
  // 	return new PermanentError(("access unauthorized")
  // }

  if (input.text !== undefined) {
    todo.text = input.text;
  }
  if (input.order !== undefined) {
    todo.order = input.order;
  }
  if (input.complete !== undefined) {
    todo.complete = input.complete;
  }

  await upsertDocument(todo);
}

/**
 * @param {string} accountID
 * @param {{id: number}} args
 */
async function deleteTodo(accountID, args) {
  const todo = await getTodo(accountID, args.id);
  if (todo) {
    await deleteDocument(todo._self);
  }
}
