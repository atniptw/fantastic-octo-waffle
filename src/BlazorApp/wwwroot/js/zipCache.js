/**
 * IndexedDB-backed ZIP cache for large mod archives.
 * Stores ZIP in fixed-size chunks and enables range reads for listing/extraction.
 */

const DB_NAME = 'RepoModBrowserZipCache';
const DB_VERSION = 1;
const META_STORE = 'zipMeta';
const CHUNK_STORE = 'zipChunks';

let dbPromise = null;
function initDb() {
  if (dbPromise) return dbPromise;

  dbPromise = new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, DB_VERSION);

    request.onerror = () => reject(request.error);
    request.onsuccess = () => resolve(request.result);
    request.onupgradeneeded = event => {
      const db = event.target.result;

      if (!db.objectStoreNames.contains(META_STORE)) {
        db.createObjectStore(META_STORE, { keyPath: 'cacheKey' });
      }

      if (!db.objectStoreNames.contains(CHUNK_STORE)) {
        const store = db.createObjectStore(CHUNK_STORE, { keyPath: 'id' });
        store.createIndex('cacheKey', 'cacheKey', { unique: false });
        store.createIndex('index', 'index', { unique: false });
      }
    };
  });

  return dbPromise;
}

async function withStore(storeName, mode, action) {
  const db = await initDb();
  return new Promise((resolve, reject) => {
    const tx = db.transaction([storeName], mode);
    const store = tx.objectStore(storeName);
    const result = action(store);
    tx.oncomplete = () => resolve(result);
    tx.onerror = () => reject(tx.error);
  });
}

async function getMeta(cacheKey) {
  const db = await initDb();
  return new Promise((resolve, reject) => {
    const tx = db.transaction([META_STORE], 'readonly');
    const store = tx.objectStore(META_STORE);
    const request = store.get(cacheKey);
    request.onsuccess = () => resolve(request.result || null);
    request.onerror = () => reject(request.error);
  });
}

async function getChunk(cacheKey, index) {
  const id = `${cacheKey}:${index}`;
  const db = await initDb();
  return new Promise((resolve, reject) => {
    const tx = db.transaction([CHUNK_STORE], 'readonly');
    const store = tx.objectStore(CHUNK_STORE);
    const request = store.get(id);
    request.onsuccess = () => resolve(request.result ? new Uint8Array(request.result.data) : null);
    request.onerror = () => reject(request.error);
  });
}

async function readRange(cacheKey, start, end) {
  if (end < start) return new Uint8Array(0);

  const meta = await getMeta(cacheKey);
  if (!meta) throw new Error('ZIP cache not found');

  const chunkSize = meta.chunkSize;
  const startChunk = Math.floor(start / chunkSize);
  const endChunk = Math.floor(end / chunkSize);
  const length = end - start + 1;
  const result = new Uint8Array(length);

  let offset = 0;
  for (let index = startChunk; index <= endChunk; index++) {
    const chunk = await getChunk(cacheKey, index);
    if (!chunk) throw new Error(`Missing ZIP chunk ${index}`);

    const chunkStart = index * chunkSize;
    const sliceStart = Math.max(0, start - chunkStart);
    const sliceEnd = Math.min(chunk.length, end - chunkStart + 1);
    const slice = chunk.subarray(sliceStart, sliceEnd);

    result.set(slice, offset);
    offset += slice.length;
  }

  return result;
}

function findEocd(buffer) {
  for (let i = buffer.length - 22; i >= 0; i--) {
    if (
      buffer[i] === 0x50 &&
      buffer[i + 1] === 0x4b &&
      buffer[i + 2] === 0x05 &&
      buffer[i + 3] === 0x06
    ) {
      return i;
    }
  }
  return -1;
}

function parseCentralDirectory(buffer) {
  const entries = [];
  const view = new DataView(buffer.buffer, buffer.byteOffset, buffer.byteLength);
  let offset = 0;
  const decoder = new TextDecoder('utf-8');

  while (offset + 46 <= buffer.length) {
    const signature = view.getUint32(offset, true);
    if (signature !== 0x02014b50) {
      break;
    }

    const method = view.getUint16(offset + 10, true);
    const compressedSize = view.getUint32(offset + 20, true);
    const uncompressedSize = view.getUint32(offset + 24, true);
    const nameLength = view.getUint16(offset + 28, true);
    const extraLength = view.getUint16(offset + 30, true);
    const commentLength = view.getUint16(offset + 32, true);
    const localHeaderOffset = view.getUint32(offset + 42, true);

    const nameStart = offset + 46;
    const nameEnd = nameStart + nameLength;
    const path = decoder.decode(buffer.slice(nameStart, nameEnd));

    entries.push({
      path,
      method,
      compressedSize,
      uncompressedSize,
      localHeaderOffset,
    });

    offset = nameEnd + extraLength + commentLength;
  }

  return entries;
}

