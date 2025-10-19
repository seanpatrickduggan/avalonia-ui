# Current Task: Architecture Hardening (Core purity, DI, lifecycle, concurrency)

## Objective
Eliminate architectural smells and lock in a clean, testable, framework-agnostic foundation: Core remains pure, all services flow through DI, a single owner manages init/shutdown, and concurrency/backpressure are explicit.

## Scope
- Core purity: no Serilog or framework types in Core contracts or implementations.
- DI purity: no statics/service locators on hot paths (UI/Infra); dependencies are injected.
- Lifecycle: single init/shutdown orchestration, with proper disposal semantics.
- Concurrency: explicit background pipeline for log writes with bounded buffering and cancellation.

## Plan
1) Purge Serilog from Core (Core purity)
   - Move SerilogOperationStructuredLogger out of Core to Infrastructure.
   - Change `IOperationContext.Initialize` to remove Serilog types (take primitives/own abstractions only).
   - Update UI/Infra to build Serilog in Infra and pass only Core-safe types into Core.
2) Refactor logging sink DI (DI purity)
   - Replace `WorkspaceSqliteSink` static access with constructor-injected dependency (e.g., `ILogWriteTarget` or `IWorkspaceRuntime`).
   - Resolve sink with DI at logger configuration time; remove static `WorkspaceDbService` usage.
3) Remove service locator from UI (DI purity)
   - Switch ViewModels to constructor injection; register all VMs in DI.
   - MainWindow obtains its DataContext via DI; eliminate `CompositionRoot.Get<T>()` calls in VMs.
4) Centralize init/shutdown (Lifecycle)
   - Choose a single owner (App startup) to `await IWorkspaceRuntime.InitializeAsync` and wire `IOperationContext`.
   - Ensure shutdown awaits `EndCurrentOperationAsync`, materialization, and runtime disposal.
   - Implement/adhere to `IAsyncDisposable` for long-lived Infra services where appropriate.
5) Concurrency/backpressure (Concurrency)
   - Introduce a bounded `Channel<LogWrite>` (or equivalent queue) for DB writes; single consumer appends to DB.
   - Flow `CancellationToken` across public async APIs; avoid `async void`.
6) Cross-cutting abstractions (Testability)
   - Add `TimeProvider`/`ITimeProvider` and file-system abstraction for materialization paths; inject via DI in Infra.

## Deliverables
- Core has no Serilog references; Serilog adapter lives in Infrastructure.
- `WorkspaceSqliteSink` and logging path use DI; no static DB/service access.
- No `CompositionRoot.Get<T>()` in ViewModels; DataContexts constructed via DI.
- Single init/shutdown path that is awaited; services disposable/async-disposable as needed.
- Background, bounded writer for DB log appends with cancellation support.

## Acceptance Criteria
- Build passes with Core free of Serilog types and no static service usage on hot paths.
- Manual run shows: one init sequence, banner reflects status, clean shutdown without leaked tasks.
- Code review checklists confirm DI-only flows and explicit concurrency policy are documented.

## Status
- DI present; many statics removed; runtime used via DI in most places.
- Pending: remove Serilog from Core, inject sink deps, remove service locator usage in VMs, centralize init/shutdown, implement bounded writer, add cross-cutting providers.
