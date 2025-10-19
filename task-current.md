# Current Task: Architecture Hardening (Core purity, DI, lifecycle, concurrency)

## Objective
Eliminate architectural smells and lock in a clean, testable, framework-agnostic foundation: Core remains pure, all services flow through DI, a single owner manages init/shutdown, and concurrency/backpressure are explicit.

## Scope
- Core purity: no Serilog or framework types in Core contracts or implementations.
- DI purity: no statics/service locators on hot paths (UI/Infra); dependencies are injected.
- Lifecycle: single init/shutdown orchestration, with proper disposal semantics.
- Concurrency: explicit background pipeline for log writes with bounded buffering and cancellation.

## Plan
1) Purge Serilog from Core (Core purity) — DONE
   - Serilog types removed from Core contracts. `IOperationContext.Initialize(string operationId, string logFilePath)` now takes primitives only.
   - Serilog-based `WorkspaceOperationStructuredLogger` lives in Infrastructure.
   - UI/Infra construct Serilog loggers; Core remains framework-agnostic.
2) Refactor logging sink DI (DI purity) — DONE (phase 1)
   - `WorkspaceSqliteSink` no longer accesses statics; it depends on injected `ILogWriteTarget`.
   - DI supplies `ILogWriteTarget` via `WorkspaceLogWriteTargetAdapter` (adapts current static runtime). Next phase: remove the static facade entirely.
3) Remove service locator from UI (DI purity) — PENDING
   - Switch ViewModels to constructor injection; register all VMs in DI.
   - MainWindow obtains its DataContext via DI; eliminate `CompositionRoot.Get<T>()` calls.
4) Centralize init/shutdown (Lifecycle) — IN PROGRESS
   - Single-flight runtime init implemented to prevent double schema resets.
   - App awaits init on window open and surfaces health; VMs still trigger init redundantly — to be removed.
   - Ensure shutdown awaits `EndCurrentOperationAsync`, materialization, and runtime disposal.
5) Concurrency/backpressure (Concurrency) — PENDING
   - Introduce bounded queue (Channel<LogWrite>) with single consumer; sink enqueues; background worker appends.
   - Flow CancellationToken across public async APIs; avoid `async void`.
6) Cross-cutting abstractions (Testability) — PENDING
   - Add `TimeProvider`/`ITimeProvider` and file-system abstraction for materialization paths; inject via DI in Infra.

## Deliverables
- Core has no Serilog references; Serilog adapter lives in Infrastructure. ✅
- `WorkspaceSqliteSink` uses DI; no direct static DB/service access. ✅ (adapter still uses current static runtime)
- Robust workspace DB lifecycle: first-run creates schema; true mismatches rebuild with notice; WAL checkpointing improved; UI warns if DB missing. ✅
- No `CompositionRoot.Get<T>()` in ViewModels; DataContexts constructed via DI. ⏳
- Single init/shutdown path that is awaited; services disposable/async-disposable as needed. ⏳
- Background, bounded writer for DB log appends with cancellation support. ⏳

## Acceptance Criteria (updated)
- Build passes with Core free of Serilog types; sink is DI-driven; app starts with one init sequence (no duplicate schema resets).
- Fresh workspace: DB is created with schema; no mismatch warning. Existing older DB: reset with notice; modern DB: no warnings.
- UI health banner shows clear warnings if the DB is missing or init fails, with retry and Settings navigation.
- Service locator removed from VMs; DataContexts resolved via DI; init/shutdown owned centrally and awaited.
- Log path uses a bounded background writer with graceful shutdown (flush on exit).

## Status
- Core purity: DONE.
- Sink DI: DONE (adapter in place); runtime still has a static facade to be eliminated later.
- Workspace DB creation/mismatch handling: DONE and verified; warnings improved.
- UI health surfacing: DONE (missing-DB and init-failure surfaced).
- Remove service locator in VMs: PENDING.
- Centralize init/shutdown (remove redundant VM init, ensure awaited path): IN PROGRESS.
- Bounded writer/cancellation: PENDING.
- Cross-cutting providers: PENDING.

## Next Actions
- UI/DI
  - Move ViewModels to constructor injection; register all VMs in DI; stop using `CompositionRoot.Get<T>()` in VMs.
  - Set MainWindow.DataContext via DI factory in App.
- Lifecycle
  - Make App the single owner: remove VM-driven `InitializeAsync`; keep health observing only.
  - Ensure shutdown sequence awaits: end operation → materialize logs → flush Serilog → runtime shutdown.
- Concurrency
  - Add `Channel<LogWrite>`-based writer in Infrastructure; inject it into sink target; implement bounded capacity, backpressure, and cancellation.
  - Add `IAsyncDisposable` to runtime/writer and dispose on shutdown.
- Tech debt cleanup
  - Replace `WorkspaceDbService` static with an instance-based runtime service; update `WorkspaceLogWriteTargetAdapter` to wrap the instance.
  - Introduce `ITimeProvider` and file-system abstraction; thread through materialization paths and tests.
