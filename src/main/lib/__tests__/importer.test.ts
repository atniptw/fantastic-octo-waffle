import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import {
  parseManifest,
  extractCosmeticInfo,
  calculateHash,
  ModImporter,
} from '../importer';
import { DatabaseWrapper } from '../database';

describe('parseManifest', () => {
  it('should parse valid manifest JSON', () => {
    const manifest = JSON.stringify({
      name: 'TestMod',
      author: 'TestAuthor',
      version_number: '1.0.0',
      description: 'A test mod',
    });

    const result = parseManifest(manifest);
    expect(result).not.toBeNull();
    expect(result?.name).toBe('TestMod');
    expect(result?.author).toBe('TestAuthor');
    expect(result?.version_number).toBe('1.0.0');
    expect(result?.description).toBe('A test mod');
  });

  it('should return null for invalid JSON', () => {
    const result = parseManifest('not valid json');
    expect(result).toBeNull();
  });

  it('should return null for missing required fields', () => {
    const manifest = JSON.stringify({
      name: 'TestMod',
      // missing author and version_number
    });

    const result = parseManifest(manifest);
    expect(result).toBeNull();
  });

  it('should parse manifest with dependencies', () => {
    const manifest = JSON.stringify({
      name: 'TestMod',
      author: 'TestAuthor',
      version_number: '1.0.0',
      dependencies: ['OtherMod-Dependency-1.0.0'],
    });

    const result = parseManifest(manifest);
    expect(result?.dependencies).toEqual(['OtherMod-Dependency-1.0.0']);
  });
});

describe('extractCosmeticInfo', () => {
  it('should extract info from valid cosmetic path', () => {
    const path = 'plugins/MyPlugin/Decorations/cool_hat.hhh';
    const result = extractCosmeticInfo(path);

    expect(result).not.toBeNull();
    expect(result?.displayName).toBe('Cool Hat');
    expect(result?.filename).toBe('cool_hat.hhh');
    expect(result?.type).toBe('decoration');
  });

  it('should handle paths with underscores and hyphens', () => {
    const path = 'plugins/SomeMod/Decorations/my-cool_head.hhh';
    const result = extractCosmeticInfo(path);

    expect(result?.displayName).toBe('My Cool Head');
  });

  it('should return null for non-cosmetic paths', () => {
    expect(extractCosmeticInfo('manifest.json')).toBeNull();
    expect(extractCosmeticInfo('icon.png')).toBeNull();
    expect(extractCosmeticInfo('plugins/Mod/Other/file.txt')).toBeNull();
  });

  it('should be case-insensitive for Decorations folder', () => {
    const pathLower = 'plugins/Mod/decorations/hat.hhh';
    const pathUpper = 'plugins/Mod/DECORATIONS/hat.hhh';

    expect(extractCosmeticInfo(pathLower)).not.toBeNull();
    expect(extractCosmeticInfo(pathUpper)).not.toBeNull();
  });
});

describe('calculateHash', () => {
  it('should return a SHA-256 hash string (64 hex characters)', () => {
    const content = new Uint8Array([1, 2, 3, 4, 5]);
    const hash = calculateHash(content);

    expect(typeof hash).toBe('string');
    expect(hash.length).toBe(64); // SHA-256 produces 64 hex characters
    expect(hash).toMatch(/^[a-f0-9]{64}$/); // Valid hex string
  });

  it('should return different hashes for different content', () => {
    const content1 = new Uint8Array([1, 2, 3]);
    const content2 = new Uint8Array([4, 5, 6]);

    const hash1 = calculateHash(content1);
    const hash2 = calculateHash(content2);

    expect(hash1).not.toBe(hash2);
  });

  it('should return same hash for same content', () => {
    const content1 = new Uint8Array([1, 2, 3, 4, 5]);
    const content2 = new Uint8Array([1, 2, 3, 4, 5]);

    const hash1 = calculateHash(content1);
    const hash2 = calculateHash(content2);

    expect(hash1).toBe(hash2);
  });
});

