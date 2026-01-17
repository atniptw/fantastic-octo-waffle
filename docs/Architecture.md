# Architecture

 

## Overview
High-level system: Blazor WASM frontend, Cloudflare Worker proxy, Thunderstore API source, Three.js renderer via JS interop.

## System Overview

## Data Flow
User → Blazor WASM → Cloudflare Worker → Thunderstore API
                    ↓
           Download ZIP → Extract → Parse Unity (.hhh) → Export Three.js JSON → Render

## Key Decisions
- Direct port logic from UnityPy for Unity parsing
- v1 API as primary; experimental API when accessible
- Cache package lists (KV), assets (IndexedDB)

## Risks & Mitigations
- Cloudflare blocking: use Worker + headers
- Large responses: cache + client-side filtering
- Parsing complexity: port from Python (no learning-by-doing)