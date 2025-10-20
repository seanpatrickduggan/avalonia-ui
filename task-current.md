# Current Task: Testing Implementation

## Objective
Establish a clear testing architecture and add an initial test suite to validate core infrastructure behaviors (logging pipeline, workspace DB lifecycle), enabling safe iteration.

## Scope
- Create a test project with best practices (xUnit, FluentAssertions, coverlet, FakeTimeProvider from Microsoft.Extensions.TimeProvider.Testing).
- Add an integration smoke test using IntegrationProbe to assert 1 event → 1 DB row.
- Provide test utilities: temp workspace harness and DI overrides for tests.
- Document the testing architecture in STRUCTURE.md.
- Define test project layout and add UI testing strategy (ViewModels + limited headless view tests).
- Add coverage aggregation locally with a single “latest only” report.

## Recommended Project Layout (best practice)
- Keep all tests under a top-level `tests/` folder, mirroring product assemblies:
  - `tests/FileProcessor.Core.Tests` — unit tests for Core.
  - `tests/FileProcessor.Infrastructure.Tests` — unit tests for Infra.
  - `tests/FileProcessor.UI.Tests` — ViewModel-focused tests; add a few headless Avalonia view tests for smoke coverage.
  - `tests/FileProcessor.IntegrationTests` — integration tests that exercise real DI + temp workspace (e.g., IntegrationProbe).
- Rationale: clear separation of unit vs integration, fast local runs, and easy CI aggregation. Co-locating tests inside src folders is less common in .NET and mixes concerns.

## Plan
1) Test project setup — xUnit, FluentAssertions, coverage collector; project refs to Core and Infrastructure.  ✅
2) IntegrationProbe test — initialize runtime against a temp workspace, emit probe event, assert exactly one DB row.  ✅
3) Harness utilities — helper to create and clean a temp workspace per test.  ✅ (inline helpers used; can consolidate later)
4) Documentation — add “Testing Architecture” section to STRUCTURE.md.  ✅
5) Add `tests/FileProcessor.UI.Tests` — reference `FileProcessor.UI`; write ViewModel tests (commands/state) with fakes for host/runtime; prepare Avalonia.Headless for view smoke tests.  ✅
6) Add 1–2 headless view smoke tests for critical flows (e.g., open LogViewer, bind, simple interaction).  ✅ (initial smoke tests)
7) Infra coverage hardening: add DB schema gate test; JSONL reader tests; DB reader tests; runtime materialization tests.  ✅
8) Add local coverage cleanup + aggregation tasks so reports only include the latest run.  ✅
9) Next: Improve Core SettingsService testability (introduce injectable storage path or abstraction) and expand runtime/ops tests (shutdown, cancellation, operation markers). ⏭️

## Acceptance Criteria
- All test projects under `tests/` build and run locally.
- IntegrationProbe test passes reliably and leaves no artifacts outside a temp directory.
- STRUCTURE.md documents test layers, tools, and practices.
- `tests/FileProcessor.UI.Tests` exists with initial ViewModel tests; headless test infra compiles and runs locally.
- Local “coverage: full” task produces a fresh report each run (cleans old TestResults and coverage-report first).

## Status
- Core: tests present; SettingsService test is currently skipped due to singleton/OS path coupling (to improve).
- Infrastructure: extensive tests added — JsonlLogReader (100% lines/branches), SqliteWorkspaceDb (CRUD + schema-mismatch recreate + notice), SqliteLogReader (delegation), WorkspaceRuntime (buffer flush, channel flush, materialize session/operation JSONL).
- UI: ViewModel tests passing; headless setup in place; initial smoke coverage added.
- Coverage: tasks updated. Added `coverage: clean` and wired `coverage: full` to run clean → build → test with coverlet → tools restore → report. Local runs now only include the latest reports.

## Decisions
- Keep only the latest local coverage; rely on CI to archive artifacts per run.
- Removed `JsonlStream.cs`; simplified JsonlLogReader with helpers and straightforward loops to reduce complexity while keeping coverage high.
- Use `Microsoft.Extensions.Time.Testing.FakeTimeProvider` only in tests.
- Test doubles that feed paginated APIs must honor paging (return empty for page > 0) to avoid infinite loops during materialization.

## Next
- Refactor Core `SettingsService` to enable unit testing (inject storage path or file IO abstraction); add tests for add/set/remove workspace and persistence.
- Add `WorkspaceRuntime` tests for cancellation in materialization and `ShutdownAsync` session end behavior.
- Add `OperationContextService` tests for start/end markers and log materialization trigger.
