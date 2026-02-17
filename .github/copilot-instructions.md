# Copilot instructions

## Project overview
- Blazor WebAssembly app for previewing and assembling R.E.P.O. cosmetic mods in-browser.
- UI is component-driven: pages in [src/BlazorApp/Pages](src/BlazorApp/Pages), layouts in [src/BlazorApp/Layout](src/BlazorApp/Layout), shared components in [src/BlazorApp/Components](src/BlazorApp/Components).
- Styling is scoped per component via co-located .razor.css files (for example [src/BlazorApp/Components/ModsWizard.razor](src/BlazorApp/Components/ModsWizard.razor) + [src/BlazorApp/Components/ModsWizard.razor.css](src/BlazorApp/Components/ModsWizard.razor.css)).

## Key flows and data
- Home route uses the planner layout: [src/BlazorApp/Pages/Home.razor](src/BlazorApp/Pages/Home.razor) + [src/BlazorApp/Layout/PlannerLayout.razor](src/BlazorApp/Layout/PlannerLayout.razor).
- The Load Mods modal is the central UX entry point, driven by `ModalStateService` (open/close + change event) and the `ModsWizard` component. See [src/BlazorApp/Services/ModalStateService.cs](src/BlazorApp/Services/ModalStateService.cs) and [src/BlazorApp/Components/ModsWizard.razor](src/BlazorApp/Components/ModsWizard.razor).
- Mod scanning is entirely client-side: `DecorationIndexService` reads the uploaded zip, filters `.hhh` entries, computes SHA-256, and groups by inferred body part tags. See [src/BlazorApp/Services/DecorationIndexService.cs](src/BlazorApp/Services/DecorationIndexService.cs).
- Display names are normalized by stripping trailing body-part tags (for example `_head`, `-leftarm`). See [src/BlazorApp/Services/DecorationNameFormatter.cs](src/BlazorApp/Services/DecorationNameFormatter.cs).
- Services are registered in the WASM host at startup. See [src/BlazorApp/Program.cs](src/BlazorApp/Program.cs).

## UI/testing conventions
- Use `data-testid` for Playwright selectors (examples in `ModsWizard`). See [src/BlazorApp/Components/ModsWizard.razor](src/BlazorApp/Components/ModsWizard.razor).
- Playwright base URL defaults to `http://localhost:5075` and can be overridden via `PLAYWRIGHT_BASE_URL`. See [playwright.config.ts](playwright.config.ts).
- E2E tests live in [tests/e2e](tests/e2e) and use fixtures in [tests/e2e/fixtures](tests/e2e/fixtures).

## Build/run/test workflow
- Run the app: `dotnet run` from [src/BlazorApp](src/BlazorApp).
- Build: `dotnet build src/BlazorApp/BlazorApp.csproj`.
- Unit tests: `dotnet test tests/UnityAssetParser.Tests/UnityAssetParser.Tests.csproj`.
- E2E tests: `npm run test:e2e`.
- Required toolchains: .NET 10 LTS and Node 24 LTS (see [README.md](README.md)).

## Agent verification policy (local)
- Prefer autonomous verification before handoff; do not ask the user to manually run commands unless all local fallback steps fail.
- For UI or modal flow changes: run at minimum `npm run test:e2e:landing`; run full `npm run test:e2e` when behavior spans multiple steps/components.
- For parser/service changes: run at minimum `npm run test:unit`; for broad parser refactors, run `npm run verify:parser`.
- For mixed UI + parser changes: run both `npm run test:unit` and `npm run test:e2e`.
- Always include command outputs and pass/fail status in the handoff summary.

## Agent issue triage policy (local)
- Reproduce first using local commands and tests.
- Collect diagnostics via terminal output and test failures.
- Apply the narrowest fix possible.
- Re-run the narrowest relevant test, then a broader check when risk is high.
- Only request manual logs/input after local reproduction and fallback commands are exhausted.

## Parser oracle policy
- Treat UnityPy as a reference implementation for parser troubleshooting.
- When parser behavior is ambiguous, run the UnityPy oracle workflow documented in [docs/parser-oracle-workflow.md](docs/parser-oracle-workflow.md) and compare deterministic outputs.
- Use oracle comparisons to validate entry paths, sizes, hashes, decompression outcomes, and selected serialized metadata.

## Integration points
- App uses Bootstrap 5 from CDN for base styling, plus custom CSS in [src/BlazorApp/wwwroot/css/app.css](src/BlazorApp/wwwroot/css/app.css).
- No backend services are referenced; mod files are processed via `IBrowserFile` streams in-browser.