async function getEntries(cacheKey) {
  const meta = await getMeta(cacheKey);
  if (!meta) throw new Error('ZIP cache not found');

  const totalSize = meta.totalSize;
  const tailSize = Math.min(65536, totalSize);
  const tailStart = Math.max(0, totalSize - tailSize);

  const tailBuffer = await readRange(cacheKey, tailStart, totalSize - 1);
  const eocdOffset = findEocd(tailBuffer);
  if (eocdOffset < 0) {
    throw new Error('EOCD not found');
  }

  const eocdView = new DataView(tailBuffer.buffer, tailBuffer.byteOffset + eocdOffset);
  const centralDirSize = eocdView.getUint32(12, true);
  const centralDirOffset = eocdView.getUint32(16, true);

  const cdBuffer = await readRange(
    cacheKey,
    centralDirOffset,
    centralDirOffset + centralDirSize - 1
  );
  return parseCentralDirectory(cdBuffer);
}

async function start(cacheKey) {
  await clear(cacheKey);
}

async function appendChunk(cacheKey, index, chunk) {
  const db = await initDb();
  return new Promise((resolve, reject) => {
    const tx = db.transaction([CHUNK_STORE], 'readwrite');
    const store = tx.objectStore(CHUNK_STORE);
    const entry = {
      id: `${cacheKey}:${index}`,
      cacheKey,
      index,
      data: chunk,
    };
    const request = store.put(entry);
    request.onsuccess = () => resolve();
    request.onerror = () => reject(request.error);
  });
}

async function finalize(cacheKey, totalSize, chunkSize, chunkCount) {
  await withStore(META_STORE, 'readwrite', store =>
    store.put({
      cacheKey,
      totalSize,
      chunkSize,
      chunkCount,
      createdAt: Date.now(),
    })
  );
}

async function hasZip(cacheKey) {
  const meta = await getMeta(cacheKey);
  return !!meta;
}

async function listHhhFiles(cacheKey) {
  const entries = await getEntries(cacheKey);
  return entries
    .filter(entry => entry.path.toLowerCase().endsWith('.hhh'))
    .map(entry => ({
      path: entry.path,
      sizeBytes: entry.uncompressedSize,
    }));
}

async function getFileBytes(cacheKey, filePath) {
  const entries = await getEntries(cacheKey);
  const entry = entries.find(candidate => candidate.path === filePath);
  if (!entry) throw new Error('File not found in archive');

  const headerBuffer = await readRange(
    cacheKey,
    entry.localHeaderOffset,
    entry.localHeaderOffset + 29
  );
  const headerView = new DataView(
    headerBuffer.buffer,
    headerBuffer.byteOffset,
    headerBuffer.byteLength
  );
  const nameLength = headerView.getUint16(26, true);
  const extraLength = headerView.getUint16(28, true);
  const dataStart = entry.localHeaderOffset + 30 + nameLength + extraLength;
  const dataEnd = dataStart + entry.compressedSize - 1;

  const compressed = await readRange(cacheKey, dataStart, dataEnd);

  if (entry.method === 0) {
    return compressed;
  }

  if (entry.method !== 8) {
    throw new Error('Unsupported compression method');
  }

  if (typeof DecompressionStream !== 'function') {
    throw new Error('Deflate-raw decompression is not supported in this browser');
  }

  const stream = new Response(compressed).body.pipeThrough(new DecompressionStream('deflate-raw'));
  const arrayBuffer = await new Response(stream).arrayBuffer();
  return new Uint8Array(arrayBuffer);
}

async function clear(cacheKey) {
  const db = await initDb();
  await new Promise((resolve, reject) => {
    const tx = db.transaction([META_STORE], 'readwrite');
    const store = tx.objectStore(META_STORE);
    const request = store.delete(cacheKey);
    request.onsuccess = () => resolve();
    request.onerror = () => reject(request.error);
  });

  await new Promise((resolve, reject) => {
    const tx = db.transaction([CHUNK_STORE], 'readwrite');
    const store = tx.objectStore(CHUNK_STORE);
    const index = store.index('cacheKey');
    const request = index.openCursor(IDBKeyRange.only(cacheKey));
    request.onsuccess = event => {
      const cursor = event.target.result;
      if (cursor) {
        cursor.delete();
        cursor.continue();
      } else {
        resolve();
      }
    };
    request.onerror = () => reject(request.error);
  });
}

window.zipCache = {
  start,
  appendChunk,
  finalize,
  hasZip,
  listHhhFiles,
  getFileBytes,
  clear,
};
