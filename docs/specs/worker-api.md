# Cloudflare Worker API Specification

## Purpose

The Cloudflare Worker acts as a **CORS-safe proxy** for the Thunderstore API and mod download endpoints. It handles:
- Listing mods by community (with search, pagination, sorting).
- Fetching mod version metadata and download URLs.
- Proxying zip file downloads with range-request and size-limit support.
- Enforcing allowlists and rate limits to prevent abuse.

**The Worker is NOT an application server:** no business logic, no mod parsing, no data storage.

## Base URL

- **Production:** `https://<worker-domain>.workers.dev`
- **Local (dev):** `http://localhost:8787` (via Miniflare)

All endpoints are HTTPS in production.

## Endpoints

### 1. List Mods

```
GET /api/mods
  ?community=repo
  &query=cosmetic%20head      (optional; search mod name/author)
  &page=1                      (optional; default 1)
  &sort=downloads              (optional; 'downloads', 'newest', 'rating')
```

**Purpose:** Fetch paginated list of mods for a given Thunderstore community.

**Proxy to:** `https://new.thunderstore.io/api/cyberstacks/repo/packages/?query=...&page=...&ordering=...`

**Response (200 OK):**
```json
{
  "count": 42,
  "next": "https://new.thunderstore.io/api/cyberstacks/repo/packages/?page=2&...",
  "previous": null,
  "results": [
    {
      "namespace": "Author",
      "name": "CoolHeadMod",
      "full_name": "Author/CoolHeadMod",
      "owner": "Author",
      "package_id": 12345,
      "is_deprecated": false,
      "total_downloads": 5000,
      "icon": "https://cdn.thunderstore.io/...",
      "versions": [
        {
          "number": "1.2.3",
          "downloads": 500,
          "timestamp": "2024-01-10T12:00:00Z",
          "file_name": "Author-CoolHeadMod-1.2.3.zip",
          "download_url": "https://cdn.thunderstore.io/file/Author-CoolHeadMod-1.2.3.zip"
        }
      ]
    }
  ]
}
```

**Error (400):** Invalid community or query params.
**Error (429):** Rate limit exceeded.
**Error (5xx):** Thunderstore API unavailable.

**Caching:** `Cache-Control: public, max-age=300` (5 minutes).

---

### 2. Get Mod Versions

```
GET /api/mod/:namespace/:name/versions
```

**Purpose:** Fetch all versions and metadata for a specific mod.

**Proxy to:** `https://new.thunderstore.io/api/cyberstacks/repo/packages/:namespace/:name/`

**Response (200 OK):**
```json
{
  "namespace": "Author",
  "name": "CoolHeadMod",
  "full_name": "Author/CoolHeadMod",
  "description": "Adds cool heads...",
  "wiki_url": "https://github.com/...",
  "versions": [
    {
      "number": "1.2.3",
      "downloads": 500,
      "timestamp": "2024-01-10T12:00:00Z",
      "file_name": "Author-CoolHeadMod-1.2.3.zip",
      "download_url": "https://cdn.thunderstore.io/file/Author-CoolHeadMod-1.2.3.zip",
      "file_size": 5000000
    }
  ]
}
```

**Caching:** `Cache-Control: public, max-age=300`.

---

### 3. Proxy Download

```
GET /proxy?url=https://cdn.thunderstore.io/file/Author-Mod-1.0.0.zip
  &Range=bytes=0-1023         (optional; for resuming large downloads)
```

**Purpose:** CORS-safe proxy for downloading mod zip files. Validates URL against allowlist and enforces size limits.

**Allowlist:** Only these hosts are proxied:
- `cdn.thunderstore.io` (primary CDN)
- `thunderstore.io` (fallback)
- Other known CDN hosts (e.g., Fastly, Cloudflare origin; to be configured per region).

**Size limit:** 200 MB per request. Return `413 Payload Too Large` if exceeded.

**Validation:**
- URL must use HTTPS.
- URL must match allowlist regex.
- No redirects outside allowlist are followed.

**Response (200 OK / 206 Partial Content):**
```
Content-Type: application/zip
Content-Length: 5000000
Content-Disposition: attachment; filename="Author-Mod-1.0.0.zip"
Accept-Ranges: bytes
Cache-Control: public, immutable, max-age=31536000   (1 year; immutable once versioned)
```

