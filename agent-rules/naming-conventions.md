# Naming Conventions

This file is part of the repository instructions referenced by the root
`AGENTS.md`.

## 1. Files and Directories

- Directory names must be lowercase kebab-case: use only lowercase ASCII
  letters, digits, and hyphens (`-`) as word separators. This rule also applies
  to directories that contain C# projects.
- Non-source file names must follow the same lowercase kebab-case convention as
  directory names.
- Do not use periods (`.`) to separate words in file or directory names. A
  period may only separate a file name from its extension.
- Prefer short, descriptive names. Avoid unnecessary abbreviations.
- Names required by tools or platform conventions, such as `AGENTS.md`,
  `README.md`, `LICENSE`, `.gitignore`, and `.editorconfig`, are exempt. Do not
  change conventional names merely to satisfy this rule.
- Generated files and directories must follow the naming rules of their
  generator and are exempt from manual renaming.

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

## 2. Source Files

- C# source files must use PascalCase without word separators, for example
  `UserService.cs` and `OrderRepository.cs`.
- Source files in all other languages must use snake_case, for example
  `user_service.py`, `order_repository.ts`, and `request_handler.go`.
- File extensions must be retained and must not be treated as part of the
  identifier.
- Do not mix naming conventions within the same language's source files.
- Generated source files are exempt from manual renaming and must follow the
  generator's conventions.

## 3. C# Source Code

Identifiers inside C# source files must follow the official Microsoft C# naming
conventions rather than the file naming rules:

- Types, methods, properties, events, namespaces, and public constants use
  PascalCase.
- Interfaces use PascalCase with an `I` prefix.
- Local variables and method parameters use camelCase.
- Private fields use camelCase, with an underscore prefix when consistent with
  the existing project style.
- Acronyms are treated as words when Pascal-cased, for example `HttpClient`
  instead of `HTTPClient`.
- Asynchronous methods use the `Async` suffix when they return a task-like type.

The existing project style takes precedence where Microsoft's guidelines allow
multiple valid choices.
