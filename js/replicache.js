// @ts-check

/**
 * @param {string} accountID
 * @param {string} clientID
 * @return {Promise<number>}
 */
async function getMutationID(accountID, clientID) {
  const value = await queryDocuments(
    "SELECT c.lastMutationID FROM c WHERE c.id = @id AND c.accountID = @accountID",
    {"@id": getReplicacheStateID(clientID), "@accountID": accountID},
  );
  if (value.length === 0) {
    return 0;
  }
  return value[0].lastMutationID;
}

/**
 * @param {string} clientID
 * @return {string}
 */
function getReplicacheStateID(clientID) {
  return `/replicache-client-state/${clientID}`;
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
