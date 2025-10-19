# All Tasks

This document tracks upcoming and planned tasks for the FileProcessor project.

## Current Task Pointer
See `task-current.md` for the single, focused current task in progress.

## Architecture Roadmap

1) Architecture Hardening (Core purity, DI, lifecycle, concurrency)
- Purge Serilog from Core; move adapters to Infrastructure.
- Refactor `WorkspaceSqliteSink` to use DI (no static access).
- Remove service locator usage in ViewModels; use constructor injection.
- Centralize init/shutdown; ensure proper disposal/async disposal.
- Introduce bounded background writer for DB appends; propagate CancellationToken.
- Add simple cross-cutting providers (TimeProvider, FileSystem) for testability.

## Upcoming Tasks

2) Documentation Pass
- Add XML docs across Core/Infra/UI; start with workspace abstractions and Infra DB/logging classes.

3) Tests and Performance
- Unit tests for `SqliteWorkspaceDb` queries and materialization.
- Validate indexes and query plans at 100k+ logs; tune PageSize/add indexes as needed.

4) Telemetry & Health (lightweight)
- Record init metrics (duration, error) and optional sink health.

5) Packaging
- Prepare distribution via Nix/Docker; document runtime dependencies.

6) UI Enhancements
- Run history and quick-open log viewers; polish visuals and accessibility.

## Future Ideas
- Move Serilog adapter entirely out of Core; consider separate Infra.Logging package.
- Add integration tests for UI flows.

## Completed (recent)
- SQLite workspace DB with operations schema and version gate.
- Dual logging (JSONL + SQLite) via custom sink.
- Log viewer with DB/JSONL backends, virtualization, debounce, tailing.
- DI adoption and removal of static `WorkspaceDbService` from UI/Infra.
- Startup health banner and commands.
