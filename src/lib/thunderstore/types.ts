/**
 * TypeScript type definitions for Thunderstore API
 * Based on https://thunderstore.io/api/docs/
 */

// Base types
export interface Community {
  identifier: string;
  name: string;
  discord_url?: string | null;
  wiki_url?: string | null;
  require_package_listing_approval: boolean;
}

export interface CyberstormCommunity extends Community {
  short_description?: string | null;
  description?: string | null;
  datetime_created: string;
  background_image_url?: string | null;
  hero_image_url?: string | null;
  cover_image_url?: string | null;
  icon_url?: string | null;
  community_icon_url?: string | null;
  total_download_count: number;
  total_package_count: number;
  has_mod_manager_support: boolean;
  is_listed: boolean;
}

export interface PackageCategory {
  name: string;
  slug: string;
}

export interface PackageDependency {
  community_identifier?: string | null;
  community_name?: string | null;
  description: string;
  image_src?: string | null;
  namespace: string;
  package_name: string;
  version_number: string;
}

export interface PackageVersion {
  date_created: string;
  download_count: number;
  download_url: string;
  install_url: string;
  version_number: string;
}

export interface PackageVersionExperimental {
  namespace: string;
  name: string;
  version_number: string;
  full_name: string;
  description: string;
  icon: string;
  dependencies: string;
  download_url: string;
  downloads: number;
  date_created: string;
  website_url: string;
  is_active: boolean;
}

export interface PackageListingExperimental {
  has_nsfw_content: boolean;
  categories: string;
  community: string;
  review_status: 'unreviewed' | 'approved' | 'rejected';
}

export interface PackageExperimental {
  namespace: string;
  name: string;
  full_name: string;
  owner: string;
  package_url: string;
  date_created: string;
  date_updated: string;
  rating_score: string;
  is_pinned: boolean;
  is_deprecated: boolean;
  total_downloads: string;
  latest: PackageVersionExperimental;
  community_listings: PackageListingExperimental[];
}

export interface PackageListing {
  name: string;
  full_name: string;
  owner: string;
  package_url: string;
  donation_link?: string;
  date_created: string;
  date_updated: string;
  uuid4: string;
  rating_score: string;
  is_pinned: boolean;
  is_deprecated: boolean;
  has_nsfw_content: boolean;
  categories: string;
}

export interface PackageIndexEntry {
  namespace: string;
  name: string;
  version_number: string;
  file_format: string;
  file_size: number;
  dependencies: string;
}

export interface PackageMetrics {
  downloads: number;
  rating_score: number;
  latest_version: string;
}

export interface PackageVersionMetrics {
  downloads: number;
}

// Pagination types
export interface PaginatedResponse<T> {
  next: string | null;
  previous: string | null;
  results: T[];
}

export interface CursorPaginatedResponse<T> {
  results: T[];
  cursor: string;
  has_more: boolean;
}

// Wiki types
export interface WikiPageIndex {
  id: string;
  title: string;
  slug: string;
  datetime_created: string;
  datetime_updated: string;
}

export interface Wiki {
  id: string;
  title: string;
  slug: string;
  datetime_created: string;
  datetime_updated: string;
  pages: WikiPageIndex[];
}

export interface WikiPage extends WikiPageIndex {
  markdown_content: string;
}

export interface PackageWiki {
  namespace: string;
  name: string;
  wiki: Wiki;
}

// Request/Response types
export interface MarkdownResponse {
  markdown: string | null;
}

export interface RenderMarkdownParams {
  markdown: string;
}

export interface RenderMarkdownResponse {
  html: string;
}

// User types
export interface UserProfile {
  username: string;
  capabilities: string[];
  connections: SocialAuthConnection[];
  subscription: SubscriptionStatus;
  rated_packages: string[];
  teams: string[];
  teams_full: UserTeam[];
  is_staff: boolean;
}

export interface SocialAuthConnection {
  provider: string;
  username: string;
  avatar: string;
}

export interface SubscriptionStatus {
  expires: string;
}

export interface UserTeam {
  name: string;
  role: string;
  member_count: number;
}

// Search & Filter options
export interface PackageListOptions {
  cursor?: string;
}

export interface CommunityPackageListOptions {
  cursor?: string;
}
