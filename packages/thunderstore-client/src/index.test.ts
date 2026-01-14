import { describe, it, expect, vi, beforeEach } from 'vitest';
import {
  getCommunityMetadata,
  getCommunityFilters,
  getPackageListing,
  getPackageDetail,
  type CommunityMetadata,
  type CommunityFilters,
  type ListingResponse,
  type PackageDetail,
} from './index';

// Mock fetch globally
global.fetch = vi.fn();

describe('Thunderstore Client', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('getCommunityMetadata', () => {
    it('should fetch community metadata', async () => {
      const mockData: CommunityMetadata = {
        name: 'REPO',
        identifier: 'repo',
        discord_url: 'https://discord.gg/repo',
        wiki_url: 'https://repo.wiki',
        require_package_listing_approval: false,
      };

      (global.fetch as any).mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => mockData,
      });

      const result = await getCommunityMetadata('repo');

      expect(global.fetch).toHaveBeenCalledWith(
        'https://thunderstore.io/api/cyberstorm/community/repo/',
        expect.objectContaining({
          headers: expect.objectContaining({
            'User-Agent': expect.any(String),
          }),
        })
      );
      expect(result).toEqual(mockData);
    });

    it('should use default community', async () => {
      (global.fetch as any).mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({
          name: 'REPO',
          identifier: 'repo',
          require_package_listing_approval: false,
        }),
      });

      await getCommunityMetadata();

      expect(global.fetch).toHaveBeenCalledWith(
        expect.stringContaining('/community/repo/'),
        expect.any(Object)
      );
    });

    it('should throw error on failed request', async () => {
      (global.fetch as any).mockResolvedValueOnce({
        ok: false,
        status: 404,
      });

      await expect(getCommunityMetadata('invalid')).rejects.toThrow(
        'Failed to fetch community metadata: 404'
      );
    });
  });

  describe('getCommunityFilters', () => {
    it('should fetch community filters', async () => {
      const mockData: CommunityFilters = {
        package_categories: [
          { slug: 'cosmetics', label: 'Cosmetics', count: 100 },
          { slug: 'tools', label: 'Tools', count: 50 },
        ],
        sections: [
          { slug: 'mods', label: 'Mods' },
          { slug: 'modpacks', label: 'Modpacks' },
        ],
      };

      (global.fetch as any).mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => mockData,
      });

      const result = await getCommunityFilters('repo');

      expect(global.fetch).toHaveBeenCalledWith(
        'https://thunderstore.io/community/repo/filters/',
        expect.any(Object)
      );
      expect(result).toEqual(mockData);
    });
  });

  describe('getPackageListing', () => {
    it('should fetch package listing with default params', async () => {
      const mockData: ListingResponse = {
        count: 100,
        next: null,
        previous: null,
        results: [],
      };

      (global.fetch as any).mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => mockData,
      });

      const result = await getPackageListing();

      expect(global.fetch).toHaveBeenCalledWith(
        'https://thunderstore.io/listing/repo/',
        expect.any(Object)
      );
      expect(result).toEqual(mockData);
    });

    it('should include query parameters', async () => {
      (global.fetch as any).mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({ count: 0, results: [], next: null, previous: null }),
      });

      await getPackageListing({
        community: 'repo',
        page: 2,
        q: 'head',
        included_categories: ['cosmetics', 'heads'],
        section: 'mods',
        ordering: '-downloads',
        page_size: 20,
      });

      const callUrl = (global.fetch as any).mock.calls[0][0];
      expect(callUrl).toContain('/listing/repo/');
      expect(callUrl).toContain('page=2');
      expect(callUrl).toContain('q=head');
      expect(callUrl).toContain('included_categories=cosmetics%2Cheads');
      expect(callUrl).toContain('section=mods');
      expect(callUrl).toContain('ordering=-downloads');
      expect(callUrl).toContain('page_size=20');
    });

    it('should handle empty included_categories', async () => {
      (global.fetch as any).mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({ count: 0, results: [], next: null, previous: null }),
      });

      await getPackageListing({
        included_categories: [],
      });

      const callUrl = (global.fetch as any).mock.calls[0][0];
      expect(callUrl).not.toContain('included_categories');
    });

    it('should throw error on failed request', async () => {
      (global.fetch as any).mockResolvedValueOnce({
        ok: false,
        status: 500,
      });

      await expect(getPackageListing()).rejects.toThrow('Failed to fetch package listing: 500');
    });
  });

  describe('getPackageDetail', () => {
    it('should fetch package detail', async () => {
      const mockData: PackageDetail = {
        namespace: 'TestAuthor',
        name: 'TestMod',
        full_name: 'TestAuthor-TestMod',
        owner: 'TestAuthor',
        package_url: 'https://thunderstore.io/c/repo/p/TestAuthor/TestMod/',
        date_created: '2024-01-01T00:00:00Z',
        date_updated: '2024-01-02T00:00:00Z',
        rating_score: 85,
        is_pinned: false,
        is_deprecated: false,
        categories: ['cosmetics'],
        versions: [],
      };

      (global.fetch as any).mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => mockData,
      });

      const result = await getPackageDetail('TestAuthor', 'TestMod', 'repo');

      expect(global.fetch).toHaveBeenCalledWith(
        'https://thunderstore.io/api/cyberstorm/community/repo/package/TestAuthor/TestMod/',
        expect.any(Object)
      );
      expect(result).toEqual(mockData);
    });

    it('should use default community', async () => {
      (global.fetch as any).mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({
          namespace: 'Test',
          name: 'Mod',
          full_name: 'Test-Mod',
          owner: 'Test',
          package_url: '',
          date_created: '',
          date_updated: '',
          rating_score: 0,
          is_pinned: false,
          is_deprecated: false,
          categories: [],
          versions: [],
        }),
      });

      await getPackageDetail('Test', 'Mod');

      expect(global.fetch).toHaveBeenCalledWith(
        expect.stringContaining('/community/repo/package/'),
        expect.any(Object)
      );
    });

    it('should throw error on failed request', async () => {
      (global.fetch as any).mockResolvedValueOnce({
        ok: false,
        status: 404,
      });

      await expect(getPackageDetail('Unknown', 'Mod')).rejects.toThrow(
        'Failed to fetch package detail: 404'
      );
    });
  });
});
