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

  describe('importModZips', () => {
    it('should import a single valid ZIP file', async () => {
      // Create a test ZIP file
      const testZipPath = '/tmp/test-mod-single.zip';
      const JSZip = (await import('jszip')).default;
      const zip = new JSZip();
      zip.file('manifest.json', JSON.stringify({
        name: 'TestMod',
        author: 'TestAuthor',
        version_number: '1.0.0',
      }));
      zip.file('plugins/TestMod/Decorations/hat.hhh', new Uint8Array([1, 2, 3]));
      
      const { writeFile } = await import('fs/promises');
      const zipBuffer = await zip.generateAsync({ type: 'nodebuffer' });
      await writeFile(testZipPath, zipBuffer);

      const result = await importer.importModZips([testZipPath]);

      expect(result.totalZips).toBe(1);
      expect(result.totalModsImported).toBe(1);
      expect(result.totalCosmeticsImported).toBe(1);
      expect(result.imported.length).toBe(1);
      expect(result.failed.length).toBe(0);
      expect(result.duplicates.length).toBe(0);
      
      const imported = result.imported[0];
      expect(imported.success).toBe(true);
      expect(imported.isDuplicate).toBe(false);
      expect(imported.cosmeticsImported).toBe(1);
    });

    it('should import multiple ZIP files in one operation', async () => {
      const JSZip = (await import('jszip')).default;
      const { writeFile } = await import('fs/promises');
      const zipPaths: string[] = [];

      // Create 3 test ZIPs
      for (let i = 1; i <= 3; i++) {
        const testZipPath = `/tmp/test-mod-${i}.zip`;
        const zip = new JSZip();
        zip.file('manifest.json', JSON.stringify({
          name: `TestMod${i}`,
          author: 'TestAuthor',
          version_number: '1.0.0',
        }));
        zip.file(`plugins/TestMod${i}/Decorations/hat${i}.hhh`, new Uint8Array([i]));
        
        const zipBuffer = await zip.generateAsync({ type: 'nodebuffer' });
        await writeFile(testZipPath, zipBuffer);
        zipPaths.push(testZipPath);
      }

      const result = await importer.importModZips(zipPaths);

      expect(result.totalZips).toBe(3);
      expect(result.totalModsImported).toBe(3);
      expect(result.totalCosmeticsImported).toBe(3);
      expect(result.imported.length).toBe(3);
      expect(result.failed.length).toBe(0);
      expect(result.duplicates.length).toBe(0);
    });

    it('should detect duplicate mod by source_zip path', async () => {
      const JSZip = (await import('jszip')).default;
      const { writeFile } = await import('fs/promises');
      const testZipPath = '/tmp/test-mod-duplicate-path.zip';
      
      const zip = new JSZip();
      zip.file('manifest.json', JSON.stringify({
        name: 'DuplicateMod',
        author: 'TestAuthor',
        version_number: '1.0.0',
      }));
      zip.file('plugins/DuplicateMod/Decorations/hat.hhh', new Uint8Array([1, 2, 3]));
      
      const zipBuffer = await zip.generateAsync({ type: 'nodebuffer' });
      await writeFile(testZipPath, zipBuffer);

      // First import should succeed
      const result1 = await importer.importModZips([testZipPath]);
      expect(result1.imported.length).toBe(1);
      expect(result1.totalModsImported).toBe(1);

      // Second import of same path should detect duplicate
      const result2 = await importer.importModZips([testZipPath]);
      expect(result2.duplicates.length).toBe(1);
      expect(result2.totalModsImported).toBe(0);
      expect(result2.duplicates[0].isDuplicate).toBe(true);
      expect(result2.duplicates[0].warnings).toContain('Mod with this ZIP path already imported');
    });

    it('should detect duplicate mod by identity (name, author, version)', async () => {
      const JSZip = (await import('jszip')).default;
      const { writeFile } = await import('fs/promises');
      
      // Create two different ZIPs with same mod identity
      const testZipPath1 = '/tmp/test-mod-identity1.zip';
      const testZipPath2 = '/tmp/test-mod-identity2.zip';
      
      const manifest = JSON.stringify({
        name: 'SameMod',
        author: 'SameAuthor',
        version_number: '1.0.0',
      });
      
      const zip1 = new JSZip();
      zip1.file('manifest.json', manifest);
      zip1.file('plugins/SameMod/Decorations/hat1.hhh', new Uint8Array([1]));
      const zipBuffer1 = await zip1.generateAsync({ type: 'nodebuffer' });
      await writeFile(testZipPath1, zipBuffer1);
      
      const zip2 = new JSZip();
      zip2.file('manifest.json', manifest);
      zip2.file('plugins/SameMod/Decorations/hat2.hhh', new Uint8Array([2]));
      const zipBuffer2 = await zip2.generateAsync({ type: 'nodebuffer' });
      await writeFile(testZipPath2, zipBuffer2);

      // First import should succeed
      const result1 = await importer.importModZips([testZipPath1]);
      expect(result1.imported.length).toBe(1);

      // Second import with same identity should detect duplicate
      const result2 = await importer.importModZips([testZipPath2]);
      expect(result2.duplicates.length).toBe(1);
      expect(result2.duplicates[0].isDuplicate).toBe(true);
      expect(result2.duplicates[0].warnings.some(w => 
        w.includes('name, author, and version already imported')
      )).toBe(true);
    });

    it('should detect duplicate cosmetics by SHA256 hash', async () => {
      const JSZip = (await import('jszip')).default;
      const { writeFile } = await import('fs/promises');
      
      // Create two ZIPs with different mods but same cosmetic content
      const testZipPath1 = '/tmp/test-mod-cosmetic1.zip';
      const testZipPath2 = '/tmp/test-mod-cosmetic2.zip';
      
      const sameContent = new Uint8Array([1, 2, 3, 4, 5]);
      
      const zip1 = new JSZip();
      zip1.file('manifest.json', JSON.stringify({
        name: 'Mod1',
        author: 'Author1',
        version_number: '1.0.0',
      }));
      zip1.file('plugins/Mod1/Decorations/hat.hhh', sameContent);
      const zipBuffer1 = await zip1.generateAsync({ type: 'nodebuffer' });
      await writeFile(testZipPath1, zipBuffer1);
      
      const zip2 = new JSZip();
      zip2.file('manifest.json', JSON.stringify({
        name: 'Mod2',
        author: 'Author2',
        version_number: '1.0.0',
      }));
      zip2.file('plugins/Mod2/Decorations/hat.hhh', sameContent);
      const zipBuffer2 = await zip2.generateAsync({ type: 'nodebuffer' });
      await writeFile(testZipPath2, zipBuffer2);

      // First import should succeed
      const result1 = await importer.importModZips([testZipPath1]);
      expect(result1.totalCosmeticsImported).toBe(1);
      expect(result1.totalCosmeticsDuplicate).toBe(0);

      // Second import should detect duplicate cosmetic
      const result2 = await importer.importModZips([testZipPath2]);
      expect(result2.totalModsImported).toBe(1); // New mod
      expect(result2.totalCosmeticsImported).toBe(0); // But duplicate cosmetic
      expect(result2.totalCosmeticsDuplicate).toBe(1);
      expect(result2.imported[0].cosmeticsDuplicate).toBe(1);
      expect(result2.imported[0].warnings.some(w => 
        w.includes('duplicate cosmetic')
      )).toBe(true);
    });

    it('should handle missing manifest gracefully', async () => {
      const JSZip = (await import('jszip')).default;
      const { writeFile } = await import('fs/promises');
      const testZipPath = '/tmp/test-mod-no-manifest.zip';
      
      const zip = new JSZip();
      zip.file('plugins/TestMod/Decorations/hat.hhh', new Uint8Array([1, 2, 3]));
      
      const zipBuffer = await zip.generateAsync({ type: 'nodebuffer' });
      await writeFile(testZipPath, zipBuffer);

      const result = await importer.importModZips([testZipPath]);

      expect(result.failed.length).toBe(1);
      expect(result.failed[0].error).toContain('manifest');
      expect(result.totalModsImported).toBe(0);
    });

    it('should handle corrupt ZIP files gracefully', async () => {
      const { writeFile } = await import('fs/promises');
      const testZipPath = '/tmp/test-mod-corrupt.zip';
      
      // Write invalid ZIP content
      await writeFile(testZipPath, Buffer.from('not a valid zip file'));

      const result = await importer.importModZips([testZipPath]);

      expect(result.failed.length).toBe(1);
      expect(result.failed[0].error).toBeDefined();
      expect(result.totalModsImported).toBe(0);
    });

    it('should continue processing other ZIPs if one fails', async () => {
      const JSZip = (await import('jszip')).default;
      const { writeFile } = await import('fs/promises');
      
      // Create one valid ZIP
      const validZipPath = '/tmp/test-mod-valid.zip';
      const validZip = new JSZip();
      validZip.file('manifest.json', JSON.stringify({
        name: 'ValidMod',
        author: 'TestAuthor',
        version_number: '1.0.0',
      }));
      validZip.file('plugins/ValidMod/Decorations/hat.hhh', new Uint8Array([1]));
      const validZipBuffer = await validZip.generateAsync({ type: 'nodebuffer' });
      await writeFile(validZipPath, validZipBuffer);
      
      // Create one corrupt ZIP
      const corruptZipPath = '/tmp/test-mod-corrupt-2.zip';
      await writeFile(corruptZipPath, Buffer.from('corrupt'));

      const result = await importer.importModZips([validZipPath, corruptZipPath]);

      expect(result.totalZips).toBe(2);
      expect(result.imported.length).toBe(1);
      expect(result.failed.length).toBe(1);
      expect(result.totalModsImported).toBe(1);
      expect(result.imported[0].zipPath).toBe(validZipPath);
      expect(result.failed[0].zipPath).toBe(corruptZipPath);
    });

    it('should track activity log for imports', async () => {
      const JSZip = (await import('jszip')).default;
      const { writeFile } = await import('fs/promises');
      const testZipPath = '/tmp/test-mod-activity.zip';
      
      const zip = new JSZip();
      zip.file('manifest.json', JSON.stringify({
        name: 'ActivityMod',
        author: 'TestAuthor',
        version_number: '1.0.0',
      }));
      zip.file('plugins/ActivityMod/Decorations/hat.hhh', new Uint8Array([1]));
      
      const zipBuffer = await zip.generateAsync({ type: 'nodebuffer' });
      await writeFile(testZipPath, zipBuffer);

      await importer.importModZips([testZipPath]);

      const activityLog = importer.getActivityLog();
      expect(activityLog.length).toBe(1);
      
      const activity = activityLog[0];
      expect(activity.zipFilename).toContain('test-mod-activity.zip');
      expect(activity.success).toBe(true);
      expect(activity.cosmeticsFound).toBe(1);
      expect(activity.cosmeticsImported).toBe(1);
      expect(activity.timestamp).toBeInstanceOf(Date);
    });

    it('should log failed imports in activity log', async () => {
      const { writeFile } = await import('fs/promises');
      const testZipPath = '/tmp/test-mod-failed-activity.zip';
      
      await writeFile(testZipPath, Buffer.from('corrupt'));

      await importer.importModZips([testZipPath]);

      const activityLog = importer.getActivityLog();
      expect(activityLog.length).toBe(1);
      
      const activity = activityLog[0];
      expect(activity.success).toBe(false);
      expect(activity.error).toBeDefined();
    });

    it('should allow clearing activity log', async () => {
      const JSZip = (await import('jszip')).default;
      const { writeFile } = await import('fs/promises');
      const testZipPath = '/tmp/test-mod-clear-activity.zip';
      
      const zip = new JSZip();
      zip.file('manifest.json', JSON.stringify({
        name: 'ClearMod',
        author: 'TestAuthor',
        version_number: '1.0.0',
      }));
      
      const zipBuffer = await zip.generateAsync({ type: 'nodebuffer' });
      await writeFile(testZipPath, zipBuffer);

      await importer.importModZips([testZipPath]);
      expect(importer.getActivityLog().length).toBe(1);

      importer.clearActivityLog();
      expect(importer.getActivityLog().length).toBe(0);
    });

    it('should handle non-existent file paths', async () => {
      const result = await importer.importModZips(['/tmp/non-existent-file.zip']);

      expect(result.failed.length).toBe(1);
      expect(result.failed[0].error).toContain('Failed to read ZIP file');
      expect(result.totalModsImported).toBe(0);
    });
  });
});
