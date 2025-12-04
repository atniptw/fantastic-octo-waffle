import { describe, it, expect } from 'vitest';
import JSZip from 'jszip';
import { scanZip, isValidScanResult, getCosmeticPaths } from '../zipScanner';

/**
 * Helper function to create a mock ZIP file for testing.
 */
async function createMockZip(files: Record<string, string | Uint8Array>): Promise<Uint8Array> {
  const zip = new JSZip();
  for (const [path, content] of Object.entries(files)) {
    zip.file(path, content);
  }
  return await zip.generateAsync({ type: 'uint8array' });
}

describe('scanZip', () => {
  it('should extract manifest.json from root', async () => {
    const manifest = JSON.stringify({
      name: 'TestMod',
      author: 'TestAuthor',
      version_number: '1.0.0',
    });

    const zipData = await createMockZip({
      'manifest.json': manifest,
    });

    const result = await scanZip(zipData);

    expect(result.manifestContent).toBe(manifest);
    expect(result.manifest).not.toBeNull();
    expect(result.manifest?.name).toBe('TestMod');
    expect(result.manifest?.author).toBe('TestAuthor');
    expect(result.manifest?.version_number).toBe('1.0.0');
  });

  it('should extract icon.png from root', async () => {
    const manifest = JSON.stringify({
      name: 'TestMod',
      author: 'TestAuthor',
      version_number: '1.0.0',
    });
    const iconData = new Uint8Array([137, 80, 78, 71]); // PNG header bytes

    const zipData = await createMockZip({
      'manifest.json': manifest,
      'icon.png': iconData,
    });

    const result = await scanZip(zipData);

    expect(result.iconData).not.toBeNull();
    expect(result.iconData).toEqual(iconData);
  });

  it('should extract .hhh files from Decorations folders', async () => {
    const manifest = JSON.stringify({
      name: 'TestMod',
      author: 'TestAuthor',
      version_number: '1.0.0',
    });
    const hhhContent = new Uint8Array([1, 2, 3, 4]);

    const zipData = await createMockZip({
      'manifest.json': manifest,
      'plugins/TestMod/Decorations/cool_hat.hhh': hhhContent,
      'plugins/TestMod/Decorations/glasses.hhh': hhhContent,
    });

    const result = await scanZip(zipData);

    expect(result.cosmeticFiles.size).toBe(2);
    expect(result.cosmeticFiles.has('plugins/TestMod/Decorations/cool_hat.hhh')).toBe(true);
    expect(result.cosmeticFiles.has('plugins/TestMod/Decorations/glasses.hhh')).toBe(true);
  });

  it('should ignore non-.hhh files', async () => {
    const manifest = JSON.stringify({
      name: 'TestMod',
      author: 'TestAuthor',
      version_number: '1.0.0',
    });

    const zipData = await createMockZip({
      'manifest.json': manifest,
      'plugins/TestMod/Decorations/valid.hhh': new Uint8Array([1]),
      'plugins/TestMod/Decorations/invalid.txt': 'text content',
      'plugins/TestMod/config.json': '{}',
    });

    const result = await scanZip(zipData);

    expect(result.cosmeticFiles.size).toBe(1);
    expect(result.cosmeticFiles.has('plugins/TestMod/Decorations/valid.hhh')).toBe(true);
  });

  it('should ignore .hhh files not in Decorations folder', async () => {
    const manifest = JSON.stringify({
      name: 'TestMod',
      author: 'TestAuthor',
      version_number: '1.0.0',
    });

    const zipData = await createMockZip({
      'manifest.json': manifest,
      'plugins/TestMod/Decorations/valid.hhh': new Uint8Array([1]),
      'plugins/TestMod/Other/invalid.hhh': new Uint8Array([2]),
      'other_folder/invalid.hhh': new Uint8Array([3]),
    });

    const result = await scanZip(zipData);

    expect(result.cosmeticFiles.size).toBe(1);
    expect(result.cosmeticFiles.has('plugins/TestMod/Decorations/valid.hhh')).toBe(true);
  });

  it('should be case-insensitive for Decorations folder', async () => {
    const manifest = JSON.stringify({
      name: 'TestMod',
      author: 'TestAuthor',
      version_number: '1.0.0',
    });

    const zipData = await createMockZip({
      'manifest.json': manifest,
      'plugins/TestMod/decorations/hat1.hhh': new Uint8Array([1]),
      'plugins/TestMod/DECORATIONS/hat2.hhh': new Uint8Array([2]),
    });

    const result = await scanZip(zipData);

    expect(result.cosmeticFiles.size).toBe(2);
  });

  it('should report error for missing manifest.json', async () => {
    const zipData = await createMockZip({
      'icon.png': new Uint8Array([137, 80, 78, 71]),
    });

    const result = await scanZip(zipData);

    expect(result.manifest).toBeNull();
    expect(result.errors).toContain('manifest.json not found');
  });

  it('should report error for invalid manifest.json', async () => {
    const zipData = await createMockZip({
      'manifest.json': 'not valid json',
    });

    const result = await scanZip(zipData);

    expect(result.manifest).toBeNull();
    expect(result.errors).toContain('Invalid manifest.json format');
  });

  it('should handle ZIP with no cosmetic files', async () => {
    const manifest = JSON.stringify({
      name: 'TestMod',
      author: 'TestAuthor',
      version_number: '1.0.0',
    });

    const zipData = await createMockZip({
      'manifest.json': manifest,
    });

    const result = await scanZip(zipData);

    expect(result.manifest).not.toBeNull();
    expect(result.cosmeticFiles.size).toBe(0);
    expect(result.errors.length).toBe(0);
  });

  it('should handle corrupt ZIP data', async () => {
    const corruptData = new Uint8Array([1, 2, 3, 4, 5]); // Not a valid ZIP

    const result = await scanZip(corruptData);

    expect(result.manifest).toBeNull();
    expect(result.errors.some(e => e.includes('Error parsing ZIP'))).toBe(true);
  });
});

