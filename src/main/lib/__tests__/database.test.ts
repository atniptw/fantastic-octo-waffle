import { describe, it, expect, beforeEach } from 'vitest';
import {
  DatabaseWrapper,
  Mod,
  Cosmetic,
  SCHEMA,
  MIGRATIONS,
  getCurrentSchemaVersion,
  getPendingMigrations,
} from '../database';

describe('Schema and Migrations', () => {
  describe('SCHEMA', () => {
    it('should have version 1', () => {
      expect(SCHEMA.version).toBe(1);
    });

    it('should define mods table', () => {
      expect(SCHEMA.tables.mods).toContain('CREATE TABLE');
      expect(SCHEMA.tables.mods).toContain('mod_name TEXT NOT NULL');
      expect(SCHEMA.tables.mods).toContain('source_zip TEXT NOT NULL UNIQUE');
    });

    it('should define cosmetics table', () => {
      expect(SCHEMA.tables.cosmetics).toContain('CREATE TABLE');
      expect(SCHEMA.tables.cosmetics).toContain('mod_id INTEGER NOT NULL');
      expect(SCHEMA.tables.cosmetics).toContain('FOREIGN KEY');
    });

    it('should define indexes', () => {
      expect(SCHEMA.indexes.cosmetics_mod_id).toContain('CREATE INDEX');
      expect(SCHEMA.indexes.cosmetics_display_name).toContain('CREATE INDEX');
      expect(SCHEMA.indexes.mods_source_zip).toContain('CREATE INDEX');
    });
  });

  describe('MIGRATIONS', () => {
    it('should have at least one migration', () => {
      expect(MIGRATIONS.length).toBeGreaterThan(0);
    });

    it('should have migration version 1', () => {
      expect(MIGRATIONS[0].version).toBe(1);
    });

    it('should have up and down statements', () => {
      expect(MIGRATIONS[0].up.length).toBeGreaterThan(0);
      expect(MIGRATIONS[0].down.length).toBeGreaterThan(0);
    });
  });

  describe('getCurrentSchemaVersion', () => {
    it('should return the latest migration version', () => {
      expect(getCurrentSchemaVersion()).toBe(1);
    });
  });

  describe('getPendingMigrations', () => {
    it('should return all migrations when starting from version 0', () => {
      const pending = getPendingMigrations(0);
      expect(pending.length).toBe(MIGRATIONS.length);
    });

    it('should return no migrations when already at latest', () => {
      const pending = getPendingMigrations(getCurrentSchemaVersion());
      expect(pending.length).toBe(0);
    });
  });
});

describe('DatabaseWrapper', () => {
  let db: DatabaseWrapper;

  beforeEach(async () => {
    db = new DatabaseWrapper();
    await db.initialize();
  });

  describe('initialize', () => {
    it('should initialize the database', async () => {
      expect(await db.getModCount()).toBe(0);
      expect(await db.getCosmeticCount()).toBe(0);
    });

    it('should set initialized flag', async () => {
      const newDb = new DatabaseWrapper();
      expect(newDb.isInitialized()).toBe(false);
      await newDb.initialize();
      expect(newDb.isInitialized()).toBe(true);
    });

    it('should set schema version', async () => {
      expect(db.getSchemaVersion()).toBe(getCurrentSchemaVersion());
    });
  });

  describe('insertMod', () => {
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
});
