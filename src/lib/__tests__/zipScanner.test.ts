import { describe, it, expect } from 'vitest';
import JSZip from 'jszip';
import {
  scanZip,
  scanZipFile,
  isValidScanResult,
  getCosmeticPaths,
  generateDisplayName,
  inferCosmeticType,
  calculateFileHash,
  extractCosmeticMetadata,
  scanMultipleZipFiles,
  parseManifest,
} from '../zipScanner';

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

/**
 * Helper function to create a Blob from Uint8Array for testing File API
 */
function createMockFile(data: Uint8Array, filename: string): File {
  // TypeScript requires assertion due to ArrayBufferLike vs ArrayBuffer distinction
  const blob = new Blob([data as BlobPart], { type: 'application/zip' });
  return new File([blob], filename, { type: 'application/zip' });
}

describe('parseManifest', () => {
  it('should parse valid manifest', () => {
    const manifest = JSON.stringify({
      name: 'TestMod',
      author: 'TestAuthor',
      version_number: '1.0.0',
    });

    const result = parseManifest(manifest);

    expect(result).not.toBeNull();
    expect(result?.name).toBe('TestMod');
    expect(result?.author).toBe('TestAuthor');
    expect(result?.version_number).toBe('1.0.0');
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
});

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

  it('should report error for missing manifest.json', async () => {
    const zipData = await createMockZip({
      'icon.png': new Uint8Array([137, 80, 78, 71]),
    });

    const result = await scanZip(zipData);

    expect(result.manifest).toBeNull();
    expect(result.errors).toContain('manifest.json not found');
  });

  it('should handle corrupt ZIP data', async () => {
    const corruptData = new Uint8Array([1, 2, 3, 4, 5]); // Not a valid ZIP

    const result = await scanZip(corruptData);

    expect(result.manifest).toBeNull();
    expect(result.hasFatalError).toBe(true);
    expect(result.errors.some(e => e.includes('Error parsing ZIP'))).toBe(true);
  });

  it('should set hasFatalError to false for non-fatal errors', async () => {
    const manifest = JSON.stringify({
      name: 'TestMod',
      author: 'TestAuthor',
      version_number: '1.0.0',
    });

    const zipData = await createMockZip({
      'manifest.json': manifest,
      // Missing icon.png is a non-fatal error
    });

    const result = await scanZip(zipData);

    expect(result.hasFatalError).toBe(false);
    expect(result.manifest).not.toBeNull();
  });
});

describe('scanZipFile', () => {
  it('should scan File object successfully', async () => {
    const manifest = JSON.stringify({
      name: 'TestMod',
      author: 'TestAuthor',
      version_number: '1.0.0',
    });

    const zipData = await createMockZip({
      'manifest.json': manifest,
      'plugins/TestMod/Decorations/hat.hhh': new Uint8Array([1, 2, 3]),
    });

    const file = createMockFile(zipData, 'test.zip');
    const result = await scanZipFile(file);

    expect(result.manifest).not.toBeNull();
    expect(result.manifest?.name).toBe('TestMod');
    expect(result.cosmetics).toHaveLength(1);
  });
});

describe('generateDisplayName', () => {
  it('should convert snake_case to Title Case', () => {
    expect(generateDisplayName('cool_hat.hhh')).toBe('Cool Hat');
    expect(generateDisplayName('awesome_glasses.hhh')).toBe('Awesome Glasses');
  });

  it('should convert kebab-case to Title Case', () => {
    expect(generateDisplayName('cool-hat.hhh')).toBe('Cool Hat');
    expect(generateDisplayName('super-awesome-head.hhh')).toBe('Super Awesome Head');
  });

  it('should handle case-insensitive extension', () => {
    expect(generateDisplayName('hat.HHH')).toBe('Hat');
    expect(generateDisplayName('hat.Hhh')).toBe('Hat');
  });
});

describe('inferCosmeticType', () => {
  it('should identify head cosmetics', () => {
    expect(inferCosmeticType('cool_head.hhh')).toBe('head');
    expect(inferCosmeticType('robot_head_v2.hhh')).toBe('head');
  });

  it('should identify hat cosmetics', () => {
    expect(inferCosmeticType('top_hat.hhh')).toBe('hat');
    expect(inferCosmeticType('steel_helmet.hhh')).toBe('hat');
  });

  it('should identify glasses cosmetics', () => {
    expect(inferCosmeticType('cool_glasses.hhh')).toBe('glasses');
    expect(inferCosmeticType('tactical_goggles.hhh')).toBe('glasses');
  });

  it('should default to decoration for unknown types', () => {
    expect(inferCosmeticType('unknown_cosmetic.hhh')).toBe('decoration');
  });

  it('should not match false positives with substring matches', () => {
    expect(inferCosmeticType('headphones.hhh')).toBe('decoration');
    expect(inferCosmeticType('hatching.hhh')).toBe('decoration');
  });
});

describe('calculateFileHash', () => {
  it('should generate consistent SHA256 hash', async () => {
    const content = new Uint8Array([1, 2, 3, 4, 5]);
    const hash1 = await calculateFileHash(content);
    const hash2 = await calculateFileHash(content);

    expect(hash1).toBe(hash2);
    expect(hash1).toHaveLength(64); // SHA256 hex string is 64 chars
    expect(hash1).toMatch(/^[a-f0-9]{64}$/);
  });

  it('should generate different hashes for different content', async () => {
    const content1 = new Uint8Array([1, 2, 3]);
    const content2 = new Uint8Array([4, 5, 6]);

    const hash1 = await calculateFileHash(content1);
    const hash2 = await calculateFileHash(content2);

    expect(hash1).not.toBe(hash2);
  });

  it('should handle empty content', async () => {
    const content = new Uint8Array([]);
    const hash = await calculateFileHash(content);

    expect(hash).toHaveLength(64);
    expect(hash).toMatch(/^[a-f0-9]{64}$/);
  });
});

