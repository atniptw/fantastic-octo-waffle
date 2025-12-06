import { describe, it, expect } from 'vitest';
import {
  SCHEMA,
  MIGRATIONS,
  getCurrentSchemaVersion,
  getPendingMigrations,
} from '../schema';

describe('Schema and Migrations', () => {
  describe('SCHEMA', () => {
    it('should have version 2', () => {
      expect(SCHEMA.version).toBe(2);
    });

    it('should define mods table with unique constraint on name, author, version', () => {
      expect(SCHEMA.tables.mods).toContain('CREATE TABLE');
      expect(SCHEMA.tables.mods).toContain('mod_name TEXT NOT NULL');
      expect(SCHEMA.tables.mods).toContain('source_zip TEXT NOT NULL UNIQUE');
      expect(SCHEMA.tables.mods).toContain('UNIQUE(mod_name, author, version)');
    });

    it('should define cosmetics table with unique hash constraint', () => {
      expect(SCHEMA.tables.cosmetics).toContain('CREATE TABLE');
      expect(SCHEMA.tables.cosmetics).toContain('mod_id INTEGER NOT NULL');
      expect(SCHEMA.tables.cosmetics).toContain('hash TEXT NOT NULL UNIQUE');
      expect(SCHEMA.tables.cosmetics).toContain('FOREIGN KEY');
    });

    it('should define indexes for fast search', () => {
      expect(SCHEMA.indexes.cosmetics_mod_id).toContain('CREATE INDEX');
      expect(SCHEMA.indexes.cosmetics_display_name).toContain('CREATE INDEX');
      expect(SCHEMA.indexes.cosmetics_mod_type_name).toContain('CREATE INDEX');
      expect(SCHEMA.indexes.cosmetics_hash).toContain('CREATE UNIQUE INDEX');
      expect(SCHEMA.indexes.mods_source_zip).toContain('CREATE INDEX');
      expect(SCHEMA.indexes.mods_name_author_version).toContain('CREATE UNIQUE INDEX');
    });
  });

  describe('MIGRATIONS', () => {
    it('should have two migrations', () => {
      expect(MIGRATIONS.length).toBe(2);
    });

    it('should have migration version 1 for initial schema', () => {
      expect(MIGRATIONS[0].version).toBe(1);
      expect(MIGRATIONS[0].description).toContain('Initial');
    });

    it('should have migration version 2 for unique constraints', () => {
      expect(MIGRATIONS[1].version).toBe(2);
      expect(MIGRATIONS[1].description).toContain('unique constraints');
    });

    it('should have up and down statements for each migration', () => {
      for (const migration of MIGRATIONS) {
        expect(migration.up.length).toBeGreaterThan(0);
        expect(migration.down.length).toBeGreaterThan(0);
      }
    });

    it('should include unique index for mods in migration 2', () => {
      const migration2 = MIGRATIONS[1];
      const hasModsIndex = migration2.up.some((sql) =>
        sql.includes('idx_mods_name_author_version')
      );
      expect(hasModsIndex).toBe(true);
    });

    it('should include unique index for cosmetics hash in migration 2', () => {
      const migration2 = MIGRATIONS[1];
      const hasCosmeticsHashIndex = migration2.up.some((sql) =>
        sql.includes('idx_cosmetics_hash')
      );
      expect(hasCosmeticsHashIndex).toBe(true);
    });

    it('should include composite index for cosmetics search in migration 2', () => {
      const migration2 = MIGRATIONS[1];
      const hasCompositeIndex = migration2.up.some((sql) =>
        sql.includes('idx_cosmetics_mod_type_name')
      );
      expect(hasCompositeIndex).toBe(true);
    });
  });

  describe('getCurrentSchemaVersion', () => {
    it('should return the latest migration version (2)', () => {
      expect(getCurrentSchemaVersion()).toBe(2);
    });
  });

  describe('getPendingMigrations', () => {
    it('should return all migrations when starting from version 0', () => {
      const pending = getPendingMigrations(0);
      expect(pending.length).toBe(MIGRATIONS.length);
      expect(pending[0].version).toBe(1);
      expect(pending[1].version).toBe(2);
    });

    it('should return only migration 2 when starting from version 1', () => {
      const pending = getPendingMigrations(1);
      expect(pending.length).toBe(1);
      expect(pending[0].version).toBe(2);
    });

    it('should return no migrations when already at latest', () => {
      const pending = getPendingMigrations(getCurrentSchemaVersion());
      expect(pending.length).toBe(0);
    });

    it('should respect target version parameter', () => {
      const pending = getPendingMigrations(0, 1);
      expect(pending.length).toBe(1);
      expect(pending[0].version).toBe(1);
    });
  });
});
