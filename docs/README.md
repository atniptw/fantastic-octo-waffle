# Project Documentation

Complete documentation for the REPO cosmetic viewer project. Start with [Architecture](./architecture.md) for an overview, then dive into specific topics below.

## Core Documentation

- **[Architecture Overview](./architecture.md)** — System design, data flow, user flow, component diagram, caching, security.
- **[Development Setup Guide](./dev-guide.md)** — Local environment, running servers, testing, debugging, deploying.

## Technical Specifications

- **[Cloudflare Worker API Spec](./specs/worker-api.md)** — Endpoint definitions, request/response formats, error handling, CORS, rate limiting.
- **[Unity AssetBundle Parser Pipeline](./specs/parser-pipeline.md)** — Binary format, parsing steps, object deserialization, three.js mapping, performance, limitations.

## Testing & Quality

- **[Testing Strategy](./testing.md)** — Unit/integration/E2E approaches, fixtures, CI/CD, running tests locally.
- **[Test Fixtures](./FIXTURES.md)** — Managing test bundles, golden files, adding new fixtures.

## Planning & Decisions

- **[Roadmap](./roadmap.md)** — Five-phase delivery plan, acceptance criteria, effort estimates, timeline.
- **[Architecture Decision Records (ADR)](./adr/)** — Key decisions:
  - [ADR 0001: Browser-Only Parsing](./adr/0001-browser-only-parsing.md)
  - [ADR 0002: Preact + Signals](./adr/0002-preact-signals.md)

## Quick Links

| Task | Document |
|------|-----------|
| Get started locally | [dev-guide.md](./dev-guide.md) |
| Understand how it works | [architecture.md](./architecture.md) |
| Build the Worker | [specs/worker-api.md](./specs/worker-api.md) |
| Implement the parser | [specs/parser-pipeline.md](./specs/parser-pipeline.md) |
| Write tests | [testing.md](./testing.md) |
| Plan the project | [roadmap.md](./roadmap.md) |
| Understand a decision | [adr/](./adr/) |

## Contributing

Before making changes:
1. Review the [Roadmap](./roadmap.md) to see what's in scope.
2. Check [ADRs](./adr/) for context on key decisions.
3. Read [dev-guide.md](./dev-guide.md#contributing) for coding standards.
4. Ensure all tests pass before opening a PR.

