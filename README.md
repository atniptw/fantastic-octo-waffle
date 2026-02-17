# fantastic-octo-waffle

Blazor WebAssembly app for previewing and assembling R.E.P.O. cosmetic mods in the browser.

## Development

- Requires .NET 10 LTS and Node 24 LTS.
- Dev container included for Docker-based setup.

### Run locally

```
cd src/BlazorApp
dotnet run
```

### Run tests

```
dotnet test tests/UnityAssetParser.Tests/UnityAssetParser.Tests.csproj
npm run test:e2e
```

For detailed AI-agent verification and troubleshooting workflows, see:

- [docs/agent-verification-matrix.md](docs/agent-verification-matrix.md)
- [docs/agent-troubleshooting.md](docs/agent-troubleshooting.md)
- [docs/parser-oracle-workflow.md](docs/parser-oracle-workflow.md)

## License

MIT
