# FileProcessor Solution Structure

## 📁 **Improved Project Organization**

This solution follows .NET and Avalonia UI best practices for maintainable, scalable applications.

### **Solution Structure**
```
FileProcessor.sln                 # Solution file
├── FileProcessor.UI/             # 🎨 Main Avalonia UI Application
│   ├── Assets/                   # Images, fonts, and static resources
│   ├── Converters/              # XAML value converters
│   ├── Interfaces/              # UI-specific interfaces
│   ├── Models/                  # UI data models and DTOs
│   ├── Resources/               # Shared XAML resources and styles
│   ├── Services/                # UI services (themes, navigation, etc.)
│   ├── ViewModels/              # MVVM ViewModels
│   ├── Views/                   # XAML views and user controls
│   ├── App.axaml               # Application entry point
│   ├── MainWindow.axaml        # Main application window
│   └── Program.cs              # Application startup
├── FileProcessor.Core/          # 🔧 Business Logic Library
│   ├── Interfaces/             # Core business interfaces
│   ├── Logging/                # Logging contracts and abstractions
│   ├── Workspace/              # Workspace database abstractions
│   ├── FileProcessingService.cs # File processing operations
│   └── FileGenerationService.cs # File generation operations
├── FileProcessor.Infrastructure/ # 🏗️ Cross-Cutting Infrastructure
│   ├── Logging/                # Logging service implementations
│   └── Workspace/              # Workspace database implementation
├── LogViewer.UI/               # 🔍 Standalone Log Viewer Application
├── tests/
│   └── FileProcessor.Tests/    # ✅ Test project (xUnit)
└── SampleFiles/                # 📄 Test files for development
```

## 🏗️ **Architecture Principles**

### **1. Separation of Concerns**
- **UI Layer** (`FileProcessor.UI`): Handles user interface, data binding, and user interactions
- **Business Layer** (`FileProcessor.Core`): Contains business logic, data processing, and domain operations
- **Infrastructure Layer** (`FileProcessor.Infrastructure`): Cross-cutting concerns like logging, configuration, external services, and workspace database

### **2. Clean Architecture**
- **UI Layer** (`FileProcessor.UI`): Handles user interface, data binding, and user interactions
- **Business Layer** (`FileProcessor.Core`): Contains business logic, data processing, and domain operations  
- **Infrastructure Layer** (`FileProcessor.Infrastructure`): Cross-cutting concerns like logging, configuration, external services, and workspace database

### **3. Dependency Injection Ready**
- All services implement interfaces for testability and flexibility
- Infrastructure services are shared between UI and CLI applications
- Services can be easily mocked for unit testing
- Ready for DI container integration (Microsoft.Extensions.DependencyInjection)

### **3. MVVM Pattern**
- **Models**: Data structures and business entities
- **ViewModels**: Presentation logic and data binding
- **Views**: XAML UI definitions with minimal code-behind

### **4. Consistent Naming**
- All projects follow `FileProcessor.*` naming convention
- Namespace alignment: `FileProcessor.UI.ViewModels`, `FileProcessor.Core.Interfaces`
- Clear, descriptive folder and file names

## 🧪 Testing Architecture

- Project: `tests/FileProcessor.Tests` using xUnit + FluentAssertions + coverlet for coverage.
- Time: BCL `TimeProvider` (no custom provider); can use `Microsoft.Extensions.Time.Testing` FakeTimeProvider in tests when needed.
- File system: Use real `SystemFileSystem` against a per-test temp directory for integration tests. For pure unit tests, optional in-memory `IFileSystem` stub lives in the test project.
- DI: Tests compose a minimal service provider, overriding `IFileSystem` or `TimeProvider` as needed without touching production code.
- Scopes:
  - Unit tests: small, deterministic; prefer fakes/stubs.
  - Integration tests: exercise `WorkspaceRuntime`, `SqliteWorkspaceDb`, and Serilog sink with a temp workspace.
- Probes: `IntegrationProbe.VerifyOneEventOneRowAsync` ensures one event → one DB row (guards duplicate sinks/double writes).

Patterns
- Temp workspace harness creates `input/`, `processed/`, `logs/` under a unique temp folder and cleans up on dispose.
- Ensure each test uses an isolated workspace and DI container instance.
- Prefer async tests and CancellationToken with timeouts to avoid hangs.

Tooling
- xUnit runner; FluentAssertions for readable assertions; coverlet collector for coverage; GitHub Actions-ready.

## 📦 **Key Components**

### **Interfaces**
- `IFileProcessingService`: Async file processing operations
- `IFileGenerationService`: Async file generation operations  
- `IThemeService`: Application theme management

### **Services**
- `FileProcessingService`: Implements file processing with async support
- `FileGenerationService`: Handles file creation and batch generation
- `ThemeService`: Manages light/dark theme switching

### **ViewModels**
- `MainWindowViewModel`: Main application state and navigation
- `FileProcessorViewModel`: File processing operations UI
- `FileGeneratorViewModel`: File generation operations UI
- `SettingsViewModel`: Application settings and preferences

### **Workspace Database**
- Purpose: SQLite-based workspace store for sessions, operations, items, and logs to enable fast querying and filtering of large log files.
- Key Interfaces (`FileProcessor.Core.Workspace`): `IWorkspaceDb`, `IOperationStore`, `ILogStore`.
- Implementation (`FileProcessor.Infrastructure.Workspace`): `SqliteWorkspaceDb` (WAL), `WorkspaceRuntime` (bounded channel writer, materialization).
- Logging Integration (`FileProcessor.Infrastructure.Logging`): `WorkspaceSqliteSink`, `WorkspaceOperationStructuredLogger`.

## 🎯 **Benefits of This Structure**

1. ✅ Maintainability: Clear separation makes code easier to understand and modify
2. ✅ Testability: Interface-based design enables comprehensive unit and integration testing
3. ✅ Scalability: Modular structure supports adding new features and services
4. ✅ Best Practices: Follows established .NET and Avalonia UI conventions
5. ✅ Team Development: Clear structure helps team members navigate the codebase

## 🚀 **Getting Started**

```bash
# Build the solution
dotnet build

# Run the UI application
dotnet run --project FileProcessor.UI

# Run tests
dotnet test
```

## 🔧 **Development Guidelines**

- Place new UI components in appropriate folders (`Views/`, `ViewModels/`, `Converters/`)
- Create interfaces for all services to maintain testability
- Use the `Resources/` folder for shared XAML styles and resources
- Follow async/await patterns for all I/O operations
- Maintain consistent namespace structure across projects
