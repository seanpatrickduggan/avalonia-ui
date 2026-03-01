# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Restore and build
dotnet restore
dotnet build FileProcessor.sln

# Run the UI application
dotnet run --project FileProcessor.UI

# Run all tests
dotnet test

# Run a single test project
dotnet test tests/FileProcessor.Core.Tests
dotnet test tests/FileProcessor.Infrastructure.Tests
dotnet test tests/FileProcessor.Integration.Tests
dotnet test tests/FileProcessor.UI.Tests

# Full coverage workflow (clean → build → collect → report)
# Equivalent to VS Code task "coverage: full"
rm -rf tests/**/TestResults coverage-report
dotnet build FileProcessor.sln
dotnet test FileProcessor.sln --collect:"XPlat Code Coverage" -v minimal
dotnet tool restore
dotnet tool run reportgenerator -reports:tests/**/TestResults/**/coverage.cobertura.xml -targetdir:coverage-report -reporttypes:Html;HtmlSummary;TextSummary;lcov;Cobertura

# Format/lint
dotnet format analyzers
```

## Solution Structure

```
FileProcessor.sln
├── FileProcessor.UI/          # Avalonia UI (Presentation Layer)
├── FileProcessor.Core/        # Business Logic (framework-agnostic Domain Layer)
├── FileProcessor.Infrastructure/  # Cross-cutting: Serilog sinks, SQLite workspace
├── LogViewer.UI/              # Standalone Log Viewer (shared by embedded use too)
└── tests/                     # xUnit test projects mirroring each layer
```

Dependencies flow inward: UI → Infrastructure → Core. Core has no framework dependencies.

## Key Architecture Points

**Composition Root**: `FileProcessor.UI/Services/CompositionRoot.cs` is the single DI entry point — registers `SettingsService.Instance`, `WorkspaceRuntime`, Serilog sinks, window factories, and all ViewModels. New windows/viewmodels must be registered here or they won't resolve in tests either.

**WorkspaceRuntime**: `FileProcessor.Infrastructure/Workspace/WorkspaceRuntime.cs` owns SQLite state, tracks sessions/operations, and implements `ILogAppender`. Uses a bounded channel (8192-entry, `Wait` policy — logs are never dropped) for async log ingestion. Call `FlushAsync`/`ShutdownAsync` (via `ApplicationHost`) before exit.

**Logging flow**: Serilog → `WorkspaceSqliteSink` → `WorkspaceLogWriteTarget` → `WorkspaceRuntime` bounded channel → SQLite + per-operation JSONL files. The runtime must be initialized before logs reach disk. Only register one `WorkspaceSqliteSink` — a debug guard warns on duplicates.

**SettingsService**: Persists to `%AppData%/FileProcessor/settings.json`. Its `WorkspaceDirectory` drives workspace DB/log locations and input/processed folder creation. Raise `WorkspaceChanged` when mutating workspace so `WorkspaceRuntime` reacts.

**File services**: `FileGenerationService` and `FileProcessingService` expose sync/async APIs with `IProgress<(completed, total)>` and optional `IItemLogFactory` for structured telemetry. Structured logs expect `cat`/`sub`/`item` properties — use `IItemLogFactory` (see `FileProcessingService.ProcessFilesWithLogs`) for consistent payloads.

## Patterns & Conventions

- **MVVM**: CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]`). See `FileProcessor.UI/ViewModels/` for canonical usage. Long-running ops use `Task.Run` + progress callbacks (see `FileGeneratorViewModel.GenerateFiles`).
- **Interfaces**: All services implement interfaces under `*/Interfaces/`. Register in composition root; avoid ad-hoc singletons.
- **Filesystem access**: UI code uses injected `IFileSystem` (`SystemFileSystem` at runtime, fakes in tests). Never access the filesystem directly from UI or Core.
- **Sync/Async duplication**: `ConvertFile` / `ConvertFileAsync` intentionally duplicate logic — the sync version runs inside `Parallel.ForEach`; calling async from sync risks deadlocks in UI contexts.
- **Usings**: System directives sorted first and separated from other groups (`.editorconfig`). IDE0005 (unused usings) is a warning.

## Testing

Tests mirror layers: Core tests cover services, Infrastructure tests exercise SQLite/filesystem abstractions, Integration tests spin up DI via `IntegrationProbe`. UI tests (`tests/FileProcessor.UI.Tests/AppTests.cs`) lock down DI wiring and logging registration — update expectations when changing `CompositionRoot`.

- Use constructor overloads with temp paths for `SettingsService` in tests to avoid polluting real user settings.
- 95%+ coverage target on Core/Infrastructure; UI excluded from metrics.
- CI/CD UI tests currently fail in headless environments due to workspace configuration requirements (known issue in TASKS.md).

## Key Pitfalls

- Do not touch user folders outside `SettingsService.WorkspaceDirectory`.
- Guard any feature behind a configured `WorkspaceDirectory` — workspace artefacts live there.
- Avalonia bootstrap (`FileProcessor.UI/Program.cs`) forbids invoking Avalonia APIs before `BuildAvaloniaApp`; keep static initialization framework-agnostic.
- Preserve cross-platform compatibility: `Path.Combine`, no Windows-only APIs, no sync IO on the UI thread.
- `SettingsService` uses a static `Instance` alongside DI (needed before DI is configured at startup) — tests use the internal constructor for isolation.
