/**
 * IndexedDB wrapper for caching parsed 3D geometry data.
 * Reduces re-parsing time by storing ThreeJsGeometry objects locally.
 */

const DB_NAME = 'RepoModBrowserCache';
const DB_VERSION = 1;
const GEOMETRY_STORE = 'geometries';

let db = null;

/**
 * Initialize IndexedDB connection.
 * Creates object store if it doesn't exist.
 * @returns {Promise<IDBDatabase>}
 */
async function initDB() {
    if (db) return db;

    return new Promise((resolve, reject) => {
        const request = indexedDB.open(DB_NAME, DB_VERSION);

        request.onerror = () => reject(request.error);
        request.onsuccess = () => {
            db = request.result;
            resolve(db);
        };

        request.onupgradeneeded = (event) => {
            const database = event.target.result;
            
            // Create geometry store with composite key (modName + fileName)
            if (!database.objectStoreNames.contains(GEOMETRY_STORE)) {
                const store = database.createObjectStore(GEOMETRY_STORE, { keyPath: 'cacheKey' });
                store.createIndex('modName', 'modName', { unique: false });
                store.createIndex('timestamp', 'timestamp', { unique: false });
            }
        };
    });
}

/**
 * Store parsed geometry in IndexedDB.
 * @param {string} modName - Mod identifier (namespace_name)
 * @param {string} fileName - Asset filename
 * @param {object} geometry - ThreeJsGeometry object
 * @returns {Promise<void>}
 */
export async function setGeometry(modName, fileName, geometry) {
    const database = await initDB();
    
    const cacheKey = `${modName}:${fileName}`;
    const entry = {
        cacheKey,
        modName,
        fileName,
        geometry,
        timestamp: Date.now(),
    };

    return new Promise((resolve, reject) => {
        const transaction = database.transaction([GEOMETRY_STORE], 'readwrite');
        const store = transaction.objectStore(GEOMETRY_STORE);
        const request = store.put(entry);

        request.onsuccess = () => resolve();
        request.onerror = () => reject(request.error);
    });
}

/**
 * Retrieve cached geometry from IndexedDB.
 * @param {string} modName - Mod identifier (namespace_name)
 * @param {string} fileName - Asset filename
 * @returns {Promise<object|null>} ThreeJsGeometry or null if not cached
 */
export async function getGeometry(modName, fileName) {
    const database = await initDB();
    const cacheKey = `${modName}:${fileName}`;

    return new Promise((resolve, reject) => {
        const transaction = database.transaction([GEOMETRY_STORE], 'readonly');
        const store = transaction.objectStore(GEOMETRY_STORE);
        const request = store.get(cacheKey);

        request.onsuccess = () => {
            const entry = request.result;
            if (entry) {
                // Optional: check if cache is stale (e.g., older than 7 days)
                const age = Date.now() - entry.timestamp;
                const MAX_AGE = 7 * 24 * 60 * 60 * 1000; // 7 days
                if (age > MAX_AGE) {
                    resolve(null); // Cache expired
                } else {
                    resolve(entry.geometry);
                }
            } else {
                resolve(null);
            }
        };
        request.onerror = () => reject(request.error);
    });
}

/**
 * Clear all cached geometries for a specific mod.
 * @param {string} modName - Mod identifier (namespace_name)
 * @returns {Promise<number>} Number of entries deleted
 */
export async function clearModCache(modName) {
    const database = await initDB();

    return new Promise((resolve, reject) => {
        const transaction = database.transaction([GEOMETRY_STORE], 'readwrite');
        const store = transaction.objectStore(GEOMETRY_STORE);
        const index = store.index('modName');
        const request = index.openCursor(IDBKeyRange.only(modName));
        let count = 0;

        request.onsuccess = (event) => {
            const cursor = event.target.result;
            if (cursor) {
                cursor.delete();
                count++;
                cursor.continue();
            } else {
                resolve(count);
            }
        };
        request.onerror = () => reject(request.error);
    });
}

/**
 * Clear all cached geometries (full cache reset).
 * @returns {Promise<void>}
 */
export async function clearAllCache() {
    const database = await initDB();

    return new Promise((resolve, reject) => {
        const transaction = database.transaction([GEOMETRY_STORE], 'readwrite');
        const store = transaction.objectStore(GEOMETRY_STORE);
        const request = store.clear();

        request.onsuccess = () => resolve();
        request.onerror = () => reject(request.error);
    });
}

/**
 * Get cache statistics (total entries, size estimate).
 * @returns {Promise<object>} { count: number, oldestTimestamp: number, newestTimestamp: number }
 */
export async function getCacheStats() {
    const database = await initDB();

    return new Promise((resolve, reject) => {
        const transaction = database.transaction([GEOMETRY_STORE], 'readonly');
        const store = transaction.objectStore(GEOMETRY_STORE);
        const countRequest = store.count();
        const cursorRequest = store.openCursor();

        let count = 0;
        let oldest = null;
        let newest = null;

        countRequest.onsuccess = () => {
            count = countRequest.result;
        };

        cursorRequest.onsuccess = (event) => {
            const cursor = event.target.result;
            if (cursor) {
                const timestamp = cursor.value.timestamp;
                if (!oldest || timestamp < oldest) oldest = timestamp;
                if (!newest || timestamp > newest) newest = timestamp;
                cursor.continue();
            } else {
                resolve({
                    count,
                    oldestTimestamp: oldest,
                    newestTimestamp: newest,
                });
            }
        };
        
        cursorRequest.onerror = () => reject(cursorRequest.error);
    });
}

// Export for use in Blazor via window object
window.cacheStorage = {
    setGeometry,
    getGeometry,
    clearModCache,
    clearAllCache,
    getCacheStats,
};
