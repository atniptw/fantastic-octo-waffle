# Contributing

## Development mode

- Preferred: VS Code devcontainer workflow.
- Optional: host machine setup with .NET SDK 10.x, Node 24.x, npm, and Playwright dependencies.

## Definition of done

Before opening a pull request:

1. Run `npm run verify`.
2. Confirm local app smoke test at `http://localhost:5075`.
3. Ensure changes include tests when behavior changes.
4. Keep commits focused and descriptive.

## Pull request requirements

- Describe what changed and why.
- Include verification evidence (commands run and outcomes).
- Call out risks, follow-ups, or known limitations.

## Coding standards

- Follow `.editorconfig` and analyzer rules.
- Keep warnings clean for changed code.
- Use deterministic scripts from `package.json` instead of ad-hoc commands.
