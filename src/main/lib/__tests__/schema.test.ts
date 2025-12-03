import { describe, it, expect } from 'vitest';
import {
  SCHEMA,
  MIGRATIONS,
  getCurrentSchemaVersion,
  getPendingMigrations,
} from '../schema';

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
