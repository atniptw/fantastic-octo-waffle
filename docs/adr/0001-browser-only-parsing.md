# ADR 0001: Browser-Only Parsing vs. Server-Side

## Status

Accepted

## Context

Unity AssetBundle parsing is computationally expensive and requires binary deserialization. We could implement this either:

1. In the browser (WebWorker + WASM for decompression)
2. On a server (Cloudflare Worker or Node.js service)

## Decision

**Browser-only parsing** using Web Workers + lightweight WASM modules for compression.

## Rationale

- **Zero server parsing cost:** Worker stays minimal (proxy-only), avoiding scaling issues.
- **User privacy:** Zips never leave the user's machine; no logs of which mods are inspected.
- **Resilience:** Parser failures don't crash the service; users can retry or clear cache.
- **Offline-first:** Once cached, models can be viewed without network (future enhancement).
- **Trade-off accepted:** Parse latency (typically 1–3s) is acceptable for cosmetic preview UX.

## Alternatives Considered

1. **Server-side parsing (Node.js or Rust service):** Would simplify parser, but adds cost, complexity, and privacy concerns.
2. **Hybrid (server cache + browser fallback):** Adds complexity; deferred to Phase 5+ if needed.

## Consequences

- Parser must be robust and handle errors gracefully (user-facing toasts).
- Web Workers required for off-thread parsing; requires careful data serialization.
- WASM modules (LZMA, optionally Crunch) must be bundled and initialized.
- Memory must be carefully managed; large bundles could exceed browser limits.

## Follow-up

- Monitor parse times in telemetry; optimize hot paths (e.g., texture decode, BufferGeometry creation).
- Consider pre-compiled glTF caching per bundle version to skip parse on repeat visits.
