/**
 * Tests for Thunderstore API Client
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ThunderstoreClient, createThunderstoreClient } from '../client';
import type { Community } from '../types';

// Helper to create mock Response objects
const mockResponse = (data: unknown, ok = true, status = 200, statusText = 'OK'): Partial<Response> => ({
  ok,
  status,
  statusText,
  json: async () => data,
  text: async () => (typeof data === 'string' ? data : JSON.stringify(data)),
} as Partial<Response>);

describe('ThunderstoreClient', () => {
  let mockFetch: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    mockFetch = vi.fn();
  });

  describe('constructor', () => {
    it('should create client with default config', () => {
      const client = new ThunderstoreClient({ proxyUrl: 'https://thunderstore.io' });
      expect(client).toBeInstanceOf(ThunderstoreClient);
    });

    it('should create client with custom config', () => {
      const client = new ThunderstoreClient({
        proxyUrl: 'https://custom.thunderstore.io',
        sessionToken: 'test-token',
        fetchImpl: mockFetch as any,
      });
      expect(client).toBeInstanceOf(ThunderstoreClient);
    });

    it('should use factory function', () => {
      const client = createThunderstoreClient();
      expect(client).toBeInstanceOf(ThunderstoreClient);
    });
  });

  describe('withSessionToken', () => {
    it('should return new client with updated session token', () => {
      const client = new ThunderstoreClient({ proxyUrl: 'https://thunderstore.io' });
      const newClient = client.withSessionToken('new-token');
      // Token is private, but we can verify through API calls
      expect(newClient).toBeInstanceOf(ThunderstoreClient);
      expect(newClient).not.toBe(client);
    });
  });

  describe('Community API', () => {
    it('should list communities', async () => {
      const mockData = {
        next: null,
        previous: null,
        results: [
          { identifier: 'valheim', name: 'Valheim', require_package_listing_approval: false },
        ],
      };

      mockFetch.mockResolvedValueOnce(mockResponse(mockData));

      const client = new ThunderstoreClient({ 
        proxyUrl: 'https://thunderstore.io',
        fetchImpl: mockFetch as any 
      });
      const result = await client.listCommunities();

      expect(mockFetch).toHaveBeenCalledWith(
        expect.stringContaining('/api/experimental/community/?'),
        expect.objectContaining({
          method: 'GET',
          headers: expect.objectContaining({
            Accept: 'application/json',
          }),
        })
      );
      expect(result).toEqual(mockData);
    });

    it('should get current community', async () => {
      const mockData: Community = {
        identifier: 'valheim',
        name: 'Valheim',
        discord_url: null,
        wiki_url: null,
        require_package_listing_approval: false,
      };

      mockFetch.mockResolvedValueOnce(mockResponse(mockData));

      const client = new ThunderstoreClient({ 
        proxyUrl: 'https://thunderstore.io',
        fetchImpl: mockFetch as any 
      });
      const result = await client.getCurrentCommunity();

      expect(result).toEqual(mockData);
    });

    it('should get community by ID', async () => {
      const mockData = {
        identifier: 'valheim',
        name: 'Valheim',
        datetime_created: '2021-01-01T00:00:00Z',
        has_mod_manager_support: true,
        is_listed: true,
        total_download_count: 1000000,
        total_package_count: 500,
      };

      mockFetch.mockResolvedValueOnce(mockResponse(mockData));

      const client = new ThunderstoreClient({ 
        proxyUrl: 'https://thunderstore.io',
        fetchImpl: mockFetch as any 
      });
      const result = await client.getCommunity('valheim');

      expect(mockFetch).toHaveBeenCalledWith(
        expect.stringContaining('/api/cyberstorm/community/valheim/'),
        expect.any(Object)
      );
      expect(result).toEqual(mockData);
    });

    it('should list community categories', async () => {
      const mockData = {
        next: null,
        previous: null,
        results: [
          { name: 'Mods', slug: 'mods' },
          { name: 'Tools', slug: 'tools' },
        ],
      };

      mockFetch.mockResolvedValueOnce(mockResponse(mockData));

      const client = new ThunderstoreClient({ 
        proxyUrl: 'https://thunderstore.io',
        fetchImpl: mockFetch as any 
      });
      const result = await client.listCommunityCategories('valheim');

      expect(result.results).toHaveLength(2);
    });
  });

  describe('Package API', () => {
    it('should get package index', async () => {
      const mockData = `{"namespace":"BepInEx","name":"BepInExPack","version_number":"5.4.21","file_format":"zip","file_size":12345678,"dependencies":""}
{"namespace":"Test","name":"TestMod","version_number":"1.0.0","file_format":"zip","file_size":1024,"dependencies":"BepInEx-BepInExPack-5.4.21"}`;

      mockFetch.mockResolvedValueOnce(mockResponse(mockData));

      const client = new ThunderstoreClient({ 
        proxyUrl: 'https://thunderstore.io',
        fetchImpl: mockFetch as any 
      });
      const result = await client.getPackageIndex();

      expect(result).toHaveLength(2);
      expect(result[0]).toMatchObject({
        namespace: 'BepInEx',
        name: 'BepInExPack',
        version_number: '5.4.21',
      });
      expect(result[1]).toMatchObject({
        namespace: 'Test',
        name: 'TestMod',
        dependencies: 'BepInEx-BepInExPack-5.4.21',
      });
    });

    it('should get a single package', async () => {
      const mockData = {
        namespace: 'BepInEx',
        name: 'BepInExPack',
        full_name: 'BepInEx-BepInExPack',
        owner: 'BepInEx',
        package_url: 'https://thunderstore.io/package/BepInEx/BepInExPack/',
        date_created: '2021-01-01T00:00:00Z',
        date_updated: '2023-01-01T00:00:00Z',
        rating_score: '100',
        is_pinned: false,
        is_deprecated: false,
        total_downloads: '1000000',
        latest: {
          namespace: 'BepInEx',
          name: 'BepInExPack',
          version_number: '5.4.21',
          full_name: 'BepInEx-BepInExPack-5.4.21',
          description: 'BepInEx pack for modding',
          icon: 'https://example.com/icon.png',
          dependencies: '',
          download_url: 'https://example.com/download',
          downloads: 1000000,
          date_created: '2023-01-01T00:00:00Z',
          website_url: 'https://github.com/BepInEx/BepInEx',
          is_active: true,
        },
        community_listings: [],
      };

      mockFetch.mockResolvedValueOnce(mockResponse(mockData));

      const client = new ThunderstoreClient({ 
        proxyUrl: 'https://thunderstore.io',
        fetchImpl: mockFetch as any 
      });
      const result = await client.getPackage('BepInEx', 'BepInExPack');

      expect(result).toEqual(mockData);
      expect(result.namespace).toBe('BepInEx');
      expect(result.name).toBe('BepInExPack');
    });

    it('should get package version', async () => {
      const mockData = {
        namespace: 'BepInEx',
        name: 'BepInExPack',
        version_number: '5.4.21',
        full_name: 'BepInEx-BepInExPack-5.4.21',
        description: 'BepInEx pack',
        icon: 'https://example.com/icon.png',
        dependencies: '',
        download_url: 'https://example.com/download',
        downloads: 1000000,
        date_created: '2023-01-01T00:00:00Z',
        website_url: 'https://github.com/BepInEx/BepInEx',
        is_active: true,
      };

      mockFetch.mockResolvedValueOnce(mockResponse(mockData));

      const client = new ThunderstoreClient({ 
        proxyUrl: 'https://thunderstore.io',
        fetchImpl: mockFetch as any 
      });
      const result = await client.getPackageVersion('BepInEx', 'BepInExPack', '5.4.21');

      expect(result.version_number).toBe('5.4.21');
    });

    it('should get package changelog', async () => {
      const mockData = {
        markdown: '# Changelog\n\n## 5.4.21\n- Bug fixes',
      };

      mockFetch.mockResolvedValueOnce(mockResponse(mockData));

      const client = new ThunderstoreClient({ 
        proxyUrl: 'https://thunderstore.io',
        fetchImpl: mockFetch as any 
      });
      const result = await client.getPackageVersionChangelog('BepInEx', 'BepInExPack', '5.4.21');

      expect(result.markdown).toContain('Changelog');
    });

    it('should get package readme', async () => {
      const mockData = {
        markdown: '# BepInExPack\n\nA modding framework',
      };

      mockFetch.mockResolvedValueOnce(mockResponse(mockData));

      const client = new ThunderstoreClient({ 
        proxyUrl: 'https://thunderstore.io',
        fetchImpl: mockFetch as any 
      });
      const result = await client.getPackageVersionReadme('BepInEx', 'BepInExPack', '5.4.21');

      expect(result.markdown).toContain('BepInExPack');
    });
  });

  describe('Package Metrics API', () => {
    it('should get package metrics', async () => {
      const mockData = {
        downloads: 1000000,
        rating_score: 95,
        latest_version: '5.4.21',
      };

      mockFetch.mockResolvedValueOnce(mockResponse(mockData));

      const client = new ThunderstoreClient({ 
        proxyUrl: 'https://thunderstore.io',
        fetchImpl: mockFetch as any 
      });
      const result = await client.getPackageMetrics('BepInEx', 'BepInExPack');

      expect(result.downloads).toBe(1000000);
      expect(result.latest_version).toBe('5.4.21');
    });

    it('should get package version metrics', async () => {
      const mockData = {
        downloads: 500000,
      };

      mockFetch.mockResolvedValueOnce(mockResponse(mockData));

      const client = new ThunderstoreClient({ 
        proxyUrl: 'https://thunderstore.io',
        fetchImpl: mockFetch as any 
      });
      const result = await client.getPackageVersionMetrics('BepInEx', 'BepInExPack', '5.4.21');

      expect(result.downloads).toBe(500000);
    });
  });

  describe('Wiki API', () => {
    it('should list package wikis', async () => {
      const mockData = {
        results: [
          {
            namespace: 'BepInEx',
            name: 'BepInExPack',
            wiki: {
              id: '1',
              title: 'BepInEx Wiki',
              slug: 'bepinex-wiki',
              datetime_created: '2021-01-01T00:00:00Z',
              datetime_updated: '2023-01-01T00:00:00Z',
              pages: [],
            },
          },
        ],
        cursor: '2023-01-01T00:00:00Z',
        has_more: false,
      };

      mockFetch.mockResolvedValueOnce(mockResponse(mockData));

      const client = new ThunderstoreClient({ 
        proxyUrl: 'https://thunderstore.io',
        fetchImpl: mockFetch as any 
      });
      const result = await client.listPackageWikis();

      expect(result.results).toHaveLength(1);
      expect(result.has_more).toBe(false);
    });

    it('should get package wiki', async () => {
      const mockData = {
        id: '1',
        title: 'BepInEx Wiki',
        slug: 'bepinex-wiki',
        datetime_created: '2021-01-01T00:00:00Z',
        datetime_updated: '2023-01-01T00:00:00Z',
        pages: [
          {
            id: '1',
            title: 'Getting Started',
            slug: 'getting-started',
            datetime_created: '2021-01-01T00:00:00Z',
            datetime_updated: '2023-01-01T00:00:00Z',
          },
        ],
      };

      mockFetch.mockResolvedValueOnce(mockResponse(mockData));

      const client = new ThunderstoreClient({ 
        proxyUrl: 'https://thunderstore.io',
        fetchImpl: mockFetch as any 
      });
      const result = await client.getPackageWiki('BepInEx', 'BepInExPack');

      expect(result.pages).toHaveLength(1);
    });

    it('should get wiki page', async () => {
      const mockData = {
        id: '1',
        title: 'Getting Started',
        slug: 'getting-started',
        datetime_created: '2021-01-01T00:00:00Z',
        datetime_updated: '2023-01-01T00:00:00Z',
        markdown_content: '# Getting Started\n\nWelcome!',
      };

      mockFetch.mockResolvedValueOnce(mockResponse(mockData));

      const client = new ThunderstoreClient({ 
        proxyUrl: 'https://thunderstore.io',
        fetchImpl: mockFetch as any 
      });
      const result = await client.getWikiPage(1);

      expect(result.markdown_content).toContain('Getting Started');
    });
  });

  describe('Markdown Rendering', () => {
    it('should render markdown to HTML', async () => {
      const mockData = {
        html: '<h1>Hello <strong>World</strong></h1>',
      };

      mockFetch.mockResolvedValueOnce(mockResponse(mockData));

      const client = new ThunderstoreClient({ 
        proxyUrl: 'https://thunderstore.io',
        fetchImpl: mockFetch as any 
      });
      const result = await client.renderMarkdown({
        markdown: '# Hello **World**',
      });

      expect(result.html).toContain('<h1>');
      expect(result.html).toContain('<strong>');
    });
  });

  describe('Helper Methods', () => {
    it('should build package download URL', () => {
      const client = new ThunderstoreClient({ proxyUrl: 'https://thunderstore.io' });
      const url = client.getPackageDownloadUrl('BepInEx', 'BepInExPack', { version: '5.4.21' });

      expect(url).toBe('https://thunderstore.io/package/download/BepInEx/BepInExPack/5.4.21/');
    });

    it('should build package URL', () => {
      const client = new ThunderstoreClient({ proxyUrl: 'https://thunderstore.io' });
      const url = client.getPackageUrl('BepInEx', 'BepInExPack');

      expect(url).toBe('https://thunderstore.io/package/BepInEx/BepInExPack/');
    });

    it('should build package version URL', () => {
      const client = new ThunderstoreClient({ proxyUrl: 'https://thunderstore.io' });
      const url = client.getPackageVersionUrl('BepInEx', 'BepInExPack', '5.4.21');

      expect(url).toBe('https://thunderstore.io/package/BepInEx/BepInExPack/5.4.21/');
    });
  });

  describe('Error Handling', () => {
    it('should throw error on failed GET request', async () => {
      mockFetch.mockResolvedValueOnce(mockResponse(null, false, 404, 'Not Found'));

      const client = new ThunderstoreClient({ 
        proxyUrl: 'https://thunderstore.io',
        fetchImpl: mockFetch as any 
      });

      await expect(client.getPackage('Invalid', 'Package')).rejects.toThrow(
        'API request failed: 404 Not Found'
      );
    });

    it('should throw error on failed POST request', async () => {
      mockFetch.mockResolvedValueOnce(mockResponse(null, false, 400, 'Bad Request'));

      const client = new ThunderstoreClient({ 
        proxyUrl: 'https://thunderstore.io',
        fetchImpl: mockFetch as any 
      });

      await expect(client.renderMarkdown({ markdown: '' })).rejects.toThrow(
        'API request failed: 400 Bad Request'
      );
    });
  });

  describe('Authentication', () => {
    it('should include session token in headers', async () => {
      mockFetch.mockResolvedValueOnce(mockResponse({ results: [] }));

      const client = new ThunderstoreClient({
        proxyUrl: 'https://thunderstore.io',
        fetchImpl: mockFetch as any,
        sessionToken: 'test-token',
      });

      await client.listCommunities();

      expect(mockFetch).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({
          headers: expect.objectContaining({
            Authorization: 'Token test-token',
          }),
        })
      );
    });
  });
});
