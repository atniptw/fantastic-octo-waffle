import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { DatabaseWrapper, Mod, Cosmetic } from '../database';
import { getCurrentSchemaVersion } from '../schema';

describe('DatabaseWrapper', () => {
  let db: DatabaseWrapper;

  beforeEach(async () => {
    // Use in-memory database for testing
    db = new DatabaseWrapper(':memory:');
    await db.initialize();
  });

  afterEach(async () => {
    await db.close();
  });

  describe('initialize', () => {
    it('should initialize the database', async () => {
      expect(await db.getModCount()).toBe(0);
      expect(await db.getCosmeticCount()).toBe(0);
    });

    it('should set initialized flag', async () => {
      const newDb = new DatabaseWrapper(':memory:');
      expect(newDb.isInitialized()).toBe(false);
      await newDb.initialize();
      expect(newDb.isInitialized()).toBe(true);
      await newDb.close();
    });

    it('should set schema version', async () => {
      expect(db.getSchemaVersion()).toBe(getCurrentSchemaVersion());
    });

    it('should be idempotent', async () => {
      // Multiple initializations should not throw
      await db.initialize();
      await db.initialize();
      expect(db.isInitialized()).toBe(true);
    });
  });

  describe('insertMod / insertOrGetMod', () => {
    it('should insert a mod and return its ID', async () => {
      const mod: Omit<Mod, 'id'> = {
        mod_name: 'TestMod',
        author: 'TestAuthor',
        version: '1.0.0',
        icon_path: '/path/to/icon.png',
        source_zip: '/path/to/mod.zip',
      };

      const id = await db.insertMod(mod);
      expect(id).toBe(1);
      expect(await db.getModCount()).toBe(1);
    });

    it('should assign incrementing IDs to multiple mods', async () => {
      const mod1: Omit<Mod, 'id'> = {
        mod_name: 'Mod1',
        author: 'Author1',
        version: '1.0.0',
        icon_path: null,
        source_zip: '/path/to/mod1.zip',
      };
      const mod2: Omit<Mod, 'id'> = {
        mod_name: 'Mod2',
        author: 'Author2',
        version: '2.0.0',
        icon_path: null,
        source_zip: '/path/to/mod2.zip',
      };

      const id1 = await db.insertMod(mod1);
      const id2 = await db.insertMod(mod2);

      expect(id1).toBe(1);
      expect(id2).toBe(2);
      expect(await db.getModCount()).toBe(2);
    });

    it('should return existing mod ID when inserting duplicate by identity', async () => {
      const mod: Omit<Mod, 'id'> = {
        mod_name: 'TestMod',
        author: 'TestAuthor',
        version: '1.0.0',
        icon_path: null,
        source_zip: '/path/to/mod1.zip',
      };

      const id1 = await db.insertOrGetMod(mod);
      // Same mod_name, author, version but different source_zip
      const modDuplicate: Omit<Mod, 'id'> = {
        ...mod,
        source_zip: '/path/to/mod2.zip',
      };
      const id2 = await db.insertOrGetMod(modDuplicate);

      expect(id1).toBe(id2);
      expect(await db.getModCount()).toBe(1);
    });

    it('should insert different versions of same mod as separate entries', async () => {
      const mod1: Omit<Mod, 'id'> = {
        mod_name: 'TestMod',
        author: 'TestAuthor',
        version: '1.0.0',
        icon_path: null,
        source_zip: '/path/to/mod1.zip',
      };
      const mod2: Omit<Mod, 'id'> = {
        mod_name: 'TestMod',
        author: 'TestAuthor',
        version: '2.0.0',
        icon_path: null,
        source_zip: '/path/to/mod2.zip',
      };

      const id1 = await db.insertOrGetMod(mod1);
      const id2 = await db.insertOrGetMod(mod2);

      expect(id1).not.toBe(id2);
      expect(await db.getModCount()).toBe(2);
    });
  });

  describe('insertCosmetic', () => {
    it('should insert a cosmetic and return its ID', async () => {
      const modId = await db.insertMod({
        mod_name: 'TestMod',
        author: 'TestAuthor',
        version: '1.0.0',
        icon_path: null,
        source_zip: '/path/to/mod.zip',
      });

      const cosmetic: Omit<Cosmetic, 'id'> = {
        mod_id: modId,
        display_name: 'Cool Hat',
        filename: 'cool_hat.hhh',
        hash: 'abc123',
        type: 'decoration',
        internal_path: 'plugins/TestMod/Decorations/cool_hat.hhh',
      };

      const cosmeticId = await db.insertCosmetic(cosmetic);
      expect(cosmeticId).toBe(1);
      expect(await db.getCosmeticCount()).toBe(1);
    });

    it('should return existing cosmetic ID when inserting duplicate by hash', async () => {
      const modId = await db.insertMod({
        mod_name: 'TestMod',
        author: 'TestAuthor',
        version: '1.0.0',
        icon_path: null,
        source_zip: '/path/to/mod.zip',
      });

      const cosmetic: Omit<Cosmetic, 'id'> = {
        mod_id: modId,
        display_name: 'Cool Hat',
        filename: 'cool_hat.hhh',
        hash: 'same_hash_value',
        type: 'decoration',
        internal_path: 'plugins/TestMod/Decorations/cool_hat.hhh',
      };

      const id1 = await db.insertCosmetic(cosmetic);
      // Same hash, different display name
      const cosmeticDuplicate: Omit<Cosmetic, 'id'> = {
        ...cosmetic,
        display_name: 'Different Name',
      };
      const id2 = await db.insertCosmetic(cosmeticDuplicate);

      expect(id1).toBe(id2);
      expect(await db.getCosmeticCount()).toBe(1);
    });
  });

  describe('getModBySourceZip', () => {
    it('should return the mod by source zip path', async () => {
      const sourcePath = '/path/to/specific-mod.zip';
      await db.insertMod({
        mod_name: 'SpecificMod',
        author: 'Author',
        version: '1.0.0',
        icon_path: null,
        source_zip: sourcePath,
      });

      const mod = await db.getModBySourceZip(sourcePath);
      expect(mod).not.toBeNull();
      expect(mod?.mod_name).toBe('SpecificMod');
    });

    it('should return null for non-existent source zip', async () => {
      const mod = await db.getModBySourceZip('/non-existent.zip');
      expect(mod).toBeNull();
    });
  });

  describe('getModByIdentity', () => {
    it('should return the mod by name, author, version', async () => {
      await db.insertMod({
        mod_name: 'TestMod',
        author: 'TestAuthor',
        version: '1.0.0',
        icon_path: null,
        source_zip: '/path/to/mod.zip',
      });

      const mod = await db.getModByIdentity('TestMod', 'TestAuthor', '1.0.0');
      expect(mod).not.toBeNull();
      expect(mod?.source_zip).toBe('/path/to/mod.zip');
    });

    it('should return null for non-existent identity', async () => {
      const mod = await db.getModByIdentity('NonExistent', 'Nobody', '0.0.0');
      expect(mod).toBeNull();
    });
  });

  describe('getAllMods', () => {
    it('should return all mods', async () => {
      await db.insertMod({
        mod_name: 'Mod1',
        author: 'Author1',
        version: '1.0.0',
        icon_path: null,
        source_zip: '/path/to/mod1.zip',
      });
      await db.insertMod({
        mod_name: 'Mod2',
        author: 'Author2',
        version: '2.0.0',
        icon_path: null,
        source_zip: '/path/to/mod2.zip',
      });

      const mods = await db.getAllMods();
      expect(mods).toHaveLength(2);
      expect(mods[0].mod_name).toBe('Mod1');
      expect(mods[1].mod_name).toBe('Mod2');
    });
  });

  describe('getCosmeticsByModId', () => {
    it('should return cosmetics for a specific mod', async () => {
      const modId = await db.insertMod({
        mod_name: 'TestMod',
        author: 'Author',
        version: '1.0.0',
        icon_path: null,
        source_zip: '/path/to/mod.zip',
      });

      await db.insertCosmetic({
        mod_id: modId,
        display_name: 'Hat 1',
        filename: 'hat1.hhh',
        hash: 'hash1',
        type: 'decoration',
        internal_path: 'plugins/TestMod/Decorations/hat1.hhh',
      });
      await db.insertCosmetic({
        mod_id: modId,
        display_name: 'Hat 2',
        filename: 'hat2.hhh',
        hash: 'hash2',
        type: 'decoration',
        internal_path: 'plugins/TestMod/Decorations/hat2.hhh',
      });

      const cosmetics = await db.getCosmeticsByModId(modId);
      expect(cosmetics).toHaveLength(2);
    });

    it('should return empty array for mod with no cosmetics', async () => {
      const cosmetics = await db.getCosmeticsByModId(999);
      expect(cosmetics).toHaveLength(0);
    });
  });

  describe('getCosmeticByHash', () => {
    it('should return cosmetic by hash', async () => {
      const modId = await db.insertMod({
        mod_name: 'TestMod',
        author: 'Author',
        version: '1.0.0',
        icon_path: null,
        source_zip: '/path/to/mod.zip',
      });

      await db.insertCosmetic({
        mod_id: modId,
        display_name: 'Test Cosmetic',
        filename: 'test.hhh',
        hash: 'unique_hash_123',
        type: 'decoration',
        internal_path: 'plugins/TestMod/Decorations/test.hhh',
      });

      const cosmetic = await db.getCosmeticByHash('unique_hash_123');
      expect(cosmetic).not.toBeNull();
      expect(cosmetic?.display_name).toBe('Test Cosmetic');
    });

    it('should return null for non-existent hash', async () => {
      const cosmetic = await db.getCosmeticByHash('nonexistent');
      expect(cosmetic).toBeNull();
    });
  });

  describe('getAllCosmetics', () => {
    it('should return all cosmetics in the database', async () => {
      const mod1Id = await db.insertMod({
        mod_name: 'Mod1',
        author: 'Author1',
        version: '1.0.0',
        icon_path: null,
        source_zip: '/path/to/mod1.zip',
      });
      const mod2Id = await db.insertMod({
        mod_name: 'Mod2',
        author: 'Author2',
        version: '1.0.0',
        icon_path: null,
        source_zip: '/path/to/mod2.zip',
      });

      await db.insertCosmetic({
        mod_id: mod1Id,
        display_name: 'Hat 1',
        filename: 'hat1.hhh',
        hash: 'hash1',
        type: 'decoration',
        internal_path: 'plugins/Mod1/Decorations/hat1.hhh',
      });
      await db.insertCosmetic({
        mod_id: mod2Id,
        display_name: 'Hat 2',
        filename: 'hat2.hhh',
        hash: 'hash2',
        type: 'decoration',
        internal_path: 'plugins/Mod2/Decorations/hat2.hhh',
      });

      const cosmetics = await db.getAllCosmetics();
      expect(cosmetics).toHaveLength(2);
      expect(cosmetics[0].display_name).toBe('Hat 1');
      expect(cosmetics[1].display_name).toBe('Hat 2');
    });

    it('should return empty array when no cosmetics exist', async () => {
      const cosmetics = await db.getAllCosmetics();
      expect(cosmetics).toHaveLength(0);
    });
  });

  describe('queryCosmetics', () => {
    beforeEach(async () => {
      const modId = await db.insertMod({
        mod_name: 'TestMod',
        author: 'Author',
        version: '1.0.0',
        icon_path: null,
        source_zip: '/path/to/mod.zip',
      });

      await db.insertCosmetic({
        mod_id: modId,
        display_name: 'Cool Blue Hat',
        filename: 'cool_blue_hat.hhh',
        hash: 'hash1',
        type: 'decoration',
        internal_path: 'plugins/TestMod/Decorations/cool_blue_hat.hhh',
      });
      await db.insertCosmetic({
        mod_id: modId,
        display_name: 'Red Glasses',
        filename: 'red_glasses.hhh',
        hash: 'hash2',
        type: 'accessory',
        internal_path: 'plugins/TestMod/Decorations/red_glasses.hhh',
      });
      await db.insertCosmetic({
        mod_id: modId,
        display_name: 'Green Shoes',
        filename: 'green_shoes.hhh',
        hash: 'hash3',
        type: 'footwear',
        internal_path: 'plugins/TestMod/Decorations/green_shoes.hhh',
      });
    });

    it('should filter by display_name', async () => {
      const results = await db.queryCosmetics({ display_name: 'Blue' });
      expect(results).toHaveLength(1);
      expect(results[0].display_name).toBe('Cool Blue Hat');
    });

    it('should filter by type', async () => {
      const results = await db.queryCosmetics({ type: 'accessory' });
      expect(results).toHaveLength(1);
      expect(results[0].display_name).toBe('Red Glasses');
    });

    it('should filter by mod_id', async () => {
      const results = await db.queryCosmetics({ mod_id: 1 });
      expect(results).toHaveLength(3);
    });

    it('should filter by free text across multiple fields', async () => {
      const results = await db.queryCosmetics({ text: 'shoes' });
      expect(results).toHaveLength(1);
      expect(results[0].display_name).toBe('Green Shoes');
    });

    it('should combine multiple filters', async () => {
      const results = await db.queryCosmetics({
        mod_id: 1,
        type: 'decoration',
      });
      expect(results).toHaveLength(1);
      expect(results[0].display_name).toBe('Cool Blue Hat');
    });

    it('should return all cosmetics with empty filters', async () => {
      const results = await db.queryCosmetics({});
      expect(results).toHaveLength(3);
    });
  });

  describe('searchCosmetics', () => {
    it('should find cosmetics by display name', async () => {
      const modId = await db.insertMod({
        mod_name: 'TestMod',
        author: 'Author',
        version: '1.0.0',
        icon_path: null,
        source_zip: '/path/to/mod.zip',
      });

      await db.insertCosmetic({
        mod_id: modId,
        display_name: 'Cool Blue Hat',
        filename: 'cool_blue_hat.hhh',
        hash: 'hash1',
        type: 'decoration',
        internal_path: 'plugins/TestMod/Decorations/cool_blue_hat.hhh',
      });
      await db.insertCosmetic({
        mod_id: modId,
        display_name: 'Red Glasses',
        filename: 'red_glasses.hhh',
        hash: 'hash2',
        type: 'decoration',
        internal_path: 'plugins/TestMod/Decorations/red_glasses.hhh',
      });

      const results = await db.searchCosmetics('blue');
      expect(results).toHaveLength(1);
      expect(results[0].display_name).toBe('Cool Blue Hat');
    });

    it('should be case insensitive', async () => {
      const modId = await db.insertMod({
        mod_name: 'TestMod',
        author: 'Author',
        version: '1.0.0',
        icon_path: null,
        source_zip: '/path/to/mod.zip',
      });

      await db.insertCosmetic({
        mod_id: modId,
        display_name: 'UPPERCASE HAT',
        filename: 'uppercase_hat.hhh',
        hash: 'hash1',
        type: 'decoration',
        internal_path: 'plugins/TestMod/Decorations/uppercase_hat.hhh',
      });

      const results = await db.searchCosmetics('uppercase');
      expect(results).toHaveLength(1);
    });
  });

  describe('modExists', () => {
    it('should return true if mod exists', async () => {
      const sourcePath = '/path/to/existing.zip';
      await db.insertMod({
        mod_name: 'ExistingMod',
        author: 'Author',
        version: '1.0.0',
        icon_path: null,
        source_zip: sourcePath,
      });

      expect(await db.modExists(sourcePath)).toBe(true);
    });

    it('should return false if mod does not exist', async () => {
      expect(await db.modExists('/non-existent.zip')).toBe(false);
    });
  });

  describe('modExistsByIdentity', () => {
    it('should return true if mod exists by identity', async () => {
      await db.insertMod({
        mod_name: 'TestMod',
        author: 'TestAuthor',
        version: '1.0.0',
        icon_path: null,
        source_zip: '/path/to/mod.zip',
      });

      expect(await db.modExistsByIdentity('TestMod', 'TestAuthor', '1.0.0')).toBe(true);
    });

    it('should return false if mod does not exist by identity', async () => {
      expect(await db.modExistsByIdentity('NonExistent', 'Nobody', '0.0.0')).toBe(false);
    });
  });

  describe('transaction', () => {
    it('should execute operations atomically', async () => {
      await db.transaction(() => {
        // This would be synchronous in the transaction
      });
      expect(db.isInitialized()).toBe(true);
    });
  });

  describe('close', () => {
    it('should close the database connection', async () => {
      await db.close();
      expect(db.isInitialized()).toBe(false);
    });
  });
});
