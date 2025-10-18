# Running Tasks

This document tracks ongoing and planned tasks for the FileProcessor project.

## Current Tasks

### 1. Documentation Pass
- **README.md**: Update with comprehensive project overview, setup instructions, architecture diagram, and usage examples.
- **Class/Function Docs**: Add XML documentation comments to all public classes, methods, and properties across Core, Infrastructure, and UI projects. Ensure consistency and completeness.

### 2. Log Viewer Support for Database or JSONL Input
- **Database Querying**: Implement LogViewer.UI to query logs from SQLite workspace DB instead of parsing JSONL files directly.
  - Add background filtering/debouncing for performance.
  - Implement UI virtualization for large log sets.
  - Support switching between DB and JSONL sources (e.g., for offline viewing).
- **Fallback to JSONL**: Ensure LogViewer can still load and display JSONL files when DB is unavailable or for portability.
- **Integration**: Wire into main UI for opening log viewers from workspace runs/sessions.

### 3. Re-Audit Architecture
- **Clean Architecture Review**: Verify adherence to separation of concerns (UI, Core, Infrastructure).
- **Dependency Injection**: Evaluate adding DI container (e.g., Microsoft.Extensions.DependencyInjection) for better testability and service management.
- **Performance**: Audit DB queries, logging overhead, and UI responsiveness.
- **Scalability**: Assess for larger workspaces (e.g., 100k+ logs) and potential optimizations.
- **Security**: Review data handling, especially in workspace DB and logging.

## Completed Tasks

- **SQLite Workspace DB Implementation**: Schema, services, and logging integration.
- **Dual Logging**: Serilog to JSONL + SQLite mirroring via custom sink.
- **Materialization**: Export logs to JSONL at run/session end for portability.
- **Startup Fixes**: Async DB init, buffering pre-init logs.
- **Build Fixes**: Resolved compilation errors in logging sink.

## Backlog / Future Tasks

- **UI Enhancements**: Improve main window with workspace management, run history, and log viewer integration.
- **Testing**: Add unit tests for Core/Infrastructure, integration tests for UI.
- **Packaging**: Setup for distribution (e.g., via Nix, Docker).
- **Performance Monitoring**: Add metrics/logging for DB operations and UI load times.

## Notes

- Prioritize log viewer DB support as it directly addresses the original performance issue with large logs.
- Documentation should be updated incrementally as features are added.
- Re-audit after log viewer completion to ensure architecture scales.
