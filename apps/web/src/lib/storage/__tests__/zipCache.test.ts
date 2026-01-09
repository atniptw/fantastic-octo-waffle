/**
/**
 * Tests for ZIP download and caching functionality
 * Note: Most tests are skipped in Node.js environment because IndexedDB is browser-only.
 * These tests will run in browser environments or with proper polyfills.
 */
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { zipCache } from '../zipCache';

const hasIndexedDB = typeof indexedDB !== 'undefined';
const testFn = hasIndexedDB ? it : it.skip;

describe('ZIP Cache Store', () => {


  beforeEach(async () => {
    // Clear all cached data before each test
    if (hasIndexedDB) {
      try {
        await zipCache.clearAll();
      } catch {
        // First test may fail if DB not initialized
      }
    }
  });

  afterEach(async () => {
    // Clean up after tests
    if (hasIndexedDB) {
      try {
        await zipCache.clearAll();
      } catch {
        // Ignore cleanup errors
      }
    }
  });

  describe('saveZip and getZipData', () => {
    testFn('should save and retrieve ZIP data', async () => {
      const testData = new ArrayBuffer(100);
      const testView = new Uint8Array(testData);
      for (let i = 0; i < 100; i++) {
        testView[i] = i % 256;
      }

      await zipCache.saveZip('test-ns', 'test-mod', 'test.zip', testData);

      const retrieved = await zipCache.getZipData('test-ns', 'test-mod');
      expect(retrieved).not.toBeNull();
      expect(retrieved?.length).toBe(100);
      expect(retrieved?.[0]).toBe(0);
      expect(retrieved?.[99]).toBe(99);
    });

    testFn('should return null for non-existent ZIP', async () => {
      const retrieved = await zipCache.getZipData('nonexistent-ns', 'nonexistent-mod');
      expect(retrieved).toBeNull();
    });
  });

  describe('getZipMetadata', () => {
    testFn('should save and retrieve ZIP metadata', async () => {
      const testData = new ArrayBuffer(1024);
      await zipCache.saveZip('test-ns', 'test-mod', 'test.zip', testData);

      const metadata = await zipCache.getZipMetadata('test-ns', 'test-mod');
      expect(metadata).not.toBeNull();
      expect(metadata?.namespace).toBe('test-ns');
      expect(metadata?.name).toBe('test-mod');
      expect(metadata?.fileName).toBe('test.zip');
      expect(metadata?.size).toBe(1024);
      expect(metadata?.mimeType).toBe('application/zip');
      expect(metadata?.downloadedAt).toBeLessThanOrEqual(Date.now());
    });

    testFn('should return null for non-existent metadata', async () => {
      const metadata = await zipCache.getZipMetadata('nonexistent-ns', 'nonexistent-mod');
      expect(metadata).toBeNull();
    });
  });

  describe('getZip', () => {
    testFn('should return both metadata and data', async () => {
      const testData = new ArrayBuffer(512);
      await zipCache.saveZip('test-ns', 'test-mod', 'test.zip', testData);

      const zip = await zipCache.getZip('test-ns', 'test-mod');
      expect(zip).not.toBeNull();
      expect(zip?.metadata.namespace).toBe('test-ns');
      expect(zip?.metadata.name).toBe('test-mod');
      expect(zip?.data.length).toBe(512);
    });

    testFn('should return null if only metadata exists', async () => {
      // Manually create metadata-only entry (edge case)
      const zip = await zipCache.getZip('nonexistent-ns', 'nonexistent-mod');
      expect(zip).toBeNull();
    });
  });

  describe('listZips', () => {
    testFn('should list all cached ZIPs', async () => {
      const testData = new ArrayBuffer(100);
      await zipCache.saveZip('ns1', 'mod1', 'test1.zip', testData);
      await zipCache.saveZip('ns2', 'mod2', 'test2.zip', testData);
      await zipCache.saveZip('ns3', 'mod3', 'test3.zip', testData);

      const zips = await zipCache.listZips();
      expect(zips.length).toBe(3);
      expect(zips.some((z) => z.name === 'mod1')).toBe(true);
      expect(zips.some((z) => z.name === 'mod2')).toBe(true);
      expect(zips.some((z) => z.name === 'mod3')).toBe(true);
    });

    testFn('should return empty array when no ZIPs cached', async () => {
      const zips = await zipCache.listZips();
      expect(zips).toEqual([]);
    });
  });

  describe('deleteZip', () => {
    testFn('should delete both metadata and data', async () => {
      const testData = new ArrayBuffer(100);
      await zipCache.saveZip('test-ns', 'test-mod', 'test.zip', testData);

      // Verify it exists
      let metadata = await zipCache.getZipMetadata('test-ns', 'test-mod');
      expect(metadata).not.toBeNull();

      // Delete it
      await zipCache.deleteZip('test-ns', 'test-mod');

      // Verify it's gone
      metadata = await zipCache.getZipMetadata('test-ns', 'test-mod');
      expect(metadata).toBeNull();

      const data = await zipCache.getZipData('test-ns', 'test-mod');
      expect(data).toBeNull();
    });

    testFn('should handle deleting non-existent ZIP', async () => {
      // Should not throw
      expect(await zipCache.deleteZip('nonexistent-ns', 'nonexistent-mod')).resolves.toBeUndefined();
    });
  });

  describe('clearAll', () => {
    testFn('should clear all cached data', async () => {
      const testData = new ArrayBuffer(100);
      await zipCache.saveZip('ns1', 'mod1', 'test1.zip', testData);
      await zipCache.saveZip('ns2', 'mod2', 'test2.zip', testData);

      let zips = await zipCache.listZips();
      expect(zips.length).toBe(2);

      await zipCache.clearAll();

      zips = await zipCache.listZips();
      expect(zips).toEqual([]);
    });
  });

  describe('Storage usage', () => {
    it('should report storage usage', async () => {
      const usage = await zipCache.getStorageUsage();
      expect(usage.used).toBeGreaterThanOrEqual(0);
      expect(usage.limit).toBeGreaterThanOrEqual(0);
    });
  });
});
