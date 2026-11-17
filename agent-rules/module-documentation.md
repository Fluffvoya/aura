# Module Documentation

This file is part of the repository instructions referenced by the root
`AGENTS.md`.

- All modules are used only within this project and must be peers directly
  under `modules/`. Do not nest one project module inside another.
- Module documentation must describe only interfaces intended for callers
  outside the module within this project. A symbol being accessible in the
  language does not by itself make it a supported interface for callers.
- Document public entry points, types, methods, parameters, configuration,
  return values, errors, and resource lifecycle behavior as needed by callers.
- Do not include installation instructions in documentation or describe using
  modules in other projects.
- Keep usage examples focused on consuming the public interfaces within this
  project. Examples must use supported entry points.
- Do not document private helpers, internal types, algorithms, source layouts,
  implementation call chains, or other implementation details.
- Keep contributor setup, build and test commands, and test implementation
  details in repository-level contributor documentation rather than module
  documentation.
- Keep module documentation aligned with the current public interfaces when
  changing them; remove descriptions of interfaces that are no longer exposed.
