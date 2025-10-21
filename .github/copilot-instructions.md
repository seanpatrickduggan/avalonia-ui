## Quick context

This repository is a modular .NET 8 desktop example using Avalonia UI. Main pieces:

- `FileProcessor.UI/` — Avalonia presentation (Views, ViewModels, Services).
- `FileProcessor.Core/` — Business logic and service implementations (IFileGenerationService, IFileProcessingService).
- `FileProcessor.Infrastructure/` — Host app wiring, workspace implementations, logging sinks (SQLite), and test fakes.
- `FileProcessor.Generator/` — CLI-style generator tool (utility; may be absent in trimmed view).

Primary goals when editing code here:

- Preserve interface-based contracts in `*/Interfaces/` and implement behind DI where appropriate.
- Keep UI code strictly MVVM (look at `FileProcessor.UI/ViewModels/*` for examples using CommunityToolkit.Mvvm).

## What to know before editing

- Target framework: .NET 8. Use `dotnet` CLI for restore/build/run and the same runtime for tests.
- UI is Avalonia 11.x — avoid calling Avalonia-specific APIs during static initialization (see `FileProcessor.UI/Program.cs` comment).
- Project prefers small, focused services. Example: `FileProcessor.Core/FileGenerationService.cs` exposes async overloads with optional `IProgress<T>` reporting.
- Logging and workspace persistence live in `FileProcessor.Infrastructure/Workspace/` (look for `WorkspaceSqliteSink`, `SqliteWorkspaceDb`, `WorkspaceDbService`).

## Typical developer workflows (concrete commands)

- Restore & build entire solution:

  dotnet restore; dotnet build

- Run the UI app:

  dotnet run --project FileProcessor.UI

- Run core unit tests (project names under `tests/`):

  dotnet test

Notes: there are Nix helper files (`nix/shell.nix`, `flake.nix`) in this repo — optional for reproducible builds but not required on Windows.

## Project-specific patterns & conventions

- MVVM: ViewModels use CommunityToolkit.Mvvm source generators: attributes like `[ObservableProperty]` and `[RelayCommand]`. Check `FileGeneratorViewModel` for common patterns.
- Services: Core services provide both sync and async variants (e.g., `GenerateFilesAsync`, `GenerateFileAsync`) and take optional progress callbacks.
- Interfaces live under `*/Interfaces/` and are small, test-friendly contracts. Prefer using interfaces in constructors over concrete types for testability.
- Workspace settings: `SettingsService` is a singleton used by UI ViewModels (see `SettingsService.Instance` usage in `FileGeneratorViewModel`). When changing settings code, update subscription handling (`WorkspaceChanged` event).
- Logging: item-level structured logging is implemented via `IItemLogFactory` and `IItemLog` in `FileProcessor.Core/Logging` and wired to SQLite in `Infrastructure/Logging/WorkspaceSqliteSink.cs`.

## Integration points & gotchas

- Database: workspace persistence uses a local SQLite file and a custom sink. Tests may use `FakeFileSystem` / `FakeTimeProvider` in `FileProcessor.Infrastructure/Abstractions`.
- UI thread: long-running work must run off the UI thread and report progress via `IProgress<T>` (see `FileGeneratorViewModel.GenerateFiles`).
- Avalonia startup: don't touch Avalonia-dependent code before `AppMain` (see `Program.cs` comment).

## Files to reference when making changes

- Architecture & onboarding: `README.md` (root).
- DI / Host wiring: `FileProcessor.Infrastructure/App/ApplicationHost.cs` and `FileProcessor.Infrastructure/Logging/WorkspaceRunStructuredLogger.cs`.
- Example services: `FileProcessor.Core/FileGenerationService.cs`, `FileProcessor.Core/FileProcessingService.cs`.
- UI examples: `FileProcessor.UI/Program.cs`, `FileProcessor.UI/ViewModels/FileGeneratorViewModel.cs`, `FileProcessor.UI/Views/*`.
- Tests: `tests/` — follow existing naming and use `dotnet test`.

## Short examples you can follow

- Implement a new background service callable from the UI:
  - Add interface to `FileProcessor.Core/Interfaces/`.
  - Implement in Core or Infrastructure and expose async methods with `IProgress<T>` support.
  - Inject into a ViewModel via constructor (use the singleton `SettingsService.Instance` only for global settings).

- Add a workspace-backed logger entry:
  - Use `IItemLogFactory` to create a scope: see `FileProcessingService.ProcessFilesWithLogs`.

## What to avoid

- Avoid direct file I/O in UI code without a FileSystem abstraction; prefer `IFileSystem` / `SystemFileSystem` fakes for tests.
- Avoid modifying Avalonia startup order or calling Avalonia APIs before `BuildAvaloniaApp()` completes.

## If you're adding tests

- Prefer the existing test projects under `tests/` and mirror patterns: small, focused unit tests that use fakes from `FileProcessor.Infrastructure/Abstractions`.

---
If anything in these notes is unclear or you want me to expand examples (DI registration, tests, or a PR template), tell me which section to iterate on.
