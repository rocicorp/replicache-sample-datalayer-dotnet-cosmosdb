// @ts-check

/**
 * @param {string} accountID
 * @param {string} clientID
 * @param {Mutation} mutation
 */
function spProcessMutation(accountID, clientID, mutation) {
  exec(() => processMutation(accountID, clientID, mutation));
}
