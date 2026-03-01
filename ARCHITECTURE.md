# Architecture

## Solution Structure

```
FileProcessor.sln
├── FileProcessor.UI/             # Avalonia UI Application
│   ├── Assets/                   # Images, fonts, static resources
│   ├── Converters/               # XAML value converters
│   ├── Interfaces/               # UI-specific interfaces
│   ├── Resources/                # Shared XAML styles
│   ├── Services/                 # UI services (themes, DI composition)
│   ├── ViewModels/               # MVVM ViewModels
│   └── Views/                    # XAML views
├── FileProcessor.Core/           # Business Logic (framework-agnostic)
│   ├── Interfaces/               # Core business interfaces
│   ├── Logging/                  # Logging contracts and abstractions
│   └── Workspace/                # Workspace database abstractions
├── FileProcessor.Infrastructure/ # Cross-Cutting Infrastructure
│   ├── Logging/                  # Serilog sinks and adapters
│   └── Workspace/                # SQLite implementation
├── LogViewer.UI/                 # Standalone Log Viewer
└── tests/                        # Test projects (xUnit)
```

## Layers

- **UI Layer** (`FileProcessor.UI`): User interface, data binding, MVVM ViewModels
- **Core Layer** (`FileProcessor.Core`): Business logic, no framework dependencies
- **Infrastructure Layer** (`FileProcessor.Infrastructure`): Logging, SQLite, external integrations

Dependencies flow inward: UI → Infrastructure → Core

## Key Patterns

### MVVM
- ViewModels use CommunityToolkit.Mvvm with source generators
- Views are XAML with minimal code-behind
- All ViewModels receive dependencies via constructor injection

### Dependency Injection
- Composition root in `FileProcessor.UI/Services/CompositionRoot.cs`
- All services implement interfaces for testability
- `IApplicationHost` abstracts app lifecycle (init/shutdown)

### Workspace Database
- SQLite with WAL mode for session/operation/log persistence
- `WorkspaceRuntime` owns bounded channel writer for async log appends
- Schema recreation on version mismatch (diagnostic data, not user data)

### Logging
- Single Serilog pipeline configured in App
- `WorkspaceSqliteSink` writes to workspace database
- Per-operation JSONL files for export

## Testing

- **Unit tests**: Isolated logic with fakes/stubs
- **Integration tests**: Real DI + temp workspaces
- **UI tests**: ViewModel state + Avalonia.Headless.XUnit
- **Coverage**: 95%+ on Core/Infrastructure, UI excluded from metrics

Run tests: `dotnet test`

---

## Design Decisions

### Singleton SettingsService
The `SettingsService` uses a static `Instance` alongside DI registration. This hybrid works because settings are needed early in startup before DI is configured. Tests use the internal constructor for isolation.

### Thread Safety in SettingsService
Settings mutations are not synchronized. Acceptable for a single-threaded desktop app where workspace changes happen through user action.

### Channel Wait Policy
The log channel uses `Wait` mode (not `DropOldest`) to guarantee no logs are dropped. A warning is logged if the 8192-entry buffer fills and writers must wait.

### Sync/Async Conversion Duplication
`ConvertFile` and `ConvertFileAsync` have similar logic. The sync version is used in `Parallel.ForEach`; calling async from sync risks deadlocks in UI contexts.

### Hardcoded UI Strings
Strings are hardcoded. Only extract to resources if internationalization becomes a requirement.

---

*Last updated: 2026-03-01*
