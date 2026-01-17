# Cloudflare Worker Design

 

## Overview
Worker acts as a proxy for Thunderstore: adds CORS, sets headers, and caches lists/categories in KV. Serves readme/changelog with markdown content types.

## Endpoints
- /api/packages → proxy /c/repo/api/v1/package/
- /api/categories → proxy experimental categories
- /api/package/{ns}/{name}/{ver}/readme → proxy readme
- /api/package/{ns}/{name}/{ver}/changelog → proxy changelog

### ZIP Download (Remote Preview)
- /api/download/{namespace}/{name}/{version}
	- HEAD upstream `download_url` to expose `Content-Length` (size)
	- Stream GET of the ZIP to the browser with `Content-Type: application/zip`
	- CORS headers for site origin; dev may allow `*`
	- Guardrails: upstream host allowlist, optional size cap, reject malformed params

## CORS
Add `Access-Control-Allow-Origin`, methods (GET, OPTIONS), headers.

## Caching
- Package list: KV, TTL 5m, key `repo:package_list`
- Categories: KV, TTL 1h, key `repo:categories`
- Downloads: browser cache (IndexedDB), not Worker

## Headers
`User-Agent: RepoModViewer/0.1 (+https://atniptw.github.io)`; `Accept: application/json`

## API Contracts
- Download Meta (HEAD)
	- Response: `{ "sizeBytes": number, "filename": string }`
	- Status: `200` when known; `204` if unknown size
- Errors
	- JSON: `{ "error": string, "status": number }`

## Errors & Retries
Handle 403/429 with backoff; serve cached data if possible.

## Worker Skeleton (JS)
Example minimal Worker with proxy, CORS, and KV caching.

```js
export default {
	async fetch(request, env, ctx) {
		const url = new URL(request.url);

		// Preflight
		if (request.method === 'OPTIONS') return cors(new Response(null, { status: 204 }));

		try {
			if (url.pathname === '/api/packages') {
				const cached = await env.CACHE.get('repo:package_list');
				if (cached) return cors(json(cached));
				const r = await fetch('https://thunderstore.io/c/repo/api/v1/package/', { headers: ua() });
				const body = await r.text();
				ctx.waitUntil(env.CACHE.put('repo:package_list', body, { expirationTtl: 300 }));
				return cors(json(body, r.status));
			}

			if (url.pathname === '/api/categories') {
				const r = await fetch('https://thunderstore.io/api/experimental/community/repo/category/', { headers: ua() });
				const body = await r.text();
				ctx.waitUntil(env.CACHE.put('repo:categories', body, { expirationTtl: 3600 }));
				return cors(json(body, r.status));
			}

			const mReadme = url.pathname.match(/^\/api\/package\/(.+?)\/(.+?)\/(.+?)\/readme$/);
			if (mReadme) {
				const [_, ns, name, ver] = mReadme;
				const upstream = `https://thunderstore.io/api/experimental/package/${ns}/${name}/${ver}/readme`;
				const r = await fetch(upstream, { headers: ua() });
				return cors(new Response(await r.text(), { status: r.status, headers: { 'Content-Type': 'text/markdown; charset=utf-8' } }));
			}

			const mChangelog = url.pathname.match(/^\/api\/package\/(.+?)\/(.+?)\/(.+?)\/changelog$/);
			if (mChangelog) {
				const [_, ns, name, ver] = mChangelog;
				const upstream = `https://thunderstore.io/api/experimental/package/${ns}/${name}/${ver}/changelog`;
				const r = await fetch(upstream, { headers: ua() });
				return cors(new Response(await r.text(), { status: r.status, headers: { 'Content-Type': 'text/markdown; charset=utf-8' } }));
			}

			return cors(new Response(JSON.stringify({ error: 'Not Found' }), { status: 404, headers: { 'Content-Type': 'application/json' } }));
		} catch (err) {
			return cors(new Response(JSON.stringify({ error: 'Upstream error' }), { status: 502, headers: { 'Content-Type': 'application/json' } }));
		}
	}
};

function ua() {
	return { 'User-Agent': 'RepoModViewer/0.1 (+https://atniptw.github.io)', 'Accept': 'application/json' };
}

function json(body, status = 200) {
	return new Response(body, { status, headers: { 'Content-Type': 'application/json; charset=utf-8' } });
}

function cors(resp) {
	const h = new Headers(resp.headers);
	h.set('Access-Control-Allow-Origin', '*');
	h.set('Access-Control-Allow-Methods', 'GET, OPTIONS');
	h.set('Access-Control-Allow-Headers', 'Content-Type');
	return new Response(resp.body, { status: resp.status, headers: h });
}
```

## Design Decisions
- CORS: Dev allows `*`; Prod locks to site origin (e.g., `https://www.yourdomain.com`). Methods `GET, OPTIONS`; header `Content-Type`.
- Headers: Upstream requests include `User-Agent` and `Accept: application/json`.
- Caching: MVP has no KV; Phase 2 adds KV keys (`repo:package_list`, `repo:categories`) with TTLs (packages 5m, categories 1h) and stale-on-error.
- Errors: Pass through upstream status with concise JSON `{ error, status }`; add backoff later if throttling appears.
- Security: Validate route params; only proxy to `thunderstore.io` hostnames; never proxy ZIP downloads.
- Observability: Use Workers Analytics; add Logpush later if needed; do not log PII.
- Constraints: Experimental endpoints may be Cloudflare-protected; fallback to v1 list + client filtering.

## Routing (TBD)
- Dev: Use `*.workers.dev`.
- Prod: Choose later between `api.yourdomain.com/*` or `yourdomain.com/api/*` after domain purchase.
- CORS origin updated accordingly when the site domain is finalized.

## Acceptance Criteria (MVP)
- Returns JSON for packages and categories with working CORS in dev and prod.
- Does not proxy asset downloads; browser fetches ZIP/CDN directly.
- Minimal config change to add KV caching and readme/changelog in a future phase without breaking API consumers.