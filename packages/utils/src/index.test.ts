import { describe, it, expect } from 'vitest';
import {
  formatDownloads,
  formatRating,
  getLatestVersion,
  getTotalDownloads,
  type ThunderstorePackageListing,
  type ThunderstorePackageVersion,
} from './index';

describe('Utils', () => {
  describe('formatDownloads', () => {
    it('should return number as string for values < 1000', () => {
      expect(formatDownloads(0)).toBe('0');
      expect(formatDownloads(999)).toBe('999');
      expect(formatDownloads(500)).toBe('500');
    });

    it('should format thousands with K suffix', () => {
      expect(formatDownloads(1000)).toBe('1.0K');
      expect(formatDownloads(1500)).toBe('1.5K');
      expect(formatDownloads(999999)).toBe('1000.0K');
    });

    it('should format millions with M suffix', () => {
      expect(formatDownloads(1000000)).toBe('1.0M');
      expect(formatDownloads(1500000)).toBe('1.5M');
      expect(formatDownloads(10000000)).toBe('10.0M');
    });

    it('should handle large numbers', () => {
      expect(formatDownloads(123456789)).toBe('123.5M');
    });
  });

  describe('formatRating', () => {
    it('should return N/A for undefined', () => {
      expect(formatRating(undefined)).toBe('N/A');
    });

    it('should return N/A for null (edge case)', () => {
      // @ts-expect-error Testing null edge case
      expect(formatRating(null)).toBe('N/A');
    });

    it('should convert 0-100 scale to 0-5 stars', () => {
      expect(formatRating(0)).toBe('0.0 ★');
      expect(formatRating(50)).toBe('2.5 ★');
      expect(formatRating(85)).toBe('4.3 ★');
      expect(formatRating(100)).toBe('5.0 ★');
    });

    it('should handle decimal values', () => {
      expect(formatRating(33.3)).toBe('1.7 ★');
      expect(formatRating(66.7)).toBe('3.3 ★');
    });

    it('should handle out-of-range values gracefully', () => {
      expect(formatRating(150)).toBe('7.5 ★');
      expect(formatRating(-10)).toBe('-0.5 ★');
    });
  });

  describe('getLatestVersion', () => {
    it('should return the first version (newest)', () => {
      const pkg: ThunderstorePackageListing = {
        namespace: 'Test',
        name: 'Mod',
        full_name: 'Test-Mod',
        owner: 'Test',
        package_url: 'https://thunderstore.io/c/repo/p/Test/Mod/',
        date_created: '2024-01-01T00:00:00Z',
        date_updated: '2024-01-02T00:00:00Z',
        uuid4: 'test-uuid',
        rating_score: 85,
        is_pinned: false,
        is_deprecated: false,
        has_nsfw_content: false,
        categories: ['cosmetics'],
        versions: [
          {
            name: 'Test-Mod',
            full_name: 'Test-Mod-2.0.0',
            description: 'Version 2',
            icon: 'https://cdn.thunderstore.io/icon.png',
            version_number: '2.0.0',
            dependencies: [],
            download_url: 'https://cdn.thunderstore.io/file2.zip',
            downloads: 100,
            date_created: '2024-01-02T00:00:00Z',
            is_active: true,
            uuid4: 'version2-uuid',
            file_size: 2000000,
          },
          {
            name: 'Test-Mod',
            full_name: 'Test-Mod-1.0.0',
            description: 'Version 1',
            icon: 'https://cdn.thunderstore.io/icon.png',
            version_number: '1.0.0',
            dependencies: [],
            download_url: 'https://cdn.thunderstore.io/file1.zip',
            downloads: 50,
            date_created: '2024-01-01T00:00:00Z',
            is_active: false,
            uuid4: 'version1-uuid',
            file_size: 1000000,
          },
        ],
      };

      const latest = getLatestVersion(pkg);
      expect(latest?.version_number).toBe('2.0.0');
    });

    it('should return undefined for package with no versions', () => {
      const pkg: ThunderstorePackageListing = {
        namespace: 'Test',
        name: 'Mod',
        full_name: 'Test-Mod',
        owner: 'Test',
        package_url: 'https://thunderstore.io/c/repo/p/Test/Mod/',
        date_created: '2024-01-01T00:00:00Z',
        date_updated: '2024-01-01T00:00:00Z',
        uuid4: 'test-uuid',
        rating_score: 85,
        is_pinned: false,
        is_deprecated: false,
        has_nsfw_content: false,
        categories: [],
        versions: [],
      };

      expect(getLatestVersion(pkg)).toBeUndefined();
    });
  });

  describe('getTotalDownloads', () => {
    it('should sum downloads from all versions', () => {
      const pkg: ThunderstorePackageListing = {
        namespace: 'Test',
        name: 'Mod',
        full_name: 'Test-Mod',
        owner: 'Test',
        package_url: 'https://thunderstore.io/c/repo/p/Test/Mod/',
        date_created: '2024-01-01T00:00:00Z',
        date_updated: '2024-01-02T00:00:00Z',
        uuid4: 'test-uuid',
        rating_score: 85,
        is_pinned: false,
        is_deprecated: false,
        has_nsfw_content: false,
        categories: ['cosmetics'],
        versions: [
          {
            name: 'Test-Mod',
            full_name: 'Test-Mod-2.0.0',
            description: 'Version 2',
            icon: 'https://cdn.thunderstore.io/icon.png',
            version_number: '2.0.0',
            dependencies: [],
            download_url: 'https://cdn.thunderstore.io/file2.zip',
            downloads: 100,
            date_created: '2024-01-02T00:00:00Z',
            is_active: true,
            uuid4: 'version2-uuid',
            file_size: 2000000,
          },
          {
            name: 'Test-Mod',
            full_name: 'Test-Mod-1.0.0',
            description: 'Version 1',
            icon: 'https://cdn.thunderstore.io/icon.png',
            version_number: '1.0.0',
            dependencies: [],
            download_url: 'https://cdn.thunderstore.io/file1.zip',
            downloads: 50,
            date_created: '2024-01-01T00:00:00Z',
            is_active: false,
            uuid4: 'version1-uuid',
            file_size: 1000000,
          },
        ],
      };

      expect(getTotalDownloads(pkg)).toBe(150);
    });

    it('should return 0 for package with no versions', () => {
      const pkg: ThunderstorePackageListing = {
        namespace: 'Test',
        name: 'Mod',
        full_name: 'Test-Mod',
        owner: 'Test',
        package_url: 'https://thunderstore.io/c/repo/p/Test/Mod/',
        date_created: '2024-01-01T00:00:00Z',
        date_updated: '2024-01-01T00:00:00Z',
        uuid4: 'test-uuid',
        rating_score: 85,
        is_pinned: false,
        is_deprecated: false,
        has_nsfw_content: false,
        categories: [],
        versions: [],
      };

      expect(getTotalDownloads(pkg)).toBe(0);
    });
  });
});
