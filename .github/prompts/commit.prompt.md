---
agent: agent
---

Analyze my staged git changes and commit them with an appropriate message following Conventional Commits specification and community best practices.

**Task:**

1. Use `get_changed_files` with `sourceControlState: ['staged']` to retrieve all staged changes
2. Review the diffs to understand what was changed and why
3. Generate a conventional commit message following this structure:

   ```
   <type>[optional scope][optional !]: <description>

   [optional body]

   [optional footer(s)]
   ```

4. Commit the staged changes using `run_in_terminal` with appropriate git commit command

**Commit Message Format:**

**Header (Required):**

- Format: `type(scope): subject`
- Types (use most appropriate):
  - `feat`: New feature for the user
  - `fix`: Bug fix for the user
  - `docs`: Documentation only changes
  - `style`: Code style changes (formatting, missing semi-colons, etc; no code logic change)
  - `refactor`: Code change that neither fixes a bug nor adds a feature
  - `perf`: Performance improvement
  - `test`: Adding missing tests or correcting existing tests
  - `build`: Changes to build system or external dependencies
  - `ci`: Changes to CI configuration files and scripts
  - `chore`: Other changes that don't modify src or test files
  - `revert`: Reverts a previous commit
- Scope: The affected app or common module (e.g., `translate`, `lms`, `api-clients`, `database-clients`)
  - Use `monorepo` for changes spanning multiple scopes
  - Scope is optional but recommended for clarity
- Breaking changes: Append `!` after type/scope (e.g., `feat(api)!:`) or add `BREAKING CHANGE:` footer
- Subject line rules:
  - Limit to 50 characters (hard limit: 72)
  - Use imperative mood ("add" not "added", "fix" not "fixed")
  - Should complete: "If applied, this commit will _[your subject]_"
  - Capitalize first letter
  - No period at the end
  - Be specific and descriptive

**Body (Optional but recommended for non-trivial changes):**

- Separate from subject with one blank line
- Wrap at 72 characters
- Explain WHAT and WHY, not HOW (code shows how)
- Include:
  - Motivation for the change
  - Contrast with previous behavior
  - Side effects or consequences
- Use multiple paragraphs if needed
- Bullet points are acceptable (use `-` or `*`)

**Footer(s) (Optional):**

- Separate from body with one blank line
- Use for:
  - Breaking changes: `BREAKING CHANGE: description`
  - Issue references: `Refs: #123`, `Fixes: #456`, `Closes: #789`
  - Other metadata: `Reviewed-by:`, `Acked-by:`, etc.
- Format: `token: value` or `token #value`

**Guidelines:**

- **Atomic commits**: If changes address multiple concerns, suggest splitting into multiple commits
- **Consistency**: Maintain lowercase types and consistent scope names
- **Context**: Provide enough information for future maintainers (including yourself)
- **Footers**: Always reference issue tracking IDs when applicable
- **Breaking changes**: Must be clearly indicated with `!` and/or `BREAKING CHANGE:` footer

**Examples:**

Simple change:

```
docs: correct spelling in README
```

With scope:

```
feat(translate): add support for batch translation requests
```

With body:

```
fix(lms): prevent race condition in checkpoint updates

Introduce a request id and reference to latest request. Dismiss
incoming responses other than from latest request.

Remove timeouts which were used to mitigate the racing issue but are
obsolete now.

Refs: #123
```

Breaking change:

```
feat(api-clients)!: change authentication flow to use OAuth2

BREAKING CHANGE: VaultApiClient now requires OAuth2 credentials instead of API key.
Update your configuration to use VAULT_OAUTH_CLIENT_ID and VAULT_OAUTH_CLIENT_SECRET.

Refs: #456
```

**Success Criteria:**

- Staged changes are committed successfully
- Commit message follows Conventional Commits 1.0.0 specification
- Subject line is clear, concise, and under 50 characters (max 72)
- Body (if present) explains why the change was made
- Breaking changes are clearly indicated
- Issue references are included when applicable
- User can understand the change context without viewing the diff
