const DB_NAME = "repo-asset-library";
const DB_VERSION = 2;
const STORE_ASSETS = "assets";
const STORE_UNITY_PACKAGES = "unityPackages";
const STORE_AVATARS = "avatars";

let dbPromise;
let dbInstance;

function openDb() {
  if (!dbPromise) {
    dbPromise = new Promise((resolve, reject) => {
      const request = indexedDB.open(DB_NAME, DB_VERSION);

      request.onupgradeneeded = () => {
        const db = request.result;
        if (!db.objectStoreNames.contains(STORE_ASSETS)) {
          db.createObjectStore(STORE_ASSETS, { keyPath: "id" });
        }
        if (!db.objectStoreNames.contains(STORE_UNITY_PACKAGES)) {
          db.createObjectStore(STORE_UNITY_PACKAGES, { keyPath: "id" });
        }
        if (!db.objectStoreNames.contains(STORE_AVATARS)) {
          db.createObjectStore(STORE_AVATARS, { keyPath: "id" });
        }
      };

      request.onsuccess = () => resolve(request.result);
      request.onerror = () => reject(request.error);
    });
  }

  return dbPromise.then((db) => {
    dbInstance = db;
    return db;
  });
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

export async function putUnityPackage(packageInventory) {
  const db = await openDb();
  const tx = db.transaction(STORE_UNITY_PACKAGES, "readwrite");
  const store = tx.objectStore(STORE_UNITY_PACKAGES);
  store.put(packageInventory);
  await runTransaction(tx);
}

export async function getAllUnityPackageMetadata() {
  const db = await openDb();
  const tx = db.transaction(STORE_UNITY_PACKAGES, "readonly");
  const store = tx.objectStore(STORE_UNITY_PACKAGES);
  const items = await runRequest(store.getAll());

  return items.map(({ anchors, resolvedPaths, ...rest }) => rest);
}

export async function getUnityPackageById(id) {
  const db = await openDb();
  const tx = db.transaction(STORE_UNITY_PACKAGES, "readonly");
  const store = tx.objectStore(STORE_UNITY_PACKAGES);

  return runRequest(store.get(id));
}

export async function getAssetById(id) {
  const db = await openDb();
  const tx = db.transaction(STORE_ASSETS, "readonly");
  const store = tx.objectStore(STORE_ASSETS);

  const result = await runRequest(store.get(id));
  if (!result) {
    return null;
  }

  // Convert ArrayBuffers to Uint8Array for Blazor interop
  return {
    ...result,
    glb: result.glb ? new Uint8Array(result.glb) : null,
    thumbnail: result.thumbnail ? new Uint8Array(result.thumbnail) : null
  };
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

export async function clearUnityPackages() {
  const db = await openDb();
  const tx = db.transaction(STORE_UNITY_PACKAGES, "readwrite");
  const store = tx.objectStore(STORE_UNITY_PACKAGES);

  store.clear();
  await runTransaction(tx);
}

export async function deleteDatabase() {
  if (dbInstance) {
    dbInstance.close();
    dbInstance = undefined;
  }

  dbPromise = undefined;

  await new Promise((resolve, reject) => {
    const request = indexedDB.deleteDatabase(DB_NAME);
    request.onsuccess = () => resolve();
    request.onerror = () => reject(request.error);
    request.onblocked = () => resolve();
  });
}

export async function putAvatar(avatar) {
  const db = await openDb();
  const tx = db.transaction(STORE_AVATARS, "readwrite");
  const store = tx.objectStore(STORE_AVATARS);
  const stored = {
    ...avatar,
    glb: toArrayBuffer(avatar.glb)
  };

  store.put(stored);
  await runTransaction(tx);
}

export async function getAvatarById(id) {
  const db = await openDb();
  const tx = db.transaction(STORE_AVATARS, "readonly");
  const store = tx.objectStore(STORE_AVATARS);

  const result = await runRequest(store.get(id));
  if (!result) {
    return null;
  }

  // Convert ArrayBuffer to Uint8Array for Blazor interop
  return {
    ...result,
    glb: result.glb ? new Uint8Array(result.glb) : null
  };
}

export async function getAllAvatarIds() {
  const db = await openDb();
  const tx = db.transaction(STORE_AVATARS, "readonly");
  const store = tx.objectStore(STORE_AVATARS);
  const items = await runRequest(store.getAll());

  return items.map(({ glb, ...rest }) => rest);
}
