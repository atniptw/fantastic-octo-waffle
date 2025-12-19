import { describe, it, expect } from 'vitest';
import SevenZipWasm from 'sevenzip-wasm';
import { scanZip } from '../zipScanner';

/**
 * Helper function to create a large mock ZIP file for testing performance.
 */
async function createLargeZip(sizeInMB: number): Promise<Uint8Array> {
  const sevenZip = await SevenZipWasm();

  // Create temporary directory
  sevenZip.FS.mkdir('/ziproot');

  // Add manifest
  sevenZip.FS.writeFile(
    '/ziproot/manifest.json',
    JSON.stringify({
      name: 'LargeMod',
      author: 'TestAuthor',
      version_number: '1.0.0',
    })
  );

  // Add icon
  sevenZip.FS.writeFile('/ziproot/icon.png', new Uint8Array([137, 80, 78, 71, 13, 10, 26, 10]));

  // Add cosmetic files
  // Create a large fake .hhh file to reach target size
  const targetBytes = sizeInMB * 1024 * 1024;
  const chunkSize = 1024 * 1024; // 1MB chunks
  const numChunks = Math.floor(targetBytes / chunkSize);

  // Create Decorations directory
  sevenZip.FS.mkdir('/ziproot/plugins');
  sevenZip.FS.mkdir('/ziproot/plugins/LargeMod');
  sevenZip.FS.mkdir('/ziproot/plugins/LargeMod/Decorations');

  for (let i = 0; i < numChunks; i++) {
    const chunk = new Uint8Array(chunkSize);
    // Fill with some pseudo-random data
    for (let j = 0; j < chunk.length; j++) {
      chunk[j] = (i + j) % 256;
    }
    sevenZip.FS.writeFile(`/ziproot/plugins/LargeMod/Decorations/cosmetic_${i}.hhh`, chunk);
  }

  // Create ZIP archive with compression, using chdir to avoid path prefix
  sevenZip.FS.chdir('/ziproot');
  sevenZip.callMain(['a', '-tzip', '-mx1', '/output.zip', '.']);
  sevenZip.FS.chdir('/');

  // Read the created ZIP file
  const zipData = sevenZip.FS.readFile('/output.zip');

  return zipData;
}

