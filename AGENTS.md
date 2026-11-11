# AGENTS.md

Instructions for AI agents and contributors working on this repository.

## 1. Language

- All code comments must be written in English.
- All commit messages must be written in English.
- Branch names, identifiers, and documentation should use English unless there is a strong reason not to.

## 2. Naming Conventions

### 2.1 Files and Directories

- Directory names must be lowercase kebab-case: use only lowercase ASCII letters, digits, and hyphens (`-`) as word separators. This rule also applies to directories that contain C# projects.
- Non-source file names must follow the same lowercase kebab-case convention as directory names.
- Do not use periods (`.`) to separate words in file or directory names. A period may only separate a file name from its extension.
- Prefer short, descriptive names. Avoid unnecessary abbreviations.
- Names required by tools or platform conventions, such as `AGENTS.md`, `README.md`, `LICENSE`, `.gitignore`, and `.editorconfig`, are exempt. Do not change conventional names merely to satisfy this rule.
- Generated files and directories must follow the naming rules of their generator and are exempt from manual renaming.

Examples:

```
src/user-service/
docs/getting-started.md
configs/log-level.json
services/order-api/
```

Forbidden examples:

```
src/UserService/
src/user_service/
docs/GettingStarted.v2.md
docs/getting.started.md
```

### 2.2 Source Files

- C# source files must use PascalCase without word separators, for example `UserService.cs` and `OrderRepository.cs`.
- Source files in all other languages must use snake_case, for example `user_service.py`, `order_repository.ts`, and `request_handler.go`.
- File extensions must be retained and must not be treated as part of the identifier.
- Do not mix naming conventions within the same language's source files.
- Generated source files are exempt from manual renaming and must follow the generator's conventions.

### 2.3 C# Source Code

Identifiers inside C# source files must follow the official Microsoft C# naming conventions rather than the file naming rules:

- Types, methods, properties, events, namespaces, and public constants use PascalCase.
- Interfaces use PascalCase with an `I` prefix.
- Local variables and method parameters use camelCase.
- Private fields use camelCase, with an underscore prefix when consistent with the existing project style.
- Acronyms are treated as words when Pascal-cased, for example `HttpClient` instead of `HTTPClient`.
- Asynchronous methods use the `Async` suffix when they return a task-like type.

The existing project style takes precedence where Microsoft's guidelines allow multiple valid choices.

## 3. Git Rules

- Never write code or commit directly on the `main` branch. All work must be done on a dedicated branch.
- Keep commit granularity appropriate: each commit should contain one logical, self-contained change. Avoid commits that are too large or too fragmented.

### 3.1 Branch Naming

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

### 3.2 Commit Messages

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

### 3.3 Development Workflow

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

### 3.4 Checklist Before Pushing

- [ ] No code or commit was made directly on `main`.
- [ ] Commit granularity is appropriate and each commit contains one logical change.
- [ ] The branch name follows `<type>/<branch-name>`.
- [ ] Every commit message follows `<type>:<message>` and is in English.
- [ ] No `Co-authored-by` trailer is present.
- [ ] The branch is up to date with `origin/main`.
- [ ] No unresolved merge conflicts remain.
- [ ] The project builds and tests pass.