describe('ModImporter', () => {
  let db: DatabaseWrapper;
  let importer: ModImporter;

  beforeEach(async () => {
    db = new DatabaseWrapper(':memory:');
    await db.initialize();
    importer = new ModImporter(db);
  });

  afterEach(async () => {
    await db.close();
  });

  describe('importMod', () => {
    it('should successfully import a valid mod', async () => {
      const manifest = JSON.stringify({
        name: 'TestMod',
        author: 'TestAuthor',
        version_number: '1.0.0',
      });

      const cosmeticFiles = new Map<string, Uint8Array>();
      cosmeticFiles.set(
        'plugins/TestMod/Decorations/cool_hat.hhh',
        new Uint8Array([1, 2, 3])
      );

      const result = await importer.importMod(
        '/path/to/testmod.zip',
        manifest,
        cosmeticFiles,
        '/path/to/icon.png'
      );

      expect(result.success).toBe(true);
      expect(result.modId).toBe(1);
      expect(result.cosmeticsCount).toBe(1);
    });

    it('should reject duplicate mod imports', async () => {
      const manifest = JSON.stringify({
        name: 'TestMod',
        author: 'TestAuthor',
        version_number: '1.0.0',
      });

      const cosmeticFiles = new Map<string, Uint8Array>();
      const zipPath = '/path/to/testmod.zip';

      // First import should succeed
      const result1 = await importer.importMod(
        zipPath,
        manifest,
        cosmeticFiles
      );
      expect(result1.success).toBe(true);

      // Second import of same path should fail
      const result2 = await importer.importMod(
        zipPath,
        manifest,
        cosmeticFiles
      );
      expect(result2.success).toBe(false);
      expect(result2.error).toBe('Mod already imported');
    });

    it('should reject invalid manifest', async () => {
      const invalidManifest = 'not valid json';
      const cosmeticFiles = new Map<string, Uint8Array>();

      const result = await importer.importMod(
        '/path/to/invalid.zip',
        invalidManifest,
        cosmeticFiles
      );

      expect(result.success).toBe(false);
      expect(result.error).toBe('Invalid manifest.json');
    });

    it('should import multiple cosmetics from one mod', async () => {
      const manifest = JSON.stringify({
        name: 'MultiCosmeticMod',
        author: 'Author',
        version_number: '1.0.0',
      });

      const cosmeticFiles = new Map<string, Uint8Array>();
      cosmeticFiles.set(
        'plugins/MultiCosmeticMod/Decorations/hat1.hhh',
        new Uint8Array([1])
      );
      cosmeticFiles.set(
        'plugins/MultiCosmeticMod/Decorations/hat2.hhh',
        new Uint8Array([2])
      );
      cosmeticFiles.set(
        'plugins/MultiCosmeticMod/Decorations/glasses.hhh',
        new Uint8Array([3])
      );

      const result = await importer.importMod(
        '/path/to/multicosmeticmod.zip',
        manifest,
        cosmeticFiles
      );

      expect(result.success).toBe(true);
      expect(result.cosmeticsCount).toBe(3);
    });

    it('should only import .hhh files from Decorations folder', async () => {
      const manifest = JSON.stringify({
        name: 'MixedMod',
        author: 'Author',
        version_number: '1.0.0',
      });

      const cosmeticFiles = new Map<string, Uint8Array>();
      cosmeticFiles.set(
        'plugins/MixedMod/Decorations/valid.hhh',
        new Uint8Array([1])
      );
      cosmeticFiles.set(
        'plugins/MixedMod/Other/notvalid.hhh',
        new Uint8Array([2])
      );
      cosmeticFiles.set('plugins/MixedMod/config.json', new Uint8Array([3]));

      const result = await importer.importMod(
        '/path/to/mixedmod.zip',
        manifest,
        cosmeticFiles
      );

      expect(result.success).toBe(true);
      expect(result.cosmeticsCount).toBe(1);
    });
  });

  describe('getStats', () => {
    it('should return correct mod and cosmetic counts', async () => {
      const manifest = JSON.stringify({
        name: 'TestMod',
        author: 'Author',
        version_number: '1.0.0',
      });

      const cosmeticFiles = new Map<string, Uint8Array>();
      cosmeticFiles.set(
        'plugins/TestMod/Decorations/hat.hhh',
        new Uint8Array([1])
      );

      await importer.importMod('/path/to/mod.zip', manifest, cosmeticFiles);

      const stats = await importer.getStats();
      expect(stats.modCount).toBe(1);
      expect(stats.cosmeticCount).toBe(1);
    });

    it('should return zero counts for empty database', async () => {
      const stats = await importer.getStats();
      expect(stats.modCount).toBe(0);
      expect(stats.cosmeticCount).toBe(0);
    });
  });
});
