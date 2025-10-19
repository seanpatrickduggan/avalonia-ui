# Completed Work (Architecture Hardening)

## Summary
We eliminated architectural smells and established a clean, testable foundation:
- Core is framework-agnostic; Serilog removed from Core and adapters live in Infrastructure.
- Single Serilog pipeline with one WorkspaceSqliteSink wired via DI-driven ILogWriteTarget.
- Instance-based WorkspaceRuntime owns the SQLite DB and a bounded Channel<LogWrite> writer; exposes AppendOrBufferAsync and FlushAsync.
- Lifecycle centralized and host-agnostic via IApplicationHost; UI delegates initialization and shutdown.
- ViewModels use constructor injection; DataContexts are provided via DI; added a DI-backed window factory.
- Robust workspace DB lifecycle: first-run schema creation; schema mismatch rebuild with notice; WAL tuning; UI health surfaced.
- Diagnostics: DEBUG-only duplicate-sink guard and Serilog SelfLog enabled in DEBUG builds.
- Logging topology: single global pipeline; per-session JSONL + DB mirror; per-operation file-only logger.

## Details
- Purged Serilog from Core. IOperationContext.Initialize(string operationId, string logFilePath) takes primitives only.
- WorkspaceSqliteSink consumes ILogWriteTarget (no static access).
- Introduced WorkspaceRuntime (IWorkspaceRuntime, ILogAppender) with bounded writer (SingleReader, DropOldest); pending writes flushed on init/shutdown.
- Removed static WorkspaceDbService and related adapters.
- OperationContextService reuses global pipeline for DB mirroring and adds a per-operation file-only logger; no duplicate sinks.
- App configured a single Serilog pipeline; SelfLog enabled in DEBUG; window factory added; UILoggingService updated to use it.
- Added a DEBUG-only guard to detect duplicate sink instances.
- Introduced IApplicationHost (Core) and ApplicationHost (Infra); App now calls host.InitializeAsync and host.ShutdownAsync.

## Evidence
- Builds green across Core/Infrastructure/UI.
- Fresh workspace initializes with schema; existing older DBs are rebuilt with notice; modern DBs show no warnings.
- UI displays clear initialization health and allows retry.
- Multiple log viewer windows operate independently with DataContexts from DI.
- Shutdown order enforced: end operation → materialize logs → host flush/shutdown → Serilog CloseAndFlush.

## Completed (recent)
- SQLite workspace DB with operations schema and version gate.
- Dual logging (JSONL + SQLite) via custom sink.
- Log viewer with DB/JSONL backends, virtualization, debounce, tailing.
- DI adoption and removal of static `WorkspaceDbService` from UI/Infra.
- Startup health banner and commands.
- Instance-based WorkspaceRuntime with bounded channel writer; single sink pipeline.
