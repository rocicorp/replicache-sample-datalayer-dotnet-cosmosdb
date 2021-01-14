// @ts-check

// Utility functions needed to support the mutators as well as to the stored
// procedures.
// It also has the generic parts of processMutation and the code to select the
// correct mutator.

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

/**
 * Error handling for a sync protocol is slightly more involved than
 * a REST API because the client must know whether to retry a
 * mutation or not.
 *
 * Thus Replicache batch endpoints must distinguish between
 * temporary and permanent errors.
 *
 * Temporary errors are things like servers being down. The client
 * should retry the mutation later, until the needed resource comes
 * back.
 *
 * Permanent errors are things like malformed requests. The client
 * sent something the server can't ever process. The server marks
 * the request as handled and moves on.
 *
 * Under normal circumstances, permanent errors are *not expected*.
 * These are effectively programmer errors since the client should
 * only ever send messages the server can understand.
 *
 * Another way to think about it is this:
 * - say you have no concept of permanent errors
 * - eventually you write a bug on the client so that it sends a
 *   message server can't process
 * - client keeps retrying and you notice it in the logs
 * - solution is to "accept" the bad message from the client and
 *   turn it into a nop.
 * - Permanent errors are just a reification of this pattern.
 */
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

  const {name, args} = mutation;
  try {
    const mutator = mutators[name];
    if (!mutator) {
      throw new PermanentError(`Unknown mutation: ${name}`);
    }
    await mutator(accountID, args);
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
