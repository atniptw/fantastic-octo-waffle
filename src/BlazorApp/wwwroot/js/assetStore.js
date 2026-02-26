const DB_NAME = 'repo-mod-composer';
const DB_VERSION = 1;
const STORE_NAME = 'app_state';
const ASSETS_KEY = 'mod_assets';

function openDb() {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, DB_VERSION);

    request.onupgradeneeded = () => {
      const db = request.result;
      if (!db.objectStoreNames.contains(STORE_NAME)) {
        db.createObjectStore(STORE_NAME);
      }
    };

    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error ?? new Error('IndexedDB open failed'));
  });
}

function readValue(db, key) {
  return new Promise((resolve, reject) => {
    const transaction = db.transaction(STORE_NAME, 'readonly');
    const store = transaction.objectStore(STORE_NAME);
    const request = store.get(key);

    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error ?? new Error('IndexedDB read failed'));
  });
}

function writeValue(db, key, value) {
  return new Promise((resolve, reject) => {
    const transaction = db.transaction(STORE_NAME, 'readwrite');
    const store = transaction.objectStore(STORE_NAME);
    const request = store.put(value, key);

    request.onsuccess = () => resolve();
    request.onerror = () => reject(request.error ?? new Error('IndexedDB write failed'));
  });
}

export async function loadAssets() {
  const db = await openDb();
  try {
    const assets = await readValue(db, ASSETS_KEY);
    return Array.isArray(assets) ? assets : [];
  } finally {
    db.close();
  }
}

export async function saveAssets(assets) {
  const db = await openDb();
  try {
    await writeValue(db, ASSETS_KEY, Array.isArray(assets) ? assets : []);
  } finally {
    db.close();
  }
}
