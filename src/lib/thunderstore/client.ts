/**
 * Thunderstore API Client
 * Browser-compatible client for the Thunderstore API
 *
 * API Documentation: https://thunderstore.io/api/docs/
 */

import type {
  Community,
  CyberstormCommunity,
  PackageCategory,
  PackageExperimental,
  PackageVersionExperimental,
  PackageListing,
  PackageIndexEntry,
  PackageMetrics,
  PackageVersionMetrics,
  PaginatedResponse,
  CursorPaginatedResponse,
  Wiki,
  WikiPage,
  PackageWiki,
  MarkdownResponse,
  RenderMarkdownParams,
  RenderMarkdownResponse,
  PackageListOptions,
} from './types';

export interface ThunderstoreClientConfig {
  baseUrl?: string;
  sessionToken?: string;
  fetchImpl?: typeof fetch;
}

export class ThunderstoreClient {
  private readonly baseUrl: string;
  private sessionToken?: string;
  private readonly fetchImpl: typeof fetch;

  constructor(config: ThunderstoreClientConfig = {}) {
    this.baseUrl = config.baseUrl || 'https://thunderstore.io';
    this.sessionToken = config.sessionToken;
    this.fetchImpl = config.fetchImpl || fetch.bind(globalThis);
  }

  /**
   * Set authentication session token
   */
  setSessionToken(token: string | undefined): void {
    this.sessionToken = token;
  }

  /**
   * Make a GET request to the API
   */
  private async get<T>(path: string, init?: RequestInit): Promise<T> {
    const headers: Record<string, string> = {
      Accept: 'application/json',
      ...(init?.headers as Record<string, string>),
    };

    if (this.sessionToken) {
      headers['Authorization'] = `Token ${this.sessionToken}`;
    }

    const response = await this.fetchImpl(`${this.baseUrl}${path}`, {
      ...init,
      method: 'GET',
      headers,
    });

    if (!response.ok) {
      throw new Error(`API request failed: ${response.status} ${response.statusText}`);
    }

    return response.json();
  }

  /**
   * Make a POST request to the API
   */
  private async post<T>(path: string, body?: unknown, init?: RequestInit): Promise<T> {
    const headers: Record<string, string> = {
      Accept: 'application/json',
      'Content-Type': 'application/json',
      ...(init?.headers as Record<string, string>),
    };

    if (this.sessionToken) {
      headers['Authorization'] = `Token ${this.sessionToken}`;
    }

    const response = await this.fetchImpl(`${this.baseUrl}${path}`, {
      ...init,
      method: 'POST',
      headers,
      body: body ? JSON.stringify(body) : undefined,
    });

    if (!response.ok) {
      throw new Error(`API request failed: ${response.status} ${response.statusText}`);
    }

    // Some endpoints return 204 No Content
    if (response.status === 204) {
      return undefined as T;
    }

    return response.json();
  }

  // ============================================================================
  // Community API
  // ============================================================================

  /**
   * List all communities
   */
  async listCommunities(options: PackageListOptions = {}): Promise<PaginatedResponse<Community>> {
    const params = new URLSearchParams();
    if (options.cursor) params.set('cursor', options.cursor);

    return this.get(`/api/experimental/community/?${params}`);
  }

  /**
   * Get current community based on domain
   */
  async getCurrentCommunity(): Promise<Community> {
    return this.get('/api/experimental/current-community/');
  }

  /**
   * Get community by ID (Cyberstorm API)
   */
  async getCommunity(communityId: string): Promise<CyberstormCommunity> {
    return this.get(`/api/cyberstorm/community/${communityId}/`);
  }

  /**
   * List categories for a community
   */
  async listCommunityCategories(
    community: string,
    options: PackageListOptions = {}
  ): Promise<PaginatedResponse<PackageCategory>> {
    const params = new URLSearchParams();
    if (options.cursor) params.set('cursor', options.cursor);

    return this.get(`/api/experimental/community/${community}/category/?${params}`);
  }

  // ============================================================================
  // Package API (V1)
  // ============================================================================

  /**
   * List all packages (V1 API)
   */
  async listPackagesV1(communityId?: string, search?: string): Promise<PackageListing[]> {
    const basePath = communityId ? `/c/${communityId}/api/v1/package/` : '/api/v1/package/';
    if (search && search.trim().length > 0) {
      const url = `${basePath}?search=${encodeURIComponent(search.trim())}`;
      try {
        return await this.get(url);
      } catch (err) {
        // Fallback to unfiltered list if server doesn't support search param
        console.warn('Thunderstore V1 search parameter failed; falling back to full list');
      }
    }
    return this.get(basePath);
  }

  /**
   * Get a package by UUID (V1 API) - Deprecated
   */
  async getPackageV1(uuid: string, communityId?: string): Promise<PackageListing> {
    const path = communityId
      ? `/c/${communityId}/api/v1/package/${uuid}/`
      : `/api/v1/package/${uuid}/`;
    return this.get(path);
  }

  // ============================================================================
  // Package API (Experimental)
  // ============================================================================

