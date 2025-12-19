/**
 * Browser-based ZIP file storage using IndexedDB
 * Stores downloaded mod ZIPs for fast re-access without re-downloading
 */

export interface CachedZipMetadata {
  id: string; // unique identifier (namespace/name)
  namespace: string;
  name: string;
  fileName: string;
  size: number;
  mimeType: string;
  downloadedAt: number;
  expiresAt?: number; // optional expiration time
}

export interface ZipCacheDB {
  zips: CachedZipMetadata;
  zipData: {
    key: string;
    data: Uint8Array;
  };
}

const DB_NAME = 'repo-cosmetic-catalog';
const DB_VERSION = 1;

class ZipCacheStore {
  private db: IDBDatabase | null = null;
  private initialized = false;

  async init(): Promise<void> {
    if (this.initialized && this.db) {
      return;
    }

    return new Promise((resolve, reject) => {
      const request = indexedDB.open(DB_NAME, DB_VERSION);

      request.onerror = () => reject(request.error);
      request.onsuccess = () => {
        this.db = request.result;
        this.initialized = true;
        resolve();
      };

      request.onupgradeneeded = (event) => {
        const db = (event.target as IDBOpenDBRequest).result;

        // Create metadata store
        if (!db.objectStoreNames.contains('zips')) {
          const metadataStore = db.createObjectStore('zips', { keyPath: 'id' });
          metadataStore.createIndex('namespace', 'namespace', { unique: false });
          metadataStore.createIndex('downloadedAt', 'downloadedAt', { unique: false });
        }

        // Create data store (stores actual ZIP file data)
        if (!db.objectStoreNames.contains('zipData')) {
          db.createObjectStore('zipData', { keyPath: 'key' });
        }
      };
    });
  }

  async saveZip(
    namespace: string,
    name: string,
    fileName: string,
    data: ArrayBuffer,
    mimeType: string = 'application/zip'
  ): Promise<CachedZipMetadata> {
    await this.init();
    if (!this.db) throw new Error('Database not initialized');

    const id = `${namespace}/${name}`;
    const metadata: CachedZipMetadata = {
      id,
      namespace,
      name,
      fileName,
      size: data.byteLength,
      mimeType,
      downloadedAt: Date.now(),
    };

    // Save metadata
    const metadataTx = this.db.transaction('zips', 'readwrite');
    const metadataStore = metadataTx.objectStore('zips');
    metadataStore.put(metadata);

    await new Promise((resolve, reject) => {
      metadataTx.oncomplete = () => resolve(undefined);
      metadataTx.onerror = () => reject(metadataTx.error);
    });

    // Save actual ZIP data
    const dataTx = this.db.transaction('zipData', 'readwrite');
    const dataStore = dataTx.objectStore('zipData');
    dataStore.put({ key: id, data: new Uint8Array(data) });

    await new Promise((resolve, reject) => {
      dataTx.oncomplete = () => resolve(undefined);
      dataTx.onerror = () => reject(dataTx.error);
    });

    return metadata;
  }

  async getZipData(namespace: string, name: string): Promise<Uint8Array | null> {
    await this.init();
    if (!this.db) throw new Error('Database not initialized');

    const id = `${namespace}/${name}`;
    const tx = this.db.transaction('zipData', 'readonly');
    const store = tx.objectStore('zipData');

    return new Promise((resolve, reject) => {
      const request = store.get(id);
      request.onsuccess = () => {
        const result = request.result;
        resolve(result ? result.data : null);
      };
      request.onerror = () => reject(request.error);
    });
  }

  async getZipMetadata(namespace: string, name: string): Promise<CachedZipMetadata | null> {
    await this.init();
    if (!this.db) throw new Error('Database not initialized');

    const id = `${namespace}/${name}`;
    const tx = this.db.transaction('zips', 'readonly');
    const store = tx.objectStore('zips');

    return new Promise((resolve, reject) => {
      const request = store.get(id);
      request.onsuccess = () => resolve(request.result || null);
      request.onerror = () => reject(request.error);
    });
  }

  async getZip(namespace: string, name: string): Promise<{ metadata: CachedZipMetadata; data: Uint8Array } | null> {
    const metadata = await this.getZipMetadata(namespace, name);
    if (!metadata) return null;

    const data = await this.getZipData(namespace, name);
    if (!data) return null;

    return { metadata, data };
  }

  async deleteZip(namespace: string, name: string): Promise<void> {
    await this.init();
    if (!this.db) throw new Error('Database not initialized');

    const id = `${namespace}/${name}`;

    // Delete metadata
    const metadataTx = this.db.transaction('zips', 'readwrite');
    const metadataStore = metadataTx.objectStore('zips');
    metadataStore.delete(id);

    await new Promise((resolve, reject) => {
      metadataTx.oncomplete = () => resolve(undefined);
      metadataTx.onerror = () => reject(metadataTx.error);
    });

    // Delete data
    const dataTx = this.db.transaction('zipData', 'readwrite');
    const dataStore = dataTx.objectStore('zipData');
    dataStore.delete(id);

    await new Promise((resolve, reject) => {
      dataTx.oncomplete = () => resolve(undefined);
      dataTx.onerror = () => reject(dataTx.error);
    });
  }

  async listZips(): Promise<CachedZipMetadata[]> {
    await this.init();
    if (!this.db) throw new Error('Database not initialized');

    const tx = this.db.transaction('zips', 'readonly');
    const store = tx.objectStore('zips');

    return new Promise((resolve, reject) => {
      const request = store.getAll();
      request.onsuccess = () => resolve(request.result);
      request.onerror = () => reject(request.error);
    });
  }

  async clearAll(): Promise<void> {
    await this.init();
    if (!this.db) throw new Error('Database not initialized');

    const tx = this.db.transaction(['zips', 'zipData'], 'readwrite');
    tx.objectStore('zips').clear();
    tx.objectStore('zipData').clear();

    await new Promise((resolve, reject) => {
      tx.oncomplete = () => resolve(undefined);
      tx.onerror = () => reject(tx.error);
    });
  }

  async getStorageUsage(): Promise<{ used: number; limit: number }> {
    if (!navigator.storage || !navigator.storage.estimate) {
      return { used: 0, limit: 0 };
    }

    const estimate = await navigator.storage.estimate();
    return {
      used: estimate.usage || 0,
      limit: estimate.quota || 0,
    };
  }
}

// Singleton instance
export const zipCache = new ZipCacheStore();
