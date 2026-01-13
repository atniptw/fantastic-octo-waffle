# Changelog

All notable changes to the Worker package will be documented in this file.

## [Unreleased]

### Changed

- **Wrangler upgrade from 3.x to 4.59.0**
  - **Reason**: Align with current Cloudflare Workers tooling and APIs. Wrangler 4.x provides improved type safety, better local development experience, and compatibility with the latest Workers runtime features.
  - **Compatibility**: No breaking changes required. The existing wrangler.toml configuration, dev/build/deploy scripts, and Worker routes are fully compatible with wrangler 4.x. All commands (wrangler dev, wrangler deploy) have been verified to work without modification.
