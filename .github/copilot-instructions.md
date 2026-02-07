## Quick Context
- Solution targets .NET 8 with Avalonia 11; major projects are `FileProcessor.Core`, `FileProcessor.Infrastructure`, `FileProcessor.UI`, `LogViewer.UI`, and matching `tests/*` suites.
- `FileProcessor.Core` hosts domain services; Infrastructure layers system/SQLite integrations; UI reuses `LogViewer.UI` pieces for the log window instead of duplicating code.

## Architecture Points
- `FileProcessor.UI/Services/CompositionRoot.cs` is the DI entry point: registers `SettingsService.Instance`, `WorkspaceRuntime`, Serilog sinks, window factories, and every ViewModel.
- `FileProcessor.Infrastructure/Workspace/WorkspaceRuntime.cs` owns workspace SQLite state (`IWorkspaceDb`), tracks sessions/operations, and implements `ILogAppender` for log ingestion.
- Logging flow: `WorkspaceSqliteSink` (Serilog) → `WorkspaceLogWriteTarget` → `WorkspaceRuntime` bounded channel → SQLite; ensure runtime is initialized before expecting logs on disk.
- File services (`FileGenerationService`, `FileProcessingService`) expose sync/async APIs with `IProgress<(completed,total)>` and optional `IItemLogFactory` scopes for structured telemetry.
- `SettingsService` persists under `%AppData%/FileProcessor/settings.json`; its `WorkspaceDirectory` config drives workspace DB/log locations and input/processed folder creation.

## Patterns & Conventions
- MVVM uses CommunityToolkit attributes (`[ObservableProperty]`, `[RelayCommand]`); inspect `FileProcessor.UI/ViewModels` for canonical usage.
- UI code should stick to injected services; filesystem access goes through `IFileSystem` (`SystemFileSystem` runtime, fakes in tests).
- New services/contracts live under `*/Interfaces/` and are registered in the composition root; avoid wiring singletons ad hoc.
- `SettingsService.WorkspaceChanged` must be raised when mutating workspaces so `WorkspaceRuntime` reacts; tests assert this behaviour.
- Use `IOperationContext`/`IWorkspaceRuntime` to wrap long tasks so logs and SQLite operations stay correlated.

## Daily Commands
- Restore and build: `dotnet restore` then `dotnet build FileProcessor.sln`.
- Launch desktop app: `dotnet run --project FileProcessor.UI`.
- Run generator utility: `dotnet run --project FileProcessor.Generator`.
- Execute unit tests: `dotnet test` or point at a specific `tests/*.csproj`.
- Coverage workflow is automated via VS Code task `coverage: full` (clean → build → collect → report).

## Integration Notes
- Avalonia bootstrap (`FileProcessor.UI/Program.cs`) forbids invoking Avalonia APIs before `BuildAvaloniaApp`; keep new static initialization framework-agnostic.
- Workspace artefacts (`workspace.db`, `logs/operation-*.jsonl`) live under the active workspace; guard any feature behind a configured `SettingsService.WorkspaceDirectory`.
- `WorkspaceRuntime` uses a bounded channel that drops oldest writes when saturated—call `FlushAsync`/`ShutdownAsync` (see `ApplicationHost`) before exit to persist remaining logs.
- Structured logs expect `cat`/`sub`/`item` properties; rely on `IItemLogFactory` (see `FileProcessingService.ProcessFilesWithLogs`) for consistent payloads instead of ad hoc logging.
- Long-running file ops should run off the UI thread and report progress; follow `FileGeneratorViewModel.GenerateFiles` pattern with `Task.Run` + progress callbacks.

## Testing Guidance
- Tests mirror layers: Core tests cover services, Infrastructure tests exercise SQLite and filesystem abstractions, Integration tests spin up DI via `IntegrationProbe`.
- UI tests (`tests/FileProcessor.UI.Tests/AppTests.cs`) lock down DI wiring and logging registration—update expectations when altering `CompositionRoot`.
- When interacting with `SettingsService`, use constructor overloads with temp paths (see tests) to avoid polluting real user settings.

## Pitfalls & Tips
- Do not touch user folders outside `SettingsService.WorkspaceDirectory`; create subdirectories via the provided abstractions.
- Preserve cross-platform compatibility (`Path.Combine`, avoid Windows-only APIs or sync IO on UI thread).
- Keep new services focused and async-friendly; expose cancellation tokens and progress when adding long-running work.
- Only register one Serilog `WorkspaceSqliteSink`; the debug guard will warn if multiple sinks are added.
- Register any new UI windows/viewmodels in `CompositionRoot` so both runtime and tests can resolve them.