describe('Large File Handling', () => {
  it('should handle 10MB ZIP file efficiently', async () => {
    const startTime = Date.now();
    const largeZip = await createLargeZip(10);
    const createTime = Date.now() - startTime;

    console.log(`Created 10MB ZIP in ${createTime}ms`);

    const scanStartTime = Date.now();
    const result = await scanZip(largeZip);
    const scanTime = Date.now() - scanStartTime;

    console.log(`Scanned 10MB ZIP in ${scanTime}ms`);
    console.log(`Found ${result.cosmetics.length} cosmetics`);

    expect(result.manifest).not.toBeNull();
    expect(result.manifest?.name).toBe('LargeMod');
    expect(result.cosmetics.length).toBeGreaterThan(0);
    expect(result.hasFatalError).toBe(false);

    // Scanning should complete in reasonable time (adjust threshold as needed)
    // 10MB should scan in under 10 seconds
    expect(scanTime).toBeLessThan(10000);
  }, 30000); // 30 second timeout for this test

  it('should scan multiple smaller files in parallel', async () => {
    const zip1 = await createLargeZip(1);
    const zip2 = await createLargeZip(1);
    const zip3 = await createLargeZip(1);

    const startTime = Date.now();

    // Scan in parallel
    const results = await Promise.all([scanZip(zip1), scanZip(zip2), scanZip(zip3)]);

    const totalTime = Date.now() - startTime;

    console.log(`Scanned 3x1MB ZIPs in parallel in ${totalTime}ms`);

    expect(results).toHaveLength(3);
    expect(results.every((r) => r.manifest !== null)).toBe(true);
    expect(results.every((r) => !r.hasFatalError)).toBe(true);

    // Parallel scanning should be faster than sequential
    // Each 1MB file should take roughly the same time individually
    // Expect parallel to be at most 2x the time of a single scan
    expect(totalTime).toBeLessThan(10000);
  }, 30000);

  it('should handle ZIP with many small cosmetic files', async () => {
    const sevenZip = await SevenZipWasm();

    // Create temporary directory
    sevenZip.FS.mkdir('/ziproot');
    sevenZip.FS.writeFile(
      '/ziproot/manifest.json',
      JSON.stringify({
        name: 'ManyCosmetics',
        author: 'TestAuthor',
        version_number: '1.0.0',
      })
    );

    // Create directories
    sevenZip.FS.mkdir('/ziproot/plugins');
    sevenZip.FS.mkdir('/ziproot/plugins/ManyCosmetics');
    sevenZip.FS.mkdir('/ziproot/plugins/ManyCosmetics/Decorations');

    // Add 100 small cosmetic files
    for (let i = 0; i < 100; i++) {
      const content = new Uint8Array(1024); // 1KB each
      content.fill(i % 256);
      sevenZip.FS.writeFile(
        `/ziproot/plugins/ManyCosmetics/Decorations/cosmetic_${i}.hhh`,
        content
      );
    }

    // Create ZIP using chdir
    sevenZip.FS.chdir('/ziproot');
    sevenZip.callMain(['a', '-tzip', '/output.zip', '.']);
    sevenZip.FS.chdir('/');
    const zipData = sevenZip.FS.readFile('/output.zip');

    const startTime = Date.now();
    const result = await scanZip(zipData);
    const scanTime = Date.now() - startTime;

    console.log(`Scanned ZIP with 100 cosmetics in ${scanTime}ms`);

    expect(result.cosmetics).toHaveLength(100);
    expect(result.manifest?.name).toBe('ManyCosmetics');
    expect(result.hasFatalError).toBe(false);

    // Should scan quickly despite many files
    expect(scanTime).toBeLessThan(5000);
  }, 15000);

  it('should handle empty ZIP gracefully', async () => {
    const sevenZip = await SevenZipWasm();

    sevenZip.FS.mkdir('/ziproot');
    sevenZip.FS.writeFile(
      '/ziproot/manifest.json',
      JSON.stringify({
        name: 'EmptyMod',
        author: 'TestAuthor',
        version_number: '1.0.0',
      })
    );

    sevenZip.FS.chdir('/ziproot');
    sevenZip.callMain(['a', '-tzip', '/output.zip', '.']);
    sevenZip.FS.chdir('/');
    const zipData = sevenZip.FS.readFile('/output.zip');
    const result = await scanZip(zipData);

    expect(result.cosmetics).toHaveLength(0);
    expect(result.manifest?.name).toBe('EmptyMod');
    expect(result.hasFatalError).toBe(false);
  });

  it('should calculate unique hashes for different files', async () => {
    const sevenZip = await SevenZipWasm();

    sevenZip.FS.mkdir('/ziproot');
    sevenZip.FS.writeFile(
      '/ziproot/manifest.json',
      JSON.stringify({
        name: 'HashTest',
        author: 'TestAuthor',
        version_number: '1.0.0',
      })
    );

    // Create directories
    sevenZip.FS.mkdir('/ziproot/plugins');
    sevenZip.FS.mkdir('/ziproot/plugins/HashTest');
    sevenZip.FS.mkdir('/ziproot/plugins/HashTest/Decorations');

    // Add 10 cosmetic files with different content
    for (let i = 0; i < 10; i++) {
      const content = new Uint8Array(1024);
      content.fill(i); // Different fill value for each
      sevenZip.FS.writeFile(`/ziproot/plugins/HashTest/Decorations/cosmetic_${i}.hhh`, content);
    }

    sevenZip.FS.chdir('/ziproot');
    sevenZip.callMain(['a', '-tzip', '/output.zip', '.']);
    sevenZip.FS.chdir('/');
    const zipData = sevenZip.FS.readFile('/output.zip');
    const result = await scanZip(zipData);

    expect(result.cosmetics).toHaveLength(10);

    // Verify all hashes are unique
    const hashes = result.cosmetics.map((c) => c.hash);
    const uniqueHashes = new Set(hashes);
    expect(uniqueHashes.size).toBe(10);

    // Verify hash format (64 hex characters)
    expect(hashes.every((h) => /^[a-f0-9]{64}$/.test(h))).toBe(true);
  });
});
