// @ts-check

// This file provides wrappers and helpers around CosmosDB's server side
// JavaScript API. It is far from complete and omits things we do not need for
// this sample. Use it as inspiration but remember that it is far from complete.

/**
 * Helper to wrap an async function for a stored procedure. If the async
 * function is rejected we abort the stored procedure with the reason. If the
 * async function succeeds we set the response body to the result of the async
 * function.
 * @param {() => any} f
 */
function exec(f) {
  (async () => {
    try {
      const res = await f();
      if (res !== undefined) {
        getContext().getResponse().setBody(res);
      }
    } catch (ex) {
      getContext().abort(ex);
    }
  })();
}

/** @typedef {(err: any, value: any, options: any) => void} CosmosCallback */

/**
 * Helper that is used by the async wrappers to CosmosDB API calls.
 * @param {(callback: CosmosCallback) => boolean} fn
 * @return {Promise<any>}
 */
function toPromise(fn) {
  return new Promise((resolve, reject) => {
    const isAccepted = fn((err, value) => {
      if (err) {
        reject(err);
      } else {
        resolve(value);
      }
    });
    if (!isAccepted) {
      reject(new Error("Not accepted"));
    }
  });
}

// Async Cosmos DB API wrappers... because using callbacks is hard/repetitive.

/**
 * @param {any} doc
 * @return {Promise<any>}
 */
function createDocument(doc) {
  return toPromise((callback) =>
    __.createDocument(__.getSelfLink(), doc, callback),
  );
}

/**
 * @param {any} doc
 * @return {Promise<any>}
 */
function upsertDocument(doc) {
  return toPromise((callback) =>
    __.upsertDocument(__.getSelfLink(), doc, callback),
  );
}

/**
 * @param {string} documentLink
 * @return {Promise<any>}
 */
function deleteDocument(documentLink) {
  return toPromise((callback) => __.deleteDocument(documentLink, callback));
}

/**
 * @param {string} query
 * @param {{[key: string]: any}} params
 * @return {Promise<any[]>}
 */
function queryDocuments(query, params = {}) {
  const parameters = Object.keys(params).map((k) => ({
    name: k,
    value: params[k],
  }));
  const sqlQuery = {query, parameters};
  return toPromise((callback) =>
    __.queryDocuments(
      __.getSelfLink(),
      sqlQuery,
      undefined, // options, we do not use this in this demo.
      callback,
    ),
  );
}
