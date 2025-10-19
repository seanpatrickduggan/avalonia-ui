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
- Standardize on BCL TimeProvider + IFileSystem abstraction.
- Add a debug-only guard to detect duplicate sink instances; enable Serilog SelfLog in DEBUG builds.

## Upcoming Tasks

2) Documentation Pass
- Add XML docs across Core/Infra/UI; start with workspace abstractions and Infra DB/logging classes.

3) Tests and Performance
- Unit tests for `SqliteWorkspaceDb` queries and materialization.
- Integration probe: emit one event after init, assert one row in `log_entries` (verifies single sink, no duplicates). Note: a runtime debug-only guard is already in place and SelfLog is enabled in DEBUG to surface duplicate sink issues during development.
- Validate indexes and query plans at 100k+ logs; tune PageSize/add indexes as needed.

4) Telemetry & Health (lightweight)
- Record init metrics (duration, error) and optional sink health.

5) Packaging
- Prepare distribution via Nix/Docker; document runtime dependencies.

6) UI Enhancements
- Run history and quick-open log viewers; polish visuals and accessibility.

7) Console/CLI Host (future)
- Create a console-based host that uses IApplicationHost to initialize, run a sample operation, and shut down cleanly.
- Validates host-agnostic lifecycle and shared DI composition outside the UI.

## Future Ideas
- Move Serilog adapter entirely out of Core; consider separate Infra.Logging package.
- Add integration tests for UI flows.