  /**
   * List all packages (Experimental API) - Deprecated, use package index instead
   */
  async listPackages(
    options: PackageListOptions = {}
  ): Promise<PaginatedResponse<PackageExperimental>> {
    const params = new URLSearchParams();
    if (options.cursor) params.set('cursor', options.cursor);

    return this.get(`/api/experimental/package/?${params}`);
  }

  /**
   * Get package index (newline-delimited JSON stream)
   * This is the recommended way to get all packages
   */
  async getPackageIndex(): Promise<PackageIndexEntry[]> {
    const response = await this.fetchImpl(`${this.baseUrl}/api/experimental/package-index/`);

    if (!response.ok) {
      throw new Error(`API request failed: ${response.status} ${response.statusText}`);
    }

    const text = await response.text();
    const lines = text.trim().split('\n');
    return lines.map((line) => JSON.parse(line));
  }

  /**
   * Get a single package
   */
  async getPackage(namespace: string, name: string): Promise<PackageExperimental> {
    return this.get(`/api/experimental/package/${namespace}/${name}/`);
  }

  /**
   * Get a single package version
   */
  async getPackageVersion(
    namespace: string,
    name: string,
    version: string
  ): Promise<PackageVersionExperimental> {
    return this.get(`/api/experimental/package/${namespace}/${name}/${version}/`);
  }

  /**
   * Get package version changelog
   */
  async getPackageVersionChangelog(
    namespace: string,
    name: string,
    version: string
  ): Promise<MarkdownResponse> {
    return this.get(`/api/experimental/package/${namespace}/${name}/${version}/changelog/`);
  }

  /**
   * Get package version readme
   */
  async getPackageVersionReadme(
    namespace: string,
    name: string,
    version: string
  ): Promise<MarkdownResponse> {
    return this.get(`/api/experimental/package/${namespace}/${name}/${version}/readme/`);
  }

  // ============================================================================
  // Package Metrics API
  // ============================================================================

  /**
   * Get metrics for a package
   */
  async getPackageMetrics(namespace: string, name: string): Promise<PackageMetrics> {
    return this.get(`/api/v1/package-metrics/${namespace}/${name}/`);
  }

  /**
   * Get metrics for a package version
   */
  async getPackageVersionMetrics(
    namespace: string,
    name: string,
    version: string
  ): Promise<PackageVersionMetrics> {
    return this.get(`/api/v1/package-metrics/${namespace}/${name}/${version}/`);
  }

  // ============================================================================
  // Wiki API
  // ============================================================================

  /**
   * List package wikis (supports querying by update time)
   */
  async listPackageWikis(after?: Date): Promise<CursorPaginatedResponse<PackageWiki>> {
    const params = new URLSearchParams();
    if (after) params.set('after', after.toISOString());

    return this.get(`/api/experimental/package/wikis/?${params}`);
  }

  /**
   * Get wiki index for a package
   */
  async getPackageWiki(namespace: string, name: string): Promise<Wiki> {
    return this.get(`/api/experimental/package/${namespace}/${name}/wiki/`);
  }

  /**
   * Get a specific wiki page
   */
  async getWikiPage(pageId: number): Promise<WikiPage> {
    return this.get(`/api/experimental/wiki/page/${pageId}/`);
  }

  /**
   * Create or update a wiki page (requires authentication)
   */
  async upsertWikiPage(
    namespace: string,
    name: string,
    data: { id?: string; title: string; markdown_content: string }
  ): Promise<WikiPage> {
    return this.post(`/api/experimental/package/${namespace}/${name}/wiki/`, data);
  }

  /**
   * Delete a wiki page (requires authentication)
   */
  async deleteWikiPage(namespace: string, name: string, pageId: string): Promise<void> {
    return this.post(
      `/api/experimental/package/${namespace}/${name}/wiki/`,
      { id: pageId },
      {
        method: 'DELETE',
      }
    );
  }

  // ============================================================================
  // Markdown Rendering API
  // ============================================================================

  /**
   * Render markdown to HTML
   */
  async renderMarkdown(params: RenderMarkdownParams): Promise<RenderMarkdownResponse> {
    return this.post('/api/experimental/frontend/render-markdown/', params);
  }

  // ============================================================================
  // Helper methods
  // ============================================================================

  /**
   * Build package download URL
   */
  getPackageDownloadUrl(namespace: string, name: string): string;
  getPackageDownloadUrl(namespace: string, name: string, version: string): string;
  getPackageDownloadUrl(namespace: string, name: string, version?: string): string {
    if (version) {
      return `${this.baseUrl}/package/download/${namespace}/${name}/${version}/`;
    }
    return `${this.baseUrl}/package/download/${namespace}/${name}/`;
  }

  /**
   * Build package page URL
   */
  getPackageUrl(namespace: string, name: string): string {
    return `${this.baseUrl}/package/${namespace}/${name}/`;
  }

  /**
   * Build package version page URL
   */
  getPackageVersionUrl(namespace: string, name: string, version: string): string {
    return `${this.baseUrl}/package/${namespace}/${name}/${version}/`;
  }
}

/**
 * Create a default Thunderstore API client instance
 */
export function createThunderstoreClient(config?: ThunderstoreClientConfig): ThunderstoreClient {
  return new ThunderstoreClient(config);
}
