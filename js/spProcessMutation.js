// @ts-check

/**
 * @param {string} accountID
 * @param {string} clientID
 * @param {Mutation} mutation
 */
function spProcessMutation(accountID, clientID, mutation) {
  exec(() => processMutation(accountID, clientID, mutation));
}

class PermanentError extends Error {}

/** @typedef {{id: number; name: string; args: any}} Mutation */

/** @typedef {{id: number; error: string}} MutationInfo */

/** @template Value */
class Ok {
  /**
   * @param {Value} value
   */
  constructor(value) {
    /** @readonly */
    this.kind = "Ok";
    /** @readonly */
    this.value = value;
  }
}

class BadRequest {
  /**
   * @param {string} message
   */
  constructor(message) {
    /** @readonly */
    this.kind = "BadRequest";
    /** @readonly */
    this.message = message;
  }
}

/**
 * @param {string} accountID
 * @param {string} clientID
 * @param {Mutation} mutation
 * @returns {Promise<BadRequest | Ok<MutationInfo | null>>}
 */
async function processMutation(accountID, clientID, mutation) {
  const currentMutationID = await getMutationID(accountID, clientID);
  const expectedMutationID = currentMutationID + 1;

  if (mutation.id > expectedMutationID) {
    return new BadRequest(
      `Mutation ID ${mutation.id} is too high - next expected mutation is ${expectedMutationID}`,
    );
  }

  if (mutation.id < expectedMutationID) {
    return new Ok({
      id: mutation.id,
      error: `Mutation ID ${mutation.id} has already been processed. Skipping.`,
    });
  }

  /** @type {MutationInfo | null} */
  let info = null;

  try {
    switch (mutation.name) {
      case "createTodo":
        await createTodo(accountID, mutation.args);
        break;
      case "updateTodo":
        await updateTodo(accountID, mutation.args);
        break;
      case "deleteTodo":
        await deleteTodo(accountID, mutation.args);
        break;
      default:
        throw new PermanentError(`Unknown mutation: ${mutation.name}`);
    }
  } catch (e) {
    if (e instanceof PermanentError) {
      info = {
        id: mutation.id,
        error: e.message,
      };
    } else {
      throw e;
    }
  }

  await setMutationID(accountID, clientID, expectedMutationID);

  return new Ok(info);
}

/**
 * @typedef {{
 *   listID: number;
 *   text: string;
 *   order: string;
 *   complete: boolean;
 * }} TodoBase
 */

/**
 * @typedef {TodoBase & {
 *   id: number;
 * }} Todo
 */

/**
 * @typedef {TodoBase & {
 *   accountID: string;
 *   id: string;
 * }} CosmosTodo
 */

/**
 * @param {number} id
 * @return {string}
 */
function toCosmosID(id) {
  return `/todo/${id}`;
}

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
  const cosmosTodo = {...todo, id: toCosmosID(todo.id), accountID};

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

/**
 * @param {string} accountID
 * @param {number} id
 */
async function getTodo(accountID, id) {
  const todos = await queryDocuments(
    "SELECT * FROM c WHERE c.id = @id AND @accountID = @accountID",
    {"@id": toCosmosID(id), "@accountID": accountID},
  );
  return todos[0];
}

/**
 * @param {string} accountID
 * @param {string} clientID
 * @param {number} lastMutationID
 * @return {Promise<void>}
 */
async function setMutationID(accountID, clientID, lastMutationID) {
  await upsertDocument({
    accountID,
    id: getReplicacheStateID(clientID),
    lastMutationID,
  });
}
