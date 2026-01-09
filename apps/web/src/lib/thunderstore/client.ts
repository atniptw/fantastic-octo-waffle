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

/**
 * Custom error class for Thunderstore API errors
 * Provides detailed information about failed requests
 */
export class ThunderstoreApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly statusText: string,
    public readonly path: string,
    public readonly responseBody?: unknown
  ) {
    super(message);
    this.name = 'ThunderstoreApiError';
    Object.setPrototypeOf(this, ThunderstoreApiError.prototype);
  }

  /**
   * Create error from fetch response
   */
  static async fromResponse(response: Response, path: string): Promise<ThunderstoreApiError> {
    let body: unknown;
    try {
      body = await response.json();
    } catch {
      try {
        body = await response.text();
      } catch {
        body = undefined;
      }
    }
    return new ThunderstoreApiError(
      `API request failed: ${response.status} ${response.statusText}`,
      response.status,
      response.statusText,
      path,
      body
    );
  }
}

export interface RetryConfig {
  maxRetries?: number;
  retryDelay?: number;
  retryableStatuses?: number[];
}

export interface ThunderstoreClientConfig {
  proxyUrl: string;
  sessionToken?: string;
  fetchImpl?: typeof fetch;
  community?: string;
  timeout?: number;
  retryConfig?: RetryConfig;
}

interface RequestOptions {
  body?: unknown;
  headers?: Record<string, string>;
  signal?: AbortSignal;
}

export class ThunderstoreClient {
  private readonly proxyUrl: string;
  private readonly sessionToken?: string;
  private readonly fetchImpl: typeof fetch;
  private readonly community?: string;
  private readonly timeout: number;
  private readonly retryConfig: RetryConfig;

  constructor(config: ThunderstoreClientConfig) {
    this.proxyUrl = config.proxyUrl;
    this.sessionToken = config.sessionToken;
    this.fetchImpl = config.fetchImpl || fetch.bind(globalThis);
    this.community = config.community;
    this.timeout = config.timeout ?? 30000;
    this.retryConfig = {
      maxRetries: config.retryConfig?.maxRetries ?? 3,
      retryDelay: config.retryConfig?.retryDelay ?? 1000,
      retryableStatuses: config.retryConfig?.retryableStatuses ?? [429, 503, 504],
    };
  }

  /**
   * Create a new client with a session token
   * @returns New client instance with the provided token
   */
  withSessionToken(token: string | undefined): ThunderstoreClient {
    return new ThunderstoreClient({
      proxyUrl: this.proxyUrl,
      sessionToken: token,
      fetchImpl: this.fetchImpl,
      community: this.community,
      timeout: this.timeout,
      retryConfig: this.retryConfig,
    });
  }

  /**
   * Create a new client with a community context
   * @returns New client instance configured for the specified community
   */
  withCommunity(community: string): ThunderstoreClient {
    return new ThunderstoreClient({
      proxyUrl: this.proxyUrl,
      sessionToken: this.sessionToken,
      fetchImpl: this.fetchImpl,
      community,
      timeout: this.timeout,
      retryConfig: this.retryConfig,
    });
  }

  /**
   * Create a new client with a timeout
   * @returns New client instance with the specified timeout
   */
  withTimeout(timeout: number): ThunderstoreClient {
    return new ThunderstoreClient({
      proxyUrl: this.proxyUrl,
      sessionToken: this.sessionToken,
      fetchImpl: this.fetchImpl,
      community: this.community,
      timeout,
      retryConfig: this.retryConfig,
    });
  }

  /**
   * Make an HTTP request to the API with retry logic and timeout support
   */
  private async request<T>(
    method: 'GET' | 'POST' | 'DELETE',
    path: string,
    options?: RequestOptions,
    retries = 0
  ): Promise<T> {
    const headers: Record<string, string> = {
      Accept: 'application/json',
      ...(method !== 'GET' ? { 'Content-Type': 'application/json' } : {}),
      ...options?.headers,
    };

    if (this.sessionToken) {
      headers['Authorization'] = `Token ${this.sessionToken}`;
    }

    const controller = new AbortController();
    const signal = options?.signal || controller.signal;
    const timeoutId = setTimeout(() => controller.abort(), this.timeout);

    try {
      const response = await this.fetchImpl(`${this.proxyUrl}${path}`, {
        method,
        headers,
        body: options?.body ? JSON.stringify(options.body) : undefined,
        signal,
      });

      clearTimeout(timeoutId);

      if (!response.ok) {
        const error = await ThunderstoreApiError.fromResponse(response, path);

        // Retry logic for specific status codes
        if (
          this.retryConfig.retryableStatuses?.includes(response.status) &&
          retries < (this.retryConfig.maxRetries ?? 3)
        ) {
          const delay = (this.retryConfig.retryDelay ?? 1000) * Math.pow(2, retries);
          await new Promise((resolve) => setTimeout(resolve, delay));
          return this.request<T>(method, path, options, retries + 1);
        }

        throw error;
      }

      // Handle 204 No Content
      if (response.status === 204) {
        return undefined as T;
      }

      return response.json();
    } catch (error) {
      clearTimeout(timeoutId);

      // Handle timeout
      if (error instanceof Error && error.name === 'AbortError') {
        throw new Error(`Request timeout after ${this.timeout}ms to ${path}`);
      }

      throw error;
    }
  }

