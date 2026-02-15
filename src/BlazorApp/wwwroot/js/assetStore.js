const DB_NAME = "repo-asset-library";
const DB_VERSION = 1;
const STORE_ASSETS = "assets";

let dbPromise;

function openDb() {
  if (!dbPromise) {
    dbPromise = new Promise((resolve, reject) => {
      const request = indexedDB.open(DB_NAME, DB_VERSION);

      request.onupgradeneeded = () => {
        const db = request.result;
        if (!db.objectStoreNames.contains(STORE_ASSETS)) {
          db.createObjectStore(STORE_ASSETS, { keyPath: "id" });
        }
      };

      request.onsuccess = () => resolve(request.result);
      request.onerror = () => reject(request.error);
    });
  }

  return dbPromise;
}

function toArrayBuffer(value) {
  if (!value) {
    return null;
  }

  if (value instanceof ArrayBuffer) {
    return value;
  }

  if (value.buffer instanceof ArrayBuffer) {
    return value.buffer.slice(value.byteOffset, value.byteOffset + value.byteLength);
  }

  return null;
}

function runRequest(request) {
  return new Promise((resolve, reject) => {
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

function runTransaction(transaction) {
  return new Promise((resolve, reject) => {
    transaction.oncomplete = () => resolve();
    transaction.onerror = () => reject(transaction.error);
  });
}

export async function putAsset(asset) {
  const db = await openDb();
  const tx = db.transaction(STORE_ASSETS, "readwrite");
  const store = tx.objectStore(STORE_ASSETS);
  const stored = {
    ...asset,
    glb: toArrayBuffer(asset.glb),
    thumbnail: toArrayBuffer(asset.thumbnail)
  };

  store.put(stored);
  await runTransaction(tx);
}

export async function getAllAssetMetadata() {
  const db = await openDb();
  const tx = db.transaction(STORE_ASSETS, "readonly");
  const store = tx.objectStore(STORE_ASSETS);
  const items = await runRequest(store.getAll());

  return items.map(({ glb, thumbnail, ...rest }) => rest);
}

export async function getAssetById(id) {
  const db = await openDb();
  const tx = db.transaction(STORE_ASSETS, "readonly");
  const store = tx.objectStore(STORE_ASSETS);

  return runRequest(store.get(id));
}

export async function touchAsset(id, processedAt, lastUsed) {
  const db = await openDb();
  const tx = db.transaction(STORE_ASSETS, "readwrite");
  const store = tx.objectStore(STORE_ASSETS);
  const asset = await runRequest(store.get(id));

  if (!asset) {
    return false;
  }

  asset.processedAt = processedAt;
  asset.lastUsed = lastUsed;
  store.put(asset);
  await runTransaction(tx);
  return true;
}

export async function deleteAsset(id) {
  const db = await openDb();
  const tx = db.transaction(STORE_ASSETS, "readwrite");
  const store = tx.objectStore(STORE_ASSETS);

  store.delete(id);
  await runTransaction(tx);
}

export async function clearAssets() {
  const db = await openDb();
  const tx = db.transaction(STORE_ASSETS, "readwrite");
  const store = tx.objectStore(STORE_ASSETS);

  store.clear();
  await runTransaction(tx);
}
