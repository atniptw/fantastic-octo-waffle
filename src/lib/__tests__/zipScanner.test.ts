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

// ============================================================================
// Test Fixtures
// ============================================================================

const VALID_MANIFEST = {
  name: 'TestMod',
  author: 'TestAuthor',
  version_number: '1.0.0',
};

const VALID_MANIFEST_JSON = JSON.stringify(VALID_MANIFEST);
const PNG_HEADER = new Uint8Array([137, 80, 78, 71]);
const SAMPLE_HHH_CONTENT = new Uint8Array([1, 2, 3, 4]);

// ============================================================================
// Test Helpers
// ============================================================================

async function createMockZip(files: Record<string, string | Uint8Array>): Promise<Uint8Array> {
  const zip = new JSZip();
  for (const [path, content] of Object.entries(files)) {
    zip.file(path, content);
  }
  return zip.generateAsync({
    type: 'uint8array',
    compression: 'DEFLATE',
    compressionOptions: { level: 6 },
  });
}

function createMockFile(data: Uint8Array, filename: string): File {
  const blob = new Blob([data as BlobPart], { type: 'application/zip' });
  return new File([blob], filename, { type: 'application/zip' });
}

async function createValidZip(additionalFiles: Record<string, string | Uint8Array> = {}) {
  return createMockZip({
    'manifest.json': VALID_MANIFEST_JSON,
    ...additionalFiles,
  });
}

// ============================================================================
// Unit Tests - Utility Functions
// ============================================================================

describe('parseManifest', () => {
  it('should parse valid manifest', () => {
    const result = parseManifest(VALID_MANIFEST_JSON);

    expect(result).not.toBeNull();
    expect(result?.name).toBe('TestMod');
    expect(result?.author).toBe('TestAuthor');
    expect(result?.version_number).toBe('1.0.0');
  });

  it('should return null for invalid JSON', () => {
    expect(parseManifest('not valid json')).toBeNull();
  });

  it('should return null for missing required fields', () => {
    const incomplete = JSON.stringify({ name: 'TestMod' });
    expect(parseManifest(incomplete)).toBeNull();
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
    expect(hash1).toHaveLength(64);
    expect(hash1).toMatch(/^[a-f0-9]{64}$/);
  });

  it('should generate different hashes for different content', async () => {
    const hash1 = await calculateFileHash(new Uint8Array([1, 2, 3]));
    const hash2 = await calculateFileHash(new Uint8Array([4, 5, 6]));

    expect(hash1).not.toBe(hash2);
  });

  it('should handle empty content', async () => {
    const hash = await calculateFileHash(new Uint8Array([]));

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
    expect(metadata.size).toBe(content.byteLength);
  });

  it('should handle backslashes in path', async () => {
    const path = 'plugins\\TestMod\\Decorations\\cool_head.hhh';
    const content = new Uint8Array([1, 2, 3]);

    const metadata = await extractCosmeticMetadata(path, content);

    expect(metadata.internalPath).toBe('plugins/TestMod/Decorations/cool_head.hhh');
    expect(metadata.filename).toBe('cool_head.hhh');
  });
});

// ============================================================================
// Integration Tests - ZIP Scanning
// ============================================================================