describe('isValidScanResult', () => {
  it('should return true for valid scan result with manifest', async () => {
    const manifest = JSON.stringify({
      name: 'TestMod',
      author: 'TestAuthor',
      version_number: '1.0.0',
    });

    const zipData = await createMockZip({
      'manifest.json': manifest,
    });

    const result = await scanZip(zipData);
    expect(isValidScanResult(result)).toBe(true);
  });

  it('should return false for scan result without manifest', async () => {
    const zipData = await createMockZip({
      'icon.png': new Uint8Array([137, 80, 78, 71]),
    });

    const result = await scanZip(zipData);
    expect(isValidScanResult(result)).toBe(false);
  });
});

describe('getCosmeticPaths', () => {
  it('should return array of cosmetic file paths', async () => {
    const manifest = JSON.stringify({
      name: 'TestMod',
      author: 'TestAuthor',
      version_number: '1.0.0',
    });

    const zipData = await createMockZip({
      'manifest.json': manifest,
      'plugins/TestMod/Decorations/hat.hhh': new Uint8Array([1]),
      'plugins/TestMod/Decorations/glasses.hhh': new Uint8Array([2]),
    });

    const result = await scanZip(zipData);
    const paths = getCosmeticPaths(result);

    expect(paths).toHaveLength(2);
    expect(paths).toContain('plugins/TestMod/Decorations/hat.hhh');
    expect(paths).toContain('plugins/TestMod/Decorations/glasses.hhh');
  });

  it('should return empty array when no cosmetics', async () => {
    const manifest = JSON.stringify({
      name: 'TestMod',
      author: 'TestAuthor',
      version_number: '1.0.0',
    });

    const zipData = await createMockZip({
      'manifest.json': manifest,
    });

    const result = await scanZip(zipData);
    const paths = getCosmeticPaths(result);

    expect(paths).toHaveLength(0);
  });
});