**Streaming:** Response body is streamed; do not load entire file into memory on Worker.

**Error (400):** URL not HTTPS, not on allowlist, or invalid.
**Error (413):** File exceeds 200 MB.
**Error (5xx):** CDN unavailable.

---

## Headers and CORS

**All responses include:**
```
Access-Control-Allow-Origin: *
Access-Control-Allow-Methods: GET, OPTIONS
Access-Control-Allow-Headers: Content-Type, Range
Access-Control-Max-Age: 3600
Vary: Origin
```

**Security headers:**
```
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
Referrer-Policy: no-referrer
```

**Caching:**
- API endpoints: `Cache-Control: public, max-age=300, stale-while-revalidate=600`
- Download proxies: `Cache-Control: public, immutable, max-age=31536000`

---

## Rate Limiting

**Per IP address:**
- **API endpoints:** 100 requests per minute.
- **Download proxies:** 20 requests per minute (to prevent abuse).

**Behavior on limit exceeded:**
- Return `429 Too Many Requests`.
- Include `Retry-After: 60` header.
- Log event for monitoring.

**Implementation:** Token bucket algorithm via Cloudflare Workers KV or Durable Objects (depends on scale).

---

## Error Responses

**Standard error format:**
```json
{
  "error": "invalid_community",
  "message": "Community 'foo' not found. Valid: repo, v2, etc.",
  "status": 400
}
```

**Status codes:**
- `200`: Success.
- `206`: Partial content (Range request).
- `400`: Bad request (invalid params, unsupported host).
- `404`: Mod/version not found (proxied from Thunderstore).
- `413`: Payload too large.
- `429`: Rate limit exceeded.
- `500`: Worker internal error.
- `502/503/504`: Thunderstore or CDN unavailable.

---

## Request/Response Examples

### Example: Search for "head" mods in REPO community

**Request:**
```
GET /api/mods?community=repo&query=head&page=1&sort=downloads
```

**Response:**
```json
{
  "count": 15,
  "next": "https://worker.dev/api/mods?community=repo&query=head&page=2",
  "results": [
    {
      "namespace": "Masaicker",
      "name": "MoreHead",
      "full_name": "Masaicker/MoreHead",
      "total_downloads": 10000,
      "versions": [
        {
          "number": "1.5.0",
          "downloads": 5000,
          "download_url": "https://cdn.thunderstore.io/file/Masaicker-MoreHead-1.5.0.zip"
        }
      ]
    }
  ]
}
```

### Example: Download a mod with resumable range

**Request 1:**
```
GET /proxy?url=https://cdn.thunderstore.io/file/Author-Mod-1.0.0.zip
Range: bytes=0-999999
```

**Response 1:**
```
206 Partial Content
Content-Length: 1000000
Content-Range: bytes 0-999999/5000000
```

**Request 2 (resume):**
```
GET /proxy?url=https://cdn.thunderstore.io/file/Author-Mod-1.0.0.zip
Range: bytes=1000000-
```

**Response 2:**
```
206 Partial Content
Content-Length: 4000000
Content-Range: bytes 1000000-4999999/5000000
```

---

## Deployment & Configuration

**Environment variables (wrangler.toml):**
```toml
[env.production]
vars = { COMMUNITY = "repo", MAX_FILE_SIZE_MB = 200 }

[env.staging]
vars = { COMMUNITY = "repo", MAX_FILE_SIZE_MB = 500 }
```

**Allowed hosts (configurable per region):**
```toml
ALLOWED_HOSTS = ["cdn.thunderstore.io", "thunderstore.io", "fastly-cdn.example.com"]
```

---

## Testing Acceptance Criteria

- [ ] `/api/mods` proxies list endpoint and normalizes JSON.
- [ ] `/api/mod/:namespace/:name/versions` returns version list.
- [ ] `/proxy?url=...` validates URL against allowlist; rejects disallowed hosts.
- [ ] `/proxy?url=...` enforces 200 MB size limit; returns 413 if exceeded.
- [ ] Range requests work; `Content-Range` header is correct.
- [ ] All responses include CORS headers.
- [ ] Rate limiting (100/min API, 20/min download) works.
- [ ] Error responses follow standard format with correct status codes.
- [ ] Miniflare local dev environment runs without errors.
- [ ] Deploy to Cloudflare with `wrangler deploy` succeeds.


