---
agent: agent
---

Analyze my staged git changes and commit them with an appropriate message following Conventional Commits specification v1.0.0 (https://www.conventionalcommits.org/en/v1.0.0/).

**Task:**

1. First run `git status` using `run_in_terminal` to refresh git state
2. Use `get_changed_files` with `sourceControlState: ['staged']` to retrieve all staged changes
3. If no staged changes are found with `get_changed_files`, fall back to using `run_in_terminal` with `git diff --cached` to get staged changes
4. Review the diffs to understand what was changed and why
5. Generate a conventional commit message following this structure:

   ```
   <type>[optional scope][optional !]: <description>

   [optional body]

   [optional footer(s)]
   ```

   **Type Rules (per Conventional Commits):**
   - **MUST** be one of: `feat`, `fix`, `build`, `chore`, `ci`, `docs`, `style`, `refactor`, `perf`, `test`
   - `feat`: A new feature (correlates with MINOR in SemVer)
   - `fix`: A bug fix (correlates with PATCH in SemVer)
   - `!` after type/scope: Indicates a BREAKING CHANGE (correlates with MAJOR in SemVer)

   **Description Rules:**
   - **MUST** be lowercase
   - **MUST** be a short summary in imperative mood (e.g., "add" not "added" or "adds")
   - **MUST NOT** end with a period
   - **SHOULD** be 50 characters or less

   **Body Rules (optional but recommended for complex changes):**
   - **MUST** begin one blank line after description
   - **SHOULD** explain the motivation for the change and contrast with previous behavior
   - **SHOULD** wrap at 72 characters

   **Footer Rules (optional):**
   - **MUST** begin one blank line after body (or description if no body)
   - **MUST** use format: `<token>: <value>` or `<token> #<issue-number>`
   - `BREAKING CHANGE:` footer **MUST** be used for breaking changes (alternative to `!`)
   - Use `Closes`, `Fixes`, `Resolves` for issue references (e.g., `Fixes #123`)

   **Examples:**

   ```
   feat(api): add user authentication endpoint

   fix: resolve memory leak in data processing

   feat!: redesign configuration API

   BREAKING CHANGE: config structure now uses nested objects

   docs: update installation instructions

   refactor(core): simplify error handling logic

   This refactoring improves code readability and reduces
   duplicate error handling code across modules.

   Fixes #456
   ```

6. Commit the staged changes using `run_in_terminal` with appropriate git commit command
