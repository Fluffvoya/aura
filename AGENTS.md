# AGENTS.md

Instructions for AI agents and contributors working on this repository.

## 1. Language

- All code comments must be written in English.
- All commit messages must be written in English.
- Branch names, identifiers, and documentation should use English unless there is a strong reason not to.

## 2. Git Rules

- Never write code or commit directly on the `main` branch. All work must be done on a dedicated branch.
- Keep commit granularity appropriate: each commit should contain one logical, self-contained change. Avoid commits that are too large or too fragmented.

### 2.1 Branch Naming

Branch names follow this format:

```
<type>/<branch-name>
```

- `<type>` must be one of the allowed types below.
- `<branch-name>` must be lowercase, kebab-case, and describe the work briefly.
- Keep branch names short and specific, e.g. `feature/add-user-login`.

Allowed branch types:

| Type      | Purpose                                              |
| --------- | ---------------------------------------------------- |
| `feature` | New features and enhancements                        |
| `fix`     | Bug fixes                                            |
| `refactor`| Code restructuring without changing behavior         |
| `docs`    | Documentation-only changes                           |
| `chore`   | Tooling, dependencies, and maintenance               |
| `test`    | Adding or updating tests                             |
| `style`   | Formatting or whitespace changes only                |

Examples:

```
feature/add-user-login
fix/empty-response-handling
refactor/split-config-loader
docs/update-setup-guide
chore/bump-openai-sdk
test/add-parser-tests
style/format-project
```

### 2.2 Commit Messages

Commit messages follow this format:

```
<type>:<message>
```

- `<type>` must be one of the allowed types below.
- `<message>` must be in English, concise, and written in the imperative mood.
- Do not add `Co-authored-by` trailers or any other co-author attribution.

Allowed commit types:

| Type       | Purpose                                                    |
| ---------- | ---------------------------------------------------------- |
| `feat`     | A new feature                                             |
| `fix`      | A bug fix                                                 |
| `docs`     | Documentation-only changes                                |
| `refactor` | Code change that neither fixes a bug nor adds a feature   |
| `perf`     | Performance improvement                                   |
| `test`     | Adding or updating tests                                  |
| `style`    | Formatting or whitespace changes with no logic change     |
| `chore`    | Other maintenance tasks (tooling, dependencies, etc.)     |

Examples:

```
feat:add user login endpoint
fix:handle empty API response
docs:update installation guide
refactor:extract config loader
perf:cache model metadata
test:add parser unit tests
style:format project with ruff
chore:bump openai dependency
```

### 2.3 Development Workflow

1. Sync the latest `main`:

   ```bash
   git checkout main
   git pull origin main
   ```

2. Create a new branch following the naming rules:

   ```bash
   git checkout -b <type>/<branch-name>
   ```

3. Do the work on that branch and commit with the required message format:

   ```bash
   git add .
   git commit -m "<type>:<message>"
   ```

4. Before pushing, fetch the latest `main` and check whether the branch conflicts with it:

   ```bash
   git fetch origin
   git rev-list --left-right --count origin/main...HEAD
   git merge origin/main
   ```

   - If there are conflicts, resolve them on the branch.
   - Make sure the project still builds and tests pass after resolving.

5. Push the branch:

   ```bash
   git push -u origin <type>/<branch-name>
   ```

6. Open a pull request targeting `main`, or merge according to the project's review process.

### 2.4 Checklist Before Pushing

- [ ] No code or commit was made directly on `main`.
- [ ] Commit granularity is appropriate and each commit contains one logical change.
- [ ] The branch name follows `<type>/<branch-name>`.
- [ ] Every commit message follows `<type>:<message>` and is in English.
- [ ] No `Co-authored-by` trailer is present.
- [ ] The branch is up to date with `origin/main`.
- [ ] No unresolved merge conflicts remain.
- [ ] The project builds and tests pass.