describe('scanZip', () => {
  describe('manifest extraction', () => {
    it('should extract manifest.json from root', async () => {
      const zipData = await createValidZip();
      const result = await scanZip(zipData);

      expect(result.manifestContent).toBe(VALID_MANIFEST_JSON);
      expect(result.manifest).not.toBeNull();
      expect(result.manifest?.name).toBe('TestMod');
      expect(result.manifest?.author).toBe('TestAuthor');
      expect(result.manifest?.version_number).toBe('1.0.0');
    });

    it('should report error for missing manifest.json', async () => {
      const zipData = await createMockZip({ 'icon.png': PNG_HEADER });
      const result = await scanZip(zipData);

      expect(result.manifest).toBeNull();
      expect(result.errors).toContain('manifest.json not found');
    });
  });

  describe('icon extraction', () => {
    it('should extract icon.png from root', async () => {
      const zipData = await createValidZip({ 'icon.png': PNG_HEADER });
      const result = await scanZip(zipData);

      expect(result.iconData).not.toBeNull();
      expect(result.iconData).toEqual(PNG_HEADER);
    });
  });

  describe('cosmetic file extraction', () => {
    it('should extract .hhh files from Decorations folders', async () => {
      const zipData = await createValidZip({
        'plugins/TestMod/Decorations/cool_hat.hhh': SAMPLE_HHH_CONTENT,
        'plugins/TestMod/Decorations/glasses.hhh': SAMPLE_HHH_CONTENT,
      });

      const result = await scanZip(zipData);

      expect(result.cosmetics).toHaveLength(2);
      const paths = result.cosmetics.map((c) => c.internalPath);
      expect(paths).toContain('plugins/TestMod/Decorations/cool_hat.hhh');
      expect(paths).toContain('plugins/TestMod/Decorations/glasses.hhh');
    });

    it('should extract .hhh files from non-conventional locations', async () => {
      const zipData = await createValidZip({
        'assets/cosmetics/hat.hhh': SAMPLE_HHH_CONTENT,
        'bundles/head.hhh': SAMPLE_HHH_CONTENT,
        'CustomFolder/accessory.hhh': SAMPLE_HHH_CONTENT,
        'root_level.hhh': SAMPLE_HHH_CONTENT,
      });

      const result = await scanZip(zipData);

      expect(result.cosmetics.length).toBe(4);
      const paths = result.cosmetics.map((c) => c.internalPath);
      expect(paths).toContain('assets/cosmetics/hat.hhh');
      expect(paths).toContain('bundles/head.hhh');
      expect(paths).toContain('CustomFolder/accessory.hhh');
      expect(paths).toContain('root_level.hhh');

      const hatCosmetic = result.cosmetics.find((c) => c.filename === 'hat.hhh');
      expect(hatCosmetic?.internalPath).toBe('assets/cosmetics/hat.hhh');
      expect(hatCosmetic?.displayName).toBe('Hat');
    });

    it('should ignore non-.hhh files', async () => {
      const zipData = await createValidZip({
        'plugins/TestMod/Decorations/valid.hhh': new Uint8Array([1]),
        'plugins/TestMod/Decorations/invalid.txt': 'text content',
        'plugins/TestMod/config.json': '{}',
      });

      const result = await scanZip(zipData);

      expect(result.cosmetics).toHaveLength(1);
      expect(result.cosmetics[0].internalPath).toBe('plugins/TestMod/Decorations/valid.hhh');
    });
  });

  describe('cosmetic metadata extraction', () => {
    it('should extract complete metadata including hash and type', async () => {
      const hhhContent = new Uint8Array([1, 2, 3, 4, 5]);
      const zipData = await createValidZip({
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
      expect(cosmetic.size).toBe(hhhContent.byteLength);
    });

    it('should extract multiple cosmetics with different types', async () => {
      const zipData = await createValidZip({
        'plugins/TestMod/Decorations/cool_head.hhh': new Uint8Array([1]),
        'plugins/TestMod/Decorations/awesome_hat.hhh': new Uint8Array([2]),
        'plugins/TestMod/Decorations/stylish_glasses.hhh': new Uint8Array([3]),
      });

      const result = await scanZip(zipData);

      expect(result.cosmetics).toHaveLength(3);
      const head = result.cosmetics.find((c) => c.type === 'head');
      const hat = result.cosmetics.find((c) => c.type === 'hat');
      const glasses = result.cosmetics.find((c) => c.type === 'glasses');

      expect(head?.displayName).toBe('Cool Head');
      expect(hat?.displayName).toBe('Awesome Hat');
      expect(glasses?.displayName).toBe('Stylish Glasses');
    });
  });

  describe('error handling', () => {
    it('should handle corrupt ZIP data', async () => {
      const corruptData = new Uint8Array([1, 2, 3, 4, 5]);
      const result = await scanZip(corruptData);

      expect(result.manifest).toBeNull();
      expect(result.hasFatalError).toBe(true);
      expect(result.errors.length).toBeGreaterThan(0);
    });

    it('should set hasFatalError to false for non-fatal errors', async () => {
      const zipData = await createValidZip(); // Missing icon.png is non-fatal
      const result = await scanZip(zipData);

      expect(result.hasFatalError).toBe(false);
      expect(result.manifest).not.toBeNull();
    });
  });
});

describe('scanZipFile', () => {
  it('should scan File object successfully', async () => {
    const zipData = await createValidZip({
      'plugins/TestMod/Decorations/hat.hhh': new Uint8Array([1, 2, 3]),
    });

    const file = createMockFile(zipData, 'test.zip');
    const result = await scanZipFile(file);

    expect(result.manifest).not.toBeNull();
    expect(result.manifest?.name).toBe('TestMod');
    expect(result.cosmetics).toHaveLength(1);
  });
});

// ============================================================================
// Helper Function Tests
// ============================================================================

describe('isValidScanResult', () => {
  it('should return true for valid scan result with manifest', async () => {
    const zipData = await createValidZip();
    const result = await scanZip(zipData);
    expect(isValidScanResult(result)).toBe(true);
  });

  it('should return false for scan result without manifest', async () => {
    const zipData = await createMockZip({ 'icon.png': PNG_HEADER });
    const result = await scanZip(zipData);
    expect(isValidScanResult(result)).toBe(false);
  });
});

describe('getCosmeticPaths', () => {
  it('should return array of cosmetic file paths', async () => {
    const zipData = await createValidZip({
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

// ============================================================================
// Batch Processing Tests
// ============================================================================

describe('scanMultipleZipFiles', () => {
  it('should scan multiple ZIPs successfully', async () => {
    const zip1 = await createMockZip({
      'manifest.json': JSON.stringify({ ...VALID_MANIFEST, name: 'Mod1', author: 'Author1' }),
      'plugins/Mod1/Decorations/hat1.hhh': new Uint8Array([1]),
    });

    const zip2 = await createMockZip({
      'manifest.json': JSON.stringify({ ...VALID_MANIFEST, name: 'Mod2', author: 'Author2', version_number: '2.0.0' }),
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
    const validZip = await createValidZip({
      'plugins/Mod/Decorations/hat.hhh': new Uint8Array([1]),
    });
    const corruptZip = new Uint8Array([1, 2, 3, 4, 5]);

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