describe('extractCosmeticMetadata', () => {
  it('should extract complete metadata from path and content', async () => {
    const path = 'plugins/TestMod/Decorations/cool_hat.hhh';
    const content = new Uint8Array([1, 2, 3, 4]);

    const metadata = await extractCosmeticMetadata(path, content);

    expect(metadata.internalPath).toBe(path);
    expect(metadata.filename).toBe('cool_hat.hhh');
    expect(metadata.displayName).toBe('Cool Hat');
    expect(metadata.type).toBe('hat');
    expect(metadata.hash).toHaveLength(64);
    expect(metadata.content).toBe(content);
  });

  it('should handle backslashes in path', async () => {
    const path = 'plugins\\TestMod\\Decorations\\cool_head.hhh';
    const content = new Uint8Array([1, 2, 3]);

    const metadata = await extractCosmeticMetadata(path, content);

    expect(metadata.internalPath).toBe('plugins/TestMod/Decorations/cool_head.hhh');
    expect(metadata.filename).toBe('cool_head.hhh');
  });
});

describe('scanZip - cosmetics metadata', () => {
  it('should extract cosmetic metadata including hash and type', async () => {
    const manifest = JSON.stringify({
      name: 'TestMod',
      author: 'TestAuthor',
      version_number: '1.0.0',
    });
    const hhhContent = new Uint8Array([1, 2, 3, 4, 5]);

    const zipData = await createMockZip({
      'manifest.json': manifest,
      'plugins/TestMod/Decorations/cool_head.hhh': hhhContent,
    });

    const result = await scanZip(zipData);

    expect(result.cosmetics).toHaveLength(1);
    
    const cosmetic = result.cosmetics[0];
    expect(cosmetic.filename).toBe('cool_head.hhh');
    expect(cosmetic.displayName).toBe('Cool Head');
    expect(cosmetic.type).toBe('head');
    expect(cosmetic.hash).toHaveLength(64);
    expect(cosmetic.internalPath).toBe('plugins/TestMod/Decorations/cool_head.hhh');
    expect(cosmetic.content).toEqual(hhhContent);
  });

  it('should extract multiple cosmetics with different types', async () => {
    const manifest = JSON.stringify({
      name: 'TestMod',
      author: 'TestAuthor',
      version_number: '1.0.0',
    });

    const zipData = await createMockZip({
      'manifest.json': manifest,
      'plugins/TestMod/Decorations/cool_head.hhh': new Uint8Array([1]),
      'plugins/TestMod/Decorations/awesome_hat.hhh': new Uint8Array([2]),
      'plugins/TestMod/Decorations/stylish_glasses.hhh': new Uint8Array([3]),
    });

    const result = await scanZip(zipData);

    expect(result.cosmetics).toHaveLength(3);
    
    const head = result.cosmetics.find(c => c.type === 'head');
    const hat = result.cosmetics.find(c => c.type === 'hat');
    const glasses = result.cosmetics.find(c => c.type === 'glasses');

    expect(head?.displayName).toBe('Cool Head');
    expect(hat?.displayName).toBe('Awesome Hat');
    expect(glasses?.displayName).toBe('Stylish Glasses');
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
});

describe('scanMultipleZipFiles', () => {
  it('should scan multiple ZIPs successfully', async () => {
    const manifest1 = JSON.stringify({
      name: 'Mod1',
      author: 'Author1',
      version_number: '1.0.0',
    });
    const manifest2 = JSON.stringify({
      name: 'Mod2',
      author: 'Author2',
      version_number: '2.0.0',
    });

    const zip1 = await createMockZip({
      'manifest.json': manifest1,
      'plugins/Mod1/Decorations/hat1.hhh': new Uint8Array([1]),
    });

    const zip2 = await createMockZip({
      'manifest.json': manifest2,
      'plugins/Mod2/Decorations/hat2.hhh': new Uint8Array([2]),
      'plugins/Mod2/Decorations/head2.hhh': new Uint8Array([3]),
    });

    const result = await scanMultipleZipFiles([
      { path: 'mod1.zip', file: createMockFile(zip1, 'mod1.zip') },
      { path: 'mod2.zip', file: createMockFile(zip2, 'mod2.zip') },
    ]);

    expect(result.total).toBe(2);
    expect(result.successful).toHaveLength(2);
    expect(result.failed).toHaveLength(0);
    expect(result.totalCosmetics).toBe(3);
  });

  it('should handle failed ZIPs gracefully', async () => {
    const validManifest = JSON.stringify({
      name: 'ValidMod',
      author: 'Author',
      version_number: '1.0.0',
    });

    const validZip = await createMockZip({
      'manifest.json': validManifest,
      'plugins/Mod/Decorations/hat.hhh': new Uint8Array([1]),
    });

    const corruptZip = new Uint8Array([1, 2, 3, 4, 5]); // Invalid ZIP

    const result = await scanMultipleZipFiles([
      { path: 'valid.zip', file: createMockFile(validZip, 'valid.zip') },
      { path: 'corrupt.zip', file: createMockFile(corruptZip, 'corrupt.zip') },
    ]);

    expect(result.total).toBe(2);
    expect(result.successful).toHaveLength(1);
    expect(result.failed).toHaveLength(1);
    expect(result.successful[0].zipPath).toBe('valid.zip');
    expect(result.failed[0].zipPath).toBe('corrupt.zip');
  });
});
