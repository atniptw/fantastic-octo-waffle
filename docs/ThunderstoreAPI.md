# Thunderstore API (R.E.P.O.)

 

## Overview
Thunderstore endpoints and categories for R.E.P.O., plus constraints and usage patterns. v1 is primary via Worker; experimental used when accessible.

## Community
- Name: R.E.P.O.
- Identifier: repo
- Community page: https://new.thunderstore.io/c/repo/
- API base (v1): https://thunderstore.io/c/repo/api/v1/

## Categories (16)
Cosmetics, Valuables, Items, Weapons, Levels, Monsters, Drones, Upgrades, Audio, Server-side, Client-side, Misc, Libraries, Tools, Modpacks, Mods

## Endpoints
- List packages (v1): GET /c/repo/api/v1/package/
- Read package (v1): GET /c/repo/api/v1/package/{uuid4}
- Frontend packages (experimental): GET /api/experimental/frontend/c/repo/packages/?page=1&page_size=20
- Categories (experimental): GET /api/experimental/community/repo/category/
- Version readme (experimental): GET /api/experimental/package/{ns}/{name}/{version}/readme
- Version changelog (experimental): GET /api/experimental/package/{ns}/{name}/{version}/changelog
- Package index (experimental): GET /api/experimental/package-index

## Notes
- v1 has no CORS; use Worker proxy
- Experimental may be Cloudflare-protected; fallback to v1 + client filtering
- `download_url` redirects to CDN; HEAD first to get `Content-Length`

## Example Requests
- v1 list: GET https://thunderstore.io/c/repo/api/v1/package/
- Categories: GET https://thunderstore.io/api/experimental/community/repo/category/
- Readme: GET https://thunderstore.io/api/experimental/package/{ns}/{name}/{version}/readme

## Sample v1 Response (truncated)
```json
[
	{
		"name": "examplemod",
		"full_name": "author-examplemod",
		"owner": "author",
		"package_url": "https://thunderstore.io/package/author/examplemod/",
		"icon": "https://thunderstore.io/assets/example.png",
		"rating_score": 4.8,
		"is_deprecated": false,
		"has_nsfw_content": false,
		"date_updated": "2024-12-20T12:34:56.000000Z",
		"categories": ["Cosmetics"],
		"versions": [
			{
				"name": "1.0.0",
				"full_name": "author-examplemod-1.0.0",
				"version_number": "1.0.0",
				"download_url": "https://thunderstore.io/package/download/author/examplemod/1.0.0/",
				"dependencies": ["author-dep-2.0.0"]
			}
		]
	}
]
```

## Data Points Needed
name, owner, categories, icon, description, rating_score, is_deprecated, has_nsfw_content, date_updated, version downloads/file_size/dependencies

## Example (MoreHead)
See `PROJECT_NOTES.md` for inline sample or query v1 endpoint.