  /**
   * Make a GET request to the API
   */
  private async get<T>(path: string, init?: RequestInit): Promise<T> {
    return this.request<T>('GET', path, {
      signal: init?.signal as AbortSignal | undefined,
      headers: init?.headers as Record<string, string> | undefined,
    });
  }

  /**
   * Make a POST request to the API
   */
  private async post<T>(path: string, body?: unknown, init?: RequestInit): Promise<T> {
    return this.request<T>('POST', path, {
      body,
      signal: init?.signal as AbortSignal | undefined,
      headers: init?.headers as Record<string, string> | undefined,
    });
  }

  /**
   * Make a DELETE request to the API
   */
  private async delete<T>(path: string, init?: RequestInit): Promise<T> {
    return this.request<T>('DELETE', path, {
      signal: init?.signal as AbortSignal | undefined,
      headers: init?.headers as Record<string, string> | undefined,
    });
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
   * @deprecated Use `listPackages()` or `getPackageIndex()` instead. V1 API will be removed in future versions.
   */
  async listPackagesV1(communityId?: string, search?: string): Promise<PackageListing[]> {
    console.warn(
      'listPackagesV1() is deprecated. Use listPackages() or getPackageIndex() instead.'
    );
    const basePath = communityId ? `/c/${communityId}/api/v1/package/` : '/api/v1/package/';
    if (search && search.trim().length > 0) {
      const url = `${basePath}?search=${encodeURIComponent(search.trim())}`;
      try {
        return await this.get(url);
      } catch {
        // Fallback to unfiltered list if server doesn't support search param
        console.warn('Thunderstore V1 search parameter failed; falling back to full list');
      }
    }
    return this.get(basePath);
  }

  /**
   * Get a package by UUID (V1 API)
   * @deprecated Use `getPackage()` instead. V1 API will be removed in future versions.
   */
  async getPackageV1(uuid: string, communityId?: string): Promise<PackageListing> {
    console.warn('getPackageV1() is deprecated. Use getPackage() instead.');
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
    const response = await this.fetchImpl(`${this.proxyUrl}/api/experimental/package-index/`);

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
  async deleteWikiPage(namespace: string, name: string, _pageId: string): Promise<void> {
    return this.delete(
      `/api/experimental/package/${namespace}/${name}/wiki/`,
      {
        headers: {
          'Content-Type': 'application/json',
        },
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
  getPackageDownloadUrl(
    namespace: string,
    name: string,
    options?: { version?: string }
  ): string {
    const communityPrefix = this.community ? `/c/${this.community}` : '';
    const versionPath = options?.version ? `/${options.version}` : '';
    return `${this.proxyUrl}${communityPrefix}/package/download/${namespace}/${name}${versionPath}/`;
  }

  /**
   * Build package page URL (for user navigation to Thunderstore website)
   */
  getPackageUrl(namespace: string, name: string): string {
    const communityPrefix = this.community ? `/c/${this.community}` : '';
    return `https://thunderstore.io${communityPrefix}/package/${namespace}/${name}/`;
  }

  /**
   * Build package version page URL (for user navigation to Thunderstore website)
   */
  getPackageVersionUrl(namespace: string, name: string, version: string): string {
    const communityPrefix = this.community ? `/c/${this.community}` : '';
    return `https://thunderstore.io${communityPrefix}/package/${namespace}/${name}/${version}/`;
  }
}

/**
 * Create a default Thunderstore API client instance
 */
export function createThunderstoreClient(
  config: Partial<ThunderstoreClientConfig> & { proxyUrl?: string } = {}
): ThunderstoreClient {
  return new ThunderstoreClient({
    proxyUrl: config.proxyUrl ?? 'https://thunderstore.io',
    ...config,
  });
}
